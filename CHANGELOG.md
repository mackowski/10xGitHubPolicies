# Changelog

All notable changes to this project will be documented in this file.

## 1.9

### Added
- **MSI DbMigrator console**: `Tools/DbMigrator` to run EF Core migrations using Azure Managed Identity (no SQL secrets in CI)
  - Reads `ConnectionStrings__DefaultConnection` from environment
  - Executes `db.Database.MigrateAsync()`
- **Production deployment guide**: New documentation for Azure OIDC + MSI-based CI/CD
  - Added `docs/production-deployment.md` (runbook in `/.ai/production-deployment.md`)
  - References GitHub Environments gating and secretless SQL

### Changed
- **CI/CD Documentation**: Updated `docs/ci-cd-workflows.md` and `README.md` to include production deployment reference and MSI migration step example

## 1.8

### Added
- **Dependabot Configuration**: Automated dependency update management
  - Created `.github/dependabot.yml` for automated dependency updates
  - Configures weekly updates for NuGet packages (.NET), GitHub Actions, and Docker dependencies
  - Includes cooldown periods (14 days) to prevent update spam
  - Automatically creates pull requests with conventional commit message prefixes
  - Assigns reviewers and applies appropriate labels for dependency updates
  - Limits concurrent open pull requests (10 for NuGet, 5 for GitHub Actions and Docker)
- **Local Workflow Testing Script**: Script to replicate CI/CD pipeline locally
  - Created `test-workflow-local.sh` to execute the same test sequence as GitHub Actions
  - Runs linting, unit tests, component tests, integration tests, and contract tests
  - Outputs test results to `./coverage/` directory as TRX files
  - Provides faster feedback cycle by catching issues before pushing to GitHub
  - Same test filters and configuration as CI/CD workflow ensures consistency
- **Enhanced GitHubClientFactory**: Improved test infrastructure support
  - Added support for custom `HttpClientHandler` in `GitHubClientFactory`
  - Enables better SSL certificate handling for test scenarios
  - Supports custom HTTP configuration for WireMock integration
  - Maintains backward compatibility with existing implementations
- **Integration Test SSL Certificate Handling**: Enhanced WireMock fixture for HTTPS support
  - Updated `GitHubApiFixture` to use `HttpClientHandler` instead of `ServicePointManager`
  - Modern .NET Core approach for SSL certificate validation
  - Handles self-signed certificates from WireMock server
  - Properly disposes of HttpClientHandler in cleanup to avoid resource leaks
  - Enables realistic SSL communication testing without certificate errors

### Changed
- **CI/CD Workflow**: Enhanced pull request workflow
  - Improved workflow structure with better job dependencies
  - Updated status comment job to provide comprehensive test results summary
- **Documentation Updates**:
  - Updated `README.md` with local workflow testing script documentation
  - Enhanced `docs/ci-cd-workflows.md` with local workflow execution guide
  - Updated `docs/testing-integration-tests.md` with SSL certificate handling details
  - Updated `README.md` with Dependabot dependency management information
  - Enhanced `docs/ci-cd-workflows.md` with Dependabot integration details

## 1.7

### Added
- **CI/CD Workflow**: Comprehensive pull request workflow for automated testing and quality checks
  - Created `.github/workflows/pull-request.yml` workflow that runs on PRs to `main` and `develop` branches
  - Multi-level testing pipeline: lint → unit tests → component tests → integration/contract tests → coverage report
  - Automated code formatting verification using `dotnet format --verify-no-changes`
  - Parallel test execution for faster feedback (unit and component tests run concurrently)
  - Comprehensive code coverage collection and reporting with ReportGenerator
  - Coverage aggregation across all test levels (unit, component, integration, contract)
  - Codecov integration for coverage tracking with separate flags per test level
  - Automated PR status comments with test results and coverage percentage
  - Test result artifacts (TRX files) uploaded for analysis
  - All GitHub Actions pinned to commit SHAs for security compliance
  - Minimal workflow permissions following principle of least privilege
