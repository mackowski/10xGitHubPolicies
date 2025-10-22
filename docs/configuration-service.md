# Configuration Service

This document describes the `IConfigurationService` and how it manages the centralized policy configuration for the 10x GitHub Policy Enforcer.

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

### `AppConfig`

The root configuration model that represents the entire `config.yaml` file.

```csharp
public class AppConfig
{
    [YamlMember(Alias = "access_control")]
    public AccessControlConfig AccessControl { get; set; }

    public List<PolicyConfig> Policies { get; set; } = new();
}
```

### `AccessControlConfig`

Defines access control settings for the dashboard.

```csharp
public class AccessControlConfig
{
    [YamlMember(Alias = "authorized_team")]
    public string AuthorizedTeam { get; set; }
}
```

**Required Fields**:
- `authorized_team`: The GitHub team (in format `org/team-slug`) authorized to access the dashboard.

### `PolicyConfig`

Defines a single policy rule.

```csharp
public class PolicyConfig
{
    public string Name { get; set; }
    public string Type { get; set; }
    public string Action { get; set; }
    public IssueDetails IssueDetails { get; set; }
}
```

**Fields**:
- `Name`: Human-readable name for the policy
- `Type`: Policy type identifier (e.g., `has_agents_md`, `has_catalog_info_yaml`, `correct_workflow_permissions`)
- `Action`: Action to take when policy is violated (`create-issue` or `archive-repo`)
- `IssueDetails`: Details for the issue to create (if `Action` is `create-issue`)

## Configuration File Format

The `config.yaml` file should be located at `.github/config.yaml` in your organization's `.github` repository.

### Example Configuration

```yaml
# Access control: Specify the GitHub team authorized to access the dashboard.
# Format: 'organization-slug/team-slug'
access_control:
  authorized_team: 'my-org/security-team'

# Policies: Define the rules to enforce across your organization's repositories.
policies:
  - name: 'Check for AGENTS.md'
    type: 'has_agents_md'
    action: 'create-issue'
    issue_details:
      title: 'Compliance: AGENTS.md file is missing'
      body: 'This repository is missing the AGENTS.md file in its root directory. Please add this file to comply with organization standards.'
      labels: ['policy-violation', 'documentation']

  - name: 'Check for catalog-info.yaml'
    type: 'has_catalog_info_yaml'
    action: 'create-issue'
    issue_details:
      title: 'Compliance: catalog-info.yaml is missing'
      body: 'This repository is missing the catalog-info.yaml file. This file is required for backstage.io service discovery.'
      labels: ['policy-violation', 'backstage']
      
  - name: 'Verify Workflow Permissions'
    type: 'correct_workflow_permissions'
    action: 'archive-repo'
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

### Basic Usage

```csharp
public class ScanningService
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<ScanningService> _logger;

    public ScanningService(
        IConfigurationService configurationService,
        ILogger<ScanningService> logger)
    {
        _configurationService = configurationService;
        _logger = logger;
    }

    public async Task ScanRepositoriesAsync()
    {
        try
        {
            var config = await _configurationService.GetConfigAsync();
            
            _logger.LogInformation(
                "Configuration loaded. Authorized team: {Team}", 
                config.AccessControl.AuthorizedTeam
            );
            
            foreach (var policy in config.Policies)
            {
                _logger.LogInformation(
                    "Policy: {Name} (Type: {Type}, Action: {Action})",
                    policy.Name,
                    policy.Type,
                    policy.Action
                );
            }
            
            // Process policies...
        }
        catch (ConfigurationNotFoundException ex)
        {
            _logger.LogError(ex, "Configuration file not found");
            throw;
        }
        catch (InvalidConfigurationException ex)
        {
            _logger.LogError(ex, "Invalid configuration");
            throw;
        }
    }
}
```

### Force Refresh

```csharp
// Force refresh the configuration (bypass cache)
var config = await _configurationService.GetConfigAsync(forceRefresh: true);
```

## Service Registration

The Configuration Service is registered as a singleton in the DI container:

```csharp
builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();
```

**Note**: The service is registered as a singleton because:
- It manages its own caching internally
- The configuration is organization-wide and not user-specific
- It uses thread-safe mechanisms (semaphore) for concurrent access

## Dependencies

### NuGet Packages

- **YamlDotNet** (v16.3.0): YAML parsing library
  - Publisher: Antoine Aubry
  - License: MIT
  - Used for deserializing the `config.yaml` file

### Naming Convention

YamlDotNet is configured to use the `UnderscoredNamingConvention` (snake_case) to match the YAML file format:

```yaml
access_control:  # Maps to AccessControl property
  authorized_team: 'my-org/team'  # Maps to AuthorizedTeam property
```

## Best Practices

1. **Error Handling**: Always wrap `GetConfigAsync()` calls in try-catch blocks to handle configuration errors gracefully.
2. **Cache Awareness**: Use `forceRefresh: true` sparingly to avoid unnecessary GitHub API calls.
3. **Validation**: Validate configuration data before using it in business logic.
4. **Logging**: Log configuration retrieval and validation for debugging and auditing.
5. **Configuration Updates**: When the `config.yaml` file changes, either:
   - Wait for the cache to expire naturally (15 minutes)
   - Call `GetConfigAsync(forceRefresh: true)` to immediately pick up changes
   - Restart the application to clear all caches

## Future Enhancements

Potential improvements for the Configuration Service:

1. **Webhook Integration**: Automatically refresh configuration when `.github` repository changes are detected.
2. **Configuration Versioning**: Track configuration versions and maintain a change history.
3. **Schema Validation**: Implement JSON Schema or similar validation for more comprehensive configuration validation.
4. **Configuration UI**: Provide a web interface for editing the configuration without directly modifying YAML files.
5. **Multi-Organization Support**: Support managing multiple organizations with different configurations.

