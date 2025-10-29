using Microsoft.Extensions.Logging;
using Bogus;
using _10xGitHubPolicies.App.Services.GitHub;
using Octokit;

namespace _10xGitHubPolicies.Tests.E2E.Fixtures;

public class TestDataManager : ITestDataManager
{
    private readonly IGitHubService _gitHubService;
    private readonly ILogger<TestDataManager> _logger;
    private readonly Faker _faker;

    public TestDataManager(IGitHubService gitHubService, ILogger<TestDataManager> logger)
    {
        _gitHubService = gitHubService;
        _logger = logger;
        _faker = new Faker();
    }

    public async Task<(string CompliantRepo, string NonCompliantRepo)> CreateTestRepositoriesAsync()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var compliantRepo = $"e2e-test-compliant-{timestamp}";
        var nonCompliantRepo = $"e2e-test-non-compliant-{timestamp}";

        // Create compliant repository
        var compliantRepoObj = await _gitHubService.CreateRepositoryAsync(compliantRepo, "E2E test compliant repository", true);
        _logger.LogInformation("Created compliant test repository: {RepoName}", compliantRepo);
        
        // Add AGENTS.md file to make it compliant
        await _gitHubService.CreateFileAsync(compliantRepoObj.Id, "AGENTS.md", "# AGENTS.md\n\nThis is a test AGENTS.md file for E2E testing.", "Add AGENTS.md for compliance testing");
        _logger.LogInformation("Added AGENTS.md to compliant repository: {RepoName}", compliantRepo);

        // Create non-compliant repository (missing AGENTS.md)
        await _gitHubService.CreateRepositoryAsync(nonCompliantRepo, "E2E test non-compliant repository", true);
        _logger.LogInformation("Created non-compliant test repository: {RepoName} with violations: has_agents_md", nonCompliantRepo);

        Console.WriteLine($"✅ Created test repositories: {compliantRepo}, {nonCompliantRepo}");
        return (compliantRepo, nonCompliantRepo);
    }

    public async Task<Repository> CreateCompliantRepositoryAsync(string name)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var repoName = $"e2e-test-compliant-{name}-{timestamp}";
        
        var repo = await _gitHubService.CreateRepositoryAsync(repoName, "E2E test compliant repository", true);
        _logger.LogInformation("Created compliant test repository: {RepoName}", repoName);
        
        // Create AGENTS.md file to make it compliant
        await _gitHubService.CreateFileAsync(repo.Id, "AGENTS.md", "# AGENTS.md\n\nThis is a test AGENTS.md file for E2E testing.", "Add AGENTS.md for compliance testing");
        _logger.LogInformation("Added AGENTS.md to compliant repository: {RepoName}", repoName);
        
        return repo;
    }

    public async Task<Repository> CreateNonCompliantRepositoryAsync(string name, string[] violations)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var repoName = $"e2e-test-non-compliant-{name}-{timestamp}";
        
        var repo = await _gitHubService.CreateRepositoryAsync(repoName, "E2E test non-compliant repository", true);
        _logger.LogInformation("Created non-compliant test repository: {RepoName} with violations: {Violations}", repoName, string.Join(", ", violations));
        
        // Intentionally don't create AGENTS.md to make it non-compliant
        // This simulates a repository that violates the has_agents_md policy
        
        return repo;
    }
}

