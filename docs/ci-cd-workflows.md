# CI/CD Workflows

## Overview

The project uses GitHub Actions for continuous integration and continuous deployment. The main workflow runs on pull requests to ensure code quality, test coverage, and compliance before code is merged.

In addition to the pull request workflow, the project uses [Dependabot](https://docs.github.com/en/code-security/dependabot) to automatically manage dependency updates. Dependabot pull requests go through the same CI/CD pipeline as regular pull requests, ensuring all updates are tested before merging.

## Pull Request Workflow

The **Pull Request** workflow (`.github/workflows/pull-request.yml`) is triggered automatically on pull requests to `main` or `develop` branches. It performs checks including code linting, multi-level testing, code coverage reporting, and automatic PR status comments.

### Workflow Structure

The workflow is organized into several interdependent jobs that run in parallel where possible to minimize execution time:

```
lint
 ├── unit-tests ──┐
 ├── component-tests ──┐
 │                    ├── integration-tests ──┐
 │                    ├── contract-tests ──┐  │
 │                    │                    │  ├── publish-coverage ──┐
 │                    │                    │  │                       │
 │                    │                    │  └── status-comment ─────┘
```

### Jobs

#### 1. Lint Job
**Purpose**: Enforces code formatting standards  
**Runs on**: Every PR  

Performs:
- Restores .NET dependencies
- Runs `dotnet format --verify-no-changes` to ensure all code adheres to project formatting rules
- Fails if any formatting changes are detected (prevents merge of unformatted code)

#### 2. Unit Tests Job
**Purpose**: Fast feedback on business logic correctness  
**Depends on**: `lint`  
**Runs on**: After linting passes  

Performs:
- Runs all tests tagged with `Category=Unit`
- Collects code coverage using Coverlet
- Generates TRX (Test Results XML) file for test reporting
- Uploads coverage and test result artifacts

**Coverage**: Business logic, services, helpers, utility functions

#### 3. Component Tests Job
**Purpose**: Validates Blazor component rendering and interactivity  
**Depends on**: `lint` (runs in parallel with unit-tests)  
**Runs on**: After linting passes  

Performs:
- Runs all tests tagged with `Category=Component`
- Tests Blazor components using bUnit
- Collects code coverage
- Generates TRX file for test reporting
- Uploads coverage and test result artifacts

**Coverage**: Blazor components, UI logic, authentication flows

#### 4. Integration Tests Job
**Purpose**: Validates service interactions with external dependencies  
**Depends on**: `unit-tests`, `component-tests`  
**Runs on**: After unit and component tests pass  

Performs:
- Runs all tests tagged with `Category=Integration`
- Uses WireMock.Net to mock GitHub API responses
- Uses Testcontainers for ephemeral database instances
- Collects code coverage
- Generates TRX file for test reporting
- Uploads coverage and test result artifacts

**Coverage**: GitHubService methods, database operations, service integrations

#### 5. Contract Tests Job
**Purpose**: Validates GitHub API contract compliance  
**Depends on**: `unit-tests`, `component-tests` (runs in parallel with integration-tests)  
**Runs on**: After unit and component tests pass  

Performs:
- Runs all tests tagged with `Category=Contract`
- Uses NJsonSchema for JSON schema validation
- Uses Verify.NET for snapshot testing
- Validates GitHub API response contracts
- Collects code coverage
- Generates TRX file for test reporting
- Uploads coverage and test result artifacts

**Coverage**: GitHub API response contracts, schema validation

#### 6. Publish Coverage Job
**Purpose**: Generates and publishes comprehensive code coverage reports  
**Depends on**: All test jobs  
**Runs on**: After all tests complete (regardless of pass/fail with `if: always()`)  

Performs:
- Downloads all test coverage artifacts from previous jobs
- Installs ReportGenerator .NET tool
- Aggregates coverage data from all test levels
- Generates HTML, JSON, Badges, and Cobertura format reports
- Filters out test projects from coverage calculations
- Uploads comprehensive coverage report artifact
- Extracts coverage percentage for PR comments
- Publishes coverage to Codecov with multiple flags (unit, component, integration, contract)

**Output Artifacts**:
- `coverage-report/index.html` - Interactive HTML coverage report
- `coverage-report/Summary.json` - Coverage summary in JSON format
- `coverage-report/Cobertura.xml` - Cobertura format for external tools
- `coverage-report/badges/` - Coverage badge images

#### 7. Status Comment Job
**Purpose**: Provides PR authors with clear feedback on CI status  
**Depends on**: All jobs (lint, tests, coverage)  
**Runs on**: After all jobs complete (regardless of pass/fail with `if: always()`)  
**Only on**: Pull requests (not on direct pushes)  

Performs:
- Downloads all test result artifacts
- Parses TRX files to extract test statistics (total, passed, failed)
- Reads coverage percentage from coverage report
- Determines overall status based on job results
- Creates or updates PR comment with:
  - Overall status (✅ All checks passed / ❌ Tests failed / ❌ Linting failed)
  - Test results summary (total, passed, failed)
  - Job status table with emoji indicators
  - Code coverage percentage
- Updates existing bot comment if one exists (prevents comment spam)

**Comment Format**:
```markdown
## 🔍 Pull Request CI Status

✅ All checks passed

### Test Results Summary
- **Total Tests:** 120
- **Passed:** 118
- **Failed:** 2

### Job Status
| Job | Status |
|-----|--------|
| Linting | ✅ |
| Unit Tests | ✅ |
| Component Tests | ✅ |
| Integration Tests | ✅ |
| Contract Tests | ✅ |

### Code Coverage
**Line Coverage:** 87.45%

---
*This comment is automatically updated on each workflow run.*
```

## Security Considerations

### Pinned Action Versions

All GitHub Actions used in workflows are **pinned to full-length commit SHAs** rather than version tags for security compliance. This prevents supply chain attacks from malicious updates to action versions.

**Pinned Actions**:
- `actions/checkout@b4ffde65f46336ab88eb53be808477a3936bae11` (v4)
- `actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9` (v4)
- `actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02` (v4)
- `actions/download-artifact@d3f86a106a0bac45b974a628896c90dbdf5c8093` (v4)
- `codecov/codecov-action@b9fd7d16f6d7d1b5d2bec1a2887e65ceed900238` (v4)
- `actions/github-script@f28e40c7f34bde8b3046d885e986cb6290c5673b` (v7)

**Updating Action SHAs**:
1. Visit the action's GitHub repository Releases page (e.g., https://github.com/actions/checkout/releases/tag/v4)
2. Find the commit SHA for the desired release
3. Update the SHA in the workflow file
4. Test the workflow to ensure compatibility

### Workflow Permissions

The workflow uses minimal required permissions:
- `contents: read` - Read repository contents
- `packages: read` - Read NuGet packages
- `pull-requests: write` - Create/update PR comments

Following the principle of least privilege reduces the attack surface if a workflow is compromised.

## Code Coverage

### Coverage Collection

Coverage is collected at multiple levels using Coverlet:
- **Unit Tests**: Coverage of business logic and services
- **Component Tests**: Coverage of Blazor components
- **Integration Tests**: Coverage of integration points
- **Contract Tests**: Coverage of contract validation logic

### Coverage Reporting

The aggregated coverage report:
- **Excludes**: Test projects (`*Tests*`, `*TestHelpers*`)
- **Formats**: HTML, JSON Summary, Cobertura XML, Badges
- **Publishes**: To Codecov with separate flags for each test level
- **Filters**: Test helpers and test infrastructure from coverage calculations

### Coverage Targets

While not enforced by CI, the project aims for:
- **Overall Coverage**: 80%+ line coverage
- **Business Logic**: 90%+ line coverage
- **Services**: 85%+ line coverage

## Local Workflow Execution

### Running the Workflow Locally

To replicate the GitHub Actions pull request workflow locally before pushing changes, use the provided script:

```bash
./test-workflow-local.sh
```

This script executes the same sequence as the CI/CD pipeline:
1. **Linting**: Runs `dotnet format --verify-no-changes`
2. **Unit Tests**: Executes all tests with `Category=Unit`
3. **Component Tests**: Executes all tests with `Category=Component`
4. **Integration Tests**: Executes all tests with `Category=Integration`
5. **Contract Tests**: Executes all tests with `Category=Contract`

**Output**:
- Test results are saved to `./coverage/` directory as TRX files
- Console output shows progress and results for each step
- Exits with error code if any step fails

**Benefits**:
- ✅ Catch issues before pushing to GitHub
- ✅ Faster feedback cycle (no CI queue wait)
- ✅ Same test filters and configuration as CI/CD
- ✅ Test result files available for local analysis

**Prerequisites**:
- .NET 8 SDK installed
- All project dependencies restored (`dotnet restore`)
- No database required for integration tests (uses WireMock)

### Manual Workflow Steps

If you prefer to run steps manually or need to debug a specific phase:

```bash
# 1. Linting
dotnet restore
dotnet format --verify-no-changes --verbosity diagnostic

# 2. Unit Tests
dotnet test \
  --filter "Category=Unit" \
  --results-directory ./coverage \
  --logger "trx;LogFileName=unit-tests.trx" \
  --logger "console;verbosity=detailed"

# 3. Component Tests
dotnet test \
  --filter "Category=Component" \
  --results-directory ./coverage \
  --logger "trx;LogFileName=component-tests.trx" \
  --logger "console;verbosity=detailed"

# 4. Integration Tests
dotnet test \
  --filter "Category=Integration" \
  --results-directory ./coverage \
  --logger "trx;LogFileName=integration-tests.trx" \
  --logger "console;verbosity=detailed"

# 5. Contract Tests
dotnet test \
  --filter "Category=Contract" \
  --results-directory ./coverage \
  --logger "trx;LogFileName=contract-tests.trx" \
  --logger "console;verbosity=detailed"
```

## Dependabot Integration

The project uses GitHub Dependabot for automated dependency management, configured via `.github/dependabot.yml`.

### Configuration

Dependabot monitors three package ecosystems:

1. **NuGet Packages** (.NET dependencies)
   - Weekly update checks
   - Up to 10 concurrent pull requests
   - Commit message prefix: `chore(nuget)`
   - Labels: `dependencies`, `nuget`

2. **GitHub Actions** (workflow action versions)
   - Weekly update checks
   - Up to 5 concurrent pull requests
   - Commit message prefix: `chore(github-actions)`
   - Labels: `dependencies`, `github-actions`

3. **Docker Dependencies** (container images)
   - Weekly update checks
   - Up to 5 concurrent pull requests
   - Commit message prefix: `chore(docker)`
   - Labels: `dependencies`, `docker`

### Update Behavior

- **Cooldown Period**: 14 days between updates for the same dependency to prevent excessive PRs
- **Automatic Review Assignment**: Pull requests are automatically assigned to designated reviewers
- **Conventional Commits**: All dependency updates follow the conventional commit format for consistency
- **CI/CD Integration**: Dependabot PRs automatically trigger the pull request workflow, running all linting, tests, and coverage checks

### Managing Dependabot PRs

1. **Review**: Each PR includes a changelog and release notes when available
2. **Test**: All PRs run through the full CI/CD pipeline automatically
3. **Merge**: Once approved and tests pass, merge using standard GitHub PR workflow
4. **Bulk Operations**: Use GitHub's "Merge" or "Rebase and merge" options for multiple dependency updates

### Best Practices

- **Regular Reviews**: Review and merge Dependabot PRs regularly to stay current with security patches and features
- **Batch Updates**: Consider batching multiple minor/patch updates together when possible
- **Test Coverage**: All dependency updates are automatically tested, but manual verification is recommended for major version updates
- **Security Updates**: Prioritize security-related dependency updates (Dependabot will label these accordingly)

For more information, see the [Dependabot documentation](https://docs.github.com/en/code-security/dependabot).

## Troubleshooting

### Workflow Failures

**Lint Failure**:
```bash
# Fix formatting issues locally
dotnet format

# Verify no changes
dotnet format --verify-no-changes
```

**Test Failures**:
- Check test output in GitHub Actions logs
- Run failing tests locally: `dotnet test --filter "Category=Unit"`
- Review test artifacts uploaded to workflow examples
- Use local workflow script: `./test-workflow-local.sh`

**Coverage Report Missing**:
- Verify coverage files are generated in test jobs
- Check artifact upload/download steps succeeded
- Review ReportGenerator installation step

**PR Comment Not Appearing**:
- Ensure workflow has `pull-requests: write` permission
- Check that `github.event_name == 'pull_request'` condition is met
- Review GitHub Script action logs for API errors

**Local Script Issues**:
- Ensure script has execute permissions: `chmod +x test-workflow-local.sh`
- Verify .NET SDK is installed: `dotnet --version`
- Check test project dependencies are restored: `dotnet restore`

## Production Deployment Workflow

The **Production Deployment** workflow (`.github/workflows/ci-cd-prod.yml`) handles building and deploying the application to Azure App Service.

### Workflow Structure

```
build
 └── deploy
```

### Trigger

- **Automatic**: On push to `main` branch
- **Manual**: Via `workflow_dispatch` in GitHub Actions UI

### Build Job

**Purpose**: Compile and package the application for production  
**Runs on**: `ubuntu-latest`  
**Permissions**: `contents: read`

**Steps**:
1. Checks out code
2. Sets up .NET 8.x SDK
3. Builds solution in Release configuration
4. Publishes application to `./publish`
5. Uploads artifact (`.net-app`) for deployment job

### Deploy Job

**Purpose**: Deploy the application to Azure App Service  
**Runs on**: `ubuntu-latest`  
**Depends on**: `build`  
**Permissions**: 
- `id-token: write` (required for OIDC authentication)
- `contents: read`

**Steps**:
1. Downloads build artifact from `build` job
2. Authenticates to Azure using OIDC (via `azure/login@v2`)
3. Deploys to Azure Web App (`wa-10xghpolicies-prod`)
4. Applies app settings (idempotent operation)

### Required Secrets

The workflow requires the following GitHub Repository Secrets:

#### Azure Authentication
- `AZUREAPPSERVICE_CLIENTID_*` - Azure AD App Registration Client ID
- `AZUREAPPSERVICE_TENANTID_*` - Azure AD Tenant ID
- `AZUREAPPSERVICE_SUBSCRIPTIONID_*` - Azure Subscription ID

**Note**: These secrets use the format generated by Azure Portal when deploying via "Deploy to Web App" action. The suffix is a hash for uniqueness.

#### Application Configuration
- `GH_APP_ID` - GitHub App ID (for backend services)
- `GH_APP_PRIVATE_KEY` - GitHub App private key (full PEM content)
- `GH_APP_INSTALLATION_ID` - GitHub App Installation ID
- `ORG_NAME` - GitHub organization name
- `OAUTH_CLIENT_ID` - GitHub OAuth App Client ID (for user login)
- `OAUTH_CLIENT_SECRET` - GitHub OAuth App Client Secret

### App Settings

The deployment workflow sets the following app settings on Azure App Service:

- `ASPNETCORE_ENVIRONMENT=Production`
- `TestMode__Enabled=false`
- `GitHubApp__AppId` - GitHub App ID
- `GitHubApp__PrivateKey` - GitHub App private key
- `GitHubApp__InstallationId` - Installation ID
- `GitHubApp__OrganizationName` - Organization name
- `GitHub__ClientId` - OAuth Client ID
- `GitHub__ClientSecret` - OAuth Client Secret
- `ConnectionStrings__DefaultConnection` - MSI-based SQL connection string

### SQL Connection

The application uses Azure Managed Identity (MSI) for secretless database access. The connection string format is:

```
Server=tcp:sql-10xghpolicies.database.windows.net,1433;Database=sqldb-10xghpolicies;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity
```

This eliminates the need for SQL username/password credentials in app settings.

For a complete production deployment guide, see `./production-deployment.md`.
