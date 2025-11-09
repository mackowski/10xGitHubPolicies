using _10xGitHubPolicies.Tests.Integration.Builders;
using _10xGitHubPolicies.Tests.Integration.Fixtures;
using FluentAssertions;
using Octokit;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace _10xGitHubPolicies.Tests.Integration.GitHub;

[Trait("Category", "Integration")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "PullRequestOperations")]
public class PullRequestOperationsTests : GitHubServiceIntegrationTestBase
{
    private readonly GitHubApiResponseBuilder _responseBuilder;

    public PullRequestOperationsTests(GitHubApiFixture fixture) : base(fixture)
    {
        _responseBuilder = new GitHubApiResponseBuilder();
    }

    /// <summary>
    /// TC-PR-001: GetOpenPullRequestsAsync - Returns Open PRs
    /// Verifies that GetOpenPullRequestsAsync returns list of open pull requests
    /// </summary>
    [Fact]
    public async Task GetOpenPullRequestsAsync_WhenPRsExist_ReturnsOpenPRs()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        var pr1Json = BuildPullRequestResponse(1, "PR 1", "abc123");
        var pr2Json = BuildPullRequestResponse(2, "PR 2", "def456");
        var prsJson = $"[{pr1Json},{pr2Json}]";

        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{repositoryId}/pulls")
                .WithParam("state", "open")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(prsJson)
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.GetOpenPullRequestsAsync(repositoryId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.All(pr => pr.State == ItemState.Open).Should().BeTrue();
        result.Should().Contain(pr => pr.Number == 1 && pr.Title == "PR 1");
        result.Should().Contain(pr => pr.Number == 2 && pr.Title == "PR 2");
    }

    /// <summary>
    /// TC-PR-002: GetOpenPullRequestsAsync - Returns Empty List
    /// Verifies that GetOpenPullRequestsAsync returns empty list when no open PRs exist
    /// </summary>
    [Fact]
    public async Task GetOpenPullRequestsAsync_WhenNoPRsExist_ReturnsEmptyList()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;

        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{repositoryId}/pulls")
                .WithParam("state", "open")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("[]")
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.GetOpenPullRequestsAsync(repositoryId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    /// <summary>
    /// TC-PR-003: CreatePullRequestCommentAsync - Creates Comment
    /// Verifies that CreatePullRequestCommentAsync creates a comment on a pull request
    /// </summary>
    [Fact]
    public async Task CreatePullRequestCommentAsync_WhenCalled_CreatesComment()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        const int pullRequestNumber = 1;
        const string comment = "This PR violates policy X";

        var commentJson = BuildIssueCommentResponse(1, comment);

        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{repositoryId}/issues/{pullRequestNumber}/comments")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(201)
                .WithBody(commentJson)
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.CreatePullRequestCommentAsync(repositoryId, pullRequestNumber, comment);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.Body.Should().Be(comment);
    }

