using _10xGitHubPolicies.App.Exceptions;
using _10xGitHubPolicies.App.Pages;
using _10xGitHubPolicies.Tests.Components.TestHelpers;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using NSubstitute;
using Xunit;

namespace _10xGitHubPolicies.Tests.Components.Pages;

[Trait("Category", "Component")]
[Trait("Component", "Onboarding")]
public class OnboardingTests : AppTestContext
{
    [Fact]
    public void Onboarding_Renders_ConfigurationTemplate()
    {
        // Act
        var cut = RenderComponent<Onboarding>();

        // Assert
        cut.Markup.Should().Contain("access_control:",
            because: "configuration template should contain access_control section");
        cut.Markup.Should().Contain("policies:",
            because: "configuration template should contain policies section");
        cut.Markup.Should().Contain("authorized_team:",
            because: "configuration template should show authorized_team field");
        cut.Markup.Should().Contain("has_agents_md",
            because: "configuration template should include sample policy");
        cut.Markup.Should().Contain("has_catalog_info_yaml",
            because: "configuration template should include sample policy");
    }

    [Fact]
    public async Task Onboarding_CheckConfiguration_ValidConfig_ShowsSuccess()
    {
        // Arrange
        var validConfig = TestDataBuilder.CreateAppConfig(
            authorizedTeam: "my-org/security-team"
        );

        ConfigurationService.GetConfigAsync(Arg.Any<bool>())
            .Returns(validConfig);

        // Act
        var cut = RenderComponent<Onboarding>();
        
        var checkButton = cut.Find("fluent-button:contains('Check Configuration')");
        await checkButton.ClickAsync(new MouseEventArgs());
        
        // Wait for async operation
        await Task.Delay(100);

        // Assert
        cut.Markup.Should().Contain("✅",
            because: "success icon should be shown for valid configuration");
        cut.Markup.Should().Contain("Configuration is valid",
            because: "success message should be displayed");
        cut.Markup.Should().Contain("my-org/security-team",
            because: "authorized team should be displayed in success message");
        cut.Markup.Should().Contain("2",
            because: "policy count should be displayed in success message");
    }

    [Fact]
    public async Task Onboarding_CheckConfiguration_InvalidConfig_ShowsError()
    {
        // Arrange
        var invalidConfig = TestDataBuilder.CreateAppConfig(
            authorizedTeam: ""  // Empty authorized team is invalid
        );

        ConfigurationService.GetConfigAsync(Arg.Any<bool>())
            .Returns(invalidConfig);

        // Act
        var cut = RenderComponent<Onboarding>();
        
        var checkButton = cut.Find("fluent-button:contains('Check Configuration')");
        await checkButton.ClickAsync(new MouseEventArgs());
        
        // Wait for async operation
        await Task.Delay(100);

        // Assert
        cut.Markup.Should().Contain("❌",
            because: "error icon should be shown for invalid configuration");
        cut.Markup.Should().Contain("missing the authorized_team",
            because: "error message should explain the validation failure");
    }

    [Fact]
    public async Task Onboarding_CheckConfiguration_MissingConfig_ShowsError()
    {
        // Arrange
        ConfigurationService.GetConfigAsync(Arg.Any<bool>())
            .Returns(Task.FromException<App.Services.Configuration.Models.AppConfig>(
                new ConfigurationNotFoundException(".github/config.yaml not found")
            ));

        // Act
        var cut = RenderComponent<Onboarding>();
        
        var checkButton = cut.Find("fluent-button:contains('Check Configuration')");
        await checkButton.ClickAsync(new MouseEventArgs());
        
        // Wait for async operation
        await Task.Delay(100);

        // Assert
        cut.Markup.Should().Contain("❌",
            because: "error icon should be shown when configuration is missing");
        cut.Markup.Should().Contain("Error loading configuration",
            because: "error message should indicate configuration loading failure");
        cut.Markup.Should().Contain(".github/config.yaml not found",
            because: "specific error message should be displayed");
    }

    [Fact]
    public async Task Onboarding_GoToDashboard_DisabledUntilValid()
    {
        // Arrange
        var validConfig = TestDataBuilder.CreateAppConfig();
        ConfigurationService.GetConfigAsync(Arg.Any<bool>())
            .Returns(validConfig);

        // Act
        var cut = RenderComponent<Onboarding>();

        // Assert - Initially, "Go to Dashboard" button should be disabled
        var dashboardButton = cut.Find("fluent-button:contains('Go to Dashboard')");
        dashboardButton.HasAttribute("disabled").Should().BeTrue(
            because: "Go to Dashboard button should be disabled until configuration is validated");

        // Act - Check configuration to enable the button
        var checkButton = cut.Find("fluent-button:contains('Check Configuration')");
        await checkButton.ClickAsync(new MouseEventArgs());
        await Task.Delay(100);

        // Assert - After successful validation, button should be enabled
        var dashboardButtonAfterCheck = cut.Find("fluent-button:contains('Go to Dashboard')");
        dashboardButtonAfterCheck.HasAttribute("disabled").Should().BeFalse(
            because: "Go to Dashboard button should be enabled after successful configuration validation");
    }
}

