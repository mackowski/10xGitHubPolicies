using YamlDotNet.Serialization;

namespace _10xGitHubPolicies.App.Models.Configuration;

public class PolicyConfig
{
    [YamlMember(Alias = "type")]
    public string Type { get; set; }

    [YamlMember(Alias = "action")]
    public string Action { get; set; }
}