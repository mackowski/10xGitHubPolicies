namespace _10xGitHubPolicies.App.ViewModels;

public class DashboardViewModel
{
    public double CompliancePercentage { get; set; }
    public List<NonCompliantRepositoryViewModel> NonCompliantRepositories { get; set; } = new();
    public int TotalRepositories { get; set; }
    public int CompliantRepositories { get; set; }
    public int NonCompliantRepositoriesCount => NonCompliantRepositories.Count;
}

public class NonCompliantRepositoryViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public List<string> ViolatedPolicies { get; set; } = new();
}