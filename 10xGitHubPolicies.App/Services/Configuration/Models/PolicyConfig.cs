using YamlDotNet.Serialization;

namespace _10xGitHubPolicies.App.Services.Configuration.Models;

public class PolicyConfig
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of actions to execute for policy violations.
    /// Supports both single string (backward compatible) and list formats in YAML.
    /// The action field will be normalized after deserialization in ConfigurationService.
    /// </summary>
    [YamlIgnore]
    public List<string> Actions { get; set; } = new();

    /// <summary>
    /// Internal property for deserializing action field (supports both string and list).
    /// This is set during deserialization, then normalized to Actions list.
    /// </summary>
    [YamlMember(Alias = "action")]
    public object? ActionRaw { get; set; }

    [YamlMember(Alias = "issue_details")]
    public IssueDetails? IssueDetails { get; set; }

    [YamlMember(Alias = "pr_comment_details")]
    public PrCommentDetails? PrCommentDetails { get; set; }

    [YamlMember(Alias = "block_prs_details")]
    public BlockPrsDetails? BlockPrsDetails { get; set; }

    /// <summary>
    /// Normalizes the Action field after deserialization.
    /// Handles both single string and list formats.
    /// </summary>
    internal void NormalizeActions()
    {
        if (ActionRaw == null)
        {
            Actions = new List<string>();
            return;
        }

        if (ActionRaw is string singleAction)
        {
            // Single string format - backward compatible
            Actions = string.IsNullOrWhiteSpace(singleAction)
                ? new List<string>()
                : new List<string> { singleAction };
        }
        else if (ActionRaw is List<object> actionList)
        {
            // List format - convert object list to string list
            Actions = actionList
                .Where(item => item != null)
                .Select(item => item.ToString() ?? string.Empty)
                .Where(action => !string.IsNullOrWhiteSpace(action))
                .ToList();
        }
        else if (ActionRaw is List<string> stringList)
        {
            // Already a string list
            Actions = stringList.Where(action => !string.IsNullOrWhiteSpace(action)).ToList();
        }
        else
        {
            // Try to convert to string
            var actionString = ActionRaw.ToString();
            Actions = string.IsNullOrWhiteSpace(actionString)
                ? new List<string>()
                : new List<string> { actionString };
        }

        ActionRaw = null; // Clear after normalization
    }
}