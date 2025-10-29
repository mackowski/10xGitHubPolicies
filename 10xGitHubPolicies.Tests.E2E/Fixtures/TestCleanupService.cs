using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using _10xGitHubPolicies.App.Services.GitHub;
using _10xGitHubPolicies.App.Data;

namespace _10xGitHubPolicies.Tests.E2E.Fixtures;

public class TestCleanupService : ITestCleanupService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IGitHubService _gitHubService;
    private readonly ILogger<TestCleanupService> _logger;

    public TestCleanupService(ApplicationDbContext dbContext, IGitHubService gitHubService, ILogger<TestCleanupService> logger)
    {
        _dbContext = dbContext;
        _gitHubService = gitHubService;
        _logger = logger;
    }

    public async Task CleanupTestDataAsync()
    {
        // Clean up database
        var recentScans = await _dbContext.Scans
            .Where(s => s.StartedAt > DateTime.UtcNow.AddHours(-1))
            .ToListAsync();

        var recentViolations = await _dbContext.PolicyViolations
            .Where(p => recentScans.Select(s => s.ScanId).Contains(p.ScanId))
            .ToListAsync();

        var recentActions = await _dbContext.ActionsLogs
            .Where(a => a.Timestamp > DateTime.UtcNow.AddHours(-1))
            .ToListAsync();

        _dbContext.ActionsLogs.RemoveRange(recentActions);
        _dbContext.PolicyViolations.RemoveRange(recentViolations);
        _dbContext.Scans.RemoveRange(recentScans);

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Cleaned up test data from database");

        // Clean up GitHub repositories
        try
        {
            var repos = await _gitHubService.GetOrganizationRepositoriesAsync();
            var testRepos = repos.Where(r => r.Name.StartsWith("e2e-test-")).ToList();

            foreach (var repo in testRepos)
            {
                try
                {
                    await _gitHubService.DeleteRepositoryAsync(repo.Name);
                    _logger.LogInformation("Cleaned up GitHub repository: {RepoName}", repo.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to cleanup repository {RepoName}: {Error}", repo.Name, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to cleanup GitHub repositories: {Error}", ex.Message);
        }
    }

    public async Task CleanupAllAsync()
    {
        await CleanupTestDataAsync();
    }
}