- **CI/CD Documentation**: Comprehensive documentation for workflows
  - Created `docs/ci-cd-workflows.md` with detailed workflow documentation
  - Covers job structure, security considerations, coverage reporting, and troubleshooting
  - Includes local testing guide and workflow best practices
  - Documents security practices for pinned action versions

### Changed
- **.gitignore**: Updated to exclude CI/CD artifacts
  - Added `coverage-report/` directory to gitignore
  - Ensures generated coverage reports are not committed to repository

### Security
- **Workflow Security**: All GitHub Actions pinned to commit SHAs (not version tags)
  - Prevents supply chain attacks from malicious action updates
  - Requires manual verification when updating action versions
  - Minimal permissions (`contents: read`, `packages: read`, `pull-requests: write`)

## 1.6

### Changed
- **Code Formatting**: Applied `dotnet format` across the entire codebase for consistent code style
  - Reorganized using statements to follow .NET conventions (system namespaces first, then third-party, then application)
  - Standardized whitespace and formatting across all files
  - Improved code readability and maintainability
  - Affects 42 files with formatting improvements

## 1.5

### Added
- **Enhanced Repository Synchronization**: Improved `ScanningService` repository synchronization logic
  - Added automatic repository rename detection and updates when repositories are renamed in GitHub
  - Added automatic repository deletion when repositories no longer exist in GitHub organization
  - Added cascading deletion of related records (PolicyViolations and ActionLogs) before removing repositories
  - Added comprehensive logging for repository operations (add, rename, remove) with detailed information
  - Ensures database stays in sync with GitHub organization state without manual intervention
- **Repository Synchronization Unit Tests**: Added comprehensive test coverage for repository synchronization scenarios
  - `PerformScanAsync_WhenRepositoriesDeletedInGitHub_RemovesFromDatabase`: Tests repository deletion with cascading cleanup
  - `PerformScanAsync_WhenRepositoryRenamed_UpdatesName`: Tests repository rename detection and updates

### Changed
- **ScanningService**: Refactored `SyncRepositoriesAsync` method for improved repository synchronization
  - Changed from filtering database repositories by GitHub IDs to loading all repositories and mapping them
  - Improved performance by using dictionary lookups instead of repeated database queries
  - Enhanced data consistency by maintaining complete repository state in database

### Technical Details
- **Repository Synchronization Strategy**: 
  - Loads all repositories from database into memory for efficient comparison
  - Uses GitHub repository ID as the unique identifier (preserves identity across renames)
  - Performs three-phase synchronization: add new repos, update existing repos, remove deleted repos
  - Cascades deletions to maintain referential integrity (PolicyViolations and ActionLogs removed before repositories)

## 1.4

### Added
- **Test Mode**: Added authentication bypass mode for E2E testing and development scenarios
  - Created `TestModeOptions` configuration class for enabling/disabling test mode
  - Implemented `TestModeAuthenticationMiddleware` and `TestModeAuthenticationHandler` for automatic authentication bypass
  - When enabled, automatically authenticates users as fake `mackowski` user without requiring GitHub OAuth
  - Skips team membership verification (always returns `true`) for authorization checks
  - GitHub App services remain fully functional for repository operations
  - Configurable via `appsettings.json`, environment variables, or command-line arguments
  - Documented in README.md with security warnings for production use
- **E2E Testing Infrastructure**: Implemented End-to-End testing framework using Playwright
  - Created `10xGitHubPolicies.Tests.E2E` project with Playwright.NET integration
  - Dual-host architecture: test host for data creation + manually running web application for UI testing
  - Page Object Model implementation with `DashboardPage` for maintainable tests
  - Test data management with `RepositoryHelper` and `DatabaseHelper` utilities
  - Screenshot capture for debugging test failures
  - Test Mode integration for authentication bypass in E2E scenarios
  - Comprehensive README in E2E test project with setup and troubleshooting guides
