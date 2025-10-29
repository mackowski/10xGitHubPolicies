using _10xGitHubPolicies.Tests.Integration.Builders;
using _10xGitHubPolicies.Tests.Integration.Fixtures;
using FluentAssertions;
using NSubstitute;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace _10xGitHubPolicies.Tests.Integration.GitHub;

[Trait("Category", "Integration")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "WorkflowPermissions")]
public class WorkflowPermissionsTests : GitHubServiceIntegrationTestBase
{
    private readonly GitHubApiResponseBuilder _responseBuilder;

    public WorkflowPermissionsTests(GitHubApiFixture fixture) : base(fixture)
    {
        _responseBuilder = new GitHubApiResponseBuilder();
    }

    /// <summary>
    /// TC-GITHUB-004: GetWorkflowPermissionsAsync - Returns "read"
    /// TC-POLICY-003: Verifies workflow permissions policy evaluation for "read" permissions
    /// </summary>
    [Fact]
    public async Task GetWorkflowPermissionsAsync_WhenPermissionsAreRead_ReturnsRead()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        var permissionsJson = _responseBuilder.BuildWorkflowPermissionsResponse("read");

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/actions/permissions/workflow")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(permissionsJson)
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.GetWorkflowPermissionsAsync(repositoryId);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be("read");
    }

    /// <summary>
    /// GetWorkflowPermissionsAsync - Returns "write"
    /// Verifies that GetWorkflowPermissionsAsync returns "write" for write permissions
    /// </summary>
    [Fact]
    public async Task GetWorkflowPermissionsAsync_WhenPermissionsAreWrite_ReturnsWrite()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        var permissionsJson = _responseBuilder.BuildWorkflowPermissionsResponse("write");

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/actions/permissions/workflow")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(permissionsJson)
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.GetWorkflowPermissionsAsync(repositoryId);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be("write");
    }

    /// <summary>
    /// TC-GITHUB-004: GetWorkflowPermissionsAsync - Actions Disabled
    /// Verifies that GetWorkflowPermissionsAsync returns null when GitHub Actions are disabled
    /// </summary>
    [Fact]
    public async Task GetWorkflowPermissionsAsync_WhenActionsDisabled_ReturnsNull()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/actions/permissions/workflow")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithBody("{\"message\": \"Not Found\"}")
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.GetWorkflowPermissionsAsync(repositoryId);

        // Assert
        result.Should().BeNull();

        // Verify warning was logged
        Logger.Received().Log(
            Microsoft.Extensions.Logging.LogLevel.Warning,
            Arg.Any<Microsoft.Extensions.Logging.EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Workflow permissions not found")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    /// <summary>
    /// GetWorkflowPermissionsAsync - Repository Not Found
    /// Verifies that GetWorkflowPermissionsAsync returns null when repository doesn't exist
    /// </summary>
    [Fact]
    public async Task GetWorkflowPermissionsAsync_WhenRepositoryNotFound_ReturnsNull()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long invalidRepositoryId = 99999;

        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{invalidRepositoryId}/actions/permissions/workflow")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithBody("{\"message\": \"Not Found\"}")
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.GetWorkflowPermissionsAsync(invalidRepositoryId);

        // Assert
        result.Should().BeNull();
    }
}

