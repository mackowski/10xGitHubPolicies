using Octokit;

namespace _10xGitHubPolicies.App.Services;

public interface IGitHubService
{
    Task<GitHubClient> GetAuthenticatedClient();
}
