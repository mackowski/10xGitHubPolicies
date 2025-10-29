namespace _10xGitHubPolicies.Tests.E2E.Fixtures;

public interface ITestCleanupService
{
    Task CleanupTestDataAsync();
    Task CleanupAllAsync();
}

