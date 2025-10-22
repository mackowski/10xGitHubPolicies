using YamlDotNet.Serialization;

namespace _10xGitHubPolicies.App.Models.Configuration;

public class AccessControlConfig
{
    [YamlMember(Alias = "authorized_team")]
    public string AuthorizedTeam { get; set; }
}