namespace _10xGitHubPolicies.App.Data.Models;

public class Scan
{
    public int ScanId { get; set; }
    public string Status { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
