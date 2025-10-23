namespace _10xGitHubPolicies.App.Data.Entities;

public class ActionLog
{
    public int ActionLogId { get; set; }

    public int RepositoryId { get; set; }
    public Repository Repository { get; set; } = null!;

    public int PolicyId { get; set; }
    public Policy Policy { get; set; } = null!;

    public string ActionType { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = null!;
    public string? Details { get; set; }
}