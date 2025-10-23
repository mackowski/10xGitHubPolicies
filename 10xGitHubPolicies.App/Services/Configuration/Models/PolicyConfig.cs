using YamlDotNet.Serialization;

namespace _10xGitHubPolicies.App.Services.Configuration.Models;

public class PolicyConfig
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "type")]
    public string Type { get; set; } = string.Empty;

    [YamlMember(Alias = "action")]
    public string Action { get; set; } = string.Empty;

    [YamlMember(Alias = "issue_details")]
    public IssueDetails? IssueDetails { get; set; }
}