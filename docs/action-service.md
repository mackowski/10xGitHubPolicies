# Action Service

This document describes the `IActionService` and how it handles automated actions for policy violations in the 10x GitHub Policy Enforcer.

## Overview

The Action Service is responsible for executing configured actions when policy violations are detected during repository scans. It processes violations based on the policy configuration and performs actions like creating issues or **archiving repositories** to enforce compliance.

> **🔒 Archive Repository Action**: The archive repository action is a powerful enforcement mechanism that automatically makes non-compliant repositories read-only. This is particularly useful for enforcing critical security policies or compliance requirements where immediate action is required.

## Services

### `IActionService`

- **Purpose**: Defines the contract for processing automated actions for policy violations.
- **Methods**:
    - `Task ProcessActionsForScanAsync(int scanId)`: Processes all actions for violations found in a specific scan.

### `ActionService`

- **Purpose**: Implements `IActionService` and handles the execution of configured actions.
- **Dependencies**:
    - `ApplicationDbContext`: For database operations and action logging
    - `IGitHubService`: For GitHub API interactions (creating issues, archiving repositories)
    - `IConfigurationService`: For retrieving policy configuration
    - `ILogger<ActionService>`: For logging action execution

## Action Types

The service supports three types of actions based on the policy configuration:

### 1. Create Issue (`create-issue` or `create_issue`)

Creates a GitHub issue in the violating repository with:
- **Title**: Configurable via `IssueDetails.Title` or default format
- **Body**: Configurable via `IssueDetails.Body` or default message
- **Labels**: Configurable via `IssueDetails.Labels` or default labels
- **Duplicate Prevention**: Checks for existing open issues with the same title and label

### 2. Archive Repository (`archive-repo` or `archive_repo`) 🔒

**Powerful Enforcement Action**: Archives the violating repository using the GitHub API, making it read-only to enforce compliance.

**Features**:
- **Duplicate Prevention**: Checks if the repository is already archived before attempting to archive
- **Error Handling**: Handles specific GitHub API exceptions:
  - `NotFoundException`: Logs warning when repository is not found
  - `ApiException` with `Forbidden` status: Logs warning for insufficient permissions
  - Other exceptions: Logs error with full exception details
- **Structured Logging**: Includes repository name, policy name, and violation ID in log messages
- **Action Logging**: All archive actions are logged to the database with status tracking

**Behavior**:
1. Checks repository archived status using `GetRepositorySettingsAsync()`
2. If already archived, logs action as "Skipped" and returns early
3. If not archived, calls `ArchiveRepositoryAsync()` to archive the repository
4. Logs success or failure with appropriate status and details

### 3. Log Only (`log-only` or `log_only`)

Logs the violation without taking any automated action.

## Configuration Models

### `IssueDetails`

Defines the details for issue creation when using the `create-issue` action.

```csharp
public class IssueDetails
{
    [YamlMember(Alias = "title")]
    public string Title { get; set; } = string.Empty;

    [YamlMember(Alias = "body")]
    public string Body { get; set; } = string.Empty;

    [YamlMember(Alias = "labels")]
    public List<string> Labels { get; set; } = new();
}
```

### Enhanced `PolicyConfig`

The `PolicyConfig` model has been enhanced to support complete policy configuration:

```csharp
public class PolicyConfig
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public IssueDetails? IssueDetails { get; set; }
}
```

## Action Logging

All actions are logged to the `ActionLog` database table with the following information:

- **ActionType**: The type of action performed (`create-issue`, `archive-repo`, `log-only`)
- **Status**: The result of the action (`Success`, `Failed`, `Skipped`)
- **Details**: Additional information about the action (issue URL, error messages, etc.)
- **Timestamp**: UTC timestamp when the action was executed
- **RepositoryId**: ID of the repository where the action was performed
- **PolicyId**: ID of the policy that was violated

## Duplicate Prevention

The service implements duplicate prevention for both issue creation and repository archiving:

### Issue Creation (US-010 requirement)

1. **Label Filtering**: Retrieves open issues filtered by the primary label
2. **Title Matching**: Checks if an issue with the same title already exists
3. **Skip Creation**: If a duplicate is found, logs the action as "Skipped" and provides the existing issue URL

### Repository Archiving

1. **Status Check**: Retrieves repository settings to check current archived status
2. **Skip Archive**: If repository is already archived, logs the action as "Skipped" and returns early
3. **Efficiency**: Prevents unnecessary API calls and improves performance

## Error Handling

The service implements comprehensive error handling:

- **Individual Action Failures**: If one action fails, other actions continue processing
- **Exception Logging**: All exceptions are logged with detailed error information
- **Action Status Tracking**: Each action is logged with its status (Success, Failed, or Skipped)
- **Graceful Degradation**: Service continues processing even if some actions fail
- **Specific Exception Handling**: 
  - `NotFoundException`: Handled gracefully with warning-level logging
  - `ApiException` with `Forbidden`: Handled with warning-level logging for permission issues
  - Other exceptions: Logged at error level with full exception details

## Usage

### Basic Usage

```csharp
public class ScanningService
{
    private readonly IActionService _actionService;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public ScanningService(IActionService actionService, IBackgroundJobClient backgroundJobClient)
    {
        _actionService = actionService;
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task PerformScanAsync()
    {
        // ... scanning logic ...
        
        // After scan completion, enqueue action processing
        _backgroundJobClient.Enqueue<IActionService>(actionService => 
            actionService.ProcessActionsForScanAsync(scan.ScanId));
    }
}
```

### Configuration Example

```yaml
# .github/config.yaml

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

## Service Registration

The Action Service is registered as a scoped service in the DI container:

```csharp
// Program.cs
builder.Services.AddScoped<IActionService, ActionService>();
```

## Best Practices

1. **Background Processing**: Always enqueue action processing as a background job to avoid blocking the UI
2. **Error Handling**: Monitor the Hangfire dashboard for failed action jobs
3. **Configuration Validation**: Ensure policy configurations have valid action types and issue details
4. **Action Logging**: Regularly review action logs to monitor compliance and identify issues
5. **Duplicate Prevention**: The service automatically prevents duplicate issues, but ensure issue titles are descriptive and unique
6. **Testing**: Test action configurations in a development environment before deploying to production

## Monitoring

### Hangfire Dashboard

Monitor action processing through the Hangfire dashboard at `/hangfire`:
- View enqueued, processing, and completed action jobs
- Investigate failed jobs and retry if necessary
- Monitor job performance and processing times

### Action Logs

Review action logs in the database to track:
- Success rates for different action types
- Common failure patterns
- Compliance trends over time

### GitHub Integration

Monitor the GitHub repository for:
- Created issues and their resolution
- Repository archiving actions
- API rate limit usage

## Future Enhancements

Potential improvements for the Action Service:

1. **Action Templates**: Predefined templates for common issue types
2. **Conditional Actions**: Actions based on violation severity or repository characteristics
3. **Action Scheduling**: Delayed actions or recurring checks
4. **Notification Actions**: Integration with Slack, Teams, or email notifications
5. **Action Rollback**: Ability to undo certain actions (e.g., unarchive repositories)
6. **Action Analytics**: Detailed reporting on action effectiveness and compliance trends
