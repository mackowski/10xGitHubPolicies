using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace _10xGitHubPolicies.Tests.Contracts.GitHub;

[Trait("Category", "Contract")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "RepositoryCrudContract")]
public class RepositoryCrudContractTests : GitHubContractTestBase
{
    // Schema validation tests verify that the API responses contain required fields
    // as defined in the GitHub API JSON schemas
    
    /// <summary>
    /// CreateRepositoryAsync - Response Schema
    /// Verifies that CreateRepositoryAsync response matches JSON schema
    /// </summary>
    [Fact]
    public async Task CreateRepositoryAsync_ResponseMatchesSchema()
    {
        // Arrange
        SetupGitHubAppAuthentication();
        
        var repositoryId = Faker.Random.Long(1, 999999);
        var repoName = Faker.Internet.DomainWord();
        var orgName = Options.OrganizationName;
        var defaultBranch = "main";
        
        var repositoryResponse = new
        {
            id = repositoryId,
            name = repoName,
            full_name = $"{orgName}/{repoName}",
            owner = new
            {
                login = orgName,
                id = Faker.Random.Long(1, 999999),
                type = "Organization"
            },
            @private = false,
            archived = false,
            default_branch = defaultBranch,
            description = "Test repository"
        };
        
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/orgs/{orgName}/repos")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(201)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(repositoryResponse));
        
        // Act
        var result = await Sut.CreateRepositoryAsync(repoName);
        
        // Assert - Verify key properties match schema requirements
        result.Should().NotBeNull();
        result.Id.Should().Be(repositoryId, "id is a required integer field");
        result.Name.Should().Be(repoName, "name is a required string field");
        result.FullName.Should().Be($"{orgName}/{repoName}", "full_name is a required string field");
        result.Owner.Should().NotBeNull("owner is a required object field");
        result.Owner.Login.Should().Be(orgName, "owner.login is a required string field");
        result.Owner.Id.Should().BeGreaterThan(0, "owner.id is a required integer field");
        result.Owner.Type.Should().NotBeNull("owner.type is a required field");
        result.Private.Should().BeFalse("private is a required boolean field");
        result.Archived.Should().BeFalse("archived is a required boolean field");
        result.DefaultBranch.Should().Be(defaultBranch, "default_branch is a required string field");
    }
    
    /// <summary>
    /// DeleteRepositoryAsync - No Response Body
    /// Verifies that DeleteRepositoryAsync handles 204 No Content response correctly
    /// </summary>
    [Fact]
    public async Task DeleteRepositoryAsync_NoResponseBody_ValidatesStatusCode()
    {
        // Arrange
        SetupGitHubAppAuthentication();
        
        var repoName = Faker.Internet.DomainWord();
        var orgName = Options.OrganizationName;
        
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repos/{orgName}/{repoName}")
                .UsingDelete())
            .RespondWith(Response.Create()
                .WithStatusCode(204));
        
        // Act & Assert - DELETE typically returns 204 with no body, should not throw
        var act = async () => await Sut.DeleteRepositoryAsync(repoName);
        await act.Should().NotThrowAsync("DELETE should succeed with 204 No Content");
        
        // Verify the request was made
        var requests = MockServer.LogEntries;
        requests.Should().ContainSingle(r => 
            r.RequestMessage.Path.Contains(repoName) && 
            r.RequestMessage.Method == "DELETE");
    }
}

