# Scanning Service

This document describes the `IScanningService` and how it orchestrates the repository scanning process for the 10x GitHub Policy Enforcer.

> **Note**: This document is part of the [Services Architecture](./architecture.md) documentation.

## Overview

The Scanning Service is responsible for orchestrating the end-to-end repository scanning process. It coordinates fetching repositories from GitHub, synchronizing them with the database, evaluating policies, and tracking violations.

## Service Interface

### `IScanningService`

- `Task PerformScanAsync()`: Performs a complete scan of all organization repositories, synchronizes data, evaluates policies, saves violations, and enqueues action processing

## Implementation

### `ScanningService`

**Dependencies**:
- `IGitHubService`: For fetching repositories from GitHub
- `IConfigurationService`: For retrieving policy configuration
- `IPolicyEvaluationService`: For evaluating repositories against policies
- `ApplicationDbContext`: For database operations
- `IBackgroundJobClient`: For enqueuing action processing jobs
- `ILogger<ScanningService>`: For logging scan operations

## Workflow

The scanning process follows these steps:

1. **Create Scan Record**: Creates a new `Scan` entity with status "InProgress"
2. **Retrieve Configuration**: Fetches policy configuration from `.github/config.yaml`
3. **Fetch Repositories**: Gets all active repositories from the GitHub organization
4. **Synchronize Policies**: Ensures all policies from config exist in the database
5. **Synchronize Repositories**: 
   - Adds new repositories to the database
   - Updates repository names if renamed
   - Removes repositories that no longer exist in GitHub
6. **Evaluate Policies**: For each repository, evaluates against all configured policies
7. **Save Violations**: Persists all detected violations to the database
8. **Complete Scan**: Updates scan status to "Completed" and records completion time
9. **Enqueue Actions**: Triggers background job to process automated actions for violations

## Repository Synchronization

The service automatically synchronizes the database with the current state of the GitHub organization:

### New Repositories
- Automatically detected and added to the database
- Initial status set to "Pending"
- Uses GitHub repository ID (not name) for identification

### Renamed Repositories
- Detected by matching GitHub repository ID (which remains constant)
- Repository name automatically updated in the database
- All related records (violations, action logs) remain intact

### Deleted Repositories
- Repositories that no longer exist in GitHub are removed
- Cascading deletion of related records:
  - Policy violations associated with the repository
  - Action logs for actions taken on the repository
- Ensures database stays in sync with GitHub organization

## Policy Synchronization

The service synchronizes policies from the configuration file with the database:

- **New Policies**: Automatically added to the database when found in config
- **Policy Key**: Uses the policy `type` field as the unique identifier
- **Description**: Auto-generated placeholder description if not provided
- **Action**: Stored from configuration for action processing

## Error Handling

The service implements comprehensive error handling:

- **Scan Status Tracking**: Failed scans are marked with status "Failed"
- **Exception Logging**: All exceptions are logged with full details
- **Graceful Degradation**: Scan failures don't affect the database integrity
- **Action Processing**: Actions are only enqueued if the scan completes successfully

## Usage

The service is typically invoked as a background job:

```csharp
// Manual trigger
_backgroundJobClient.Enqueue<IScanningService>(s => s.PerformScanAsync());

// Scheduled (daily at midnight UTC)
RecurringJob.AddOrUpdate<IScanningService>(
    "daily-scan",
    service => service.PerformScanAsync(),
    "0 0 * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
```

## Service Registration

Registered as a **scoped** service because each scan operation is independent and uses a scoped database context.

## Performance Considerations

- Batch database operations for synchronization
- Efficient LINQ queries with proper includes
- Long-running operations offloaded to background jobs
- Comprehensive logging for monitoring

## Related Documentation

- [Policy Evaluation Service](./policy-evaluation.md) - How policies are evaluated
- [Action Service](./action-service.md) - How violations trigger actions
- [Hangfire Integration](../hangfire-integration.md) - Background job processing
- [Database Schema](../database.md) - Database structure
- [Configuration Service](./configuration-service.md) - Policy configuration

