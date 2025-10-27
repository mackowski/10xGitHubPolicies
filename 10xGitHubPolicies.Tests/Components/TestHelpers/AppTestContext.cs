using _10xGitHubPolicies.App.Services.Authorization;
using _10xGitHubPolicies.App.Services.Configuration;
using _10xGitHubPolicies.App.Services.Dashboard;
using _10xGitHubPolicies.App.Services.GitHub;
using _10xGitHubPolicies.App.Services.Scanning;
using Bunit;
using Bunit.TestDoubles;
using Hangfire;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using System.Globalization;

namespace _10xGitHubPolicies.Tests.Components.TestHelpers;

/// <summary>
/// Base test context for Blazor component tests with pre-configured service mocks and Fluent UI setup
/// </summary>
public class AppTestContext : TestContext
{
    protected readonly IDashboardService DashboardService;
    protected readonly IScanningService ScanningService;
    protected readonly IAuthorizationService AuthorizationService;
    protected readonly IConfigurationService ConfigurationService;
    protected readonly IGitHubService GitHubService;
    protected readonly IBackgroundJobClient BackgroundJobClient;
    protected readonly ILogger<TestContext> Logger;
    protected readonly TestAuthorizationContext AuthContext;

    public AppTestContext()
    {
        // Set culture to en-US for consistent test behavior across different locales
        var culture = new CultureInfo("en-US");
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        // Create mocks for all services
        DashboardService = Substitute.For<IDashboardService>();
        ScanningService = Substitute.For<IScanningService>();
        AuthorizationService = Substitute.For<IAuthorizationService>();
        ConfigurationService = Substitute.For<IConfigurationService>();
        GitHubService = Substitute.For<IGitHubService>();
        BackgroundJobClient = Substitute.For<IBackgroundJobClient>();
        Logger = Substitute.For<ILogger<TestContext>>();

        // Register services with DI container
        Services.AddSingleton(DashboardService);
        Services.AddSingleton(ScanningService);
        Services.AddSingleton(AuthorizationService);
        Services.AddSingleton(ConfigurationService);
        Services.AddSingleton(GitHubService);
        Services.AddSingleton(BackgroundJobClient);
        Services.AddSingleton(Logger);

        // Add Fluent UI services - required for FluentButton, FluentDataGrid, etc.
        Services.AddSingleton<LibraryConfiguration>(new LibraryConfiguration());
        Services.AddSingleton<IKeyCodeService>(Substitute.For<IKeyCodeService>());

        // Setup test authorization - can be customized in individual tests
        AuthContext = this.AddTestAuthorization();
        AuthContext.SetAuthorized("test-user");

        // Add HttpContextAccessor mock (for logout scenarios)
        Services.AddSingleton(Substitute.For<IHttpContextAccessor>());

        // Configure JSInterop to handle Fluent UI component JavaScript calls
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>
    /// Configure authorization to simulate an authorized user
    /// </summary>
    protected void SetupAuthorizedUser(string username = "test-user")
    {
        AuthContext.SetAuthorized(username);
        AuthContext.SetClaims(
            new System.Security.Claims.Claim("name", username),
            new System.Security.Claims.Claim("sub", "12345")
        );
    }

    /// <summary>
    /// Configure authorization to simulate an unauthorized user
    /// </summary>
    protected void SetupUnauthorizedUser()
    {
        AuthContext.SetNotAuthorized();
    }
}

