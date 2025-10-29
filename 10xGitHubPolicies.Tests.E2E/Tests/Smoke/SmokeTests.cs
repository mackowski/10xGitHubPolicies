using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using FluentAssertions;
using Octokit;
using _10xGitHubPolicies.Tests.E2E.Infrastructure;
using _10xGitHubPolicies.Tests.E2E.Fixtures;

namespace _10xGitHubPolicies.Tests.E2E.Smoke;

[TestFixture]
public class SmokeTests : NonBrowserTestBase
{
    [Test]
    [Category("E2E-Smoke")]
    public async Task SmokeTests_EnvironmentValidation_Success()
    {
        Console.WriteLine("Running Environment Smoke Tests");
        
        // Test 1: Configuration Validation
        Console.WriteLine("Testing GitHub App Configuration...");
        var configuration = ServiceProvider.GetRequiredService<IConfiguration>();
        
        var appId = configuration["GitHubApp:AppId"];
        var installationId = configuration["GitHubApp:InstallationId"];
        var privateKey = configuration["GitHubApp:PrivateKey"];
        
        appId.Should().NotBeNullOrEmpty("GitHubApp:AppId should be configured");
        installationId.Should().NotBeNullOrEmpty("GitHubApp:InstallationId should be configured");
        privateKey.Should().NotBeNullOrEmpty("GitHubApp:PrivateKey should be configured");
        
        Console.WriteLine($"✓ GitHub App Configuration: AppId={appId}, InstallationId={installationId}, PrivateKey=SET");
        
        // Test 2: Database Connection
        Console.WriteLine("Testing Database Connection...");
        var canConnect = await DbContext.Database.CanConnectAsync();
        canConnect.Should().BeTrue("Database connection should be successful");
        
        Console.WriteLine("✓ Database connection successful");
        
        // Test 3: GitHub API Connectivity
        Console.WriteLine("Testing GitHub API Connectivity...");
        var repositories = await GitHubService.GetOrganizationRepositoriesAsync();
        repositories.Should().NotBeNull("GitHub service should be able to connect");
        repositories.Should().BeAssignableTo<IReadOnlyList<Octokit.Repository>>();
        
        Console.WriteLine($"✓ GitHub API connectivity successful - Found {repositories.Count} repositories");
        
        // Test 4: Service Resolution
        Console.WriteLine("Testing Service Resolution...");
        var testDataManager = ServiceProvider.GetRequiredService<ITestDataManager>();
        var cleanupService = ServiceProvider.GetRequiredService<ITestCleanupService>();
        
        testDataManager.Should().NotBeNull("TestDataManager should be resolvable");
        cleanupService.Should().NotBeNull("TestCleanupService should be resolvable");
        
        Console.WriteLine("✓ All services resolved successfully");
        
        Console.WriteLine("All smoke tests passed - Environment is ready for E2E testing!");
    }
}

