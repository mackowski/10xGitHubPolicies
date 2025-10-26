using _10xGitHubPolicies.App.Shared;
using _10xGitHubPolicies.Tests.Components.TestHelpers;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace _10xGitHubPolicies.Tests.Components.Shared;

[Trait("Category", "Component")]
[Trait("Component", "RedirectToLogin")]
public class RedirectToLoginTests : AppTestContext
{
    [Fact]
    public void RedirectToLogin_RedirectsToLoginPage()
    {
        // Arrange
        var navManager = Services.GetRequiredService<FakeNavigationManager>();

        // Act
        var cut = RenderComponent<RedirectToLogin>();

        // Assert
        navManager.Uri.Should().EndWith("/login",
            because: "component should redirect to login page on initialization");
    }
}

