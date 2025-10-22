namespace _10xGitHubPolicies.App.Data.Models;

public class Policy
{
    public int PolicyId { get; set; }
    public string PolicyKey { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Action { get; set; } = null!;
}