using System.Net.Http;

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
    private readonly HttpClientHandler? _httpClientHandler;

    /// <summary>
    /// Initializes a new instance of the GitHubClientFactory.
    /// </summary>
    /// <param name="baseUrl">Optional custom base URL for GitHub API. If null, uses default GitHub API URL.</param>
    /// <param name="httpClientHandler">Optional HttpClientHandler for custom HTTP configuration (e.g., for test environments with self-signed certificates).</param>
    public GitHubClientFactory(string? baseUrl = null, HttpClientHandler? httpClientHandler = null)
    {
        _baseUrl = baseUrl;
        _httpClientHandler = httpClientHandler;
    }

    /// <inheritdoc />
    public GitHubClient CreateClient(string token)
    {
        var productHeader = new ProductHeaderValue("10xGitHubPolicies");
        var credentials = new Credentials(token);
        var credentialStore = new InMemoryCredentialStore(credentials);

        if (_httpClientHandler != null)
        {
            // For custom base URLs (test scenarios), Connection should add /api/v3 automatically for Enterprise mode
            // However, with custom HttpClientAdapter, Connection may not detect Enterprise mode correctly
            // So we need to ensure the base URL format triggers Enterprise mode detection
            var baseUri = _baseUrl != null ? new Uri(_baseUrl) : GitHubClient.GitHubApiUrl;
            var httpClientAdapter = new HttpClientAdapter(() => _httpClientHandler);
            var connection = new Connection(productHeader, baseUri, credentialStore, httpClientAdapter, new SimpleJsonSerializer());

            return new GitHubClient(connection);
        }

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

        if (_httpClientHandler != null)
        {
            // For custom base URLs (test scenarios), Connection should add /api/v3 automatically for Enterprise mode
            // However, with custom HttpClientAdapter, Connection may not detect Enterprise mode correctly
            // So we need to ensure the base URL format triggers Enterprise mode detection
            var baseUri = _baseUrl != null ? new Uri(_baseUrl) : GitHubClient.GitHubApiUrl;
            var httpClientAdapter = new HttpClientAdapter(() => _httpClientHandler);
            var connection = new Connection(productHeader, baseUri, credentialStore, httpClientAdapter, new SimpleJsonSerializer());

            return new GitHubClient(connection);
        }

        if (_baseUrl != null)
        {
            return new GitHubClient(productHeader, credentialStore, new Uri(_baseUrl));
        }

        return new GitHubClient(productHeader, credentialStore);
    }
}