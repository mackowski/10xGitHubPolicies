# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
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

### Changed
- **GitHubService**: Changed `GetAuthenticatedClient()` method from public to private - clients should use the new specialized methods instead
- **ScanningService**: 
  - Now injects and uses `IConfigurationService` to retrieve organization configuration
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

---

## Previous Releases

_No previous releases documented yet. This is the initial development phase of the project._

