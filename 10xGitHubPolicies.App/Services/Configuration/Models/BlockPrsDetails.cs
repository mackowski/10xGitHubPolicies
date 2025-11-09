using YamlDotNet.Serialization;

namespace _10xGitHubPolicies.App.Services.Configuration.Models;

/// <summary>
/// Configuration details for blocking PRs via status checks.
/// </summary>
public class BlockPrsDetails
{
    [YamlMember(Alias = "status_check_name")]
    public string StatusCheckName { get; set; } = "Policy Compliance Check";
}