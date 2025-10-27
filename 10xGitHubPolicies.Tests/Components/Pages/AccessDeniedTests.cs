using _10xGitHubPolicies.App.Pages;
using _10xGitHubPolicies.Tests.Components.TestHelpers;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace _10xGitHubPolicies.Tests.Components.Pages;

[Trait("Category", "Component")]
[Trait("Component", "AccessDenied")]
public class AccessDeniedTests : AppTestContext
{
    [Fact]
    public async Task AccessDenied_DisplaysAuthorizedTeam_WhenConfigured()
    {
        // Arrange
        AuthorizationService.GetAuthorizedTeamAsync()
            .Returns("my-org/security-team");

        // Act
        var cut = RenderComponent<AccessDenied>();

        // Wait for async initialization
        await Task.Delay(100);

        // Assert
        cut.Markup.Should().Contain("my-org/security-team",
            because: "authorized team name should be displayed to user");
        cut.Markup.Should().Contain("You must be a member of the",
            because: "explanatory text should be shown");
    }

    [Fact]
    public async Task AccessDenied_HidesTeamInfo_WhenConfigNotAvailable()
    {
        // Arrange
        AuthorizationService.GetAuthorizedTeamAsync()
            .Returns(Task.FromException<string>(
                new Exception("Configuration not available")
            ));

        // Act
        var cut = RenderComponent<AccessDenied>();

        // Wait for async initialization
        await Task.Delay(100);

        // Assert
        cut.Markup.Should().NotContain("You must be a member of the",
            because: "team-specific message should not be shown when config is unavailable");
        cut.Markup.Should().Contain("Access Denied",
            because: "generic access denied message should still be displayed");
    }

    [Fact]
    public void AccessDenied_TryLoginAgain_NavigatesToLogin()
    {
        // Arrange
        AuthorizationService.GetAuthorizedTeamAsync()
            .Returns("my-org/security-team");

        var navManager = Services.GetRequiredService<Bunit.TestDoubles.FakeNavigationManager>();

        // Act
        var cut = RenderComponent<AccessDenied>();

        var tryAgainButton = cut.Find("fluent-button:contains('Try Login Again')");
        tryAgainButton.Click();

        // Assert
        navManager.Uri.Should().EndWith("/login",
            because: "Try Login Again button should navigate to login page");
    }

    // Note: Logout test requires full authentication services setup which is complex for unit tests.
    // This functionality is better tested via E2E tests with Playwright.
    // [Fact]
    // public async Task AccessDenied_Logout_SignsOutAndRedirects() { ... }
}

