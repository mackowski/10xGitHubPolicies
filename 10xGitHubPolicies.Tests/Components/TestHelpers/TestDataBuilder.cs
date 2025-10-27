using _10xGitHubPolicies.App.Services.Configuration.Models;
using _10xGitHubPolicies.App.ViewModels;
using Bogus;

namespace _10xGitHubPolicies.Tests.Components.TestHelpers;

/// <summary>
/// Factory methods for generating test data for Blazor component tests
/// </summary>
public static class TestDataBuilder
{
    private static readonly Faker _faker = new();

    /// <summary>
    /// Creates a DashboardViewModel with specified non-compliant repository count and compliance percentage
    /// </summary>
    public static DashboardViewModel CreateDashboardViewModel(
        int nonCompliantCount = 5,
        double compliancePercentage = 85.5,
        int totalRepositories = 100)
    {
        var nonCompliantRepos = new List<NonCompliantRepositoryViewModel>();

        for (int i = 0; i < nonCompliantCount; i++)
        {
            nonCompliantRepos.Add(CreateNonCompliantRepositoryViewModel(
                name: $"repo-{i}",
                violations: new List<string> { "has_agents_md", "has_catalog_info_yaml" }
            ));
        }

        return new DashboardViewModel
        {
            CompliancePercentage = compliancePercentage,
            TotalRepositories = totalRepositories,
            CompliantRepositories = totalRepositories - nonCompliantCount,
            NonCompliantRepositories = nonCompliantRepos
        };
    }

    /// <summary>
    /// Creates a single NonCompliantRepositoryViewModel with specified name and violations
    /// </summary>
    public static NonCompliantRepositoryViewModel CreateNonCompliantRepositoryViewModel(
        string? name = null,
        List<string>? violations = null)
    {
        name ??= _faker.Lorem.Word();
        violations ??= new List<string> { "has_agents_md" };

        return new NonCompliantRepositoryViewModel
        {
            Name = name,
            Url = $"https://github.com/test-org/{name}",
            ViolatedPolicies = violations
        };
    }

    /// <summary>
    /// Creates an AppConfig with specified authorized team and policies
    /// </summary>
    public static AppConfig CreateAppConfig(
        string authorizedTeam = "test-org/security-team",
        List<PolicyConfig>? policies = null)
    {
        policies ??= new List<PolicyConfig>
        {
            new()
            {
                Name = "Check for AGENTS.md",
                Type = "has_agents_md",
                Action = "create-issue",
                IssueDetails = new IssueDetails
                {
                    Title = "Missing AGENTS.md",
                    Body = "Please add AGENTS.md file",
                    Labels = new List<string> { "policy-violation" }
                }
            },
            new()
            {
                Name = "Check for catalog-info.yaml",
                Type = "has_catalog_info_yaml",
                Action = "create-issue",
                IssueDetails = new IssueDetails
                {
                    Title = "Missing catalog-info.yaml",
                    Body = "Please add catalog-info.yaml file",
                    Labels = new List<string> { "policy-violation", "backstage" }
                }
            }
        };

        return new AppConfig
        {
            AccessControl = new AccessControlConfig
            {
                AuthorizedTeam = authorizedTeam
            },
            Policies = policies
        };
    }

    /// <summary>
    /// Creates a list of NonCompliantRepositoryViewModel for testing filtering
    /// </summary>
    public static List<NonCompliantRepositoryViewModel> CreateNonCompliantRepositories(int count = 10)
    {
        var repositories = new List<NonCompliantRepositoryViewModel>();

        for (int i = 0; i < count; i++)
        {
            repositories.Add(CreateNonCompliantRepositoryViewModel(
                name: $"test-repo-{i}",
                violations: new List<string> { "has_agents_md" }
            ));
        }

        return repositories;
    }
}

