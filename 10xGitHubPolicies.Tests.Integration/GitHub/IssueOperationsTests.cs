using _10xGitHubPolicies.Tests.Integration.Builders;
using _10xGitHubPolicies.Tests.Integration.Fixtures;
using FluentAssertions;
using Octokit;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace _10xGitHubPolicies.Tests.Integration.GitHub;

[Trait("Category", "Integration")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "IssueOperations")]
public class IssueOperationsTests : GitHubServiceIntegrationTestBase
{
    private readonly GitHubApiResponseBuilder _responseBuilder;

    public IssueOperationsTests(GitHubApiFixture fixture) : base(fixture)
    {
        _responseBuilder = new GitHubApiResponseBuilder();
    }

    /// <summary>
    /// TC-ACTION-001: CreateIssueAsync - Success
    /// Verifies that CreateIssueAsync creates an issue with title, body, and labels
    /// </summary>
    [Fact]
    public async Task CreateIssueAsync_WhenCalled_CreatesIssueWithLabels()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        const string issueTitle = "Policy Violation: Missing AGENTS.md";
        const string issueBody = "This repository is missing the required AGENTS.md file.";
        const string label = "policy-violation";

        var issueJson = _responseBuilder.BuildIssueResponse(1, issueTitle, label);

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/issues")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(201)
                .WithBody(issueJson)
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.CreateIssueAsync(repositoryId, issueTitle, issueBody, new[] { label });

        // Assert
        result.Should().NotBeNull();
        result.Number.Should().Be(1);
        result.Title.Should().Be(issueTitle);
        // Note: State is StringEnum<ItemState>, compare the Value property
        result.State.Value.Should().Be(ItemState.Open);
        result.Labels.Should().ContainSingle(l => l.Name == label);
    }

    /// <summary>
    /// CreateIssueAsync - Multiple Labels
    /// Verifies that CreateIssueAsync handles multiple labels correctly
    /// </summary>
    [Fact]
    public async Task CreateIssueAsync_WithMultipleLabels_CreatesIssueCorrectly()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        const string issueTitle = "Multiple Policy Violations";
        const string issueBody = "This repository has multiple violations.";
        var labels = new[] { "policy-violation", "high-priority", "documentation" };

        // Build response with multiple labels
        var issueJson = $$"""
        {
          "id": {{Faker.Random.Int(1000000, 9999999)}},
          "number": 2,
          "title": "{{issueTitle}}",
          "body": "{{issueBody}}",
          "state": "open",
          "labels": [
            {"id": 1, "name": "policy-violation"},
            {"id": 2, "name": "high-priority"},
            {"id": 3, "name": "documentation"}
          ],
          "html_url": "https://github.com/test-org/test-repo/issues/2"
        }
        """;

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/issues")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(201)
                .WithBody(issueJson)
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.CreateIssueAsync(repositoryId, issueTitle, issueBody, labels);

        // Assert
        result.Should().NotBeNull();
        result.Number.Should().Be(2);
        result.Labels.Should().HaveCount(3);
        result.Labels.Select(l => l.Name).Should().Contain(labels);
    }

    /// <summary>
    /// TC-ACTION-004: GetOpenIssuesAsync - Returns Open Issues
    /// Verifies that GetOpenIssuesAsync returns filtered list of open issues with specific label
    /// </summary>
    [Fact]
    public async Task GetOpenIssuesAsync_WhenIssuesExist_ReturnsFilteredList()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        const string label = "policy-violation";

        var issue1 = _responseBuilder.BuildIssueResponse(1, "Issue 1", label);
        var issue2 = _responseBuilder.BuildIssueResponse(2, "Issue 2", label);
        var issuesJson = $"[{issue1},{issue2}]";

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/issues")
                .WithParam("state", "open")
                .WithParam("labels", label)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(issuesJson)
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.GetOpenIssuesAsync(repositoryId, label);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.All(i => i.State == ItemState.Open).Should().BeTrue();
        result.All(i => i.Labels.Any(l => l.Name == label)).Should().BeTrue();
    }

    /// <summary>
    /// GetOpenIssuesAsync - No Issues
    /// Verifies that GetOpenIssuesAsync returns empty list when no issues exist
    /// </summary>
    [Fact]
    public async Task GetOpenIssuesAsync_WhenNoIssuesExist_ReturnsEmptyList()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        const string label = "policy-violation";

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/issues")
                .WithParam("state", "open")
                .WithParam("labels", label)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("[]")
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.GetOpenIssuesAsync(repositoryId, label);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    /// <summary>
    /// TC-ACTION-004: GetOpenIssuesAsync - Repository Not Found
    /// Verifies that GetOpenIssuesAsync returns empty list when repository doesn't exist
    /// </summary>
    [Fact]
    public async Task GetOpenIssuesAsync_WhenRepositoryNotFound_ReturnsEmptyList()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long invalidRepositoryId = 99999;
        const string label = "policy-violation";

        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{invalidRepositoryId}/issues")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithBody("{\"message\": \"Not Found\"}")
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.GetOpenIssuesAsync(invalidRepositoryId, label);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }
}

