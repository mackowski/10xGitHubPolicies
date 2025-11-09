# Action Service

This document describes the `IActionService` and how it handles automated actions for policy violations in the 10x GitHub Policy Enforcer.

> **Note**: This document is part of the [Services Architecture](./architecture.md) documentation.

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

The service uses `PolicyConfig` from the configuration service, which includes:
- `Actions`: A list of action types (`create-issue`, `archive-repo`, or `log-only`). Supports both single action (backward compatible) and multiple actions per policy.
- `IssueDetails`: Optional details for issue creation (title, body, labels)

## Multiple Actions Per Policy

Policies can be configured with multiple actions that will be executed in sequence for each violation:

- **Single Action Format** (backward compatible): `action: 'create-issue'`
- **Multiple Actions Format** (new): `action: ['create-issue', 'archive-repo']`

When multiple actions are configured:
- All actions are executed in the order specified
- Each action executes independently - one failure doesn't block others
- Each action creates a separate `ActionLog` entry
- Actions are processed sequentially for each violation

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

- **Individual Action Failures**: If one action fails, other actions continue processing (both within a single policy's action list and across different violations)
- **Action Isolation**: When multiple actions are configured per policy, each action is wrapped in try-catch to ensure failures don't block subsequent actions
- **Exception Logging**: All exceptions are logged with detailed error information
- **Action Status Tracking**: Each action is logged with its status (Success, Failed, or Skipped)
- **Graceful Degradation**: Service continues processing even if some actions fail
- **Specific Exception Handling**: 
  - `NotFoundException`: Handled gracefully with warning-level logging
  - `ApiException` with `Forbidden`: Handled with warning-level logging for permission issues
  - Other exceptions: Logged at error level with full exception details

## Usage

Actions are typically enqueued as background jobs after a scan completes:

```csharp
_backgroundJobClient.Enqueue<IActionService>(service => 
    service.ProcessActionsForScanAsync(scanId));
```

### Configuration Example

```yaml
policies:
  - name: 'Check for AGENTS.md'
    type: 'has_agents_md'
    action: 'create-issue'  # Single action
    issue_details:
      title: 'Compliance: AGENTS.md file is missing'
      body: 'This repository is missing the AGENTS.md file...'
      labels: ['policy-violation', 'documentation']
      
  - name: 'Critical Security Policy'
    type: 'has_agents_md'
    action: ['create-issue', 'archive-repo']  # Multiple actions
    issue_details:
      title: 'Critical: Security policy violation'
      body: 'This repository violates critical security policies...'
      labels: ['policy-violation', 'security', 'critical']
      
  - name: 'Verify Workflow Permissions'
    type: 'correct_workflow_permissions'
    action: 'archive-repo'  # Single action
```

## Service Registration

Registered as a **scoped** service in the DI container.

## Best Practices

- Always enqueue action processing as a background job to avoid blocking the UI
- Monitor the Hangfire dashboard for failed action jobs
- Ensure policy configurations have valid action types and issue details
- The service automatically prevents duplicate issues and archive actions

## Monitoring

- **Hangfire Dashboard** (`/hangfire`): Monitor job status, failures, and performance
- **Action Logs**: Track success rates, failure patterns, and compliance trends in the database
- **GitHub**: Monitor created issues, repository archiving, and API rate limits

