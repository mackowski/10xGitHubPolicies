using FluentAssertions;
using Octokit;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace _10xGitHubPolicies.Tests.Contracts.GitHub;

[Trait("Category", "Contract")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "PullRequestContract")]
public class PullRequestResponseContractTests : GitHubContractTestBase
{
    // Schema validation tests verify that Octokit objects contain all required fields
    // as defined in the GitHub API JSON schemas

    /// <summary>
    /// TC-CONTRACT-PR-001: GetOpenPullRequestsAsync - Response Schema
    /// Verifies that GetOpenPullRequestsAsync response matches JSON schema
    /// </summary>
    [Fact]
    public async Task GetOpenPullRequestsAsync_ResponseMatchesSchema()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        var repositoryId = Faker.Random.Long(1, 999999);
        var repoName = Faker.Internet.DomainWord();
        var orgName = Options.OrganizationName;
        var headSha = Faker.Random.AlphaNumeric(40);
        var baseSha = Faker.Random.AlphaNumeric(40);

        var pullRequests = Enumerable.Range(1, 2).Select(i => new
        {
            id = Faker.Random.Long(1, 999999),
            number = i,
            title = $"PR {i}",
            body = $"PR body {i}",
            state = "open",
            head = new
            {
                sha = headSha,
                @ref = "feature-branch",
                repo = new
                {
                    id = repositoryId,
                    name = repoName,
                    full_name = $"{orgName}/{repoName}"
                }
            },
            @base = new
            {
                sha = baseSha,
                @ref = "main",
                repo = new
                {
                    id = repositoryId,
                    name = repoName,
                    full_name = $"{orgName}/{repoName}"
                }
            },
            html_url = $"https://github.com/{orgName}/{repoName}/pull/{i}",
            user = new
            {
                login = "test-user",
                id = Faker.Random.Int(1, 999999),
                type = "User"
            }
        }).ToList();

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/pulls")
                .WithParam("state", "open")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(pullRequests));

        // Act
        var result = await Sut.GetOpenPullRequestsAsync(repositoryId);

        // Assert - Verify each PR has required schema fields
        result.Should().NotBeNull();
        result.Should().HaveCount(2, "should return all mocked pull requests");

        foreach (var pr in result)
        {
            pr.Id.Should().BeGreaterThan(0, "id is required");
            pr.Number.Should().BeGreaterThan(0, "number is required");
            pr.Title.Should().NotBeNullOrEmpty("title is required");
            pr.State.Value.Should().Be(ItemState.Open, "should only return open pull requests");
            pr.HtmlUrl.Should().NotBeNull("html_url is required");
            pr.Head.Should().NotBeNull("head is required");
            pr.Head.Sha.Should().NotBeNullOrEmpty("head.sha is required");
            pr.Base.Should().NotBeNull("base is required");
            pr.Base.Sha.Should().NotBeNullOrEmpty("base.sha is required");
        }
    }

    /// <summary>
    /// TC-CONTRACT-PR-002: CreatePullRequestCommentAsync - Response Schema
    /// Verifies that CreatePullRequestCommentAsync response matches JSON schema
    /// </summary>
    [Fact]
    public async Task CreatePullRequestCommentAsync_ResponseMatchesSchema()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        var repositoryId = Faker.Random.Long(1, 999999);
        var pullRequestNumber = Faker.Random.Int(1, 9999);
        var commentId = Faker.Random.Long(1, 999999);
        var commentBody = "This PR violates policy X";

        var commentResponse = new
        {
            id = commentId,
            node_id = $"IC_{commentId}",
            url = $"https://api.github.com/repos/{Options.OrganizationName}/test-repo/issues/comments/{commentId}",
            html_url = $"https://github.com/{Options.OrganizationName}/test-repo/issues/comments/{commentId}",
            body = commentBody,
            created_at = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            updated_at = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            user = new
            {
                login = "test-user",
                id = Faker.Random.Int(1, 999999),
                type = "User"
            },
            author_association = "NONE"
        };

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/issues/{pullRequestNumber}/comments")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(201)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(commentResponse));

        // Act
        var result = await Sut.CreatePullRequestCommentAsync(repositoryId, pullRequestNumber, commentBody);

        // Assert - Verify key properties match schema requirements
        result.Should().NotBeNull();
        result.Id.Should().Be(commentId, "id is a required integer field");
        result.Body.Should().Be(commentBody, "body is a required string field");
        result.Url.Should().NotBeNull("url is a required URI field");
        result.HtmlUrl.Should().NotBeNull("html_url is a required URI field");
        result.User.Should().NotBeNull("user is a required object field");
        result.User.Login.Should().NotBeNullOrEmpty("user.login is a required string field");
        result.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), "created_at is a required timestamp field");
    }

    /// <summary>
    /// TC-CONTRACT-PR-003: GetPullRequestCommentsAsync - Response Schema
    /// Verifies that GetPullRequestCommentsAsync response array matches schema
    /// </summary>
    [Fact]
    public async Task GetPullRequestCommentsAsync_ResponseMatchesSchema()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        var repositoryId = Faker.Random.Long(1, 999999);
        var pullRequestNumber = Faker.Random.Int(1, 9999);

        var comments = Enumerable.Range(1, 2).Select(i => new
        {
            id = Faker.Random.Long(1, 999999),
            node_id = $"IC_{i}",
            url = $"https://api.github.com/repos/{Options.OrganizationName}/test-repo/issues/comments/{i}",
            html_url = $"https://github.com/{Options.OrganizationName}/test-repo/issues/comments/{i}",
            body = $"Comment {i}",
            created_at = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            updated_at = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            user = new
            {
                login = "test-user",
                id = Faker.Random.Int(1, 999999),
                type = "User"
            },
            author_association = "NONE"
        }).ToList();

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/issues/{pullRequestNumber}/comments")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(comments));

        // Act
        var result = await Sut.GetPullRequestCommentsAsync(repositoryId, pullRequestNumber);

        // Assert - Verify each comment has required schema fields
        result.Should().NotBeNull();
        result.Should().HaveCount(2, "should return all mocked comments");

        foreach (var comment in result)
        {
            comment.Id.Should().BeGreaterThan(0, "id is required");
            comment.Body.Should().NotBeNullOrEmpty("body is required");
            comment.Url.Should().NotBeNull("url is required");
            comment.HtmlUrl.Should().NotBeNull("html_url is required");
            comment.User.Should().NotBeNull("user is required");
            comment.User.Login.Should().NotBeNullOrEmpty("user.login is required");
        }
    }

    /// <summary>
    /// TC-CONTRACT-PR-004: CreateStatusCheckAsync - Response Schema
    /// Verifies that CreateStatusCheckAsync response matches JSON schema
    /// </summary>
    [Fact]
    public async Task CreateStatusCheckAsync_ResponseMatchesSchema()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        var repositoryId = Faker.Random.Long(1, 999999);
        var checkRunId = Faker.Random.Long(1, 999999);
        var headSha = Faker.Random.AlphaNumeric(40);
        var checkName = "Policy Compliance Check";

        var checkRunResponse = new
        {
            id = checkRunId,
            name = checkName,
            status = "completed",
            conclusion = "failure",
            head_sha = headSha,
            html_url = $"https://github.com/{Options.OrganizationName}/test-repo/runs/{checkRunId}",
            output = new
            {
                title = checkName,
                summary = "Check completed"
            }
        };

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/check-runs")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(201)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(checkRunResponse));

        // Act
        var result = await Sut.CreateStatusCheckAsync(repositoryId, headSha, checkName, "completed", "failure");

        // Assert - Verify key properties match schema requirements
        result.Should().NotBeNull();
        result.Id.Should().Be(checkRunId, "id is a required integer field");
        result.Name.Should().Be(checkName, "name is a required string field");
        result.Status.Value.Should().Be(CheckStatus.Completed, "status must be 'completed' or 'in_progress'");
        result.Conclusion.Should().NotBeNull("conclusion is required for completed checks");
        result.Conclusion!.Value.ToString().Should().Be("failure", "conclusion must be 'success', 'failure', 'neutral', 'cancelled', 'skipped', or 'timed_out'");
        result.HeadSha.Should().Be(headSha, "head_sha is a required string field");
        result.HtmlUrl.Should().NotBeNull("html_url is a required URI field");
    }

    /// <summary>
    /// TC-CONTRACT-PR-005: UpdateStatusCheckAsync - Response Schema
    /// Verifies that UpdateStatusCheckAsync response matches JSON schema
    /// </summary>
    [Fact]
    public async Task UpdateStatusCheckAsync_ResponseMatchesSchema()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        var repositoryId = Faker.Random.Long(1, 999999);
        var checkRunId = Faker.Random.Long(1, 999999);
        var headSha = Faker.Random.AlphaNumeric(40);
        var checkName = "Policy Compliance Check";

        var checkRunResponse = new
        {
            id = checkRunId,
            name = checkName,
            status = "completed",
            conclusion = "success",
            head_sha = headSha,
            html_url = $"https://github.com/{Options.OrganizationName}/test-repo/runs/{checkRunId}",
            output = new
            {
                title = checkName,
                summary = "Check updated"
            }
        };

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/check-runs/{checkRunId}")
                .UsingPatch())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(checkRunResponse));

        // Act
        var result = await Sut.UpdateStatusCheckAsync(repositoryId, checkRunId, "completed", "success");

        // Assert - Verify key properties match schema requirements
        result.Should().NotBeNull();
        result.Id.Should().Be(checkRunId, "id is a required integer field");
        result.Name.Should().Be(checkName, "name is a required string field");
        result.Status.Value.Should().Be(CheckStatus.Completed, "status must be 'completed' or 'in_progress'");
        result.Conclusion.Should().NotBeNull("conclusion is required for completed checks");
        result.Conclusion!.Value.ToString().Should().Be("success", "conclusion must be 'success', 'failure', 'neutral', 'cancelled', 'skipped', or 'timed_out'");
        result.HeadSha.Should().Be(headSha, "head_sha is a required string field");
        result.HtmlUrl.Should().NotBeNull("html_url is a required URI field");
    }

    /// <summary>
    /// TC-CONTRACT-PR-006: GetCheckRunsForRefAsync - Response Schema
    /// Verifies that GetCheckRunsForRefAsync response matches JSON schema
    /// </summary>
    [Fact]
    public async Task GetCheckRunsForRefAsync_ResponseMatchesSchema()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        var repositoryId = Faker.Random.Long(1, 999999);
        var headSha = Faker.Random.AlphaNumeric(40);

        var checkRuns = Enumerable.Range(1, 2).Select(i => new
        {
            id = Faker.Random.Long(1, 999999),
            name = $"Check {i}",
            status = "completed",
            conclusion = i == 1 ? "success" : "failure",
            head_sha = headSha,
            html_url = $"https://github.com/{Options.OrganizationName}/test-repo/runs/{i}",
            output = new
            {
                title = $"Check {i}",
                summary = "Check completed"
            }
        }).ToList();

        var checkRunsResponse = new
        {
            total_count = checkRuns.Count,
            check_runs = checkRuns
        };

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/commits/{headSha}/check-runs")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(checkRunsResponse));

        // Act
        var result = await Sut.GetCheckRunsForRefAsync(repositoryId, headSha);

        // Assert - Verify each check run has required schema fields
        result.Should().NotBeNull();
        result.Should().HaveCount(2, "should return all mocked check runs");

        foreach (var checkRun in result)
        {
            checkRun.Id.Should().BeGreaterThan(0, "id is required");
            checkRun.Name.Should().NotBeNullOrEmpty("name is required");
            checkRun.Status.Value.Should().Be(CheckStatus.Completed, "status must be 'completed' or 'in_progress'");
            checkRun.Conclusion.Should().NotBeNull("conclusion is required for completed checks");
            checkRun.HeadSha.Should().Be(headSha, "head_sha is required");
            checkRun.HtmlUrl.Should().NotBeNull("html_url is required");
        }
    }
}

