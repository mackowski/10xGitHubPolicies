using _10xGitHubPolicies.App.Pages;
using _10xGitHubPolicies.Tests.Components.TestHelpers;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace _10xGitHubPolicies.Tests.Components.Pages;

[Trait("Category", "Component")]
[Trait("Component", "Login")]
public class LoginTests : AppTestContext
{
    [Fact]
    public void Login_Renders_WithLoginButton()
    {
        // Act
        var cut = RenderComponent<Login>();

        // Assert
        cut.Markup.Should().Contain("Login with GitHub",
            because: "login button should be visible on the page");
        cut.Markup.Should().Contain("10x GitHub Policy Enforcer",
            because: "application title should be displayed");
        cut.Markup.Should().Contain("Secure access to your organization's compliance dashboard",
            because: "description text should be shown");
        cut.Markup.Should().Contain("You must be a member of the authorized team",
            because: "authorization requirement should be communicated");
    }

    [Fact]
    public void Login_WhenErrorInQuery_DisplaysErrorMessage()
    {
        // Arrange
        var navManager = Services.GetRequiredService<FakeNavigationManager>();
        navManager.NavigateTo("/login?error=oauth_state_invalid");

        // Act
        var cut = RenderComponent<Login>();

        // Assert
        cut.Markup.Should().Contain("Authentication error:",
            because: "error message header should be displayed");
        cut.Markup.Should().Contain("oauth_state_invalid",
            because: "specific error message from query parameter should be shown");
    }

    [Fact]
    public void Login_LoginButton_NavigatesToChallenge()
    {
        // Arrange
        var navManager = Services.GetRequiredService<FakeNavigationManager>();

        // Act
        var cut = RenderComponent<Login>();
        
        var loginButton = cut.Find("fluent-button:contains('Login with GitHub')");
        loginButton.Click();

        // Assert
        navManager.Uri.Should().EndWith("/challenge",
            because: "login button should navigate to OAuth challenge endpoint");
    }
}

