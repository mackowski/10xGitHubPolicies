using _10xGitHubPolicies.Tests.Integration.Builders;
using _10xGitHubPolicies.Tests.Integration.Fixtures;
using FluentAssertions;
using Octokit;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace _10xGitHubPolicies.Tests.Integration.GitHub;

[Trait("Category", "Integration")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "FileOperations")]
public class FileOperationsTests : GitHubServiceIntegrationTestBase
{
    private readonly GitHubApiResponseBuilder _responseBuilder;
    
    public FileOperationsTests(GitHubApiFixture fixture) : base(fixture)
    {
        _responseBuilder = new GitHubApiResponseBuilder();
    }
    
    /// <summary>
    /// TC-GITHUB-003: FileExistsAsync - File Exists
    /// Verifies that FileExistsAsync returns true when a file exists in the repository
    /// </summary>
    [Fact]
    public async Task FileExistsAsync_WhenFileExists_ReturnsTrue()
    {
        // Arrange
        SetupGitHubAppAuthentication(); // Setup authentication first!
        
        const long repositoryId = 12345;
        const string filePath = "AGENTS.md";
        
        var fileContent = _responseBuilder.BuildFileContentResponse(
            "# Agents Documentation", 
            filePath
        );
        
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/contents/{filePath}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(fileContent)
                .WithHeader("Content-Type", "application/json"));
        
        // Act
        try
        {
            var result = await Sut.FileExistsAsync(repositoryId, filePath);
            
            // Assert
            result.Should().BeTrue();
        }
        catch
        {
            // Log all WireMock requests to debug authentication issues
            LogWireMockRequests();
            throw;
        }
    }
    
    /// <summary>
    /// TC-GITHUB-003: FileExistsAsync - File Not Found
    /// Verifies that FileExistsAsync returns false when a file doesn't exist
    /// </summary>
    [Fact]
    public async Task FileExistsAsync_WhenFileNotFound_ReturnsFalse()
    {
        // Arrange
        SetupGitHubAppAuthentication();
        
        const long repositoryId = 12345;
        const string filePath = "missing-file.md";
        
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/contents/{filePath}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithBody("{\"message\": \"Not Found\"}")
                .WithHeader("Content-Type", "application/json"));
        
        // Act
        var result = await Sut.FileExistsAsync(repositoryId, filePath);
        
        // Assert
        result.Should().BeFalse();
    }
    
    /// <summary>
    /// TC-GITHUB-003: FileExistsAsync - Repository Not Found
    /// Verifies that FileExistsAsync returns false when repository doesn't exist
    /// </summary>
    [Fact]
    public async Task FileExistsAsync_WhenRepositoryNotFound_ReturnsFalse()
    {
        // Arrange
        SetupGitHubAppAuthentication();
        
        const long invalidRepositoryId = 99999;
        const string filePath = "AGENTS.md";
        
        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{invalidRepositoryId}/contents/{filePath}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithBody("{\"message\": \"Not Found\"}")
                .WithHeader("Content-Type", "application/json"));
        
        // Act
        var result = await Sut.FileExistsAsync(invalidRepositoryId, filePath);
        
        // Assert
        result.Should().BeFalse();
    }
    
    /// <summary>
    /// TC-CONFIG-004: GetFileContentAsync - File Exists
    /// Verifies that GetFileContentAsync returns Base64-encoded content when file exists
    /// </summary>
    [Fact]
    public async Task GetFileContentAsync_WhenFileExists_ReturnsBase64Content()
    {
        // Arrange
        SetupGitHubAppAuthentication();
        
        const string repoName = "test-repo";
        const string path = ".github/config.yaml";
        const string fileContent = "authorized_team: 'org/team'";
        
        var responseJson = _responseBuilder.BuildFileContentResponse(fileContent, path);
        
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repos/{Options.OrganizationName}/{repoName}/contents/{path}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(responseJson)
                .WithHeader("Content-Type", "application/json"));
        
        // Act
        var result = await Sut.GetFileContentAsync(repoName, path);
        
        // Assert
        result.Should().NotBeNull();
        
        // Decode Base64 to verify content
        var decodedBytes = Convert.FromBase64String(result!);
        var decodedContent = System.Text.Encoding.UTF8.GetString(decodedBytes);
        decodedContent.Should().Be(fileContent);
    }
    
    /// <summary>
    /// TC-CONFIG-001: GetFileContentAsync - File Not Found
    /// Verifies that GetFileContentAsync returns null when file doesn't exist
    /// </summary>
    [Fact]
    public async Task GetFileContentAsync_WhenFileNotFound_ReturnsNull()
    {
        // Arrange
        SetupGitHubAppAuthentication();
        
        const string repoName = "test-repo";
        const string path = "missing-file.yaml";
        
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repos/{Options.OrganizationName}/{repoName}/contents/{path}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithBody("{\"message\": \"Not Found\"}")
                .WithHeader("Content-Type", "application/json"));
        
        // Act
        var result = await Sut.GetFileContentAsync(repoName, path);
        
        // Assert
        result.Should().BeNull();
    }
    
    /// <summary>
    /// GetFileContentAsync - Invalid Repository
    /// Verifies that GetFileContentAsync returns null when repository doesn't exist
    /// </summary>
    [Fact]
    public async Task GetFileContentAsync_WhenRepositoryNotFound_ReturnsNull()
    {
        // Arrange
        SetupGitHubAppAuthentication();
        
        const string invalidRepoName = "non-existent-repo";
        const string path = "README.md";
        
        MockServer
            .Given(Request.Create()
                .WithPath($"/repos/{Options.OrganizationName}/{invalidRepoName}/contents/{path}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithBody("{\"message\": \"Not Found\"}")
                .WithHeader("Content-Type", "application/json"));
        
        // Act
        var result = await Sut.GetFileContentAsync(invalidRepoName, path);
        
        // Assert
        result.Should().BeNull();
    }
}

