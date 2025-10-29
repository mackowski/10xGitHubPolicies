namespace _10xGitHubPolicies.Tests.E2E.Models;

public class NonCompliantRepository
{
    public string Name { get; set; } = string.Empty;
    public List<string> Violations { get; set; } = new();
}

