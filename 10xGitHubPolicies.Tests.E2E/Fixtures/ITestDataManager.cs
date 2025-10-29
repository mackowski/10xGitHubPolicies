using Octokit;

namespace _10xGitHubPolicies.Tests.E2E.Fixtures;

public interface ITestDataManager
{
    Task<(string CompliantRepo, string NonCompliantRepo)> CreateTestRepositoriesAsync();
    Task<Repository> CreateCompliantRepositoryAsync(string name);
    Task<Repository> CreateNonCompliantRepositoryAsync(string name, string[] violations);
}

