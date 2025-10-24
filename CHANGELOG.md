# Changelog

All notable changes to this project will be documented in this file.

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
