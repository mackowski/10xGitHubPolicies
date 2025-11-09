# Authentication Documentation

This document describes the authentication and authorization system for the 10x GitHub Policy Enforcer application.

## Overview

The application uses a dual-authentication strategy:

1. **GitHub OAuth App**: For user authentication to the web dashboard
2. **GitHub App**: For backend services to interact with the GitHub API

## User Authentication Flow

### 1. OAuth Flow

The user authentication follows the standard OAuth 2.0 flow:

1. User clicks "Login with GitHub" on the login page
2. User is redirected to the `/challenge` endpoint
3. The application initiates OAuth challenge with proper state handling
4. User is redirected to GitHub's OAuth consent screen
5. User authorizes the application
6. GitHub redirects back to `/signin-github` with an authorization code and state
7. Application validates the state and exchanges the code for an access token
8. Application creates a secure session cookie
9. User is redirected to the dashboard

### 2. Authorization Process

After successful authentication, the application performs authorization checks:

1. **Configuration Check**: Verifies that `config.yaml` exists in the `.github` repository
2. **Team Membership Check**: Verifies that the user is a member of the authorized team specified in `config.yaml`

### 3. Session Management

- **Session Duration**: 24 hours (fixed expiration)
- **Token Storage**: OAuth access tokens are stored securely in encrypted authentication cookies
- **Scope**: Only `read:org` scope is requested (least privilege principle)

## Application Authentication (GitHub App)

This section describes how backend services authenticate to the GitHub API independently of any logged-in user. This authentication method enables unattended, automated operations such as repository scanning, issue creation, and repository archiving.

### Purpose

The GitHub App authentication enables:

- **Scheduled Repository Scans**: Daily automated scans of all organization repositories
- **Policy Enforcement Actions**: Creating issues for policy violations
- **Administrative Actions**: Archiving non-compliant repositories
- **Unattended Operations**: All operations run independently of user sessions

### Authentication Flow

The application authentication follows this flow:

```mermaid
sequenceDiagram
    autonumber
    participant Hangfire Job
    participant ScanningService
    participant GitHubService
    participant GitHub API

    Hangfire Job->>ScanningService: Triggers repository scan
    activate ScanningService
    ScanningService->>GitHubService: Request authenticated GitHub client
    activate GitHubService

    alt Installation token is cached and valid
        GitHubService-->>ScanningService: Returns client with existing token
    else Token is missing or expired
        GitHubService->>GitHubService: Generate JWT from private key
        GitHubService->>GitHub API: Request new installation access token using JWT
        activate GitHub API
        GitHub API-->>GitHubService: Returns installation token
        deactivate GitHub API
        GitHubService->>GitHubService: Cache the new token with expiry
        GitHubService-->>ScanningService: Returns client with new token
    end
    deactivate GitHubService

    ScanningService->>GitHub API: Uses client to fetch repositories
    activate GitHub API
    GitHub API-->>ScanningService: Returns list of repositories
    deactivate GitHub API
    deactivate ScanningService
```

### Token Management

The `GitHubService` (a singleton service) manages authentication tokens using the following strategy:

1. **JWT Generation**: Generates a JSON Web Token (JWT) using the GitHub App's private key
   - The JWT is signed using RSA with the private key stored in configuration
   - JWT includes the App ID and expiration time (9 minutes, with a 1-minute buffer for clock skew)

2. **Installation Token Exchange**: Uses the JWT to request an installation access token from GitHub
   - The installation token grants permissions to access the organization's repositories
   - Installation tokens are short-lived (1 hour) for security

3. **Token Caching**: Caches the installation token in memory with intelligent expiration
   - Tokens are cached for **55 minutes** (5 minutes before the 60-minute expiration)
   - This ensures seamless operation without token expiration during long-running operations
   - When a token expires, a new one is automatically generated on the next request

4. **Automatic Refresh**: Token refresh happens transparently
   - No manual intervention required
   - Failed requests due to expired tokens trigger automatic token regeneration

### Service Architecture

The application authentication is implemented through the following services:

- **`GitHubService`** (Singleton): 
  - Manages GitHub App authentication (JWT generation and installation token caching)
  - Provides high-level methods for GitHub API operations
  - Handles both installation token authentication (for backend operations) and user token authentication (for team membership checks)
  - See [GitHub Integration](./github-integration.md) for detailed API documentation

- **`ScanningService`** (Scoped):
  - Orchestrates repository scanning and policy evaluation
  - Consumes `IGitHubService` via dependency injection
  - Triggered by Hangfire jobs (both scheduled and on-demand)

