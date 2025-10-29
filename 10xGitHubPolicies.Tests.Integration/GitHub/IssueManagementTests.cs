using _10xGitHubPolicies.Tests.Integration.Builders;
using _10xGitHubPolicies.Tests.Integration.Fixtures;
using FluentAssertions;
using Octokit;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace _10xGitHubPolicies.Tests.Integration.GitHub;

[Trait("Category", "Integration")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "IssueManagement")]
public class IssueManagementTests : GitHubServiceIntegrationTestBase
{
    private readonly GitHubApiResponseBuilder _responseBuilder;

    public IssueManagementTests(GitHubApiFixture fixture) : base(fixture)
    {
        _responseBuilder = new GitHubApiResponseBuilder();
    }

    /// <summary>
    /// CloseIssueAsync - Success
    /// Verifies that CloseIssueAsync closes an issue successfully
    /// </summary>
    [Fact]
    public async Task CloseIssueAsync_WhenIssueExists_ClosesIssue()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        const int issueNumber = 42;
        const string issueTitle = "Test Issue";

        var closedIssueJson = _responseBuilder.BuildClosedIssueResponse(issueNumber, issueTitle);

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/issues/{issueNumber}")
                .UsingPatch())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(closedIssueJson)
                .WithHeader("Content-Type", "application/json"));

        // Act
        await Sut.CloseIssueAsync(repositoryId, issueNumber);

        // Assert - Verify the mock server received the close request
        var requests = MockServer.LogEntries;
        requests.Should().ContainSingle(r =>
            r.RequestMessage.Path.Contains($"/issues/{issueNumber}") &&
            r.RequestMessage.Method == "PATCH");
    }

    /// <summary>
    /// CloseIssueAsync - Not Found
    /// Verifies that CloseIssueAsync throws NotFoundException when issue doesn't exist
    /// </summary>
    [Fact]
    public async Task CloseIssueAsync_WhenIssueNotFound_ThrowsNotFoundException()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        const int invalidIssueNumber = 99999;

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/issues/{invalidIssueNumber}")
                .UsingPatch())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithBody("{\"message\": \"Not Found\"}")
                .WithHeader("Content-Type", "application/json"));

        // Act
        var act = async () => await Sut.CloseIssueAsync(repositoryId, invalidIssueNumber);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    /// <summary>
    /// CloseIssueAsync - Already Closed
    /// Verifies that CloseIssueAsync doesn't throw when issue is already closed
    /// </summary>
    [Fact]
    public async Task CloseIssueAsync_WhenIssueAlreadyClosed_DoesNotThrow()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        const int issueNumber = 42;
        const string issueTitle = "Already Closed Issue";

        var closedIssueJson = _responseBuilder.BuildClosedIssueResponse(issueNumber, issueTitle);

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/issues/{issueNumber}")
                .UsingPatch())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(closedIssueJson)
                .WithHeader("Content-Type", "application/json"));

        // Act
        var act = async () => await Sut.CloseIssueAsync(repositoryId, issueNumber);

        // Assert
        await act.Should().NotThrowAsync("closing an already-closed issue should not throw");
    }

    /// <summary>
    /// GetRepositoryIssuesAsync - Success
    /// Verifies that GetRepositoryIssuesAsync returns all issues for a repository
    /// </summary>
    [Fact]
    public async Task GetRepositoryIssuesAsync_WhenIssuesExist_ReturnsAllIssues()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const string repoName = "test-repo";

        var issue1 = _responseBuilder.BuildIssueResponse(1, "Issue 1", "bug");
        var issue2 = _responseBuilder.BuildIssueResponse(2, "Issue 2", "enhancement");
        var issuesJson = $"[{issue1},{issue2}]";

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repos/{Options.OrganizationName}/{repoName}/issues")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(issuesJson)
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.GetRepositoryIssuesAsync(repoName);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].Number.Should().Be(1);
        result[0].Title.Should().Be("Issue 1");
        result[1].Number.Should().Be(2);
        result[1].Title.Should().Be("Issue 2");
    }

    /// <summary>
    /// GetRepositoryIssuesAsync - No Issues
    /// Verifies that GetRepositoryIssuesAsync returns empty list when no issues exist
    /// </summary>
    [Fact]
    public async Task GetRepositoryIssuesAsync_WhenNoIssuesExist_ReturnsEmptyList()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const string repoName = "empty-repo";

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repos/{Options.OrganizationName}/{repoName}/issues")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("[]")
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.GetRepositoryIssuesAsync(repoName);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    /// <summary>
    /// GetRepositoryIssuesAsync - Repository Not Found
    /// Verifies that GetRepositoryIssuesAsync throws NotFoundException when repository doesn't exist
    /// </summary>
    [Fact]
    public async Task GetRepositoryIssuesAsync_WhenRepositoryNotFound_ThrowsNotFoundException()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const string invalidRepoName = "non-existent-repo";

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repos/{Options.OrganizationName}/{invalidRepoName}/issues")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithBody("{\"message\": \"Not Found\"}")
                .WithHeader("Content-Type", "application/json"));

        // Act
        var act = async () => await Sut.GetRepositoryIssuesAsync(invalidRepoName);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}

