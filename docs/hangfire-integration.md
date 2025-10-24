# Background Jobs with Hangfire

This document describes how the 10x GitHub Policy Enforcer uses Hangfire for reliable background job processing.

## Overview

Hangfire is an open-source framework for .NET that allows for easy creation, processing, and management of background jobs. In this application, it is used to offload long-running tasks like repository scanning and automated action processing from the main web thread. This ensures that the user interface remains responsive and can handle these tasks without timing out.

## Configuration

Hangfire is configured in `Program.cs` to use a SQL Server database as its job storage.

### Service Registration

```csharp
// Program.cs

// Add Hangfire services.
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add the processing server as IHostedService
builder.Services.AddHangfireServer();
```

-   `AddHangfire()`: Registers Hangfire services and configures it to use SQL Server for storage. This makes job persistence reliable, as jobs will survive application restarts.
-   `AddHangfireServer()`: Adds the Hangfire Server to the application. The server is responsible for picking up and processing enqueued jobs from the storage.

### Dashboard

The Hangfire dashboard is enabled to provide a UI for monitoring jobs.

```csharp
// Program.cs

app.UseHangfireDashboard();
```

The dashboard is accessible at the `/hangfire` endpoint of the application (e.g., `https://localhost:5001/hangfire`). It provides a detailed view of enqueued, processing, succeeded, and failed jobs.

## Usage in the Application

Hangfire is used in three main scenarios:

1.  **On-Demand Repository Scanning**: When a user clicks the "Scan Now" button on the dashboard.
2.  **Daily Automated Scanning**: A recurring job that automatically scans all repositories daily at midnight UTC.
3.  **Processing Actions for Violations**: After a scan is completed, a job is enqueued to process the configured actions for any violations found using the `ActionService`.

### Enqueuing a Scan

In `Index.razor`, the `IScanningService.PerformScanAsync()` method is enqueued as a background job when the user initiates a scan.

```csharp
// Pages/Index.razor

private async Task StartScan()
{
    _isScanning = true;
    StateHasChanged();

    BackgroundJobClient.Enqueue<IScanningService>(s => s.PerformScanAsync());
    
    // ... UI update logic ...
}
```

By using `_backgroundJobClient.Enqueue()`, the `PerformScanAsync` method is executed on a background thread by a Hangfire worker. This immediately returns control to the UI, which can then display a "Scanning..." status to the user.

### Daily Automated Scanning

The application is configured with a recurring job that automatically scans all repositories daily:

```csharp
// Program.cs

// Configure recurring jobs
RecurringJob.AddOrUpdate<IScanningService>(
    "daily-scan",
    service => service.PerformScanAsync(),
    "0 0 * * *", // Daily at midnight UTC
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.Utc
    });
```

This configuration:
- **Job Name**: `"daily-scan"` - unique identifier for the recurring job
- **Cron Expression**: `"0 0 * * *"` - runs daily at midnight UTC
- **Timezone**: UTC to ensure consistent execution times
- **Service**: Uses `IScanningService.PerformScanAsync()` for the actual scanning logic

The recurring job ensures that all repositories are automatically scanned for policy compliance without manual intervention, providing continuous monitoring of organizational compliance.

### Enqueuing Actions Post-Scan

In the `ScanningService`, after a scan is successfully completed and violations have been saved, a job is enqueued for the `IActionService` to process the results.

```csharp
// Services/ScanningService.cs

public async Task PerformScanAsync()
{
    // ... scanning logic ...

    scan.Status = "Completed";
    scan.CompletedAt = DateTime.UtcNow;
    await _dbContext.SaveChangesAsync();

    _backgroundJobClient.Enqueue<IActionService>(actionService => actionService.ProcessActionsForScanAsync(scan.ScanId));

    _logger.LogInformation("Repository scan finished. Enqueued action processing job.");
}
```

The `ActionService` processes violations by:
- Creating GitHub issues for violations with `create-issue` action
- Archiving repositories for violations with `archive-repo` action  
- Logging violations for `log-only` actions
- Preventing duplicate issue creation
- Logging all actions to the `ActionLog` table

This decouples the scanning process from the action-taking process. This is a robust design because even if the action-taking process fails, it can be retried independently from the scan itself, and the scan results are already safely stored in the database.

## Best Practices

-   **Idempotent Jobs**: Whenever possible, design background jobs to be idempotent. This means that running the job multiple times with the same input will produce the same result. Hangfire has built-in retry mechanisms, so idempotent jobs are safer to run.
-   **Small, Focused Jobs**: Keep background jobs small and focused on a single responsibility. For example, the scan and action processes are two separate jobs.
-   **Dependency Injection**: Hangfire jobs can leverage dependency injection, just like the rest of the application. The services required by the job method (e.g., `ApplicationDbContext`, `IGitHubService`) will be correctly injected at runtime.
-   **Monitoring**: Regularly check the Hangfire dashboard, especially in production, to monitor the health of your background job processing and to investigate any failed jobs.
