using YamlDotNet.Serialization;

namespace _10xGitHubPolicies.App.Services.Configuration.Models;

public class IssueDetails
{
    [YamlMember(Alias = "title")]
    public string Title { get; set; } = string.Empty;

    [YamlMember(Alias = "body")]
    public string Body { get; set; } = string.Empty;

    [YamlMember(Alias = "labels")]
    public List<string> Labels { get; set; } = new();
}