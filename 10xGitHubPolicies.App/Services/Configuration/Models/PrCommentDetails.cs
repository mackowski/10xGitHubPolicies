using YamlDotNet.Serialization;

namespace _10xGitHubPolicies.App.Services.Configuration.Models;

/// <summary>
/// Configuration details for PR comment actions.
/// </summary>
public class PrCommentDetails
{
    [YamlMember(Alias = "message")]
    public string Message { get; set; } = string.Empty;
}