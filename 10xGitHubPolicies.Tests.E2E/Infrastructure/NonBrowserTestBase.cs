using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using _10xGitHubPolicies.App.Data;
using _10xGitHubPolicies.App.Services.GitHub;
using _10xGitHubPolicies.App.Options;
using _10xGitHubPolicies.Tests.E2E.Fixtures;

namespace _10xGitHubPolicies.Tests.E2E.Infrastructure;

/// <summary>
/// Base class for tests that need test host services (database, GitHub API) but don't require browser/Playwright.
/// </summary>
[Parallelizable(ParallelScope.None)]
public abstract class NonBrowserTestBase
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
        Host = E2ETestBase.CreateTestHost();
        await Host.StartAsync();
        
        Console.WriteLine($"🚀 Test services started (separate from web app at {BaseUrl})");
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
}

