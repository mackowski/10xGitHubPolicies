using Octokit;

namespace _10xGitHubPolicies.App.Services.GitHub;

/// <summary>
/// Factory for creating GitHubClient instances with optional custom base URLs.
/// Supports both user token authentication and GitHub App JWT authentication.
/// </summary>
public interface IGitHubClientFactory
{
    /// <summary>
    /// Creates a GitHubClient authenticated with a user or installation access token.
    /// </summary>
    /// <param name="token">The access token for authentication</param>
    /// <returns>A configured GitHubClient instance</returns>
    GitHubClient CreateClient(string token);

    /// <summary>
    /// Creates a GitHubClient authenticated with a GitHub App JWT token.
    /// </summary>
    /// <param name="jwt">The JWT token for GitHub App authentication</param>
    /// <returns>A configured GitHubClient instance for app-level operations</returns>
    GitHubClient CreateAppClient(string jwt);
}