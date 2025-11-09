using System.Text;

using FluentAssertions;
using NSubstitute;
using Xunit;
using Bogus;

using _10xGitHubPolicies.App.Exceptions;
using _10xGitHubPolicies.App.Options;
using _10xGitHubPolicies.App.Services.Configuration;
using _10xGitHubPolicies.App.Services.Configuration.Models;
using _10xGitHubPolicies.App.Services.GitHub;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace _10xGitHubPolicies.Tests.Services.Configuration;

public class ConfigurationServiceTests : IAsyncLifetime
{
    private readonly IGitHubService _githubService;
    private readonly IMemoryCache _cache;
    private readonly IOptions<GitHubAppOptions> _options;
    private readonly ILogger<ConfigurationService> _logger;
    private readonly ConfigurationService _sut; // System Under Test
    private readonly Faker _faker; // For test data generation

    public ConfigurationServiceTests()
    {
        // Arrange - Create mocks
        _githubService = Substitute.For<IGitHubService>();
        _cache = new MemoryCache(new MemoryCacheOptions()); // Use real MemoryCache for testing
        _options = Substitute.For<IOptions<GitHubAppOptions>>();
        _logger = Substitute.For<ILogger<ConfigurationService>>();
        _faker = new Faker();

        // Setup default options
        _options.Value.Returns(new GitHubAppOptions
        {
            AppId = 12345,
            PrivateKey = "test-key",
            InstallationId = 67890,
            OrganizationName = "test-org"
        });

        // Create system under test
        _sut = new ConfigurationService(_githubService, _cache, _options, _logger);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "Configuration")]
    public async Task GetConfigAsync_WhenValidConfigExists_ReturnsAppConfig()
    {
        // Arrange
        var validYaml = """
            access_control:
              authorized_team: "test-org/test-team"
            policies:
              - type: "has_agents_md"
                action: "create-issue"
            """;

        var base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(validYaml));

        _githubService.GetFileContentAsync(".github", "config.yaml")
            .Returns(base64Content);

        // Act
        var result = await _sut.GetConfigAsync();

        // Assert
        result.Should().NotBeNull(because: "service should always return a value");
        result.Should().BeOfType<AppConfig>(because: "return type should be AppConfig");
        result.AccessControl.AuthorizedTeam.Should().Be("test-org/test-team",
            because: "authorized team should be parsed correctly");
        result.Policies.Should().HaveCount(1,
            because: "should have one policy configured");
        result.Policies[0].Type.Should().Be("has_agents_md",
            because: "policy type should be parsed correctly");
        result.Policies[0].Actions.Should().Equal(new List<string> { "create-issue" },
            because: "policy action should be parsed correctly");

        // Verify GitHub service was called
        await _githubService.Received(1).GetFileContentAsync(".github", "config.yaml");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "Configuration")]
    public async Task GetConfigAsync_WhenConfigCached_ReturnsCachedConfig()
    {
        // Arrange
        var validYaml = """
            access_control:
              authorized_team: "cached-org/cached-team"
            policies:
              - type: "has_agents_md"
                action: "create-issue"
            """;

        var base64Content = ToBase64(validYaml);

        _githubService.GetFileContentAsync(".github", "config.yaml")
            .Returns(base64Content);

        // Act - First call to populate cache
        var firstResult = await _sut.GetConfigAsync();

        // Clear call history
        _githubService.ClearReceivedCalls();

        // Act - Second call should hit cache
        var secondResult = await _sut.GetConfigAsync();

        // Assert
        secondResult.Should().BeSameAs(firstResult, because: "cached instance should be returned");
        secondResult.AccessControl.AuthorizedTeam.Should().Be("cached-org/cached-team");

        // Verify GitHub service was NOT called on second request
        await _githubService.DidNotReceive().GetFileContentAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "Configuration")]
    public async Task GetConfigAsync_WhenConfigFileMissing_ThrowsConfigurationNotFoundException()
    {
        // Arrange
        _githubService.GetFileContentAsync(".github", "config.yaml")
            .Returns((string?)null);

        // Act
        var act = async () => await _sut.GetConfigAsync();

        // Assert
        await act.Should()
            .ThrowAsync<ConfigurationNotFoundException>()
            .WithMessage("*config.yaml*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "Configuration")]
    public async Task GetConfigAsync_WhenConfigFileMissingEmptyString_ThrowsConfigurationNotFoundException()
    {
        // Arrange
        _githubService.GetFileContentAsync(".github", "config.yaml")
            .Returns(string.Empty);

        // Act
        var act = async () => await _sut.GetConfigAsync();

        // Assert
        await act.Should()
            .ThrowAsync<ConfigurationNotFoundException>()
            .WithMessage("*config.yaml*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "Configuration")]
    public async Task GetConfigAsync_WhenYamlInvalid_ThrowsInvalidConfigurationException()
    {
        // Arrange
        var invalidYaml = """
            invalid: yaml: syntax:
              - missing quotes
              unclosed: [bracket
            """;

        var base64Content = ToBase64(invalidYaml);

        _githubService.GetFileContentAsync(".github", "config.yaml")
            .Returns(base64Content);

        // Act
        var act = async () => await _sut.GetConfigAsync();

        // Assert
        await act.Should()
            .ThrowAsync<InvalidConfigurationException>()
            .WithMessage("*malformed*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "Configuration")]
    public async Task GetConfigAsync_WhenAuthorizedTeamMissing_ThrowsInvalidConfigurationException()
    {
        // Arrange
        var yamlWithoutTeam = """
            policies:
              - type: "has_agents_md"
                action: "create-issue"
            """;

        var base64Content = ToBase64(yamlWithoutTeam);

        _githubService.GetFileContentAsync(".github", "config.yaml")
            .Returns(base64Content);

        // Act
        var act = async () => await _sut.GetConfigAsync();

        // Assert
        await act.Should()
            .ThrowAsync<InvalidConfigurationException>()
            .WithMessage("*authorized_team*must be set*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "Configuration")]
    public async Task GetConfigAsync_WhenAuthorizedTeamEmpty_ThrowsInvalidConfigurationException()
    {
        // Arrange
        var yamlWithEmptyTeam = """
            access_control:
              authorized_team: ""
            policies:
              - type: "has_agents_md"
                action: "create-issue"
            """;

        var base64Content = ToBase64(yamlWithEmptyTeam);

        _githubService.GetFileContentAsync(".github", "config.yaml")
            .Returns(base64Content);

        // Act
        var act = async () => await _sut.GetConfigAsync();

        // Assert
        await act.Should()
            .ThrowAsync<InvalidConfigurationException>()
            .WithMessage("*authorized_team*must be set*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "Configuration")]
    public async Task GetConfigAsync_WhenAccessControlNull_ThrowsInvalidConfigurationException()
    {
        // Arrange
        var yamlWithoutAccessControl = """
            policies:
              - type: "has_agents_md"
                action: "create-issue"
            """;

        var base64Content = ToBase64(yamlWithoutAccessControl);

        _githubService.GetFileContentAsync(".github", "config.yaml")
            .Returns(base64Content);

        // Act
        var act = async () => await _sut.GetConfigAsync();

        // Assert
        await act.Should()
            .ThrowAsync<InvalidConfigurationException>()
            .WithMessage("*authorized_team*must be set*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "Configuration")]
    public async Task GetConfigAsync_WhenForceRefreshTrue_BypassesCache()
    {
        // Arrange
        var firstYaml = """
            access_control:
              authorized_team: "org/team1"
            policies:
              - type: "has_agents_md"
                action: "create-issue"
            """;

        var secondYaml = """
            access_control:
              authorized_team: "org/team2"
            policies:
              - type: "has_catalog_info_yaml"
                action: "log-only"
            """;

        var firstBase64 = ToBase64(firstYaml);
        var secondBase64 = ToBase64(secondYaml);

        _githubService.GetFileContentAsync(".github", "config.yaml")
            .Returns(firstBase64, secondBase64);

        // Act - First call to populate cache
        var firstResult = await _sut.GetConfigAsync();

        // Act - Second call with forceRefresh
        var secondResult = await _sut.GetConfigAsync(forceRefresh: true);

        // Assert
        firstResult.AccessControl.AuthorizedTeam.Should().Be("org/team1");
        secondResult.AccessControl.AuthorizedTeam.Should().Be("org/team2");
        secondResult.Policies[0].Type.Should().Be("has_catalog_info_yaml");

        // Verify GitHub service was called twice
        await _githubService.Received(2).GetFileContentAsync(".github", "config.yaml");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "Configuration")]
    public async Task GetConfigAsync_WhenCalledConcurrently_FetchesOnlyOnce()
    {
        // Arrange
        var validYaml = """
            access_control:
              authorized_team: "test-org/test-team"
            policies:
              - type: "has_agents_md"
                action: "create-issue"
            """;

        var base64Content = ToBase64(validYaml);
        var callCount = 0;

        _githubService.GetFileContentAsync(".github", "config.yaml")
            .Returns(async callInfo =>
            {
                Interlocked.Increment(ref callCount);
                await Task.Delay(100);
                return base64Content!;
            });

        // Act - 10 concurrent calls
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _sut.GetConfigAsync())
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert
        callCount.Should().Be(1, because: "semaphore should prevent duplicate fetches");
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
        results.Should().AllSatisfy(r => r.AccessControl.AuthorizedTeam.Should().Be("test-org/test-team"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "Configuration")]
    public async Task GetConfigAsync_WhenMultipleThreadsWait_SecondCheckPreventsRefetch()
    {
        // Arrange
        var validYaml = """
            access_control:
              authorized_team: "test-org/test-team"
            policies:
              - type: "has_agents_md"
                action: "create-issue"
            """;

        var base64Content = ToBase64(validYaml);
        var fetchCount = 0;

        _githubService.GetFileContentAsync(".github", "config.yaml")
            .Returns(callInfo =>
            {
                Interlocked.Increment(ref fetchCount);
                // Add small delay to allow concurrent calls to reach semaphore
                Thread.Sleep(50);
                return base64Content;
            });

        // Act - Two concurrent calls that both initially miss cache
        var task1 = _sut.GetConfigAsync();
        var task2 = Task.Run(async () => await _sut.GetConfigAsync());

        var results = await Task.WhenAll(task1, task2);

        // Assert - Double-check locking should prevent duplicate fetch
        fetchCount.Should().BeLessThanOrEqualTo(1,
            because: "double-check locking should prevent unnecessary refetch");
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "Configuration")]
    public async Task GetConfigAsync_WhenCaching_CachesConfigBetweenCalls()
    {
        // Arrange
        var validYaml = """
            access_control:
              authorized_team: "test-org/test-team"
            policies:
              - type: "has_agents_md"
                action: "create-issue"
            """;

        var base64Content = ToBase64(validYaml);

        _githubService.GetFileContentAsync(".github", "config.yaml")
            .Returns(base64Content);

        // Act - Multiple calls within cache window
        var result1 = await _sut.GetConfigAsync();
        var result2 = await _sut.GetConfigAsync();
        var result3 = await _sut.GetConfigAsync();

        // Assert
        result1.Should().BeSameAs(result2, because: "cached instance should be returned");
        result2.Should().BeSameAs(result3, because: "cached instance should be returned");

        // Verify GitHub service was called only once
        await _githubService.Received(1).GetFileContentAsync(".github", "config.yaml");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "Configuration")]
    public async Task GetConfigAsync_WhenMultiplePolicies_ParsesAllCorrectly()
    {
        // Arrange
        var yamlWithMultiplePolicies = """
            access_control:
              authorized_team: "test-org/test-team"
            policies:
              - type: "has_agents_md"
                action: "create-issue"
                issue_details:
                  title: "Missing AGENTS.md"
                  body: "Please add AGENTS.md"
                  labels: ["compliance"]
              - type: "has_catalog_info_yaml"
                action: "log-only"
              - type: "correct_workflow_permissions"
                action: "archive-repo"
            """;

        var base64Content = ToBase64(yamlWithMultiplePolicies);

        _githubService.GetFileContentAsync(".github", "config.yaml")
            .Returns(base64Content);

        // Act
        var result = await _sut.GetConfigAsync();

        // Assert
        result.Policies.Should().HaveCount(3, because: "all three policies should be parsed");

        result.Policies[0].Type.Should().Be("has_agents_md");
        result.Policies[0].Actions.Should().Equal(new List<string> { "create-issue" });
        result.Policies[0].IssueDetails.Should().NotBeNull();
        result.Policies[0].IssueDetails!.Title.Should().Be("Missing AGENTS.md");
        result.Policies[0].IssueDetails!.Labels.Should().Contain("compliance");

        result.Policies[1].Type.Should().Be("has_catalog_info_yaml");
        result.Policies[1].Actions.Should().Equal(new List<string> { "log-only" });

        result.Policies[2].Type.Should().Be("correct_workflow_permissions");
        result.Policies[2].Actions.Should().Equal(new List<string> { "archive-repo" });
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "Configuration")]
    public async Task GetConfigAsync_WhenGitHubServiceFails_PropagatesException()
    {
        // Arrange
        var expectedException = new Exception("GitHub API error");

        _githubService.When(x => x.GetFileContentAsync(Arg.Any<string>(), Arg.Any<string>()))
            .Do(_ => throw expectedException);

        // Act
        var act = async () => await _sut.GetConfigAsync();

        // Assert
        await act.Should()
            .ThrowAsync<Exception>()
            .WithMessage("GitHub API error");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "Configuration")]
    public async Task GetConfigAsync_WhenActionIsString_ParsesAsSingleItemList()
    {
        // Arrange - Test backward compatibility with single string action
        var yamlWithStringAction = """
            access_control:
              authorized_team: "test-org/test-team"
            policies:
              - type: "has_agents_md"
                action: "create-issue"
            """;

        var base64Content = ToBase64(yamlWithStringAction);

        _githubService.GetFileContentAsync(".github", "config.yaml")
            .Returns(base64Content);

        // Act
        var result = await _sut.GetConfigAsync();

        // Assert
        result.Policies.Should().HaveCount(1);
        result.Policies[0].Actions.Should().Equal(new List<string> { "create-issue" },
            because: "single string action should be normalized to list");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "Configuration")]
    public async Task GetConfigAsync_WhenActionIsList_ParsesCorrectly()
    {
        // Arrange - Test new list format
        var yamlWithListAction = """
            access_control:
              authorized_team: "test-org/test-team"
            policies:
              - type: "has_agents_md"
                action: ["create-issue", "archive-repo"]
            """;

        var base64Content = ToBase64(yamlWithListAction);

        _githubService.GetFileContentAsync(".github", "config.yaml")
            .Returns(base64Content);

        // Act
        var result = await _sut.GetConfigAsync();

        // Assert
        result.Policies.Should().HaveCount(1);
        result.Policies[0].Actions.Should().Equal(new List<string> { "create-issue", "archive-repo" },
            because: "list action should be parsed correctly");
    }

    // Helper method for Base64 encoding
    private static string ToBase64(string yaml)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(yaml));

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;
}
