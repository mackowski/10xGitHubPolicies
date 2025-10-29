using _10xGitHubPolicies.Tests.Integration.Builders;
using _10xGitHubPolicies.Tests.Integration.Fixtures;
using FluentAssertions;
using Octokit;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace _10xGitHubPolicies.Tests.Integration.GitHub;

[Trait("Category", "Integration")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "RepositoryOperations")]
public class RepositoryOperationsTests : GitHubServiceIntegrationTestBase
{
    private readonly GitHubApiResponseBuilder _responseBuilder;

    public RepositoryOperationsTests(GitHubApiFixture fixture) : base(fixture)
    {
        _responseBuilder = new GitHubApiResponseBuilder();
    }

    /// <summary>
    /// GetOrganizationRepositoriesAsync - Success
    /// Verifies that GetOrganizationRepositoriesAsync returns list of repositories
    /// </summary>
    [Fact]
    public async Task GetOrganizationRepositoriesAsync_WhenCalled_ReturnsRepositories()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        var repo1 = _responseBuilder.BuildRepositoryResponse(12345, "repo1", false);
        var repo2 = _responseBuilder.BuildRepositoryResponse(12346, "repo2", false);
        var repo3 = _responseBuilder.BuildRepositoryResponse(12347, "repo3", true);

        var repositoriesJson = $"[{repo1},{repo2},{repo3}]";

        MockServer
            .Given(Request.Create()
                .WithPath($"/orgs/{Options.OrganizationName}/repos")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(repositoriesJson)
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.GetOrganizationRepositoriesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result[0].Id.Should().Be(12345);
        result[0].Name.Should().Be("repo1");
        result[0].Archived.Should().BeFalse();
        result[2].Archived.Should().BeTrue();
    }

    /// <summary>
    /// GetRepositorySettingsAsync - Success
    /// Verifies that GetRepositorySettingsAsync returns repository details
    /// </summary>
    [Fact]
    public async Task GetRepositorySettingsAsync_WhenRepositoryExists_ReturnsSettings()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        const string repoName = "test-repo";

        var repoJson = _responseBuilder.BuildRepositoryResponse(repositoryId, repoName, false);

        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{repositoryId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(repoJson)
                .WithHeader("Content-Type", "application/json"));

        // Act
        var result = await Sut.GetRepositorySettingsAsync(repositoryId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(repositoryId);
        result.Name.Should().Be(repoName);
        result.Archived.Should().BeFalse();
    }

    /// <summary>
    /// GetRepositorySettingsAsync - Not Found
    /// Verifies that GetRepositorySettingsAsync throws NotFoundException for invalid repository
    /// </summary>
    [Fact]
    public async Task GetRepositorySettingsAsync_WhenRepositoryNotFound_ThrowsNotFoundException()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long invalidRepositoryId = 99999;

        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{invalidRepositoryId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithBody("{\"message\": \"Not Found\"}")
                .WithHeader("Content-Type", "application/json"));

        // Act
        var act = async () => await Sut.GetRepositorySettingsAsync(invalidRepositoryId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    /// <summary>
    /// TC-ACTION-002: ArchiveRepositoryAsync - Success
    /// Verifies that ArchiveRepositoryAsync sets repository to archived state
    /// </summary>
    [Fact]
    public async Task ArchiveRepositoryAsync_WhenCalled_SetsArchivedToTrue()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        const string repoName = "repo-to-archive";

        // Mock the PATCH request to archive the repository
        var archivedRepoJson = _responseBuilder.BuildRepositoryResponse(repositoryId, repoName, true);

        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{repositoryId}")
                .UsingPatch()
                .WithBody("*archived*true*")) // Simple body match
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(archivedRepoJson)
                .WithHeader("Content-Type", "application/json"));

        // Act
        await Sut.ArchiveRepositoryAsync(repositoryId);

        // Assert - Verify the mock server received the archive request
        var requests = MockServer.LogEntries;
        requests.Should().ContainSingle(r =>
            r.RequestMessage.Path.Contains(repositoryId.ToString()) &&
            r.RequestMessage.Method == "PATCH");
    }

    /// <summary>
    /// ArchiveRepositoryAsync - Invalid Repository
    /// Verifies that ArchiveRepositoryAsync throws exception for invalid repository
    /// </summary>
    [Fact]
    public async Task ArchiveRepositoryAsync_WhenRepositoryNotFound_ThrowsNotFoundException()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long invalidRepositoryId = 99999;

        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{invalidRepositoryId}")
                .UsingPatch())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithBody("{\"message\": \"Not Found\"}")
                .WithHeader("Content-Type", "application/json"));

        // Act
        var act = async () => await Sut.ArchiveRepositoryAsync(invalidRepositoryId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}