- **GitHubService E2E Testing Methods**: Added 9 new methods to `IGitHubService` specifically for E2E testing scenarios
  - **Repository Operations**:
    - `CreateRepositoryAsync()`: Creates new repositories in the organization
    - `DeleteRepositoryAsync()`: Deletes repositories by name
    - `UnarchiveRepositoryAsync()`: Unarchives previously archived repositories
  - **File Operations**:
    - `CreateFileAsync()`: Creates new files in repositories
    - `UpdateFileAsync()`: Updates existing files in repositories
    - `DeleteFileAsync()`: Two overloads (by repositoryId or repositoryName) for deleting files
  - **Issue Operations**:
    - `CloseIssueAsync()`: Closes issues by number
    - `GetRepositoryIssuesAsync()`: Gets all issues for a repository by name
  - **Workflow Operations**:
    - `UpdateWorkflowPermissionsAsync()`: Updates workflow permissions for repositories
  - All methods use GitHub App authentication and follow existing service patterns
- **E2E Test Implementation**: Created E2E test suite
  - `WorkflowTests.cs` with complete policy enforcement workflow test
  - Tests repository creation, policy compliance checking, scanning, and UI interactions
  - Integration with Test Mode for seamless authentication
  - Automatic cleanup of test repositories and database records
- **Documentation**: Created comprehensive E2E testing documentation
  - Added `docs/testing-e2e-tests.md` with detailed setup, architecture, and usage guide
  - Updated `testing-strategy.md` with E2E testing details
  - Updated `github-integration.md` with new E2E testing methods
  - Updated README.md with E2E testing section

### Changed
- **AuthorizationService**: Added Test Mode support to bypass team membership checks
  - Checks `TestModeOptions.Enabled` before performing team membership verification
  - Returns `true` immediately when Test Mode is enabled
  - Logs test mode bypass for debugging purposes
- **Program.cs**: Enhanced authentication configuration to support Test Mode
  - Conditionally registers GitHub OAuth authentication only when Test Mode is disabled
  - Registers `TestModeAuthenticationHandler` when Test Mode is enabled
  - Adds `TestModeAuthenticationMiddleware` to the request pipeline for authentication bypass
  - Configures `TestModeOptions` from configuration section
- **README.md**: Enhanced with comprehensive Test Mode and E2E testing documentation
  - Added detailed Test Mode configuration section with all options
  - Added E2E testing section with prerequisites and quick start guide
  - Updated testing section to include E2E test project information

### Security
- **Test Mode Warning**: Test Mode completely bypasses authentication and authorization controls
  - Should NEVER be enabled in production environments
  - Documented security considerations in README.md and AGENTS.md
  - Warning messages in code comments and documentation

### Technical Details
- **New Dependencies**:
  - Microsoft.Playwright (latest) - Browser automation for E2E tests
  - Test Mode implemented using ASP.NET Core authentication handlers and middleware
- **Test Architecture**:
  - Dual-host approach separates test data creation from UI testing
  - Test host uses GitHubService for repository management
  - Web application runs manually for realistic testing environment
  - Test Mode enables authentication bypass without affecting GitHub App functionality

## 1.3

### Added
- **GitHubService Integration Tests**: Completed comprehensive integration test suite for GitHub API interactions
  - Created `10xGitHubPolicies.Tests.Integration` project with 33 passing tests
  - HTTP-level mocking using WireMock.Net to simulate GitHub API responses
  - Test coverage for all GitHubService methods: repositories, files, issues, workflow permissions, team membership
  - Rate limiting and error handling test scenarios
  - Token caching behavior validation
  - All tests use `IGitHubClientFactory` pattern for proper dependency injection
- **GitHubService Contract Tests**: Implemented contract testing to detect GitHub API breaking changes
  - Created `10xGitHubPolicies.Tests.Contracts` project with 11 tests
  - 6 schema validation tests using NJsonSchema to verify response structure
  - 5 snapshot tests using Verify.NET to detect unexpected API changes
  - JSON schemas for repository, issue, and workflow permissions responses
  - Baseline snapshots for structure stability monitoring
