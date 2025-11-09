# Configuration Service

This document describes the `IConfigurationService` and how it manages the centralized policy configuration for the 10x GitHub Policy Enforcer.

> **Note**: This document is part of the [Services Architecture](./architecture.md) documentation.

## Overview

The Configuration Service is responsible for:
- Retrieving the `config.yaml` file from the organization's `.github` repository
- Parsing and validating the YAML configuration
- Caching the configuration in memory to reduce API calls
- Providing thread-safe access to the configuration

## Services

### `IConfigurationService`

- **Purpose**: Defines the contract for managing application configuration.
- **Methods**:
    - `Task<AppConfig> GetConfigAsync(bool forceRefresh = false)`: Retrieves the application configuration. If `forceRefresh` is `true`, bypasses the cache and fetches a fresh copy from GitHub.

### `ConfigurationService`

- **Purpose**: Implements `IConfigurationService` and manages the lifecycle of the configuration.
- **Dependencies**:
    - `IGitHubService`: Used to retrieve the `config.yaml` file from GitHub
    - `IMemoryCache`: Used to cache the parsed configuration
    - `GitHubAppOptions`: Contains the organization name and other GitHub App settings
    - `ILogger<ConfigurationService>`: For logging configuration operations

## Configuration Models

The service uses strongly-typed models to represent the configuration:

- **`AppConfig`**: Root configuration containing access control and policies
- **`AccessControlConfig`**: Defines the authorized team for dashboard access (format: `org/team-slug`)
- **`PolicyConfig`**: Defines a single policy with `Name`, `Type`, `Actions` (list of actions), and optional `IssueDetails`
- **`IssueDetails`**: Contains `Title`, `Body`, and `Labels` for issue creation actions

## Configuration File Format

The `config.yaml` file should be located at `.github/config.yaml` in your organization's `.github` repository.

### Example Configuration

```yaml
access_control:
  authorized_team: 'my-org/security-team'

policies:
  - name: 'Check for AGENTS.md'
    type: 'has_agents_md'
    action: 'create-issue'  # Single action (backward compatible)
    issue_details:
      title: 'Compliance: AGENTS.md file is missing'
      body: 'This repository is missing the AGENTS.md file...'
      labels: ['policy-violation', 'documentation']
      
  - name: 'Critical Security Policy'
    type: 'has_agents_md'
    action: ['create-issue', 'archive-repo']  # Multiple actions (new format)
    issue_details:
      title: 'Critical: Security policy violation'
      body: 'This repository violates critical security policies...'
      labels: ['policy-violation', 'security', 'critical']
      
  - name: 'Verify Workflow Permissions'
    type: 'correct_workflow_permissions'
    action: 'archive-repo'  # Single action
```

## Caching Strategy

The Configuration Service implements an intelligent caching strategy:

1. **Sliding Expiration**: The configuration is cached for 15 minutes with sliding expiration. Each access extends the cache lifetime.
2. **Double-Check Locking**: Uses a semaphore to ensure thread-safe cache updates when multiple requests arrive simultaneously.
3. **Force Refresh**: The `forceRefresh` parameter allows bypassing the cache when needed (e.g., when configuration changes are made).

### Cache Key

- `AppConfigCacheKey`: The key used to store the configuration in `IMemoryCache`.

## Validation

The Configuration Service validates the configuration before caching:

1. **Required Fields**: Ensures `access_control.authorized_team` is present and not empty.
2. **YAML Syntax**: Catches and reports YAML parsing errors with detailed exception information.

## Exception Handling

The service throws two custom exceptions:

### `ConfigurationNotFoundException`

Thrown when the `.github/config.yaml` file is not found in the organization's `.github` repository.

### `InvalidConfigurationException`

Thrown when:
- The YAML syntax is invalid
- Required fields are missing or empty
- The configuration structure doesn't match the expected schema

## Usage

```csharp
// Get cached configuration
var config = await _configurationService.GetConfigAsync();

// Force refresh (bypass cache)
var config = await _configurationService.GetConfigAsync(forceRefresh: true);
```

**Error Handling**: The service throws `ConfigurationNotFoundException` when the config file is missing, and `InvalidConfigurationException` for invalid YAML or missing required fields.

## Service Registration

Registered as a **singleton** in the DI container because it manages organization-wide configuration caching with thread-safe mechanisms.

## Dependencies

Uses **YamlDotNet** for YAML parsing with `UnderscoredNamingConvention` (snake_case) to match the YAML file format.

## Best Practices

- Handle `ConfigurationNotFoundException` and `InvalidConfigurationException` appropriately
- Use `forceRefresh: true` sparingly to avoid unnecessary GitHub API calls
- Configuration cache expires after 15 minutes (sliding expiration)
- Changes to `config.yaml` are picked up automatically after cache expiration or via `forceRefresh`

