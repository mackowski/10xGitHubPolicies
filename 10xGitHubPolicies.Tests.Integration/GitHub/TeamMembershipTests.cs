using _10xGitHubPolicies.Tests.Integration.Builders;
using _10xGitHubPolicies.Tests.Integration.Fixtures;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace _10xGitHubPolicies.Tests.Integration.GitHub;

[Trait("Category", "Integration")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "TeamMembership")]
public class TeamMembershipTests : GitHubServiceIntegrationTestBase
{
    private readonly GitHubApiResponseBuilder _responseBuilder;

    public TeamMembershipTests(GitHubApiFixture fixture) : base(fixture)
    {
        _responseBuilder = new GitHubApiResponseBuilder();
    }

    /// <summary>
    /// TC-AUTH-001: IsUserMemberOfTeamAsync - Active Member
    /// TC-AUTH-003: Verifies team membership checking for active members
    /// </summary>
    [Fact]
    public async Task IsUserMemberOfTeamAsync_WhenUserIsActiveMember_ReturnsTrue()
    {
        // Arrange
        const string userAccessToken = "test-user-token";
        const string org = "test-org";
        const string teamSlug = "developers";
        const int teamId = 12345;

        // Mock get team by name
        var teamJson = _responseBuilder.BuildTeamResponse(teamId, teamSlug);
        MockServer
            .Given(Request.Create()
                .WithPath($"/orgs/{org}/teams/{teamSlug}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(teamJson)
                .WithHeader("Content-Type", "application/json"));

        // Mock get current user
        MockServer
            .Given(Request.Create()
                .WithPath("/user")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"login\": \"test-user\", \"id\": 54321}")
                .WithHeader("Content-Type", "application/json"));

        // Mock team membership check
        var membershipJson = _responseBuilder.BuildTeamMembershipResponse("active");
        MockServer
            .Given(Request.Create()
                .WithPath($"/teams/{teamId}/memberships/test-user")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(membershipJson)
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.IsUserMemberOfTeamAsync(userAccessToken, org, teamSlug);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// TC-AUTH-002: IsUserMemberOfTeamAsync - Not Member
    /// Verifies that IsUserMemberOfTeamAsync returns false for non-members
    /// </summary>
    [Fact]
    public async Task IsUserMemberOfTeamAsync_WhenUserNotMember_ReturnsFalse()
    {
        // Arrange
        const string userAccessToken = "test-user-token";
        const string org = "test-org";
        const string teamSlug = "developers";
        const int teamId = 12345;

        // Mock get team by name
        var teamJson = _responseBuilder.BuildTeamResponse(teamId, teamSlug);
        MockServer
            .Given(Request.Create()
                .WithPath($"/orgs/{org}/teams/{teamSlug}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(teamJson)
                .WithHeader("Content-Type", "application/json"));

        // Mock get current user
        MockServer
            .Given(Request.Create()
                .WithPath("/user")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"login\": \"test-user\", \"id\": 54321}")
                .WithHeader("Content-Type", "application/json"));

        // Mock team membership check - not a member
        MockServer
            .Given(Request.Create()
                .WithPath($"/teams/{teamId}/memberships/test-user")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithBody("{\"message\": \"Not Found\"}")
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.IsUserMemberOfTeamAsync(userAccessToken, org, teamSlug);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// TC-AUTH-002: IsUserMemberOfTeamAsync - Team Not Found
    /// Verifies that IsUserMemberOfTeamAsync returns false when team doesn't exist
    /// </summary>
    [Fact]
    public async Task IsUserMemberOfTeamAsync_WhenTeamNotFound_ReturnsFalse()
    {
        // Arrange
        const string userAccessToken = "test-user-token";
        const string org = "test-org";
        const string invalidTeamSlug = "non-existent-team";

        // Mock get team by name - team not found
        MockServer
            .Given(Request.Create()
                .WithPath($"/orgs/{org}/teams/{invalidTeamSlug}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithBody("{\"message\": \"Not Found\"}")
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.IsUserMemberOfTeamAsync(userAccessToken, org, invalidTeamSlug);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// GetUserOrganizationsAsync - Success
    /// Verifies that GetUserOrganizationsAsync returns list of user's organizations
    /// </summary>
    [Fact]
    public async Task GetUserOrganizationsAsync_WhenCalled_ReturnsOrganizations()
    {
        // Arrange
        const string userAccessToken = "test-user-token";

        var orgsJson = """
        [
          {
            "login": "test-org-1",
            "id": 11111,
            "description": "Test Organization 1"
          },
          {
            "login": "test-org-2",
            "id": 22222,
            "description": "Test Organization 2"
          }
        ]
        """;

        MockServer
            .Given(Request.Create()
                .WithPath("/user/orgs")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(orgsJson)
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.GetUserOrganizationsAsync(userAccessToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].Login.Should().Be("test-org-1");
        result[1].Login.Should().Be("test-org-2");
    }

    /// <summary>
    /// GetOrganizationTeamsAsync - Success
    /// Verifies that GetOrganizationTeamsAsync returns list of organization's teams
    /// </summary>
    [Fact]
    public async Task GetOrganizationTeamsAsync_WhenCalled_ReturnsTeams()
    {
        // Arrange
        const string userAccessToken = "test-user-token";
        const string org = "test-org";

        var team1 = _responseBuilder.BuildTeamResponse(12345, "developers");
        var team2 = _responseBuilder.BuildTeamResponse(12346, "admins");
        var teamsJson = $"[{team1},{team2}]";

        MockServer
            .Given(Request.Create()
                .WithPath($"/orgs/{org}/teams")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(teamsJson)
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.GetOrganizationTeamsAsync(userAccessToken, org);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].Slug.Should().Be("developers");
        result[1].Slug.Should().Be("admins");
    }
}

