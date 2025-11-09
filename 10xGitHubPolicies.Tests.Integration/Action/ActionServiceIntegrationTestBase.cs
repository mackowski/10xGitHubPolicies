using _10xGitHubPolicies.App.Data;
using _10xGitHubPolicies.App.Data.Entities;
using _10xGitHubPolicies.App.Options;
using _10xGitHubPolicies.App.Services.Action;
using _10xGitHubPolicies.App.Services.Configuration;
using _10xGitHubPolicies.App.Services.GitHub;
using _10xGitHubPolicies.Tests.Integration.Fixtures;
using Bogus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using WireMock.Server;

namespace _10xGitHubPolicies.Tests.Integration.Action;

/// <summary>
/// Base class for ActionService integration tests.
/// Provides:
/// - Ephemeral SQLite in-memory database (shared via CollectionFixture)
/// - WireMock server for GitHub API mocking
/// - Manual database cleanup between tests
/// - Helper methods for creating test data
/// </summary>
public abstract class ActionServiceIntegrationTestBase : IClassFixture<GitHubApiFixture>, IAsyncLifetime
{
    protected readonly DatabaseFixture DatabaseFixture;
    protected readonly GitHubApiFixture GitHubApiFixture;
    protected readonly ApplicationDbContext DbContext;
    protected readonly WireMockServer MockServer;
    protected readonly ActionService Sut;
    protected readonly IGitHubService GitHubService;
    protected readonly IConfigurationService ConfigurationService;
    protected readonly Faker Faker;

    protected ActionServiceIntegrationTestBase(GitHubApiFixture gitHubApiFixture, DatabaseFixture databaseFixture)
    {
        Console.WriteLine($"[ActionServiceIntegrationTestBase] Constructor called at {DateTime.UtcNow:HH:mm:ss.fff}");
        DatabaseFixture = databaseFixture;
        GitHubApiFixture = gitHubApiFixture;
        MockServer = gitHubApiFixture.MockServer;
        Faker = new Faker();
        Console.WriteLine($"[ActionServiceIntegrationTestBase] Fixtures assigned at {DateTime.UtcNow:HH:mm:ss.fff}");

        // Create DbContext with the test database connection
        // Note: For SQLite, we reuse the same connection from the fixture to keep the in-memory DB alive
        Console.WriteLine($"[ActionServiceIntegrationTestBase] Creating DbContext with SQLite connection");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(DatabaseFixture.DbContext.Database.GetDbConnection())
            .Options;
        DbContext = new ApplicationDbContext(options);
        Console.WriteLine($"[ActionServiceIntegrationTestBase] DbContext created at {DateTime.UtcNow:HH:mm:ss.fff}");

        // Setup GitHubService with mocked API
        var logger = Substitute.For<ILogger<GitHubService>>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var gitHubOptions = CreateTestGitHubOptions();
        gitHubOptions.BaseUrl = MockServer.Url;
        var optionsWrapper = Options.Create(gitHubOptions);
        var clientFactory = new GitHubClientFactory(MockServer.Url, gitHubApiFixture.HttpClientHandler);
        GitHubService = new GitHubService(optionsWrapper, logger, cache, clientFactory);

        // Setup ConfigurationService (mocked)
        ConfigurationService = Substitute.For<IConfigurationService>();

        // Setup ActionService
        var actionLogger = Substitute.For<ILogger<ActionService>>();
        Sut = new ActionService(DbContext, GitHubService, ConfigurationService, actionLogger);
    }

