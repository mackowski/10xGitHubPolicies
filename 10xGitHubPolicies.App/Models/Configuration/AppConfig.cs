using YamlDotNet.Serialization;

namespace _10xGitHubPolicies.App.Models.Configuration;

public class AppConfig
{
    [YamlMember(Alias = "access_control")]
    public AccessControlConfig AccessControl { get; set; }

    public List<PolicyConfig> Policies { get; set; } = new();
}