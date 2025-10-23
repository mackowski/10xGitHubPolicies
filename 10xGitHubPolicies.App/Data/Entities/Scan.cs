namespace _10xGitHubPolicies.App.Data.Entities;

public class Scan
{
    public int ScanId { get; set; }
    public string Status { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}