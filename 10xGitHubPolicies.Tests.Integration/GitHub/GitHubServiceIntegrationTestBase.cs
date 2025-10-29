using _10xGitHubPolicies.App.Options;
using _10xGitHubPolicies.App.Services.GitHub;
using _10xGitHubPolicies.Tests.Integration.Fixtures;
using Bogus;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using WireMock.Server;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace _10xGitHubPolicies.Tests.Integration.GitHub;

public abstract class GitHubServiceIntegrationTestBase : IClassFixture<GitHubApiFixture>, IAsyncLifetime
{
    protected readonly WireMockServer MockServer;
    protected readonly GitHubService Sut;
    protected readonly IMemoryCache Cache;
    protected readonly ILogger<GitHubService> Logger;
    protected readonly Faker Faker;
    protected readonly GitHubAppOptions Options;
    protected readonly IGitHubClientFactory ClientFactory;

    protected GitHubServiceIntegrationTestBase(GitHubApiFixture fixture)
    {
        MockServer = fixture.MockServer;
        Faker = new Faker();
        Logger = Substitute.For<ILogger<GitHubService>>();
        Cache = new MemoryCache(new MemoryCacheOptions());

        Options = CreateTestOptions();
        Options.BaseUrl = MockServer.Url; // Point to WireMock!

        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(Options);
        ClientFactory = new GitHubClientFactory(MockServer.Url, fixture.HttpClientHandler);

        Sut = new GitHubService(optionsWrapper, Logger, Cache, ClientFactory);
    }

    /// <summary>
    /// Logs all WireMock requests for debugging purposes
    /// Call this after a test fails to see what requests were made
    /// </summary>
    protected void LogWireMockRequests()
    {
        Console.WriteLine("\n=== WireMock Request Log ===");
        var logEntries = MockServer.LogEntries;
        foreach (var entry in logEntries)
        {
            Console.WriteLine($"[{entry.RequestMessage.DateTime:HH:mm:ss.fff}] {entry.RequestMessage.Method} {entry.RequestMessage.Path}");
            Console.WriteLine($"  URL: {entry.RequestMessage.AbsoluteUrl}");
            if (entry.RequestMessage.Headers != null)
            {
                foreach (var header in entry.RequestMessage.Headers)
                {
                    Console.WriteLine($"  Header: {header.Key} = {string.Join(", ", header.Value)}");
                }
            }
            if (!string.IsNullOrEmpty(entry.RequestMessage.Body))
            {
                Console.WriteLine($"  Body: {entry.RequestMessage.Body}");
            }
            Console.WriteLine($"  Response: {entry.ResponseMessage.StatusCode}");
            Console.WriteLine("---");
        }
        Console.WriteLine("=== End Request Log ===\n");
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
    /// Sets up WireMock stub for GitHub App authentication (JWT token exchange).
    /// This must be called in the Arrange phase of every test that makes authenticated API calls.
    /// Note: When using custom HttpClientAdapter, Octokit prepends / to all paths
    /// </summary>
    /// <param name="expiresAt">Optional expiration time for the token. Defaults to 1 hour from now.</param>
    protected void SetupGitHubAppAuthentication(DateTimeOffset? expiresAt = null)
    {
        var tokenExpiry = expiresAt ?? DateTimeOffset.UtcNow.AddHours(1);
        var installationToken = Faker.Random.Hexadecimal(40, prefix: "ghs_");

        // Mock the installation token endpoint
        // POST /app/installations/{installationId}/access_tokens
        // Note: / prefix is added by Octokit when using custom HttpClientAdapter
        MockServer
            .Given(Request.Create()
                .WithPath("/app/installations/*/access_tokens")
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

    public virtual Task InitializeAsync()
    {
        MockServer.Reset();
        return Task.CompletedTask;
    }

    public virtual async Task DisposeAsync()
    {
        Cache?.Dispose();
        await Task.CompletedTask;
    }
}