- **Test Infrastructure**: Resolved Octokit `GitHubClient` redirection challenge
  - Implemented `IGitHubClientFactory` interface and `GitHubClientFactory` implementation
  - Added `BaseUrl` configuration option to `GitHubAppOptions` for test environments
  - Refactored `GitHubService` to use factory pattern for GitHubClient instantiation
  - Created `GitHubServiceIntegrationTestBase` with authentication mocking helpers
  - Created `GitHubContractTestBase` with shared test infrastructure
  - Response builders (`GitHubApiResponseBuilder`) for consistent test data generation
- **Documentation**: Created comprehensive contract testing guide
  - Added `docs/testing-contract-tests.md` with detailed step-by-step explanation
  - Covers WireMock.Net, Verify.NET, and NJsonSchema usage
  - Includes test organization, best practices, and debugging tips
  - Added references in README.md and testing-strategy.md

### Changed
- **GitHubService**: Refactored to accept optional `IGitHubClientFactory` parameter
  - All internal `GitHubClient` instantiations now use the factory
  - Maintains backward compatibility with default factory if none provided
  - Enables test scenarios to redirect API calls to WireMock
- **Dependency Injection**: Registered `IGitHubClientFactory` in `Program.cs`
  - Factory uses `BaseUrl` from `GitHubAppOptions` when configured
  - Defaults to standard GitHub API URL in production
- **Integration Test Suite**: Removed 3 documentation placeholder tests
  - Cleaned up `RateLimitHandlingTests` and `TokenCachingTests`
  - All 33 executable tests now passing with proper mocking
- **Contract Test Configuration**: Schema files configured to copy to output directory
  - Added build configuration in `.csproj` to include schema JSON files
  - Ensures NJsonSchema can load schemas during test execution

### Fixed
- **Octokit Path Prefix**: Discovered and fixed `/api/v3/` prefix requirement
  - Octokit prepends `/api/v3/` when custom BaseUrl is provided (GitHub Enterprise mode)
  - Updated all WireMock stubs to include correct path prefix
  - Authentication endpoint now properly mocks at `/api/v3/app/installations/*/access_tokens`
- **Test Assertions**: Corrected assertion mismatches in integration tests
  - `RateLimitHandlingTests` now expects `ApiException` instead of `RateLimitExceededException`
  - `IssueOperationsTests` now correctly accesses `State.Value` enum property
- **Contract Test Method Signatures**: Fixed method calls to match actual service signatures
  - `GetFileContentAsync` now uses `repoName` parameter instead of `repositoryId`
  - `IsUserMemberOfTeamAsync` now includes all three required parameters

### Technical Details
- **New Dependencies**:
  - WireMock.Net (v1.5.59) - HTTP mocking for integration tests
  - NJsonSchema (v11.0.2) - JSON schema validation for contract tests
  - Verify.Xunit (v28.6.0) - Snapshot testing for contract tests
  - Bogus (v35.4.0) - Test data generation
- **Test Categories**: Tests are categorized with Traits for selective execution
  - Integration tests: `[Trait("Category", "Integration")]`
  - Contract tests: `[Trait("Category", "Contract")]`
- **Documentation**: Updated plan document (`.cursor/plans/githubservice-integration-contract-f95b8e0c.plan.md`) with completion status

### Test Coverage
- **Integration Tests**: 33/33 tests passing
  - File Operations: 5 tests
  - Repository Operations: 7 tests
  - Issue Operations: 5 tests
  - Workflow Permissions: 3 tests
  - Rate Limit Handling: 5 tests
  - Token Caching: 3 tests
  - Team Membership: 5 tests
- **Contract Tests**: 11/11 tests implemented
  - Repository Response Schema: 3 tests
  - Issue Response Schema: 2 tests
  - Workflow Permissions Schema: 1 test
  - API Snapshots: 5 tests (require baseline approval on first run)

## 1.2

### Added
- **Comprehensive Testing Infrastructure**: Implemented a multi-level testing strategy to ensure code quality and reliability
  - **Unit Testing**: xUnit + NSubstitute + FluentAssertions for fast, isolated business logic testing with 85-90% coverage target
  - **Integration Testing**: Testcontainers + Respawn + WireMock.Net for database operations and GitHub API HTTP-level mocking
  - **Contract Testing**: NJsonSchema + Verify.NET for detecting GitHub API breaking changes with schema validation and snapshot testing
  - **✅ Blazor Component Testing**: bUnit for testing UI component rendering and user interactions - **COMPLETED** (22 tests, 100% pass rate)
  - **End-to-End Testing**: Playwright for critical user workflow validation (used sparingly)
