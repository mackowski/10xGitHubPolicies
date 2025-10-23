using _10xGitHubPolicies.App.Data;
using _10xGitHubPolicies.App.Data.Entities;
using _10xGitHubPolicies.App.Services.Configuration;
using _10xGitHubPolicies.App.Services.Configuration.Models;
using _10xGitHubPolicies.App.Services.GitHub;
using _10xGitHubPolicies.App.Services.Policies;
using _10xGitHubPolicies.App.Services.Action;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace _10xGitHubPolicies.App.Services.Scanning;

public class ScanningService : IScanningService
{
    private readonly IGitHubService _gitHubService;
    private readonly IConfigurationService _configurationService;
    private readonly IPolicyEvaluationService _policyEvaluationService;
    private readonly ApplicationDbContext _dbContext;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<ScanningService> _logger;

    public ScanningService(
        IGitHubService gitHubService,
        IConfigurationService configurationService,
        IPolicyEvaluationService policyEvaluationService,
        ApplicationDbContext dbContext,
        IBackgroundJobClient backgroundJobClient,
        ILogger<ScanningService> logger)
    {
        _gitHubService = gitHubService;
        _configurationService = configurationService;
        _policyEvaluationService = policyEvaluationService;
        _dbContext = dbContext;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    public async Task PerformScanAsync()
    {
        _logger.LogInformation("Starting repository scan...");

        var scan = new Scan
        {
            Status = "InProgress",
            StartedAt = DateTime.UtcNow
        };
        _dbContext.Scans.Add(scan);
        await _dbContext.SaveChangesAsync();

        try
        {
            var config = await _configurationService.GetConfigAsync();
            var repositories = await _gitHubService.GetOrganizationRepositoriesAsync();

            var policyEntities = await SyncPoliciesAsync(config.Policies);
            await SyncRepositoriesAsync(repositories);

            _logger.LogInformation("Found {Count} repositories to scan.", repositories.Count);

            foreach (var repo in repositories)
            {
                var repoEntity = await _dbContext.Repositories.SingleAsync(r => r.GitHubRepositoryId == repo.Id);
                var violations = await _policyEvaluationService.EvaluateRepositoryAsync(repo, config.Policies);

                if (violations.Any())
                {
                    foreach (var violation in violations)
                    {
                        violation.ScanId = scan.ScanId;
                        violation.RepositoryId = repoEntity.RepositoryId;
                        violation.PolicyId = policyEntities[violation.PolicyType].PolicyId;
                        _dbContext.PolicyViolations.Add(violation);
                    }
                }
            }

            scan.Status = "Completed";
            scan.CompletedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            _backgroundJobClient.Enqueue<IActionService>(actionService => actionService.ProcessActionsForScanAsync(scan.ScanId));

            _logger.LogInformation("Repository scan finished. Found {ViolationCount} violations. Enqueued action processing job.", _dbContext.PolicyViolations.Count(v => v.ScanId == scan.ScanId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during the scan.");
            scan.Status = "Failed";
            scan.CompletedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
    }

    private async Task<Dictionary<string, Policy>> SyncPoliciesAsync(IEnumerable<PolicyConfig> policyConfigs)
    {
        var policiesInDb = await _dbContext.Policies.ToListAsync();
        var policyMap = policiesInDb.ToDictionary(p => p.PolicyKey, p => p);

        foreach (var policyConfig in policyConfigs)
        {
            if (!policyMap.ContainsKey(policyConfig.Type))
            {
                var newPolicy = new Policy
                {
                    PolicyKey = policyConfig.Type,
                    Description = $"Policy for {policyConfig.Type}", // Placeholder description
                    Action = policyConfig.Action
                };
                _dbContext.Policies.Add(newPolicy);
                policyMap[newPolicy.PolicyKey] = newPolicy;
            }
        }
        await _dbContext.SaveChangesAsync();
        return policyMap;
    }

    private async Task SyncRepositoriesAsync(IReadOnlyList<Octokit.Repository> repositories)
    {
        var githubRepoIds = repositories.Select(r => r.Id).ToList();
        var reposInDb = await _dbContext.Repositories
            .Where(r => githubRepoIds.Contains(r.GitHubRepositoryId))
            .ToListAsync();
        var reposInDbIds = reposInDb.Select(r => r.GitHubRepositoryId).ToHashSet();

        foreach (var repo in repositories)
        {
            if (!reposInDbIds.Contains(repo.Id))
            {
                _dbContext.Repositories.Add(new Repository
                {
                    GitHubRepositoryId = repo.Id,
                    Name = repo.FullName,
                    ComplianceStatus = "Pending"
                });
            }
        }
        await _dbContext.SaveChangesAsync();
    }
}