# 10x GitHub Policy Enforcer

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A GitHub App to automate the enforcement of organizational policies and security best practices across all repositories within a GitHub organization.

---

## Table of Contents

- [About The Project](#about-the-project)
- [Features](#features)
- [Tech Stack](#tech-stack)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Installation](#installation)
- [Configuration](#configuration)
- [Testing](#testing)
- [Dependency Management](#dependency-management)
- [Documentation](#documentation)
- [Production Deployment](#production-deployment)
- [Project Scope](#project-scope)
- [License](#license)

---

## About The Project

The 10x GitHub Policy Enforcer is a GitHub App with an accompanying web UI designed to automate the enforcement of organizational policies and security best practices across all repositories within a GitHub organization.

It uses a flexible policy evaluation engine to scan repositories for compliance with a centrally managed configuration file. When violations are found, it can automatically perform actions like creating issues in the repository or archiving it. The web dashboard provides a clear overview of your organization's compliance posture.

The application uses a dual-authentication strategy:
- **GitHub App**: For backend services to perform automated scans and actions
- **GitHub OAuth App**: For user authentication to the web dashboard

---

## Features

*   **Automated Scans:** Performs daily (via scheduled jobs) and on-demand scans of all active repositories.
*   **Centralized Configuration:** All policies are defined in a single `.github/config.yaml` file for easy management.
*   **Extensible Policy Engine:** The application uses a strategy pattern to make it easy to add new policy evaluators.
*   **Automated Actions:** Creates issues or archives repositories that violate policies with duplicate prevention and comprehensive action logging.
*   **Compliance Dashboard:** A Blazor-based web UI to view non-compliant repositories, violation details, and overall compliance metrics.
*   **Background Job Processing:** Uses Hangfire for reliable background processing of scans and actions, ensuring the UI remains responsive.
*   **Action Logging:** All automated actions are logged to the database with status tracking and detailed information.
*   **API Documentation:** Provides Swagger/OpenAPI documentation for any exposed API endpoints.

---

## Tech Stack

| Category          | Technology                               |
| ----------------- | ---------------------------------------- |
| **Backend**       | ASP.NET Core (.NET 8)                    |
| **Frontend**      | Blazor Server with Microsoft Fluent UI   |
| **Database**      | Azure SQL Database (or local SQL Server) |
| **Background Jobs** | Hangfire                                 |
| **API Docs**      | Swagger / OpenAPI                        |
| **Hosting**       | Azure App Service                        |
| **GitHub API**    | Octokit.net                              |
| **Testing**       | xUnit, bUnit, NSubstitute, WireMock.Net, Testcontainers, NJsonSchema, Verify.NET, Playwright |
| **CI/CD**         | GitHub Actions                           |

---

## Getting Started

To get a local copy up and running, follow these simple steps.

### Prerequisites

*   [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
*   [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### Installation

1.  Clone the repo:
    ```sh
    git clone https://github.com/your_username/10xGitHubPolicies.git
    ```
2.  Navigate to the project root directory:
    ```sh
    cd 10xGitHubPolicies
    ```
3. Start the local SQL Server database:
    ```sh
    docker-compose up -d
    ```
4.  Navigate to the application directory:
    ```sh
    cd 10xGitHubPolicies.App
    ```
5.  Install dependencies:
    ```sh
    dotnet restore
    ```
6. Apply database migrations:
    ```sh
    dotnet ef database update
    ```
7.  Trust the HTTPS development certificate:
    ```sh
    dotnet dev-certs https --trust
    ```
8.  Run the application with HTTPS profile:
    ```sh
    dotnet run --launch-profile https
    ```
Alternatively, you can run the project from the root directory:
```sh
dotnet run --project 10xGitHubPolicies.App/10xGitHubPolicies.App.csproj --launch-profile https
```

The application will be available at:
- **HTTPS**: `https://localhost:7040` (primary)
- **HTTP**: `http://localhost:5222` (redirects to HTTPS)

## Application URLs

| URL | Description | Authentication Required |
|-----|-------------|------------------------|
| `/` | Main dashboard - compliance overview and repository scanning | ✅ Yes |
| `/login` | GitHub OAuth login page | ❌ No (public) |
| `/logout` | User logout and session cleanup | ❌ No (public) |
| `/access-denied` | Access denied page for unauthorized users | ❌ No (public) |
| `/onboarding` | First-time setup wizard for configuration | ❌ No (public) |
| `/debug` | Debug information and authentication details | ✅ Yes |
| `/hangfire` | Background job dashboard and monitoring | ✅ Yes |
| `/challenge` | OAuth challenge endpoint for authentication flow | ❌ No (public) |
| `/signin-github` | GitHub OAuth callback endpoint | ❌ No (public) |

---

## Configuration

The application is configured via `appsettings.json` and user secrets for sensitive data.

### Test Mode

Test Mode is a special application mode designed for E2E testing and development scenarios. When enabled, it bypasses user authentication and authorization checks, allowing automated testing without requiring real GitHub OAuth tokens or team membership verification.

#### What Test Mode Does

- **Authentication Bypass**: Automatically authenticates users as a fake `mackowski` user
- **Authorization Bypass**: Skips team membership verification (always returns `true`)
- **GitHub App Services**: Remain fully functional for repository operations
- **No Real Tokens**: Eliminates the need for actual GitHub Personal Access Tokens

#### Enabling Test Mode

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

#### Disabling Test Mode

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

#### Test Mode Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `TestMode:Enabled` | `bool` | `false` | Enables/disables test mode authentication bypass |

#### When to Use Test Mode

- **E2E Testing**: Automated browser tests that need to bypass authentication
- **Development**: Local development without GitHub OAuth setup
- **CI/CD**: Automated testing pipelines
- **Demo Environments**: Quick demonstrations without authentication setup

#### Security Considerations

⚠️ **Important**: Test Mode should **NEVER** be enabled in production environments. It completely bypasses security controls and should only be used in development, testing, or demo scenarios.

### GitHub App Settings
The application uses a dual-authentication strategy requiring both a GitHub App (for backend services) and a GitHub OAuth App (for user authentication).

#### GitHub App (Backend Services)
The GitHub App is used by backend services to perform automated scans and actions against the GitHub API.

1.  Initialize user secrets for the project (if you haven't already):
    ```sh
    cd 10xGitHubPolicies.App
    dotnet user-secrets init
    ```
2.  Set the GitHub App secrets. Replace the placeholder values with your App's credentials:
    ```sh
    dotnet user-secrets set "GitHubApp:AppId" "YOUR_GITHUB_APP_ID"
    dotnet user-secrets set "GitHubApp:InstallationId" "YOUR_GITHUB_APP_INSTALLATION_ID"
    dotnet user-secrets set "GitHubApp:PrivateKey" "PASTE_YOUR_PRIVATE_KEY_CONTENTS_HERE"
    ```
    **Note**: When setting the `PrivateKey`, paste the full content of the `.pem` file, including the `-----BEGIN RSA PRIVATE KEY-----` and `-----END RSA PRIVATE KEY-----` markers.

3.  Configure the organization name in `appsettings.json`:
    ```json
    {
      "GitHubApp": {
        "OrganizationName": "your-organization-name"
      }
    }
    ```
    Replace `your-organization-name` with your GitHub organization's slug.

#### GitHub App Setup
To create a GitHub App for backend services:

1. Go to [GitHub Developer Settings](https://github.com/settings/apps)
2. Click "New GitHub App"
3. Configure the following:
   - **GitHub App name**: 10x GitHub Policy Enforcer
   - **Homepage URL**: `https://localhost:7040/` (for local development)
   - **Webhook URL**: Leave empty for local development
   - **Repository permissions**:
     - **Administration**: Read & write (to archive repositories)
     - **Contents**: Read-only (to check for file presence)
     - **Issues**: Read & write (to create and check for duplicate issues)
     - **Metadata**: Read-only (to list repositories)
   - **Organization permissions**: None required
4. After creating the app:
   - Note the **App ID** (found on the app's general page)
   - Generate a **Private Key** (download the `.pem` file)
   - Install the app on your organization and note the **Installation ID**

#### GitHub OAuth App (User Authentication)
For user authentication, you need to create a GitHub OAuth App:

1. Go to [GitHub Developer Settings](https://github.com/settings/developers)
2. Click "New OAuth App"
3. Configure the following:
   - **Application Name**: 10x GitHub Policy Enforcer
   - **Homepage URL**: `https://localhost:7040/` (for local development)
   - **Authorization Callback URL**: `https://localhost:7040/signin-github`
4. Note the Client ID and Client Secret
5. Set the OAuth App secrets:
    ```sh
    dotnet user-secrets set "GitHub:ClientId" "YOUR_OAUTH_APP_CLIENT_ID"
    dotnet user-secrets set "GitHub:ClientSecret" "YOUR_OAUTH_APP_CLIENT_SECRET"
    ```

### Policy Configuration
The policy configuration is managed via a `config.yaml` file located in the root of your organization's `.github` repository.

Here is an example configuration:

```yaml
# .github/config.yaml

# Access control: Specify the GitHub team authorized to access the dashboard.
# Format: 'organization-slug/team-slug'
authorized_team: 'my-org/security-team'

# Policies: Define the rules to enforce across your organization's repositories.
policies:
  - name: 'Check for AGENTS.md'
    type: 'has_agents_md'
    action: 'create_issue' # 'create_issue', 'archive_repo', or 'log_only'
    issue_details:
      title: 'Compliance: AGENTS.md file is missing'
      body: 'This repository is missing the AGENTS.md file in its root directory. Please add this file to comply with organization standards.'
      labels: ['policy-violation', 'documentation']

  - name: 'Check for catalog-info.yaml'
    type: 'has_catalog_info_yaml'
    action: 'create_issue'
    issue_details:
      title: 'Compliance: catalog-info.yaml is missing'
      body: 'This repository is missing the catalog-info.yaml file. This file is required for backstage.io service discovery.'
      labels: ['policy-violation', 'backstage']
      
  - name: 'Verify Workflow Permissions'
    type: 'correct_workflow_permissions'
    action: 'log_only'
```

---

## Testing

The project employs a comprehensive multi-level testing strategy:

### Test Projects

1. **10xGitHubPolicies.Tests** - Unit tests
   - Services, policies, and business logic
   - Blazor component tests using bUnit
   - Run: `dotnet test 10xGitHubPolicies.Tests/10xGitHubPolicies.Tests.csproj`

2. **10xGitHubPolicies.Tests.Integration** - Integration tests 
   - 33 tests for GitHubService with WireMock.Net
   - HTTP-level mocking of GitHub API
   - Coverage: file operations, repositories, issues, workflow permissions, rate limiting, token caching, team membership
   - Run: `dotnet test 10xGitHubPolicies.Tests.Integration/10xGitHubPolicies.Tests.Integration.csproj --filter "Category=Integration"`

3. **10xGitHubPolicies.Tests.Contracts** - Contract tests 
   - 11 tests validating GitHub API response contracts
   - Schema validation using NJsonSchema (6 tests)
   - Snapshot testing using Verify.NET (5 tests)
   - Coverage: repository, issue, and workflow permissions responses
   - Run: `dotnet test 10xGitHubPolicies.Tests.Contracts/10xGitHubPolicies.Tests.Contracts.csproj --filter "Category=Contract"`

4. **10xGitHubPolicies.Tests.E2E** - End-to-end tests
   - Browser automation tests using Playwright
   - Full workflow testing from UI to database
   - Uses Test Mode for authentication bypass (see [Test Mode](#test-mode) section)
   - Requires Playwright browsers installation: `pwsh bin/Debug/net8.0/playwright.ps1 install chromium`
   - **Note**: Web application must be running manually before executing E2E tests
   - Run: `dotnet test 10xGitHubPolicies.Tests.E2E/10xGitHubPolicies.Tests.E2E.csproj`

### Running All Tests

```sh
dotnet test
```

### Running Local Workflow (Replicates CI/CD Pipeline)

A local testing script is available to replicate the GitHub Actions pull request workflow locally:

```sh
./test-workflow-local.sh
```

This script executes the same sequence as the CI/CD pipeline:
1. **Linting**: Code formatting verification
2. **Unit Tests**: Fast business logic validation
3. **Component Tests**: Blazor UI component testing
4. **Integration Tests**: GitHub API integration with WireMock
5. **Contract Tests**: API contract validation

Test results are saved to the `./coverage` directory as TRX files for analysis.

**Note**: This script uses the same test filters and logging configuration as the GitHub Actions workflow, ensuring consistency between local and CI environments.

For more details, see **[Testing Strategy](./docs/testing-strategy.md)** and **[CI/CD Workflows](./docs/ci-cd-workflows.md)**.

---

## Dependency Management

The project uses [Dependabot](https://docs.github.com/en/code-security/dependabot) to automatically keep dependencies up to date.

### Automated Dependency Updates

Dependabot is configured via `.github/dependabot.yml` and monitors the following:

- **NuGet Packages** (.NET): Weekly updates with conventional commit prefixes (`chore(nuget)`)
- **GitHub Actions**: Weekly updates for workflow action versions (`chore(github-actions)`)
- **Docker Dependencies**: Weekly updates for Docker images in `docker-compose.yml` (`chore(docker)`)

### Configuration Details

- **Update Schedule**: Weekly checks
- **Cooldown Period**: 14 days to prevent update spam and malware
- **Pull Request Limits**: 
  - NuGet: Up to 10 concurrent PRs
  - GitHub Actions: Up to 5 concurrent PRs
  - Docker: Up to 5 concurrent PRs
- **Commit Messages**: Follow conventional commit format with scoped prefixes
- **Reviewers**: Pull requests are automatically assigned for review
- **Labels**: Automatic labeling (`dependencies`, `nuget`, `github-actions`, `docker`)

### Managing Updates

Dependabot pull requests automatically run through the same CI/CD pipeline as regular pull requests, including:
- Code formatting checks
- Unit, component, integration, and contract tests
- Code coverage reporting

You can review and merge these updates through the standard pull request process.

---

## Documentation

Detailed documentation for specific features and integrations:

- **[Authentication](./docs/authentication.md)**: User authentication and authorization system
- **[GitHub Integration](./docs/github-integration.md)**: How to use the GitHub API service for repository management
- **[GitHub Client Factory](./docs/github-client-factory.md)**: Factory pattern for testable GitHub API integration
- **[Configuration Service](./docs/configuration-service.md)**: Managing centralized policy configuration from `.github/config.yaml`
- **[Action Service](./docs/action-service.md)**: Automated action processing for policy violations
- **[Hangfire Integration](./docs/hangfire-integration.md)**: Background job processing and scheduling
- **[Policy Evaluation](./docs/policy-evaluation.md)**: How the policy evaluation engine works and how to add new policies
- **[Testing Strategy](./docs/testing-strategy.md)**: Comprehensive testing approach, tooling, and best practices
- **[Contract Testing](./docs/testing-contract-tests.md)**: Detailed guide to contract testing with WireMock, Verify.NET, and JSON Schema
- **[E2E Testing](./docs/testing-e2e-tests.md)**: Complete guide to End-to-End testing with Playwright
- **[CI/CD Workflows](./docs/ci-cd-workflows.md)**: GitHub Actions workflows, code coverage, and automated testing pipelines

---

## Production Deployment

For production, use GitHub Actions with Azure OIDC (workload identity federation) and secretless SQL access via Azure Managed Identity (MSI) or Azure AD token authentication.

- Guide: **[Production Deployment (Azure OIDC + MSI)](./docs/production-deployment.md)**
- Database migrations: The `Tools/DbMigrator` console application supports both:
  - **Azure AD Token Authentication** (used in CI/CD pipelines)
  - **Azure Managed Identity** (used in Azure App Service)

The migrations run automatically as part of the production deployment workflow. For manual execution:

```bash
dotnet run --project Tools/DbMigrator/DbMigrator.csproj --configuration Release
```

See the [Production Deployment guide](./docs/production-deployment.md) for detailed authentication configuration.

---

## Project Scope

### In Scope (MVP)
*   ✅ `[done]` Configuration managed via a single `config.yaml` file in the `.github` repository.
*   ✅ `[done]` Daily and on-demand scanning of all active repositories.
*   ✅ `[done]` Core policies:
    *   ✅ `[done]` Verify presence of `AGENTS.md`.
    *   ✅ `[done]` Verify presence of `catalog-info.yaml`.
    *   ✅ `[done]` Verify repository Workflow Permissions are set to 'Read repository contents and packages permissions'.
*   ✅ `[done]` Automated actions:
    *   ✅ `[done]` Create a GitHub Issue in the non-compliant repository with duplicate prevention.
    *   ✅ `[done]` Archive the non-compliant repository.
    *   ✅ `[done]` Log-only actions for monitoring without taking automated steps.
    *   ✅ `[done]` Comprehensive action logging with status tracking.
*   ✅ `[done]` A simple dashboard showing non-compliant repositories, violation details, overall compliance percentage, and a repository name filter.
*   ✅ `[done]` Authentication via "Login with GitHub" OAuth flow.
*   ✅ `[done]` Access restricted to a specified GitHub Team.


### Out of Scope (MVP)
*   Automatically fixing policy violations (e.g., creating the missing file).
*   Support for other version control systems (e.g., GitLab, Bitbucket).
*   Advanced policy types (e.g., checking file content, branch protection rules).
*   User-level permissions within the application.
*   Repository-level exceptions or overrides in the UI.

### Ideas
*   Logs - production
*   Advanced policy types (e.g., checking file content)
*   Action - PR Blocking
*   Action - Log only
*   Action - Slack notification
*   Action - Fix (update github settings)
*   Exception policies
*   Getting team ownership 
*   Better project structure e.g. FrontEnd/UI 
*   Remove dups between .ai and /docs
*   Review test coverage
*   E2E tests and scenatios improvements

---

## License

Distributed under the MIT License. See `LICENSE` for more information.