- **Testing Documentation**: Created comprehensive testing strategy documentation at `/docs/testing-strategy.md`
  - Detailed testing pyramid with coverage targets for each level
  - Testing tool rationale and version requirements
  - Project structure and naming conventions
  - Development workflow and CI/CD pipeline guidance
  - Quick decision tree for choosing the right test type
  - Common commands cheat sheet
- **Test Plan**: Added comprehensive test plan document at `.ai/test-plan.md` with 50+ detailed test scenarios
  - Authentication and authorization test cases
  - Configuration management test cases
  - Policy evaluation test cases
  - Dashboard functionality test cases
  - Automated actions test cases
  - Background job processing test cases
  - GitHub API integration and contract testing test cases
  - Performance and security testing strategies
- **Blazor Component Testing Implementation**: Completed comprehensive UI component testing with bUnit
  - **Test Files Created**: 7 test files covering all major UI components
    - `Components/Pages/IndexTests.cs` (5 tests) - Dashboard component with compliance metrics and filtering
    - `Components/Pages/OnboardingTests.cs` (5 tests) - Configuration setup wizard
    - `Components/Pages/AccessDeniedTests.cs` (3 tests) - Authorization error handling
    - `Components/Pages/LoginTests.cs` (3 tests) - Authentication flow
    - `Components/Shared/MainLayoutTests.cs` (2 tests) - Layout and navigation
    - `Components/Shared/RedirectToLoginTests.cs` (1 test) - Navigation flow
    - `Components/Integration/AuthorizationFlowTests.cs` (3 tests) - Complete auth workflows
  - **Test Infrastructure**: Created reusable test infrastructure
    - `AppTestContext.cs` - Base test context with common setup and mocking
    - `TestDataBuilder.cs` - Factory for creating test data objects
  - **Dependencies Added**: bUnit (v1.28.9), bUnit.web (v1.28.9), Microsoft.AspNetCore.Components.Authorization (v8.0.4)
  - **Test Coverage**: 22 tests covering core UI functionality, authentication flows, and user interactions
  - **Known Limitations**: Complex Fluent UI interactions and logout functionality deferred to E2E tests

### Changed
- **Tech Stack Documentation**: Updated `.ai/tech-stack.md` with comprehensive testing stack section
  - Added rationale for each testing tool selection
  - Explained how each tool supports the project's testing needs
- **Coding Practices**: Updated `AGENTS.md` with multi-level testing strategy
  - Replaced generic testing guidelines with specific technology recommendations
  - Added clear guidance on when to use each testing level
  - Included database testing with Respawn for test isolation
- **README**: Enhanced tech stack table to include testing tools
  - Added Testing row with xUnit, bUnit, WireMock.Net, Testcontainers
  - Added link to comprehensive testing strategy documentation

### Technical Details
- **Testing Tools Added to Documentation**:
  - Core: xUnit (v2.6+), FluentAssertions (v6.12+), NSubstitute (v5.1+)
  - Specialized: bUnit (v1.28+), Playwright (latest), Bogus (v35.4+)
  - Integration: Testcontainers.MsSql (v3.7+), Respawn (v6.2+), WireMock.Net (v1.5+)
  - Contract: NJsonSchema (v11.0+), Verify.NET (v25.0+)
  - Code Quality: Coverlet (v6.0+)

## 1.1

### Added
- **Daily Automated Scanning**: Implemented recurring job configuration for automatic daily repository scanning
  - Added `RecurringJob.AddOrUpdate()` configuration in `Program.cs` for daily scans at midnight UTC
  - Scans run automatically without manual intervention using Hangfire's recurring job system
  - Uses `IScanningService.PerformScanAsync()` for consistent scanning logic
- **Enhanced Error Handling**: Improved configuration validation and error handling
  - Added null checks and proper exception handling for GitHub OAuth configuration
  - Enhanced error messages for missing configuration values

