namespace _10xGitHubPolicies.App.Data.Entities;

public class Repository
{
    public int RepositoryId { get; set; }
    public long GitHubRepositoryId { get; set; }
    public string Name { get; set; } = null!;
    public string ComplianceStatus { get; set; } = null!;
    public DateTime? LastScannedAt { get; set; }
}