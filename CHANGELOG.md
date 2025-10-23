# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

## Previous Releases

_No previous releases documented yet. This is the initial development phase of the project._