### Changed
- **UI Simplification**: Removed `FluentThemeProvider` wrapper from `MainLayout.razor` for cleaner component structure
- **Code Quality**: Fixed nullable reference warnings across multiple files
  - `PolicyViolation.cs`: Added default value for `PolicyType` property
  - `ConfigurationService.cs`: Enhanced null checking for cached configuration
- **Documentation**: Updated Hangfire integration documentation to include recurring job configuration

### Fixed
- **Nullable Reference Warnings**: Resolved compiler warnings for nullable reference types
- **Configuration Caching**: Improved thread safety and null handling in configuration service

## 1.0

### Added
- **User Authentication System**: Implemented complete GitHub OAuth-based authentication for the web dashboard
  - Added `AspNet.Security.OAuth.GitHub` package for OAuth integration
  - Created `IAuthorizationService` and `AuthorizationService` for team-based authorization
  - Implemented secure session management with 24-hour fixed expiration
  - Added authentication middleware configuration in `Program.cs`
- **Authentication Pages**: Created comprehensive authentication UI components
  - `Login.razor`: GitHub OAuth login page with branded UI
  - `Logout.razor`: Secure logout handling with session cleanup
  - `AccessDenied.razor`: Clear messaging for unauthorized users with team information
  - `Onboarding.razor`: Step-by-step wizard for first-time configuration setup
- **Authorization Logic**: Integrated team-based access control throughout the application
  - Dashboard (`Index.razor`) now requires authentication and team membership verification
  - Configuration validation redirects to onboarding when `config.yaml` is missing
  - Team membership verification using GitHub API with proper error handling
- **UI Enhancements**: Updated application layout and navigation
  - `MainLayout.razor` now shows user information and logout button for authenticated users
  - `App.razor` updated with `CascadingAuthenticationState` and `AuthorizeRouteView`
  - Added `RedirectToLogin.razor` component for unauthenticated users
- **Security Features**: Implemented comprehensive security measures
  - OAuth access tokens stored securely in encrypted authentication cookies
  - Minimal scope request (`read:org` only) following least privilege principle
  - CSRF protection via Blazor Server's built-in anti-forgery protection
  - Secure cookie configuration for production environments
- **Documentation**: Added comprehensive authentication documentation
  - Created `docs/authentication.md` with detailed OAuth flow explanation
  - Updated `README.md` with GitHub OAuth App setup instructions
  - Added troubleshooting guide for common authentication issues
- **Security Enhancements**: Implemented comprehensive endpoint security
  - Added `[Authorize]` attribute to Debug page (`/debug`) to prevent unauthorized access
  - Secured Hangfire dashboard (`/hangfire`) with custom `HangfireAuthorizationFilter`
  - Removed Swagger/OpenAPI dependencies and endpoints to reduce attack surface
  - Added URL documentation with authentication status in README.md and authentication.md
  - All administrative and debugging endpoints now require authentication

### Changed
- **Configuration**: Updated `appsettings.json` to include OAuth configuration placeholders
- **Service Registration**: Added `IAuthorizationService` and `HttpContextAccessor` to DI container
- **Project Scope**: Marked authentication features as completed in README.md

### Dependencies
- Added `AspNet.Security.OAuth.GitHub` 8.0.0 for GitHub OAuth integration

## 0.4

### Added
- **Automated Actions Service**: Implemented full `ActionService` to execute configured actions for policy violations
  - Added `IssueDetails` model class for YAML configuration of issue creation details (title, body, labels)
  - Updated `PolicyConfig` model with `Name` and `IssueDetails` properties to support complete policy configuration
  - Implemented `CreateIssueForViolationAsync()` method with duplicate prevention logic (US-010)
  - Implemented `ArchiveRepositoryForViolationAsync()` method for automatic repository archiving
  - Added comprehensive error handling with action logging to `ActionLog` table
  - Each action is logged with status (Success, Failed, or Skipped) and detailed information
