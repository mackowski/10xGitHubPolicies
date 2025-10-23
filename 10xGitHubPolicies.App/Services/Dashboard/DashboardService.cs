using _10xGitHubPolicies.App.Data;
using _10xGitHubPolicies.App.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace _10xGitHubPolicies.App.Services.Dashboard;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _dbContext;

    public DashboardService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DashboardViewModel> GetDashboardViewModelAsync(string? nameFilter = null)
    {
        var viewModel = new DashboardViewModel();

        var latestScan = await _dbContext.Scans
            .Where(s => s.Status == "Completed")
            .OrderByDescending(s => s.CompletedAt)
            .FirstOrDefaultAsync();

        if (latestScan == null)
        {
            return viewModel; // Return empty view model if no scans have been run
        }

        var totalRepositories = await _dbContext.Repositories.CountAsync();
        viewModel.TotalRepositories = totalRepositories;

        var violations = await _dbContext.PolicyViolations
            .Where(v => v.ScanId == latestScan.ScanId)
            .Include(v => v.Repository)
            .Include(v => v.Policy)
            .ToListAsync();

        var nonCompliantRepoIds = violations.Select(v => v.RepositoryId).Distinct().ToHashSet();

        viewModel.CompliantRepositories = totalRepositories - nonCompliantRepoIds.Count;
        viewModel.CompliancePercentage = totalRepositories > 0
            ? (double)viewModel.CompliantRepositories / totalRepositories * 100
            : 100;

        var nonCompliantRepos = await _dbContext.Repositories
            .Where(r => nonCompliantRepoIds.Contains(r.RepositoryId))
            .Where(r => string.IsNullOrEmpty(nameFilter) || r.Name.Contains(nameFilter))
            .ToListAsync();

        foreach (var repo in nonCompliantRepos)
        {
            viewModel.NonCompliantRepositories.Add(new NonCompliantRepositoryViewModel
            {
                Name = repo.Name,
                Url = $"https://github.com/{repo.Name}", // Assuming the name is the full name
                ViolatedPolicies = violations
                    .Where(v => v.RepositoryId == repo.RepositoryId)
                    .Select(v => v.Policy.PolicyKey)
                    .ToList()
            });
        }

        return viewModel;
    }
}
