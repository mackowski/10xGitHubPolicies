namespace _10xGitHubPolicies.App.Services;

public class ScanningService : IScanningService
{
    private readonly IGitHubService _gitHubService;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<ScanningService> _logger;

    public ScanningService(IGitHubService gitHubService, IConfigurationService configurationService, ILogger<ScanningService> logger)
    {
        _gitHubService = gitHubService;
        _configurationService = configurationService;
        _logger = logger;
    }

    public async Task PerformScanAsync()
    {
        _logger.LogInformation("Starting repository scan...");

        try
        {
            var config = await _configurationService.GetConfigAsync();
            _logger.LogInformation("Successfully retrieved configuration. Authorized team: {AuthorizedTeam}", config.AccessControl.AuthorizedTeam);
            foreach (var policy in config.Policies)
            {
                _logger.LogInformation("- Policy: Type={Type}, Action={Action}", policy.Type, policy.Action);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve configuration.");
        }

        var repositories = await _gitHubService.GetOrganizationRepositoriesAsync();

        _logger.LogInformation("Found {Count} repositories:", repositories.Count);
        foreach (var repo in repositories)
        {
            _logger.LogInformation("- {RepoName}", repo.FullName);
        }

        _logger.LogInformation("Repository scan finished.");



    }
}