- **GitHub Service Enhancement**: Added `GetOpenIssuesAsync()` method to check for existing open issues
  - Supports filtering by label to prevent duplicate issue creation
  - Returns empty list on repository not found errors
  - Used for duplicate detection before creating new policy violation issues
- **Action Logging**: All actions are recorded in the `ActionLog` database table with:
  - ActionType: "create-issue" or "archive-repo"
  - Status: "Success", "Failed", or "Skipped"
  - Details: JSON or text with action details/error messages
  - Timestamp: UTC timestamp of action execution

### Changed
- **Service Registration**: Replaced `LoggingActionService` with full `ActionService` implementation in `Program.cs`
- **Policy Configuration**: Enhanced `PolicyConfig` to include policy name and issue details for better configuration management
- **Duplicate Prevention**: Issues are only created if no open issue with the same title and label exists (implements US-010 requirement)
- **Error Handling**: Individual action failures no longer block processing of other violations

### Removed
- **LoggingActionService**: Deleted placeholder logging-only implementation
- **Test Endpoints**: Removed `/verify-scan` and `/log-job` endpoints (functionality exists in UI via "Scan Now" button)

### Fixed
- **Nullable Annotations**: Fixed nullable reference warnings in `GitHubService` for `GetFileContentAsync()` and `GetWorkflowPermissionsAsync()` methods
- **WorkflowPermissionsResponse**: Added default value for `DefaultWorkflowPermissions` property to resolve nullable warning

### Dependencies
- No new dependencies added (uses existing Octokit.net, Entity Framework Core, and Hangfire)

## 0.3

### Added
- **Workflow Permissions Policy Evaluator**: Implemented `CorrectWorkflowPermissionsEvaluator` to enforce security best practices for GitHub Actions workflow permissions.
  - Added `GetWorkflowPermissionsAsync()` method to `IGitHubService` and `GitHubService` to retrieve repository workflow permissions via GitHub API
  - Added `WorkflowPermissionsResponse` class for API response deserialization
  - Policy checks that repositories have read-only workflow permissions (security compliance)
  - Handles cases where GitHub Actions are disabled (treated as compliant)
- **Enhanced GitHub Integration**: Extended GitHub service with workflow permissions API integration
  - Uses GitHub API endpoint `GET /repos/{owner}/{repo}/actions/permissions/workflow`
  - Proper error handling for repositories with disabled Actions
  - Returns "read", "write", or null for disabled Actions
- **Documentation Updates**: 
  - Updated `docs/github-integration.md` with new `GetWorkflowPermissionsAsync` method documentation and usage examples
  - Updated `docs/policy-evaluation.md` with complete workflow permissions policy configuration example
  - Added policy violation issue template for workflow permissions violations

### Changed
- **Policy Evaluation**: `CorrectWorkflowPermissionsEvaluator` now implements actual policy logic instead of placeholder TODO
- **Security Enhancement**: Enforces that repositories must have read-only workflow permissions to prevent unnecessary write access in GitHub Actions

### Dependencies
- No new dependencies added (uses existing Octokit.net library)

---

## 0.2

### Changed
- **UI Framework**: Replaced `MudBlazor` with `Microsoft.FluentUI.AspNetCore.Components` for the entire web UI. This includes updating all components, layouts, and styling to align with the Fluent UI design system.

### Dependencies
- Replaced `MudBlazor` with `Microsoft.FluentUI.AspNetCore.Components` and `Microsoft.FluentUI.AspNetCore.Components.Icons`.

---

## 0.1

### Added
- **Policy Evaluation Engine**: Implemented a flexible and extensible policy evaluation engine using the Strategy Pattern.
  - `IPolicyEvaluationService`: Orchestrates the evaluation of a repository against all configured policies.
  - `IPolicyEvaluator`: Interface for individual policy checks.
- **New Policy Evaluators**: Added three initial policy evaluators:
  - `HasAgentsMdEvaluator`: Checks for the presence of an `AGENTS.md` file.
  - `HasCatalogInfoYamlEvaluator`: Checks for the presence of a `catalog-info.yaml` file.
  - `CorrectWorkflowPermissionsEvaluator`: Verifies that repository workflow permissions are set correctly.
