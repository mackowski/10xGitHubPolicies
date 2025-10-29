using _10xGitHubPolicies.Tests.Integration.Builders;
using _10xGitHubPolicies.Tests.Integration.Fixtures;
using FluentAssertions;
using Octokit;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace _10xGitHubPolicies.Tests.Integration.GitHub;

[Trait("Category", "Integration")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "RepositoryCrudOperations")]
public class RepositoryCrudOperationsTests : GitHubServiceIntegrationTestBase
{
    private readonly GitHubApiResponseBuilder _responseBuilder;
    
    public RepositoryCrudOperationsTests(GitHubApiFixture fixture) : base(fixture)
    {
        _responseBuilder = new GitHubApiResponseBuilder();
    }
    
    /// <summary>
    /// CreateRepositoryAsync - Success
    /// Verifies that CreateRepositoryAsync creates a new repository with the specified properties
    /// </summary>
    [Fact]
    public async Task CreateRepositoryAsync_WhenCalled_CreatesRepository()
    {
        // Arrange
        SetupGitHubAppAuthentication();
        
        const long repositoryId = 12345;
        const string repoName = "test-repo";
        const string description = "Test repository description";
        const bool isPrivate = false;
        const string defaultBranch = "main";
        
        var repoJson = _responseBuilder.BuildRepositoryCreationResponse(repositoryId, repoName, isPrivate, defaultBranch);
        
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/orgs/{Options.OrganizationName}/repos")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(201)
                .WithBody(repoJson)
                .WithHeader("Content-Type", "application/json"));
        
        // Act
        var result = await Sut.CreateRepositoryAsync(repoName, description, isPrivate);
        
        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(repositoryId);
        result.Name.Should().Be(repoName);
        result.Private.Should().Be(isPrivate);
        result.DefaultBranch.Should().Be(defaultBranch);
        result.Archived.Should().BeFalse();
    }
    
    /// <summary>
    /// CreateRepositoryAsync - Duplicate Name
    /// Verifies that CreateRepositoryAsync throws exception when repository name already exists
    /// </summary>
    [Fact]
    public async Task CreateRepositoryAsync_WhenNameAlreadyExists_ThrowsException()
    {
        // Arrange
        SetupGitHubAppAuthentication();
        
        const string repoName = "existing-repo";
        
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/orgs/{Options.OrganizationName}/repos")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(422)
                .WithBody("{\"message\": \"Repository creation failed.\", \"errors\": [{\"resource\": \"Repository\", \"code\": \"custom\", \"field\": \"name\", \"message\": \"name already exists on this account\"}]}")
                .WithHeader("Content-Type", "application/json"));
        
        // Act
        var act = async () => await Sut.CreateRepositoryAsync(repoName);
        
        // Assert
        await act.Should().ThrowAsync<ApiValidationException>();
    }
    
    /// <summary>
    /// DeleteRepositoryAsync - Success
    /// Verifies that DeleteRepositoryAsync deletes the repository successfully
    /// </summary>
    [Fact]
    public async Task DeleteRepositoryAsync_WhenRepositoryExists_DeletesRepository()
    {
        // Arrange
        SetupGitHubAppAuthentication();
        
        const string repoName = "repo-to-delete";
        
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repos/{Options.OrganizationName}/{repoName}")
                .UsingDelete())
            .RespondWith(Response.Create()
                .WithStatusCode(204));
        
        // Act
        await Sut.DeleteRepositoryAsync(repoName);
        
        // Assert - Verify the mock server received the delete request
        var requests = MockServer.LogEntries;
        requests.Should().ContainSingle(r => 
            r.RequestMessage.Path.Contains(repoName) && 
            r.RequestMessage.Method == "DELETE");
    }
    
    /// <summary>
    /// DeleteRepositoryAsync - Not Found
    /// Verifies that DeleteRepositoryAsync throws NotFoundException when repository doesn't exist
    /// </summary>
    [Fact]
    public async Task DeleteRepositoryAsync_WhenRepositoryNotFound_ThrowsNotFoundException()
    {
        // Arrange
        SetupGitHubAppAuthentication();
        
        const string invalidRepoName = "non-existent-repo";
        
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repos/{Options.OrganizationName}/{invalidRepoName}")
                .UsingDelete())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithBody("{\"message\": \"Not Found\"}")
                .WithHeader("Content-Type", "application/json"));
        
        // Act
        var act = async () => await Sut.DeleteRepositoryAsync(invalidRepoName);
        
        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
    
    /// <summary>
    /// UnarchiveRepositoryAsync - Success
    /// Verifies that UnarchiveRepositoryAsync sets repository archived state to false
    /// </summary>
    [Fact]
    public async Task UnarchiveRepositoryAsync_WhenRepositoryArchived_UnarchivesRepository()
    {
        // Arrange
        SetupGitHubAppAuthentication();
        
        const long repositoryId = 12345;
        const string repoName = "archived-repo";
        
        // Mock the PATCH request to unarchive the repository
        var unarchivedRepoJson = _responseBuilder.BuildRepositoryResponse(repositoryId, repoName, false);
        
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}")
                .UsingPatch()
                .WithBody("*archived*false*"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(unarchivedRepoJson)
                .WithHeader("Content-Type", "application/json"));
        
        // Act
        await Sut.UnarchiveRepositoryAsync(repositoryId);
        
        // Assert - Verify the mock server received the unarchive request
        var requests = MockServer.LogEntries;
        requests.Should().ContainSingle(r => 
            r.RequestMessage.Path.Contains(repositoryId.ToString()) && 
            r.RequestMessage.Method == "PATCH");
    }
    
    /// <summary>
    /// UnarchiveRepositoryAsync - Not Found
    /// Verifies that UnarchiveRepositoryAsync throws NotFoundException when repository doesn't exist
    /// </summary>
    [Fact]
    public async Task UnarchiveRepositoryAsync_WhenRepositoryNotFound_ThrowsNotFoundException()
    {
        // Arrange
        SetupGitHubAppAuthentication();
        
        const long invalidRepositoryId = 99999;
        
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{invalidRepositoryId}")
                .UsingPatch())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithBody("{\"message\": \"Not Found\"}")
                .WithHeader("Content-Type", "application/json"));
        
        // Act
        var act = async () => await Sut.UnarchiveRepositoryAsync(invalidRepositoryId);
        
        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}

