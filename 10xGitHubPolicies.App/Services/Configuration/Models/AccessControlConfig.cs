using YamlDotNet.Serialization;

namespace _10xGitHubPolicies.App.Services.Configuration.Models;

public class AccessControlConfig
{
    [YamlMember(Alias = "authorized_team")]
    public string AuthorizedTeam { get; set; } = string.Empty;
}