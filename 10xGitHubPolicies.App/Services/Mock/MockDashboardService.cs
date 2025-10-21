using _10xGitHubPolicies.App.ViewModels;

namespace _10xGitHubPolicies.App.Services.Mock;

public class MockDashboardService : IDashboardService
{
    public Task<DashboardViewModel> GetDashboardViewModelAsync(string? nameFilter = null)
    {
        var allRepositories = new List<NonCompliantRepositoryViewModel>
        {
            new()
            {
                Name = "customer-portal",
                Url = "https://github.com/my-org/customer-portal",
                ViolatedPolicies = new List<string> { "Missing AGENTS.md", "Missing catalog-info.yaml" }
            },
            new()
            {
                Name = "api-gateway",
                Url = "https://github.com/my-org/api-gateway",
                ViolatedPolicies = new List<string> { "Incorrect Workflow Permissions" }
            },
            new()
            {
                Name = "data-processing-service",
                Url = "https://github.com/my-org/data-processing-service",
                ViolatedPolicies = new List<string> { "Missing AGENTS.md" }
            },
            new()
            {
                Name = "legacy-monolith",
                Url = "https://github.com/my-org/legacy-monolith",
                ViolatedPolicies = new List<string> { "Missing AGENTS.md", "Missing catalog-info.yaml", "Incorrect Workflow Permissions" }
            }
        };

        var filteredRepositories = string.IsNullOrWhiteSpace(nameFilter)
            ? allRepositories
            : allRepositories.Where(r => r.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        var viewModel = new DashboardViewModel
        {
            TotalRepositories = 25,
            CompliantRepositories = 21,
            CompliancePercentage = (21.0 / 25.0) * 100,
            NonCompliantRepositories = filteredRepositories
        };

        return Task.FromResult(viewModel);
    }
}
