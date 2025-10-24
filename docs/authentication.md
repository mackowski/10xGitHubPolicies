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

## Configuration

### Required Settings

The following settings must be configured in user secrets for local development:

```bash
dotnet user-secrets set "GitHub:ClientId" "YOUR_OAUTH_APP_CLIENT_ID"
dotnet user-secrets set "GitHub:ClientSecret" "YOUR_OAUTH_APP_CLIENT_SECRET"
```

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
- **`/logout`**: Handles user sign-out and redirects to login
- **`/access-denied`**: Shows when user is not authorized
- **`/onboarding`**: First-time setup wizard for configuration

## Application URLs and Security

The application has several endpoints with different authentication requirements:

### Public Endpoints (No Authentication Required)
| URL | Description | Purpose |
|-----|-------------|---------|
| `/login` | GitHub OAuth login page | User authentication entry point |
| `/logout` | User logout and session cleanup | Authentication exit point |
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

### Azure Key Vault Integration

For production deployments, secrets should be stored in Azure Key Vault:

1. Create Azure Key Vault instance
2. Store secrets with double-underscore naming:
   - `GitHub--ClientId`
   - `GitHub--ClientSecret`
3. Configure managed identity for the App Service
4. Grant Key Vault access permissions

### Environment Configuration

- **Development**: Use .NET Secret Manager
- **Production**: Use Azure Key Vault with managed identity
- **Staging**: Use Azure Key Vault with managed identity

## API Integration

The authentication system integrates with:

- **GitHub API**: For team membership verification
- **Configuration Service**: For retrieving authorized team settings
- **Dashboard Service**: For loading user-specific data

## Future Enhancements

Potential improvements to the authentication system:

1. **Multi-Factor Authentication**: Integration with GitHub's 2FA
2. **Role-Based Access**: Different permission levels within the application
3. **Audit Logging**: Detailed tracking of authentication events
4. **Session Management**: Admin interface for managing active sessions
5. **SSO Integration**: Support for enterprise SSO providers