- **Hangfire Jobs**:
  - Scheduled jobs trigger daily repository scans
  - On-demand jobs can be triggered from the dashboard
  - All jobs use the GitHub App authentication automatically

### Required GitHub App Permissions

When setting up your GitHub App, configure the following repository permissions:

| Permission | Level | Purpose |
|------------|-------|---------|
| **Administration** | Read & write | To read repository settings and archive repositories |
| **Contents** | Read-only | To check for file presence and read file contents |
| **Issues** | Read & write | To create issues for policy violations and check for duplicate issues |
| **Metadata** | Read-only | To list repositories and access basic repository information |

**Organization Permissions**: None required

### Configuration

The GitHub App authentication requires the following configuration settings:

**For Local Development** (using .NET Secret Manager):

```bash
cd 10xGitHubPolicies.App
dotnet user-secrets set "GitHubApp:AppId" "YOUR_GITHUB_APP_ID"
dotnet user-secrets set "GitHubApp:InstallationId" "YOUR_GITHUB_APP_INSTALLATION_ID"
dotnet user-secrets set "GitHubApp:PrivateKey" "PASTE_YOUR_PRIVATE_KEY_CONTENTS_HERE"
```

**Note**: When setting the `PrivateKey`, paste the full content of the `.pem` file, including the `-----BEGIN RSA PRIVATE KEY-----` and `-----END RSA PRIVATE KEY-----` markers.

**In `appsettings.json`**:

```json
{
  "GitHubApp": {
    "OrganizationName": "your-organization-name"
  }
}
```

### GitHub App Setup

To create a GitHub App for backend services:

