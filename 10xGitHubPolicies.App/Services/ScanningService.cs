namespace _10xGitHubPolicies.App.Services;

public class ScanningService : IScanningService
{
    private readonly IGitHubService _gitHubService;
    private readonly ILogger<ScanningService> _logger;

    public ScanningService(IGitHubService gitHubService, ILogger<ScanningService> logger)
    {
        _gitHubService = gitHubService;
        _logger = logger;
    }

    public async Task PerformScanAsync()
    {
        _logger.LogInformation("Starting repository scan...");

        var client = await _gitHubService.GetAuthenticatedClient();
        var installationRepositories = await client.GitHubApps.Installation.GetAllRepositoriesForCurrent();
        var repositories = installationRepositories.Repositories;
        
        _logger.LogInformation("Found {Count} repositories:", repositories.Count);
        foreach (var repo in repositories)
        {
            _logger.LogInformation("- {RepoName}", repo.FullName);
        }

        _logger.LogInformation("Repository scan finished.");
    }
}
