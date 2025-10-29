using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright.NUnit;
using Microsoft.Playwright;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using _10xGitHubPolicies.App.Data;
using _10xGitHubPolicies.App.Services.GitHub;
using _10xGitHubPolicies.App.Services.Scanning;
using _10xGitHubPolicies.App.Services.Policies;
using _10xGitHubPolicies.App.Services.Action;
using _10xGitHubPolicies.App.Options;
using _10xGitHubPolicies.Tests.E2E.Fixtures;

namespace _10xGitHubPolicies.Tests.E2E.Infrastructure;

[Parallelizable(ParallelScope.None)]
public abstract class E2ETestBase : PageTest
{
    protected IHost Host { get; private set; } = null!;
    protected IServiceProvider ServiceProvider => Host.Services;
    protected ApplicationDbContext DbContext => ServiceProvider.GetRequiredService<ApplicationDbContext>();
    protected IGitHubService GitHubService => ServiceProvider.GetRequiredService<IGitHubService>();
    protected string BaseUrl => TestConstants.BaseUrl;
    
    [SetUp]
    public async Task SetupAsync()
    {
        // Create a minimal host ONLY for GitHub API operations and database access
        // This is separate from the web application you're testing
        Host = CreateTestHost();
        await Host.StartAsync();
        
        Console.WriteLine($"🚀 Test services started (separate from web app at {BaseUrl})");
        Console.WriteLine($"🌐 Testing against manually running web app at {BaseUrl}");
        
        // Verify the web app is running
        await VerifyWebAppIsRunning();
    }
    
    [TearDown]
    public async Task TearDownAsync()
    {
        if (Host != null)
        {
            await Host.StopAsync();
            Host.Dispose();
        }
    }
    
    private async Task VerifyWebAppIsRunning()
    {
        try
        {
            // Try to navigate to the web app to verify it's running
            await Page.GotoAsync(BaseUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Console.WriteLine($"✅ Web app is running and accessible at {BaseUrl}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Web app is not accessible at {BaseUrl}: {ex.Message}");
            Console.WriteLine($"🚨 Make sure to start the web app with: dotnet run --launch-profile https");
            throw new InvalidOperationException($"Web application is not running at {BaseUrl}. Start it with: dotnet run --launch-profile https", ex);
        }
    }
    
    internal static IHost CreateTestHost()
    {
        var hostBuilder = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddEnvironmentVariables();
                config.AddUserSecrets<E2ETestBase>();
            })
            .ConfigureServices((context, services) =>
            {
                // Add logging
                services.AddLogging(builder => builder.AddConsole());
                
                // Add memory cache
                services.AddMemoryCache();
                
                // Add application services
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlServer(context.Configuration.GetConnectionString("DefaultConnection")));
                
                // Configure GitHub App options
                services.Configure<GitHubAppOptions>(context.Configuration.GetSection(GitHubAppOptions.GitHubApp));
                
                // Register GitHub client factory
                services.AddSingleton<IGitHubClientFactory>(sp =>
                {
                    var options = sp.GetRequiredService<IOptions<GitHubAppOptions>>();
                    return new GitHubClientFactory(options.Value.BaseUrl);
                });
                
                services.AddScoped<IGitHubService, GitHubService>();
                
                // Add test-specific services
                services.AddScoped<ITestDataManager, TestDataManager>();
                services.AddScoped<ITestCleanupService, TestCleanupService>();
            });
            
        return hostBuilder.Build();
    }
}

