using _10xGitHubPolicies.App.ViewModels;

namespace _10xGitHubPolicies.App.Services.Dashboard;

public interface IDashboardService
{
    Task<DashboardViewModel> GetDashboardViewModelAsync(string? nameFilter = null);
}