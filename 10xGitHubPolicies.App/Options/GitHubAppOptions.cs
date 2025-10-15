namespace _10xGitHubPolicies.App.Options;

public class GitHubAppOptions
{
    public const string GitHubApp = "GitHubApp";

    public long AppId { get; set; }
    public string PrivateKey { get; set; } = string.Empty;
    public long InstallationId { get; set; }
}