1. Go to [GitHub Developer Settings](https://github.com/settings/apps)
2. Click "New GitHub App"
3. Configure the following:
   - **GitHub App name**: 10x GitHub Policy Enforcer
   - **Homepage URL**: `https://localhost:7040/` (for local development)
   - **Webhook URL**: Leave empty for local development
   - **Repository permissions** (as specified in the table above):
     - **Administration**: Read & write
     - **Contents**: Read-only
     - **Issues**: Read & write
     - **Metadata**: Read-only
   - **Organization permissions**: None required
4. After creating the app:
   - Note the **App ID** (found on the app's general page)
   - Generate and download a **private key** (`.pem` file)
   - Install the app to your organization and note the **Installation ID**

For detailed setup instructions, see the [README.md](../README.md#github-app-settings) section.

### Security Considerations

- **Private Key Storage**: Private keys are stored securely using .NET Secret Manager (development) or Azure Key Vault (production)
- **Token Lifetime**: Short-lived installation tokens (1 hour) minimize security exposure
- **Token Caching**: Tokens are cached in memory only, never persisted to disk
- **Least Privilege**: Only the minimum required permissions are requested
- **No User Context**: Backend operations run independently of user sessions, ensuring consistent behavior

## Test Mode

Test Mode is a special application mode designed for E2E testing and development scenarios. When enabled, it bypasses user authentication and authorization checks, allowing automated testing without requiring real GitHub OAuth tokens or team membership verification.

### What Test Mode Does

- **Authentication Bypass**: Automatically authenticates users as a fake `mackowski` user
- **Authorization Bypass**: Skips team membership verification (always returns `true`)
- **GitHub App Services**: Remain fully functional for repository operations
- **No Real Tokens**: Eliminates the need for actual GitHub Personal Access Tokens

### Enabling Test Mode

**Via Configuration File** (Recommended for development):

1. Edit `appsettings.Development.json`:
   ```json
   {
     "TestMode": {
       "Enabled": true
     }
   }
   ```

2. Restart the application:
   ```sh
   dotnet run --launch-profile https
   ```

**Via Environment Variable** (Useful for CI/CD):

```sh
export TestMode__Enabled=true
dotnet run --launch-profile https
```

**Via Command Line** (Temporary override):

```sh
dotnet run --launch-profile https --TestMode:Enabled=true
```

### Disabling Test Mode

**Via Configuration File**:

1. Edit `appsettings.Development.json`:
   ```json
   {
     "TestMode": {
       "Enabled": false
     }
   }
   ```

2. Restart the application

**Via Environment Variable**:

```sh
export TestMode__Enabled=false
dotnet run --launch-profile https
```

### Test Mode Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `TestMode:Enabled` | `bool` | `false` | Enables/disables test mode authentication bypass |

### When to Use Test Mode

- **E2E Testing**: Automated browser tests that need to bypass authentication
- **Development**: Local development without GitHub OAuth setup
- **CI/CD**: Automated testing pipelines
- **Demo Environments**: Quick demonstrations without authentication setup

### Security Considerations

⚠️ **Important**: Test Mode should **NEVER** be enabled in production environments. It completely bypasses security controls and should only be used in development, testing, or demo scenarios.

## Configuration

### Required Settings

The following settings must be configured in user secrets for local development:

**GitHub OAuth App** (for user authentication):
```bash
dotnet user-secrets set "GitHub:ClientId" "YOUR_OAUTH_APP_CLIENT_ID"
dotnet user-secrets set "GitHub:ClientSecret" "YOUR_OAUTH_APP_CLIENT_SECRET"
```

**GitHub App** (for backend services):
```bash
dotnet user-secrets set "GitHubApp:AppId" "YOUR_GITHUB_APP_ID"
dotnet user-secrets set "GitHubApp:InstallationId" "YOUR_GITHUB_APP_INSTALLATION_ID"
dotnet user-secrets set "GitHubApp:PrivateKey" "PASTE_YOUR_PRIVATE_KEY_CONTENTS_HERE"
```

**Note**: When setting the `PrivateKey`, paste the full content of the `.pem` file, including the `-----BEGIN RSA PRIVATE KEY-----` and `-----END RSA PRIVATE KEY-----` markers.

### HTTPS Development Setup

For local development, you need to configure HTTPS certificates:

1. **Trust the development certificate**:
   ```bash
   dotnet dev-certs https --trust
   ```

2. **Verify certificate is valid**:
   ```bash
   dotnet dev-certs https --check
   ```

3. **Run with HTTPS profile**:
   ```bash
   dotnet run --launch-profile https
   ```

The application will be available at:
- **HTTPS**: `https://localhost:7040` (primary)
- **HTTP**: `http://localhost:5222` (redirects to HTTPS)

### GitHub OAuth App Setup

1. Go to [GitHub Developer Settings](https://github.com/settings/developers)
2. Click "New OAuth App"
3. Configure the following:
   - **Application Name**: 10x GitHub Policy Enforcer
   - **Homepage URL**: `https://localhost:7040/` (for local development)
   - **Authorization Callback URL**: `https://localhost:7040/signin-github`
4. Note the Client ID and Client Secret

## Pages and Components

### Authentication Pages

- **`/login`**: Login page with GitHub OAuth button
- **`/logout`**: Handles user sign-out (redirects to `/Account/Logout` which performs the actual logout and redirects to login)
- **`/access-denied`**: Shows when user is not authorized
- **`/onboarding`**: First-time setup wizard for configuration

## Application URLs and Security

The application has several endpoints with different authentication requirements:

### Public Endpoints (No Authentication Required)
| URL | Description | Purpose |
|-----|-------------|---------|
| `/login` | GitHub OAuth login page | User authentication entry point |
| `/logout` | User logout page (redirects to `/Account/Logout`) | Authentication exit point |
| `/Account/Logout` | Logout endpoint that performs sign-out | Controller endpoint for logout |
| `/access-denied` | Access denied page for unauthorized users | Error handling for unauthorized access |
| `/onboarding` | First-time setup wizard for configuration | Initial application setup |
| `/challenge` | OAuth challenge endpoint for authentication flow | OAuth authentication initiation |
| `/signin-github` | GitHub OAuth callback endpoint | OAuth authentication callback |

### Protected Endpoints (Authentication Required)
| URL | Description | Purpose |
|-----|-------------|---------|
| `/` | Main dashboard - compliance overview and repository scanning | Primary application interface |
| `/debug` | Debug information and authentication details | Development and troubleshooting |
| `/hangfire` | Background job dashboard and monitoring | Administrative interface for job management |

### Security Implementation
- **Protected endpoints** use ASP.NET Core's `[Authorize]` attribute or custom authorization filters
- **Hangfire dashboard** uses a custom `HangfireAuthorizationFilter` for authentication
- **Debug page** requires authentication to prevent unauthorized access to sensitive information
- **Public endpoints** are intentionally accessible without authentication for proper OAuth flow

### Authorization Service

The `IAuthorizationService` provides:

- `IsUserAuthorizedAsync(ClaimsPrincipal user)`: Checks if user is authorized
- `GetAuthorizedTeamAsync()`: Retrieves the authorized team from configuration

## Security Considerations

### Token Security

- OAuth access tokens are encrypted in authentication cookies
- Tokens are only accessible server-side
- No sensitive data is exposed to client-side JavaScript

### Session Security

- Sessions expire after 24 hours
- No sliding expiration to prevent indefinite sessions
- Secure cookie settings in production (HTTPS only)

### Authorization

- Team membership is verified on every request
- Configuration is cached but refreshed when needed
- Failed authorization attempts are logged

## Error Handling

### Common Scenarios

1. **Missing Configuration**: Redirects to onboarding wizard
2. **Invalid Configuration**: Redirects to onboarding wizard
3. **Unauthorized User**: Shows access denied page with instructions
4. **Network Errors**: Graceful fallback to access denied

### Logging

All authentication and authorization events are logged with appropriate levels:

- **Information**: Successful authentication and authorization
- **Warning**: Failed team membership checks
- **Error**: Configuration errors and unexpected failures

## Troubleshooting

### Common Issues

1. **"Access Denied" for Valid Team Members**
   - Check that the authorized team is correctly configured in `config.yaml`
   - Verify the team slug format: `organization/team-slug`
   - Ensure the user is actually a member of the team

2. **Configuration Not Found**
   - Verify that `.github/config.yaml` exists in your organization's `.github` repository
   - Check that the file is in the root of the repository
   - Ensure the YAML syntax is valid

3. **OAuth Callback Issues**
   - Verify the callback URL in your GitHub OAuth App settings
   - Ensure the URL matches exactly: `https://localhost:7040/signin-github`
   - Check that the application is running on the correct port
   - Ensure HTTPS is properly configured and certificates are trusted

4. **OAuth State Validation Errors**
   - The application now uses a dedicated `/challenge` endpoint for OAuth initiation
   - OAuth state validation is handled automatically by ASP.NET Core
   - If you encounter "oauth state was missing or invalid" errors, restart the application
   - Ensure the application is running with the HTTPS profile: `dotnet run --launch-profile https`

### Debug Steps

1. Check application logs for authentication errors
2. Verify GitHub OAuth App configuration
3. Test team membership manually via GitHub API
4. Validate `config.yaml` syntax and content

## Production Deployment

### GitHub Secrets Configuration

For production deployments, secrets are stored in GitHub Repository Secrets and applied to Azure App Service during CI/CD:

1. **GitHub Repository Secrets** (Settings → Secrets → Actions):
   - `GH_APP_ID` - GitHub App ID (for backend services)
   - `GH_APP_PRIVATE_KEY` - GitHub App private key (full PEM content)
   - `GH_APP_INSTALLATION_ID` - GitHub App Installation ID
   - `ORG_NAME` - GitHub organization name
   - `OAUTH_CLIENT_ID` - GitHub OAuth App Client ID (for user login)
   - `OAUTH_CLIENT_SECRET` - GitHub OAuth App Client Secret
   - `AZUREAPPSERVICE_CLIENTID_*` - Azure AD App Registration Client ID
   - `AZUREAPPSERVICE_TENANTID_*` - Azure AD Tenant ID
   - `AZUREAPPSERVICE_SUBSCRIPTIONID_*` - Azure Subscription ID

2. **App Settings Applied via CI/CD**: The production deployment workflow (`.github/workflows/ci-cd-prod.yml`) automatically applies these secrets as app settings to Azure App Service during deployment.

3. **Secretless SQL Access**: The application uses Azure Managed Identity (MSI) for database access, eliminating the need for SQL credentials in app settings.

### Environment Configuration

- **Development**: Use .NET Secret Manager (`dotnet user-secrets`)
- **Production**: Use GitHub Secrets (applied to Azure App Service via CI/CD workflow)

## API Integration

The authentication system integrates with:

- **GitHub API**: 
  - For team membership verification (via `IGitHubService.IsUserMemberOfTeamAsync()` - see [GitHub Integration](./github-integration.md))
  - For backend operations using GitHub App authentication (see [Application Authentication (GitHub App)](#application-authentication-github-app) section above)
- **Configuration Service**: For retrieving authorized team settings
- **Dashboard Service**: For loading user-specific data

**Note**: 
- **User Authentication**: User OAuth access tokens obtained during authentication are stored in the authentication cookie. The `AuthorizationService` extracts these tokens and passes them to `GitHubService.IsUserMemberOfTeamAsync()` (described in [GitHub Integration](./github-integration.md)) to verify team membership for authorization checks.
- **Application Authentication**: Backend services use GitHub App authentication (JWT and installation tokens) managed by `GitHubService` for all automated operations. See the [Application Authentication (GitHub App)](#application-authentication-github-app) section for details.