- **On-Demand Scanning**: Added a "Scan Now" button to the dashboard to trigger a repository scan immediately.
- **Background Job Processing**: Integrated `Hangfire` to run scans and actions in the background, ensuring the UI remains responsive.
- **Action Service**: Introduced `IActionService` to handle automated actions (like logging) after a scan is complete. `LoggingActionService` is the initial implementation.
- **Database Persistence**:
  - Added `Scan`, `Policy`, and `PolicyViolation` models to the database.
  - `ScanningService` now records scan history and all policy violations.
- **API Documentation**: Added `Swashbuckle.AspNetCore` to generate Swagger/OpenAPI documentation for the API.
- **New Endpoints**: Added `/verify-scan` for manual scan triggering and `/log-job` for testing Hangfire.
- **Configuration Service**: New `IConfigurationService` and `ConfigurationService` for reading and managing organization policy configuration from `.github/config.yaml`
  - Memory caching with 15-minute sliding expiration
  - Thread-safe configuration retrieval using semaphore
  - YAML parsing with YamlDotNet
  - Configuration validation (checks for required fields like `access_control.authorized_team`)
- **Configuration Models**:
  - `AppConfig`: Root configuration model containing access control and policies
  - `AccessControlConfig`: Configuration for team-based access control
  - `PolicyConfig`: Policy definition model (type, action, issue details)
- **Custom Exceptions**:
  - `ConfigurationNotFoundException`: Thrown when `.github/config.yaml` is not found
  - `InvalidConfigurationException`: Thrown when configuration is malformed or invalid
- **Enhanced GitHub Service**: Extended `IGitHubService` with new methods for repository management:
  - `GetOrganizationRepositoriesAsync()`: Retrieve all repositories in the organization
  - `FileExistsAsync()`: Check if a file exists in a repository
  - `GetRepositorySettingsAsync()`: Get repository settings and metadata
  - `CreateIssueAsync()`: Create an issue in a repository with labels
  - `ArchiveRepositoryAsync()`: Archive a repository
  - `IsUserMemberOfTeamAsync()`: Verify team membership for access control
  - `GetFileContentAsync()`: Retrieve file content from a repository (Base64 encoded)
- **New Configuration Option**: Added `OrganizationName` to `GitHubAppOptions` for specifying the target GitHub organization
- **New Dependency**: Added `YamlDotNet` (v16.3.0) for YAML configuration parsing
- **New Dependencies**: Added `Hangfire.AspNetCore` and `Hangfire.SqlServer` for background job processing. Added `Swashbuckle.AspNetCore` for API documentation.

### Changed
- **Dashboard (`Index.razor`)**: Now uses `IScanningService` and `IBackgroundJobClient` to trigger on-demand scans. The UI provides feedback when a scan is in progress.
- **`Program.cs`**:
  - Configured Hangfire services and dashboard.
  - Registered all new services (`IScanningService`, `IPolicyEvaluationService`, `IPolicyEvaluator` implementations, `IActionService`) in the DI container.
  - Added Swagger middleware.
- **GitHubService**: Changed `GetAuthenticatedClient()` method from public to private - clients should use the new specialized methods instead
- **ScanningService**: 
  - Now injects and uses `IPolicyEvaluationService` to evaluate policies.
  - Saves scan results and violations to the database.
  - Enqueues a job for `IActionService` after completing a scan.
  - Added `SyncPoliciesAsync` and `SyncRepositoriesAsync` to keep the database in sync with the configuration and organization repositories.
  - Uses new `GetOrganizationRepositoriesAsync()` method instead of directly accessing GitHubClient
  - Added configuration logging to show loaded policies and access control settings
- **Service Registration**: Registered `IConfigurationService` as a singleton in DI container

### Fixed
- N/A

### Security
- Configuration validation ensures required fields are present before use
- Team membership verification added for future access control implementation

### Dependencies
- Added `YamlDotNet` 16.3.0
- Added `Hangfire.AspNetCore` and `Hangfire.SqlServer`
- Added `Swashbuckle.AspNetCore`

---
