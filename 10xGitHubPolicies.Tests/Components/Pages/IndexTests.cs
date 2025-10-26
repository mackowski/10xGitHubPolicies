using _10xGitHubPolicies.App.Exceptions;
using _10xGitHubPolicies.App.Pages;
using _10xGitHubPolicies.App.Services.Configuration.Models;
using _10xGitHubPolicies.Tests.Components.TestHelpers;
using Bunit;
using FluentAssertions;
using Hangfire;
using NSubstitute;
using Xunit;

namespace _10xGitHubPolicies.Tests.Components.Pages;

[Trait("Category", "Component")]
[Trait("Component", "Index")]
public class IndexTests : AppTestContext
{
    [Fact]
    public void Index_WhenLoading_DisplaysProgressIndicator()
    {
        // Arrange - Don't setup DashboardService to return immediately, simulating loading
        var tcs = new TaskCompletionSource<App.ViewModels.DashboardViewModel>();
        DashboardService.GetDashboardViewModelAsync(Arg.Any<string>())
            .Returns(tcs.Task);
        
        ConfigurationService.GetConfigAsync(Arg.Any<bool>())
            .Returns(TestDataBuilder.CreateAppConfig());
        
        AuthorizationService.IsUserAuthorizedAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>())
            .Returns(true);

        // Act
        var cut = RenderComponent<_10xGitHubPolicies.App.Pages.Index>();

        // Assert
        cut.FindAll("fluent-progress").Should().HaveCountGreaterThan(0,
            because: "loading indicator should be displayed while data is loading");
    }

    [Fact]
    public async Task Index_DisplaysCorrectComplianceMetrics()
    {
        // Arrange
        var viewModel = TestDataBuilder.CreateDashboardViewModel(
            nonCompliantCount: 5,
            compliancePercentage: 85.50,
            totalRepositories: 100
        );

        DashboardService.GetDashboardViewModelAsync(Arg.Any<string>())
            .Returns(viewModel);
        
        ConfigurationService.GetConfigAsync(Arg.Any<bool>())
            .Returns(TestDataBuilder.CreateAppConfig());
        
        AuthorizationService.IsUserAuthorizedAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>())
            .Returns(true);

        // Act
        var cut = RenderComponent<_10xGitHubPolicies.App.Pages.Index>();
        
        // Wait for async initialization
        await Task.Delay(100);

        // Assert
        var complianceValue = cut.FindAll(".kpi-value")[0];
        complianceValue.TextContent.Should().Contain("85.50",
            because: "compliance percentage should be displayed with 2 decimal places");
        
        var totalRepos = cut.FindAll(".kpi-value")[1];
        totalRepos.TextContent.Should().Contain("100",
            because: "total repository count should be displayed");
        
        var compliantRepos = cut.FindAll(".kpi-value")[2];
        compliantRepos.TextContent.Should().Contain("95",
            because: "compliant repository count should be displayed");
        
        var nonCompliantRepos = cut.FindAll(".kpi-value")[3];
        nonCompliantRepos.TextContent.Should().Contain("5",
            because: "non-compliant repository count should be displayed");
    }

    [Fact]
    public async Task Index_WhenNoViolations_DisplaysEmptyMessage()
    {
        // Arrange
        var viewModel = TestDataBuilder.CreateDashboardViewModel(
            nonCompliantCount: 0,
            compliancePercentage: 100.0,
            totalRepositories: 100
        );

        DashboardService.GetDashboardViewModelAsync(Arg.Any<string>())
            .Returns(viewModel);
        
        ConfigurationService.GetConfigAsync(Arg.Any<bool>())
            .Returns(TestDataBuilder.CreateAppConfig());
        
        AuthorizationService.IsUserAuthorizedAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>())
            .Returns(true);

        // Act
        var cut = RenderComponent<_10xGitHubPolicies.App.Pages.Index>();
        
        // Wait for async initialization
        await Task.Delay(100);

        // Assert
        cut.Markup.Should().Contain("All repositories are compliant",
            because: "empty state message should be shown when there are no violations");
    }

    [Fact]
    public async Task Index_FilterInput_FiltersRepositoriesInRealTime()
    {
        // Arrange
        var repos = new List<App.ViewModels.NonCompliantRepositoryViewModel>
        {
            TestDataBuilder.CreateNonCompliantRepositoryViewModel(name: "frontend-app"),
            TestDataBuilder.CreateNonCompliantRepositoryViewModel(name: "backend-api"),
            TestDataBuilder.CreateNonCompliantRepositoryViewModel(name: "mobile-app")
        };

        var viewModel = new App.ViewModels.DashboardViewModel
        {
            CompliancePercentage = 70.0,
            TotalRepositories = 10,
            CompliantRepositories = 7,
            NonCompliantRepositories = repos
        };

        DashboardService.GetDashboardViewModelAsync(Arg.Any<string>())
            .Returns(viewModel);
        
        ConfigurationService.GetConfigAsync(Arg.Any<bool>())
            .Returns(TestDataBuilder.CreateAppConfig());
        
        AuthorizationService.IsUserAuthorizedAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>())
            .Returns(true);

        // Act
        var cut = RenderComponent<_10xGitHubPolicies.App.Pages.Index>();
        await Task.Delay(100);

        // Initial state - all repositories shown
        cut.Markup.Should().Contain("frontend-app");
        cut.Markup.Should().Contain("backend-api");
        cut.Markup.Should().Contain("mobile-app");

        // Act - Filter by "frontend"
        var filterInput = cut.Find("fluent-text-field");
        filterInput.Change("frontend");
        
        // Assert - Only frontend-app should be visible
        cut.Markup.Should().Contain("frontend-app",
            because: "filter should show matching repositories");
        cut.Markup.Should().NotContain("backend-api",
            because: "non-matching repositories should be filtered out");
        cut.Markup.Should().NotContain("mobile-app",
            because: "non-matching repositories should be filtered out");
    }

    [Fact]
    public async Task Index_ClearFilter_RestoresFullList()
    {
        // Arrange
        var repos = TestDataBuilder.CreateNonCompliantRepositories(5);
        var viewModel = new App.ViewModels.DashboardViewModel
        {
            CompliancePercentage = 50.0,
            TotalRepositories = 10,
            CompliantRepositories = 5,
            NonCompliantRepositories = repos
        };

        DashboardService.GetDashboardViewModelAsync(Arg.Any<string>())
            .Returns(viewModel);
        
        ConfigurationService.GetConfigAsync(Arg.Any<bool>())
            .Returns(TestDataBuilder.CreateAppConfig());
        
        AuthorizationService.IsUserAuthorizedAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>())
            .Returns(true);

        // Act
        var cut = RenderComponent<_10xGitHubPolicies.App.Pages.Index>();
        await Task.Delay(100);

        var filterInput = cut.Find("fluent-text-field");
        
        // Apply filter
        filterInput.Change("test-repo-1");
        cut.Markup.Should().Contain("test-repo-1");
        cut.Markup.Should().NotContain("test-repo-2");

        // Clear filter
        filterInput.Change("");

        // Assert - All repositories should be visible again
        cut.Markup.Should().Contain("test-repo-0");
        cut.Markup.Should().Contain("test-repo-1");
        cut.Markup.Should().Contain("test-repo-2");
    }
}

