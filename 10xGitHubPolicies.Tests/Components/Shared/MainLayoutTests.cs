using _10xGitHubPolicies.App.Shared;
using _10xGitHubPolicies.Tests.Components.TestHelpers;
using Bunit;
using FluentAssertions;
using Xunit;

namespace _10xGitHubPolicies.Tests.Components.Shared;

[Trait("Category", "Component")]
[Trait("Component", "MainLayout")]
public class MainLayoutTests : AppTestContext
{
    [Fact]
    public void MainLayout_WhenAuthenticated_ShowsUserName()
    {
        // Arrange
        SetupAuthorizedUser("john.doe");

        // Act
        var cut = RenderComponent<MainLayout>();

        // Assert
        cut.Markup.Should().Contain("john.doe",
            because: "authenticated user's name should be displayed in the header");
    }

    [Fact]
    public void MainLayout_WhenNotAuthenticated_ShowsLoginButton()
    {
        // Arrange
        SetupUnauthorizedUser();

        // Act
        var cut = RenderComponent<MainLayout>();

        // Assert
        cut.Markup.Should().Contain("Login",
            because: "login button should be shown for unauthenticated users");
        cut.Markup.Should().NotContain("john.doe",
            because: "no user name should be displayed when not authenticated");
    }
}

