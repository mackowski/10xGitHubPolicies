using Microsoft.EntityFrameworkCore;
using _10xGitHubPolicies.App.Data;
using _10xGitHubPolicies.App.Data.Entities;
using _10xGitHubPolicies.Tests.E2E.Infrastructure;

namespace _10xGitHubPolicies.Tests.E2E.Helpers;

/// <summary>
/// Helper class for database query operations in E2E tests.
/// Extracted from WorkflowTests to reduce code duplication.
/// </summary>
public static class DatabaseHelper
{
    /// <summary>
    /// Waits for the latest completed scan and verifies it contains violations for a specific repository.
    /// Extracted from WorkflowTests lines 253-290.
    /// </summary>
    /// <param name="dbContext">The database context</param>
    /// <param name="repositoryId">The GitHub repository ID to check for violations</param>
    /// <returns>The latest completed scan, or null if not found within timeout</returns>
    public static async Task<Scan?> WaitForScanCompleteAsync(
        ApplicationDbContext dbContext,
        long repositoryId)
    {
        Console.WriteLine("⏳ Waiting for scan results to be processed and stored in database...");
        var maxWaitTime = TimeSpan.FromSeconds(TestConstants.DatabaseQueryTimeoutSeconds);
        var startTime = DateTime.UtcNow;
        var scanCompleted = false;
        Scan? latestScan = null;

        while (DateTime.UtcNow - startTime < maxWaitTime && !scanCompleted)
        {
            latestScan = await dbContext.Scans
                .Where(s => s.Status == "Completed")
                .OrderByDescending(s => s.CompletedAt)
                .FirstOrDefaultAsync();
            
            if (latestScan != null && latestScan.CompletedAt > DateTime.UtcNow.AddMinutes(-1))
            {
                // Check if our test repositories have violations in this scan
                var violations = await dbContext.PolicyViolations
                    .Where(v => v.ScanId == latestScan.ScanId)
                    .Include(v => v.Repository)
                    .Where(v => v.Repository.GitHubRepositoryId == repositoryId)
                    .FirstOrDefaultAsync();
                
                if (violations != null)
                {
                    Console.WriteLine($"✅ Scan results stored in database (Scan ID: {latestScan.ScanId}, Found violations for test repo)");
                    scanCompleted = true;
                    break;
                }
            }
            
            await Task.Delay(1000);
        }

        if (!scanCompleted)
        {
            Console.WriteLine("⚠️ Warning: Scan results not found in database within timeout period");
        }

        return latestScan;
    }

    /// <summary>
    /// Waits for action logs with success status for a repository within the last minute.
    /// Extracted from WorkflowTests lines 392-427.
    /// </summary>
    /// <param name="dbContext">The database context</param>
    /// <param name="repositoryId">The GitHub repository ID</param>
    /// <returns>True if action logs found, false if timeout exceeded</returns>
    public static async Task<bool> WaitForActionLogsAsync(
        ApplicationDbContext dbContext,
        long repositoryId)
    {
        Console.WriteLine("⏳ Waiting for action processing to complete...");
        var actionMaxWaitTime = TimeSpan.FromSeconds(TestConstants.ActionTimeoutSeconds);
        var actionStartTime = DateTime.UtcNow;
        var actionsCompleted = false;
        
        // Get the repository entity from database
        var repoEntity = await dbContext.Repositories
            .FirstOrDefaultAsync(r => r.GitHubRepositoryId == repositoryId);
        
        if (repoEntity != null)
        {
            while (DateTime.UtcNow - actionStartTime < actionMaxWaitTime && !actionsCompleted)
            {
                // Check if action log entries exist for our repository
                var actionLogs = await dbContext.ActionsLogs
                    .Where(a => a.RepositoryId == repoEntity.RepositoryId && 
                                a.Status == "Success" &&
                                a.Timestamp > DateTime.UtcNow.AddMinutes(-1))
                    .CountAsync();
                
                if (actionLogs > 0)
                {
                    Console.WriteLine($"✅ Action processing completed ({actionLogs} actions processed)");
                    actionsCompleted = true;
                    break;
                }
                
                await Task.Delay(1000);
            }
        }
        
        if (!actionsCompleted)
        {
            Console.WriteLine("⚠️ Warning: Action processing not completed within timeout period");
        }

        return actionsCompleted;
    }

    /// <summary>
    /// Waits for a new scan (different from previous scan ID) to complete and verifies it contains violations for a specific repository.
    /// Extracted from WorkflowTests lines 496-533.
    /// </summary>
    /// <param name="dbContext">The database context</param>
    /// <param name="previousScanId">The scan ID to exclude (the previous scan)</param>
    /// <param name="repositoryId">The GitHub repository ID to check for violations</param>
    /// <returns>The new completed scan, or null if not found within timeout</returns>
    public static async Task<Scan?> WaitForRescanCompleteAsync(
        ApplicationDbContext dbContext,
        int? previousScanId,
        long repositoryId)
    {
        Console.WriteLine("⏳ Waiting for re-scan results to be processed and stored in database...");
        var rescanMaxWaitTime = TimeSpan.FromSeconds(TestConstants.DatabaseQueryTimeoutSeconds);
        var rescanStartTime = DateTime.UtcNow;
        var rescanCompleted = false;
        Scan? rescanLatestScan = null;

        while (DateTime.UtcNow - rescanStartTime < rescanMaxWaitTime && !rescanCompleted)
        {
            rescanLatestScan = await dbContext.Scans
                .Where(s => s.Status == "Completed")
                .OrderByDescending(s => s.CompletedAt)
                .FirstOrDefaultAsync();
            
            if (rescanLatestScan != null && rescanLatestScan.ScanId != previousScanId && rescanLatestScan.CompletedAt > DateTime.UtcNow.AddMinutes(-1))
            {
                // Check if our test repositories have violations in this scan
                var violations = await dbContext.PolicyViolations
                    .Where(v => v.ScanId == rescanLatestScan.ScanId)
                    .Include(v => v.Repository)
                    .Where(v => v.Repository.GitHubRepositoryId == repositoryId)
                    .FirstOrDefaultAsync();
                
                if (violations != null)
                {
                    Console.WriteLine($"✅ Re-scan results stored in database (Scan ID: {rescanLatestScan.ScanId}, Found violations for test repo)");
                    rescanCompleted = true;
                    break;
                }
            }
            
            await Task.Delay(1000);
        }

        if (!rescanCompleted)
        {
            Console.WriteLine("⚠️ Warning: Re-scan results not found in database within timeout period");
        }

        return rescanLatestScan;
    }
}

