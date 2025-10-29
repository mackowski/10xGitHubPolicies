namespace _10xGitHubPolicies.App.Options;

public class GitHubAppOptions
{
    public const string GitHubApp = "GitHubApp";

    public long AppId { get; set; }
    public string PrivateKey { get; set; } = string.Empty;
    public long InstallationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;

    /// <summary>
    /// Optional base URL for GitHub API. If null, uses default GitHub API (https://api.github.com).
    /// Primarily used for testing with WireMock.
    /// </summary>
    public string? BaseUrl { get; set; }
}