    /// <summary>
    /// TC-PR-004: GetPullRequestCommentsAsync - Returns Comments
    /// Verifies that GetPullRequestCommentsAsync returns list of comments on a pull request
    /// </summary>
    [Fact]
    public async Task GetPullRequestCommentsAsync_WhenCommentsExist_ReturnsComments()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        const int pullRequestNumber = 1;
        var comment1Json = BuildIssueCommentResponse(1, "Comment 1");
        var comment2Json = BuildIssueCommentResponse(2, "Comment 2");
        var commentsJson = $"[{comment1Json},{comment2Json}]";

        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{repositoryId}/issues/{pullRequestNumber}/comments")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(commentsJson)
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.GetPullRequestCommentsAsync(repositoryId, pullRequestNumber);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(c => c.Id == 1 && c.Body == "Comment 1");
        result.Should().Contain(c => c.Id == 2 && c.Body == "Comment 2");
    }

    /// <summary>
    /// TC-PR-005: CreateStatusCheckAsync - Creates Status Check
    /// Verifies that CreateStatusCheckAsync creates a status check for a commit
    /// </summary>
    [Fact]
    public async Task CreateStatusCheckAsync_WhenCalled_CreatesStatusCheck()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        const string headSha = "abc123def456";
        const string checkName = "Policy Compliance Check";
        const string status = "completed";
        const string conclusion = "failure";

        var checkRunJson = BuildCheckRunResponse(1, checkName, status, conclusion, headSha);

        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{repositoryId}/check-runs")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(201)
                .WithBody(checkRunJson)
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.CreateStatusCheckAsync(repositoryId, headSha, checkName, status, conclusion);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.Name.Should().Be(checkName);
        result.Status.Value.Should().Be(CheckStatus.Completed);
        result.Conclusion.Should().NotBeNull();
        result.Conclusion!.Value.ToString().Should().Be("failure");
    }

    /// <summary>
    /// TC-PR-006: UpdateStatusCheckAsync - Updates Status Check
    /// Verifies that UpdateStatusCheckAsync updates an existing status check
    /// </summary>
    [Fact]
    public async Task UpdateStatusCheckAsync_WhenCalled_UpdatesStatusCheck()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        const long checkRunId = 1;
        const string status = "completed";
        const string conclusion = "success";

        var checkRunJson = BuildCheckRunResponse(checkRunId, "Policy Compliance Check", status, conclusion, "abc123");

        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{repositoryId}/check-runs/{checkRunId}")
                .UsingPatch())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(checkRunJson)
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.UpdateStatusCheckAsync(repositoryId, checkRunId, status, conclusion);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(checkRunId);
        result.Status.Value.Should().Be(CheckStatus.Completed);
        result.Conclusion.Should().NotBeNull();
        result.Conclusion!.Value.ToString().Should().Be("success");
    }

    /// <summary>
    /// TC-PR-007: GetCheckRunsForRefAsync - Returns Check Runs
    /// Verifies that GetCheckRunsForRefAsync returns list of check runs for a ref
    /// </summary>
    [Fact]
    public async Task GetCheckRunsForRefAsync_WhenCheckRunsExist_ReturnsCheckRuns()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        const string headSha = "abc123def456";
        var checkRun1Json = BuildCheckRunResponse(1, "Check 1", "completed", "success", headSha);
        var checkRun2Json = BuildCheckRunResponse(2, "Check 2", "completed", "failure", headSha);
        var checkRunsJson = $$"""
        {
          "total_count": 2,
          "check_runs": [{{checkRun1Json}},{{checkRun2Json}}]
        }
        """;

        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{repositoryId}/commits/{headSha}/check-runs")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(checkRunsJson)
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.GetCheckRunsForRefAsync(repositoryId, headSha);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(cr => cr.Id == 1 && cr.Name == "Check 1");
        result.Should().Contain(cr => cr.Id == 2 && cr.Name == "Check 2");
    }

    /// <summary>
    /// TC-PR-008: GetCheckRunsForRefAsync - Returns Empty List
    /// Verifies that GetCheckRunsForRefAsync returns empty list when no check runs exist
    /// </summary>
    [Fact]
    public async Task GetCheckRunsForRefAsync_WhenNoCheckRunsExist_ReturnsEmptyList()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        const string headSha = "abc123def456";
        var checkRunsJson = $$"""
        {
          "total_count": 0,
          "check_runs": []
        }
        """;

        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{repositoryId}/commits/{headSha}/check-runs")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(checkRunsJson)
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.GetCheckRunsForRefAsync(repositoryId, headSha);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    // Helper Methods

    private string BuildPullRequestResponse(int number, string title, string headSha)
    {
        return $$"""
        {
          "id": {{Faker.Random.Int(1000000, 9999999)}},
          "number": {{number}},
          "title": "{{title}}",
          "body": "PR body",
          "state": "open",
          "head": {
            "sha": "{{headSha}}",
            "ref": "feature-branch"
          },
          "base": {
            "sha": "base123",
            "ref": "main"
          },
          "html_url": "https://github.com/test-org/test-repo/pull/{{number}}"
        }
        """;
    }

    private string BuildIssueCommentResponse(int id, string body)
    {
        return $$"""
        {
          "id": {{id}},
          "node_id": "IC_{{id}}",
          "url": "https://api.github.com/repos/test-org/test-repo/issues/comments/{{id}}",
          "html_url": "https://github.com/test-org/test-repo/issues/comments/{{id}}",
          "body": "{{body.Replace("\"", "\\\"")}}",
          "created_at": "{{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ssZ}}",
          "updated_at": "{{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ssZ}}",
          "user": {
            "login": "test-user",
            "id": 1,
            "type": "User"
          },
          "author_association": "NONE"
        }
        """;
    }

    private string BuildCheckRunResponse(long id, string name, string status, string conclusion, string headSha)
    {
        return $$"""
        {
          "id": {{id}},
          "name": "{{name}}",
          "status": "{{status}}",
          "conclusion": "{{conclusion}}",
          "head_sha": "{{headSha}}",
          "html_url": "https://github.com/test-org/test-repo/runs/{{id}}",
          "output": {
            "title": "{{name}}",
            "summary": "Check completed"
          }
        }
        """;
    }
}

