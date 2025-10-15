# Background Jobs with Hangfire

This document describes how background jobs are implemented using Hangfire.

## Configuration

Hangfire is configured in `Program.cs` to use a SQL Server database for job storage.

- **Services**: `AddHangfire` and `AddHangfireServer` are used to register Hangfire services.
- **Dashboard**: The Hangfire dashboard is enabled at the `/hangfire` endpoint.

## Usage

To enqueue a new background job, inject `IBackgroundJobClient` into your service and use one of its methods (`Enqueue`, `Schedule`, etc.).

### Example

```csharp
public class MyService
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public MyService(IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public void EnqueueJob()
    {
        _backgroundJobClient.Enqueue(() => Console.WriteLine("Hello from a Hangfire job!"));
    }
}
```

## Monitoring

The Hangfire dashboard is available at `/hangfire` and can be used to monitor the status of background jobs.
