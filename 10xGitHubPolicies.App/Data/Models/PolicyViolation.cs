using System.ComponentModel.DataAnnotations;

namespace _10xGitHubPolicies.App.Data.Models;

public class PolicyViolation
{
    [Key]
    public int ViolationId { get; set; }

    public int ScanId { get; set; }
    public Scan Scan { get; set; } = null!;

    public int RepositoryId { get; set; }
    public Repository Repository { get; set; } = null!;

    public int PolicyId { get; set; }
    public Policy Policy { get; set; } = null!;
}