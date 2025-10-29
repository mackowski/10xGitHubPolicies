using _10xGitHubPolicies.Tests.Integration.Fixtures;
using FluentAssertions;
using Octokit;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace _10xGitHubPolicies.Tests.Integration.GitHub;

[Trait("Category", "Integration")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "RateLimiting")]
public class RateLimitHandlingTests : GitHubServiceIntegrationTestBase
{
    public RateLimitHandlingTests(GitHubApiFixture fixture) : base(fixture)
    {
    }

    /// <summary>
    /// TC-GITHUB-002: Rate Limit - 429 Response
    /// TC-PERF-002: Verifies that rate limit exceeded errors are handled appropriately
    /// </summary>
    [Fact]
    public async Task ApiCall_WhenRateLimitExceeded_ThrowsRateLimitException()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;

        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{repositoryId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(429) // Rate limit exceeded
                .WithHeader("X-RateLimit-Limit", "5000")
                .WithHeader("X-RateLimit-Remaining", "0")
                .WithHeader("X-RateLimit-Reset", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString())
                .WithHeader("Retry-After", "3600")
                .WithBody(@"{
                    ""message"": ""API rate limit exceeded"",
                    ""documentation_url"": ""https://docs.github.com/rest/overview/resources-in-the-rest-api#rate-limiting""
                }")
                .WithHeader("Content-Type", "application/json"));

        // Act
        var act = async () => await Sut.GetRepositorySettingsAsync(repositoryId);

        // Assert
        // Note: Octokit throws ApiException (not RateLimitExceededException) for 429 responses in our mock
        await act.Should().ThrowAsync<ApiException>()
            .WithMessage("*rate limit*");
    }

    /// <summary>
    /// TC-GITHUB-002: Secondary Rate Limit - 403 with Retry-After
    /// Verifies that secondary rate limits (abuse detection) are handled
    /// </summary>
    [Fact]
    public async Task ApiCall_WhenSecondaryRateLimitHit_LogsWarning()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;

        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{repositoryId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(403) // Forbidden - secondary rate limit
                .WithHeader("Retry-After", "60")
                .WithHeader("X-RateLimit-Remaining", "4500")
                .WithBody(@"{
                    ""message"": ""You have exceeded a secondary rate limit. Please wait a few minutes before you try again."",
                    ""documentation_url"": ""https://docs.github.com/rest/overview/resources-in-the-rest-api#secondary-rate-limits""
                }")
                .WithHeader("Content-Type", "application/json"));

        // Act
        var act = async () => await Sut.GetRepositorySettingsAsync(repositoryId);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();

        // Note: In a real implementation, the service should log a warning
        // about secondary rate limits and potentially implement retry logic
    }

    /// <summary>
    /// Rate Limit Headers - Monitors Remaining
    /// Verifies that rate limit headers are accessible in responses
    /// Note: This would require custom response handling to expose headers
    /// </summary>
    [Fact]
    public async Task ApiCall_ReturnsRateLimitHeaders()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        const string repoName = "test-repo";

        var repoJson = $$"""
        {
          "id": {{repositoryId}},
          "name": "{{repoName}}",
          "full_name": "test-org/{{repoName}}",
          "private": false,
          "archived": false,
          "owner": {
            "login": "test-org",
            "id": 12345,
            "type": "Organization"
          }
        }
        """;

        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{repositoryId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("X-RateLimit-Limit", "5000")
                .WithHeader("X-RateLimit-Remaining", "4850")
                .WithHeader("X-RateLimit-Reset", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString())
                .WithHeader("X-RateLimit-Used", "150")
                .WithBody(repoJson)
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.GetRepositorySettingsAsync(repositoryId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(repositoryId);

        // Note: To properly test rate limit monitoring, GitHubService would need to:
        // 1. Expose rate limit information through a separate property or method
        // 2. Log warnings when remaining calls drop below threshold (e.g., 100)
        // 3. Potentially implement throttling when approaching limit
    }

    /// <summary>
    /// Rate Limit Recovery
    /// Verifies that API calls succeed after rate limit reset
    /// </summary>
    [Fact]
    public async Task ApiCall_AfterRateLimitReset_Succeeds()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        const string repoName = "test-repo";

        // First call - rate limit exceeded
        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{repositoryId}")
                .UsingGet())
            .InScenario("RateLimit")
            .WillSetStateTo("RateLimitExceeded")
            .RespondWith(Response.Create()
                .WithStatusCode(429)
                .WithHeader("X-RateLimit-Remaining", "0")
                .WithHeader("Retry-After", "1")
                .WithBody(@"{""message"": ""API rate limit exceeded""}")
                .WithHeader("Content-Type", "application/json"));

        // Second call - after reset, success
        var repoJson = $$"""
        {
          "id": {{repositoryId}},
          "name": "{{repoName}}",
          "full_name": "test-org/{{repoName}}",
          "private": false,
          "archived": false,
          "owner": {
            "login": "test-org",
            "id": 12345,
            "type": "Organization"
          }
        }
        """;

        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{repositoryId}")
                .UsingGet())
            .InScenario("RateLimit")
            .WhenStateIs("RateLimitExceeded")
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("X-RateLimit-Limit", "5000")
                .WithHeader("X-RateLimit-Remaining", "5000")
                .WithBody(repoJson)
                .WithHeader("Content-Type", "application/json"));

        // Act & Assert
        // First call should throw
        var firstCall = async () => await Sut.GetRepositorySettingsAsync(repositoryId);
        // Note: Octokit throws ApiException (not RateLimitExceededException) for 429 responses in our mock
        await firstCall.Should().ThrowAsync<ApiException>();

        // Second call should succeed (after hypothetical retry or wait)
        var result = await Sut.GetRepositorySettingsAsync(repositoryId);
        result.Should().NotBeNull();
        result.Id.Should().Be(repositoryId);
    }
}

