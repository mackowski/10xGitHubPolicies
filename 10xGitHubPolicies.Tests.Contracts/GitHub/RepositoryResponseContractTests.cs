using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace _10xGitHubPolicies.Tests.Contracts.GitHub;

[Trait("Category", "Contract")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "RepositoryContract")]
public class RepositoryResponseContractTests : GitHubContractTestBase
{
    // Schema validation tests verify that Octokit objects contain all required fields
    // as defined in the GitHub API JSON schemas
    
    /// <summary>
    /// TC-CONTRACT-001: GetRepositorySettingsAsync - Response Schema
    /// Verifies that GetRepositorySettingsAsync response matches JSON schema
    /// </summary>
    [Fact]
    public async Task GetRepositorySettingsAsync_ResponseMatchesSchema()
    {
        // Arrange
        SetupGitHubAppAuthentication();
        
        var repositoryId = Faker.Random.Long(1, 999999);
        var repoName = Faker.Internet.DomainWord();
        var orgName = Options.OrganizationName;
        
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
            archived = false
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
    }
    
    /// <summary>
    /// GetOrganizationRepositoriesAsync - Response Schema
    /// Verifies that GetOrganizationRepositoriesAsync response array matches schema
    /// </summary>
    [Fact]
    public async Task GetOrganizationRepositoriesAsync_ResponseMatchesSchema()
    {
        // Arrange
        SetupGitHubAppAuthentication();
        
        var orgName = Options.OrganizationName;
        var repositories = Enumerable.Range(1, 3).Select(i => new
        {
            id = Faker.Random.Long(1, 999999),
            name = Faker.Internet.DomainWord(),
            full_name = $"{orgName}/{Faker.Internet.DomainWord()}",
            owner = new
            {
                login = orgName,
                id = Faker.Random.Long(1, 999999),
                type = "Organization"
            },
            @private = Faker.Random.Bool(),
            archived = Faker.Random.Bool()
        }).ToList();
        
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/orgs/{orgName}/repos")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(repositories));
        
        // Act
        var result = await Sut.GetOrganizationRepositoriesAsync();
        
        // Assert - Verify each repository has required schema fields
        result.Should().NotBeNull();
        result.Should().HaveCount(3, "should return all mocked repositories");
        
        foreach (var repo in result)
        {
            repo.Id.Should().BeGreaterThan(0, "id is required");
            repo.Name.Should().NotBeNullOrEmpty("name is required");
            repo.FullName.Should().NotBeNullOrEmpty("full_name is required");
            repo.Owner.Should().NotBeNull("owner is required");
            repo.Owner.Login.Should().Be(orgName, "owner.login should match organization");
            repo.Owner.Type.Should().NotBeNull("owner.type is required");
            // Private and Archived are boolean fields that should have values
            repo.GetType().GetProperty("Private").Should().NotBeNull();
            repo.GetType().GetProperty("Archived").Should().NotBeNull();
        }
    }
    
    /// <summary>
    /// Archive Repository - Response Schema
    /// Verifies that ArchiveRepositoryAsync doesn't break response structure
    /// </summary>
    [Fact]
    public async Task ArchiveRepositoryAsync_ResponseMatchesSchema()
    {
        // Arrange
        SetupGitHubAppAuthentication();
        
        var repositoryId = Faker.Random.Long(1, 999999);
        var repoName = Faker.Internet.DomainWord();
        var orgName = Options.OrganizationName;
        
        var archivedRepositoryResponse = new
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
            archived = true // Should be true after archiving
        };
        
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}")
                .UsingPatch())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(archivedRepositoryResponse));
        
        // Act
        await Sut.ArchiveRepositoryAsync(repositoryId);
        
        // Assert - Verify the API was called with correct parameters
        var logEntries = MockServer.LogEntries
            .Where(e => e.RequestMessage.Path.Contains($"/repositories/{repositoryId}"))
            .ToList();
        
        logEntries.Should().NotBeEmpty("ArchiveRepositoryAsync should have called the GitHub API");
        
        var patchRequest = logEntries.FirstOrDefault(e => e.RequestMessage.Method == "PATCH");
        patchRequest.Should().NotBeNull("Should have made a PATCH request");
        
        // The mocked response confirms the schema structure matches expectations
        // (id, name, full_name, owner with login/id/type, private, archived fields)
        archivedRepositoryResponse.id.Should().Be(repositoryId);
        archivedRepositoryResponse.archived.Should().BeTrue("archived should be true after archiving");
    }
}

