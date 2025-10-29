using Octokit;
using Octokit.Internal;

namespace _10xGitHubPolicies.App.Services.GitHub;

/// <summary>
/// Default implementation of IGitHubClientFactory.
/// Creates GitHubClient instances with support for custom base URLs (primarily for testing).
/// </summary>
public class GitHubClientFactory : IGitHubClientFactory
{
    private readonly string? _baseUrl;

    /// <summary>
    /// Initializes a new instance of the GitHubClientFactory.
    /// </summary>
    /// <param name="baseUrl">Optional custom base URL for GitHub API. If null, uses default GitHub API URL.</param>
    public GitHubClientFactory(string? baseUrl = null)
    {
        _baseUrl = baseUrl;
    }

    /// <inheritdoc />
    public GitHubClient CreateClient(string token)
    {
        var productHeader = new ProductHeaderValue("10xGitHubPolicies");
        var credentials = new Credentials(token);
        var credentialStore = new InMemoryCredentialStore(credentials);

        if (_baseUrl != null)
        {
            return new GitHubClient(productHeader, credentialStore, new Uri(_baseUrl));
        }

        return new GitHubClient(productHeader, credentialStore);
    }

    /// <inheritdoc />
    public GitHubClient CreateAppClient(string jwt)
    {
        var productHeader = new ProductHeaderValue("10xGitHubPolicies");
        var credentials = new Credentials(jwt, AuthenticationType.Bearer);
        var credentialStore = new InMemoryCredentialStore(credentials);

        if (_baseUrl != null)
        {
            return new GitHubClient(productHeader, credentialStore, new Uri(_baseUrl));
        }

        return new GitHubClient(productHeader, credentialStore);
    }
}