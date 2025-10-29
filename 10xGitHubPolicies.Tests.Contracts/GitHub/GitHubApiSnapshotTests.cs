using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace _10xGitHubPolicies.Tests.Contracts.GitHub;

[Trait("Category", "Contract")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "ApiSnapshots")]
public class GitHubApiSnapshotTests : GitHubContractTestBase
{
    /// <summary>
    /// TC-CONTRACT-002: Repository Response Structure Stability
    /// Uses Verify.NET to capture and compare repository response structure
    /// Any changes to the response structure will fail this test
    /// </summary>
    [Fact]
    public async Task GetRepositorySettingsAsync_StructureRemainsStable()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        var repositoryId = 123456L; // Fixed for snapshot stability
        var repoName = "test-repo";
        var orgName = Options.OrganizationName;

        var repositoryResponse = new
        {
            id = repositoryId,
            name = repoName,
            full_name = $"{orgName}/{repoName}",
            owner = new
            {
                login = orgName,
                id = 789L,
                type = "Organization"
            },
            @private = false,
            archived = false,
            description = "Test repository",
            html_url = $"https://github.com/{orgName}/{repoName}",
            created_at = "2024-01-01T00:00:00Z",
            updated_at = "2024-01-02T00:00:00Z"
        };

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(repositoryResponse));

        // Act
        var result = await Sut.GetRepositorySettingsAsync(repositoryId);

        // Assert - Snapshot test
        await Verify(result)
            .UseDirectory("Snapshots")
            .UseMethodName("RepositoryResponse")
            .ScrubMembers("CreatedAt", "UpdatedAt", "PushedAt"); // Scrub dynamic date values
    }

    /// <summary>
    /// File Content Response Structure
    /// Verifies that GetFileContentAsync response structure remains stable
    /// </summary>
    [Fact]
    public async Task GetFileContentAsync_StructureRemainsStable()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        var repoName = "test-repo";
        var orgName = Options.OrganizationName;
        var filePath = ".github/config.yaml";
        var fileContent = "test: value";
        var encodedContent = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(fileContent));

        // Mock file content response
        var fileContentResponse = new
        {
            name = "config.yaml",
            path = filePath,
            sha = "abc123",
            size = fileContent.Length,
            type = "file",
            content = encodedContent,
            encoding = "base64"
        };

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repos/{orgName}/{repoName}/contents/{filePath}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(fileContentResponse));

        // Act
        var result = await Sut.GetFileContentAsync(repoName, filePath);

        // Assert - Snapshot test
        await Verify(new { content = result, structure = fileContentResponse })
            .UseDirectory("Snapshots")
            .UseMethodName("FileContentResponse")
            .ScrubMembers("sha"); // Scrub dynamic SHA values
    }

    /// <summary>
    /// Issue Response Structure
    /// Verifies that CreateIssueAsync response structure remains stable
    /// </summary>
    [Fact]
    public async Task CreateIssueAsync_StructureRemainsStable()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        var repositoryId = 123456L;
        var repoName = "test-repo";
        var orgName = Options.OrganizationName;
        var issueNumber = 42;
        var issueId = 789L;

        // Mock issue creation using repositoryId endpoint
        var issueResponse = new
        {
            id = issueId,
            number = issueNumber,
            title = "Test Issue",
            body = "Test body",
            state = "open",
            labels = new[]
            {
                new { id = 1L, name = "policy-violation" },
                new { id = 2L, name = "auto-generated" }
            },
            html_url = $"https://github.com/{orgName}/{repoName}/issues/{issueNumber}",
            created_at = "2024-01-01T00:00:00Z",
            updated_at = "2024-01-02T00:00:00Z"
        };

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/issues")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(201)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(issueResponse));

        // Act
        var result = await Sut.CreateIssueAsync(
            repositoryId,
            "Test Issue",
            "Test body",
            new[] { "policy-violation", "auto-generated" });

        // Assert - Snapshot test
        await Verify(result)
            .UseDirectory("Snapshots")
            .UseMethodName("IssueResponse")
            .ScrubMembers("Id", "Number", "CreatedAt", "UpdatedAt", "HtmlUrl");
    }

    /// <summary>
    /// Workflow Permissions Response Structure
    /// Verifies that GetWorkflowPermissionsAsync response structure remains stable
    /// </summary>
    [Fact]
    public async Task GetWorkflowPermissionsAsync_StructureRemainsStable()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        var repositoryId = 123456L;
        var repoName = "test-repo";
        var orgName = Options.OrganizationName;

        // Mock repository lookup
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    id = repositoryId,
                    name = repoName,
                    full_name = $"{orgName}/{repoName}",
                    owner = new { login = orgName, type = "Organization" }
                }));

        // Mock workflow permissions response
        var workflowPermissionsResponse = new
        {
            default_workflow_permissions = "read",
            can_approve_pull_request_reviews = true
        };

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repos/{orgName}/{repoName}/actions/permissions/workflow")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(workflowPermissionsResponse));

        // Act
        var result = await Sut.GetWorkflowPermissionsAsync(repositoryId);

        // Assert - Snapshot test (capture the result string)
        await Verify(new { permissions = result, apiResponse = workflowPermissionsResponse })
            .UseDirectory("Snapshots")
            .UseMethodName("WorkflowPermissionsResponse");
    }

    /// <summary>
    /// Team Membership Response Structure
    /// Verifies that IsUserMemberOfTeamAsync response structure remains stable
    /// </summary>
    [Fact]
    public async Task IsUserMemberOfTeamAsync_StructureRemainsStable()
    {
        // Arrange
        var orgName = Options.OrganizationName;
        var teamSlug = "test-team";
        var teamId = 123;
        var username = "test-user";
        var userId = 456;

        // Note: This method uses user token, not app authentication
        // So we don't call SetupGitHubAppAuthentication()

        // Mock team lookup
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/orgs/{orgName}/teams/{teamSlug}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    id = teamId,
                    name = "Test Team",
                    slug = teamSlug,
                    description = "Test team description"
                }));

        // Mock current user
        MockServer
            .Given(Request.Create()
                .WithPath("/api/v3/user")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    login = username,
                    id = userId,
                    type = "User"
                }));

        // Mock team membership
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/teams/{teamId}/memberships/{username}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    state = "active",
                    role = "member"
                }));

        // Act
        var userToken = "ghu_testtoken123";
        var result = await Sut.IsUserMemberOfTeamAsync(userToken, orgName, teamSlug);

        // Assert - Snapshot test (capture the boolean result and mock responses)
        var captureData = new
        {
            isMember = result,
            mockResponses = new
            {
                team = new { id = teamId, name = "Test Team", slug = teamSlug },
                user = new { login = username, id = userId },
                membership = new { state = "active", role = "member" }
            }
        };

        await Verify(captureData)
            .UseDirectory("Snapshots")
            .UseMethodName("TeamMembershipResponse")
            .ScrubMembers("id");
    }
}

