using _10xGitHubPolicies.App.Exceptions;
using _10xGitHubPolicies.App.Pages;
using _10xGitHubPolicies.Tests.Components.TestHelpers;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace _10xGitHubPolicies.Tests.Components.Integration;

[Trait("Category", "Component")]
[Trait("Component", "AuthorizationFlow")]
public class AuthorizationFlowTests : AppTestContext
{
    [Fact]
    public async Task Index_WhenUnauthorized_RedirectsToAccessDenied()
    {
        // Arrange
        ConfigurationService.GetConfigAsync(Arg.Any<bool>())
            .Returns(TestDataBuilder.CreateAppConfig());

        // User is not authorized
        AuthorizationService.IsUserAuthorizedAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>())
            .Returns(false);

        var navManager = Services.GetRequiredService<FakeNavigationManager>();

        // Act
        var cut = RenderComponent<_10xGitHubPolicies.App.Pages.Index>();
        
        // Wait for async initialization
        await Task.Delay(100);

        // Assert
        navManager.Uri.Should().EndWith("/access-denied",
            because: "unauthorized users should be redirected to access denied page");
    }

    [Fact]
    public async Task Index_WhenConfigMissing_RedirectsToOnboarding()
    {
        // Arrange
        ConfigurationService.GetConfigAsync(Arg.Any<bool>())
            .Returns(Task.FromException<App.Services.Configuration.Models.AppConfig>(
                new ConfigurationNotFoundException("Configuration file not found")
            ));

        var navManager = Services.GetRequiredService<FakeNavigationManager>();

        // Act
        var cut = RenderComponent<_10xGitHubPolicies.App.Pages.Index>();
        
        // Wait for async initialization
        await Task.Delay(100);

        // Assert
        navManager.Uri.Should().EndWith("/onboarding",
            because: "missing configuration should redirect to onboarding page");
    }

    [Fact]
    public async Task Index_WhenConfigInvalid_RedirectsToOnboarding()
    {
        // Arrange
        ConfigurationService.GetConfigAsync(Arg.Any<bool>())
            .Returns(Task.FromException<App.Services.Configuration.Models.AppConfig>(
                new InvalidConfigurationException("Invalid YAML format")
            ));

        var navManager = Services.GetRequiredService<FakeNavigationManager>();

        // Act
        var cut = RenderComponent<_10xGitHubPolicies.App.Pages.Index>();
        
        // Wait for async initialization
        await Task.Delay(100);

        // Assert
        navManager.Uri.Should().EndWith("/onboarding",
            because: "invalid configuration should redirect to onboarding page for setup guidance");
    }
}

