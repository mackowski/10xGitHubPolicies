using _10xGitHubPolicies.App.Services.GitHub;
using Octokit;
using _10xGitHubPolicies.Tests.E2E.Infrastructure;

namespace _10xGitHubPolicies.Tests.E2E.Helpers;

/// <summary>
/// Helper class for GitHub repository operations in E2E tests.
/// Includes cleanup, verification, and waiting operations.
/// </summary>
public static class RepositoryHelper
{
    public static async Task CleanupRepositoriesAsync(
        IGitHubService gitHubService,
        IEnumerable<string> repositoryNames)
    {
        foreach (var repoName in repositoryNames)
        {
            try
            {
                await gitHubService.DeleteRepositoryAsync(repoName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to cleanup repository {repoName}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Verifies repositories are visible to the GitHub API, with retry logic.
    /// Extracted from WorkflowTests lines 151-172.
    /// </summary>
    /// <param name="gitHubService">The GitHub service</param>
    /// <param name="repositoryNames">The repository names to verify</param>
    /// <returns>List of visible repositories (null if not found)</returns>
    public static async Task<List<Octokit.Repository>> VerifyRepositoriesVisibleAsync(
        IGitHubService gitHubService,
        params string[] repositoryNames)
    {
        Console.WriteLine("🔍 Verifying repositories are visible to GitHub API...");
        var allRepos = await gitHubService.GetOrganizationRepositoriesAsync();

        var foundRepos = new List<Octokit.Repository>();
        foreach (var repoName in repositoryNames)
        {
            var repo = allRepos.FirstOrDefault(r => r.Name == repoName);
            foundRepos.Add(repo!);
            Console.WriteLine($"🔍 Repository {repoName} visible: {repo != null} (ID: {repo?.Id})");
        }

        if (foundRepos.Any(r => r == null))
        {
            Console.WriteLine("❌ Some test repositories are not visible to GitHub API yet. Waiting longer...");
            await Task.Delay(10000);

            // Try again
            allRepos = await gitHubService.GetOrganizationRepositoriesAsync();
            foundRepos.Clear();
            foreach (var repoName in repositoryNames)
            {
                var repo = allRepos.FirstOrDefault(r => r.Name == repoName);
                foundRepos.Add(repo!);
                Console.WriteLine($"🔍 After additional wait - Repository {repoName} visible: {repo != null}");
            }
        }

        return foundRepos;
    }

    /// <summary>
    /// Waits for policy violation issues to appear in a repository.
    /// Extracted from WorkflowTests lines 429-462.
    /// </summary>
    /// <param name="gitHubService">The GitHub service</param>
    /// <param name="repositoryName">The repository name</param>
    /// <returns>List of policy violation issues, or empty list if timeout</returns>
    public static async Task<IReadOnlyList<Issue>> WaitForPolicyViolationIssuesAsync(
        IGitHubService gitHubService,
        string repositoryName)
    {
        Console.WriteLine("⏳ Waiting for GitHub issues to be visible...");
        var issuesMaxWaitTime = TimeSpan.FromSeconds(TestConstants.ActionTimeoutSeconds);
        var issuesStartTime = DateTime.UtcNow;
        var issuesFound = false;
        IReadOnlyList<Issue>? policyViolationIssues = null;

        while (DateTime.UtcNow - issuesStartTime < issuesMaxWaitTime && !issuesFound)
        {
            var issues = await gitHubService.GetRepositoryIssuesAsync(repositoryName);
            policyViolationIssues = issues.Where(i => i.Labels.Any(l => l.Name == "policy-violation")).ToList();

            if (policyViolationIssues.Any())
            {
                Console.WriteLine($"✅ Found {policyViolationIssues.Count} policy violation issues for {repositoryName}");
                issuesFound = true;
                break;
            }

            await Task.Delay(2000); // Wait 2 seconds between retries
        }

        if (!issuesFound)
        {
            // Log what issues we found for debugging
            var allIssues = await gitHubService.GetRepositoryIssuesAsync(repositoryName);
            Console.WriteLine($"⚠️ Warning: No policy violation issues found. Total issues in repo: {allIssues.Count}");
            foreach (var issue in allIssues)
            {
                Console.WriteLine($"  - Issue #{issue.Number}: {issue.Title} (Labels: {string.Join(", ", issue.Labels.Select(l => l.Name))})");
            }
        }

        return policyViolationIssues ?? Array.Empty<Issue>();
    }
}