    public virtual async Task InitializeAsync()
    {
        Console.WriteLine($"[ActionServiceIntegrationTestBase] InitializeAsync started at {DateTime.UtcNow:HH:mm:ss.fff}");

        try
        {
            Console.WriteLine($"[ActionServiceIntegrationTestBase] Resetting MockServer at {DateTime.UtcNow:HH:mm:ss.fff}");
            MockServer.Reset();
            Console.WriteLine($"[ActionServiceIntegrationTestBase] MockServer reset at {DateTime.UtcNow:HH:mm:ss.fff}");

            // For SQLite, manually clear all data instead of using Respawn
            // Respawn doesn't have native SQLite support, and manual deletion is fast for in-memory DBs
            Console.WriteLine($"[ActionServiceIntegrationTestBase] Clearing database data at {DateTime.UtcNow:HH:mm:ss.fff}");

            // Delete all data from tables (in reverse order of dependencies)
            DbContext.PolicyViolations.RemoveRange(DbContext.PolicyViolations);
            DbContext.ActionsLogs.RemoveRange(DbContext.ActionsLogs);
            DbContext.Repositories.RemoveRange(DbContext.Repositories);
            DbContext.Policies.RemoveRange(DbContext.Policies);
            DbContext.Scans.RemoveRange(DbContext.Scans);

            await DbContext.SaveChangesAsync();
            Console.WriteLine($"[ActionServiceIntegrationTestBase] Database reset completed at {DateTime.UtcNow:HH:mm:ss.fff}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ActionServiceIntegrationTestBase] ERROR in InitializeAsync: {ex.GetType().Name} - {ex.Message}");
            Console.WriteLine($"[ActionServiceIntegrationTestBase] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    public virtual async Task DisposeAsync()
    {
        Console.WriteLine($"[ActionServiceIntegrationTestBase] DisposeAsync started at {DateTime.UtcNow:HH:mm:ss.fff}");
        try
        {
            await DbContext.DisposeAsync();
            Console.WriteLine($"[ActionServiceIntegrationTestBase] DbContext disposed at {DateTime.UtcNow:HH:mm:ss.fff}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ActionServiceIntegrationTestBase] ERROR in DisposeAsync: {ex.GetType().Name} - {ex.Message}");
        }
    }

    /// <summary>
    /// Creates test GitHub App options for integration tests
    /// </summary>
    protected GitHubAppOptions CreateTestGitHubOptions()
    {
        return new GitHubAppOptions
        {
            AppId = 12345,
            PrivateKey = GenerateTestPrivateKey(),
            InstallationId = 67890,
            OrganizationName = "test-org"
        };
    }

    /// <summary>
    /// Generates a test RSA private key in PEM format
    /// </summary>
    protected string GenerateTestPrivateKey()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        return rsa.ExportRSAPrivateKeyPem();
    }

    /// <summary>
    /// Sets up GitHub App authentication for WireMock
    /// </summary>
    protected void SetupGitHubAppAuthentication(DateTimeOffset? expiresAt = null)
    {
        var tokenExpiry = expiresAt ?? DateTimeOffset.UtcNow.AddHours(1);
        var installationToken = Faker.Random.Hexadecimal(40, prefix: "ghs_");

        MockServer
            .Given(WireMock.RequestBuilders.Request.Create()
                .WithPath("/app/installations/*/access_tokens")
                .UsingPost())
            .RespondWith(WireMock.ResponseBuilders.Response.Create()
                .WithStatusCode(201)
                .WithHeader("Content-Type", "application/json")
                .WithBody($@"{{
                    ""token"": ""{installationToken}"",
                    ""expires_at"": ""{tokenExpiry:yyyy-MM-ddTHH:mm:ssZ}"",
                    ""permissions"": {{
                        ""contents"": ""read"",
                        ""metadata"": ""read"",
                        ""issues"": ""write"",
                        ""administration"": ""write""
                    }},
                    ""repository_selection"": ""all""
                }}"));
    }

    /// <summary>
    /// Creates a test repository entity in the database
    /// </summary>
    protected async Task<Repository> CreateRepositoryAsync(string? name = null, long? gitHubRepositoryId = null)
    {
        var repository = new Repository
        {
            GitHubRepositoryId = gitHubRepositoryId ?? Faker.Random.Long(1000, 999999),
            Name = name ?? Faker.Company.CompanyName(),
            ComplianceStatus = "Unknown",
            LastScannedAt = DateTime.UtcNow
        };

        DbContext.Repositories.Add(repository);
        await DbContext.SaveChangesAsync();
        return repository;
    }

    /// <summary>
    /// Creates a test policy entity in the database
    /// </summary>
    protected async Task<Policy> CreatePolicyAsync(string? policyKey = null, string? action = null)
    {
        var policy = new Policy
        {
            PolicyKey = policyKey ?? $"test-policy-{Faker.Random.AlphaNumeric(8)}",
            Description = Faker.Lorem.Sentence(),
            Action = action ?? "log-only"
        };

        DbContext.Policies.Add(policy);
        await DbContext.SaveChangesAsync();
        return policy;
    }

    /// <summary>
    /// Creates a test scan entity in the database
    /// </summary>
    protected async Task<Scan> CreateScanAsync(string? status = null)
    {
        var scan = new Scan
        {
            StartedAt = DateTime.UtcNow,
            Status = status ?? "Completed"
        };

        DbContext.Scans.Add(scan);
        await DbContext.SaveChangesAsync();
        return scan;
    }

    /// <summary>
    /// Creates a test policy violation entity in the database
    /// </summary>
    protected async Task<PolicyViolation> CreateViolationAsync(int scanId, int policyId, int repositoryId)
    {
        var violation = new PolicyViolation
        {
            ScanId = scanId,
            PolicyId = policyId,
            RepositoryId = repositoryId
        };

        DbContext.PolicyViolations.Add(violation);
        await DbContext.SaveChangesAsync();
        return violation;
    }
}

