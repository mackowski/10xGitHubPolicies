using _10xGitHubPolicies.App.Options;
using _10xGitHubPolicies.App.Services.GitHub;
using Bogus;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace _10xGitHubPolicies.Tests.Contracts.GitHub;

public abstract class GitHubContractTestBase : IAsyncLifetime
{
    protected readonly WireMockServer MockServer;
    protected readonly GitHubService Sut;
    protected readonly IMemoryCache Cache;
    protected readonly ILogger<GitHubService> Logger;
    protected readonly Faker Faker;
    protected readonly GitHubAppOptions Options;
    protected readonly IGitHubClientFactory ClientFactory;

    protected GitHubContractTestBase()
    {
        Faker = new Faker();
        Logger = Substitute.For<ILogger<GitHubService>>();
        Cache = new MemoryCache(new MemoryCacheOptions());

        MockServer = WireMockServer.Start();

        Options = CreateTestOptions();
        Options.BaseUrl = MockServer.Url; // Point to WireMock!

        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(Options);
        ClientFactory = new GitHubClientFactory(MockServer.Url);

        Sut = new GitHubService(optionsWrapper, Logger, Cache, ClientFactory);
    }

    protected GitHubAppOptions CreateTestOptions()
    {
        return new GitHubAppOptions
        {
            AppId = Faker.Random.Int(100000, 999999),
            InstallationId = Faker.Random.Long(1000000, 9999999),
            OrganizationName = "test-org",
            PrivateKey = GenerateTestPrivateKey()
        };
    }

    protected string GenerateTestPrivateKey()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        return rsa.ExportRSAPrivateKeyPem();
    }

    /// <summary>
    /// Sets up GitHub App authentication by mocking the installation token endpoint
    /// </summary>
    protected void SetupGitHubAppAuthentication(DateTimeOffset? expiresAt = null)
    {
        var tokenExpiry = expiresAt ?? DateTimeOffset.UtcNow.AddHours(1);
        var installationToken = Faker.Random.Hexadecimal(40, prefix: "ghs_");

        // Mock the installation token endpoint
        // POST /api/v3/app/installations/{installationId}/access_tokens
        // Note: /api/v3/ prefix is added by Octokit for Enterprise GitHub
        MockServer
            .Given(Request.Create()
                .WithPath("/api/v3/app/installations/*/access_tokens")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(201)
                .WithHeader("Content-Type", "application/json")
                .WithBody($@"{{
                    ""token"": ""{installationToken}"",
                    ""expires_at"": ""{tokenExpiry:yyyy-MM-ddTHH:mm:ssZ}"",
                    ""permissions"": {{
                        ""contents"": ""read"",
                        ""metadata"": ""read"",
                        ""issues"": ""write""
                    }},
                    ""repository_selection"": ""all""
                }}"));
    }

    /// <summary>
    /// Logs all WireMock requests for debugging
    /// </summary>
    protected void LogWireMockRequests()
    {
        var logEntries = MockServer.LogEntries.ToList();
        Console.WriteLine($"\n=== WireMock Log ({logEntries.Count} requests) ===");
        foreach (var entry in logEntries)
        {
            Console.WriteLine($"{entry.RequestMessage.Method} {entry.RequestMessage.Path}");
            Console.WriteLine($"Response: {entry.ResponseMessage.StatusCode}");
        }
        Console.WriteLine("=== End WireMock Log ===\n");
    }

    public virtual Task InitializeAsync()
    {
        MockServer.Reset();
        return Task.CompletedTask;
    }

    public virtual async Task DisposeAsync()
    {
        MockServer?.Stop();
        MockServer?.Dispose();
        Cache?.Dispose();
        await Task.CompletedTask;
    }
}

