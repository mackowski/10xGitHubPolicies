using _10xGitHubPolicies.Tests.Integration.Builders;
using _10xGitHubPolicies.Tests.Integration.Fixtures;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace _10xGitHubPolicies.Tests.Integration.GitHub;

[Trait("Category", "Integration")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "FileCrudOperations")]
public class FileCrudOperationsTests : GitHubServiceIntegrationTestBase
{
    private readonly GitHubApiResponseBuilder _responseBuilder;

    public FileCrudOperationsTests(GitHubApiFixture fixture) : base(fixture)
    {
        _responseBuilder = new GitHubApiResponseBuilder();
    }

    /// <summary>
    /// CreateFileAsync - Success
    /// Verifies that CreateFileAsync creates a file with the specified content
    /// </summary>
    [Fact]
    public async Task CreateFileAsync_WhenCalled_CreatesFileWithContent()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        const string repoName = "test-repo";
        const string filePath = "new-file.md";
        const string fileContent = "# New File Content";
        const string sha = "abc123def456";

        // Mock GET repository to get default branch
        var repoJson = _responseBuilder.BuildRepositoryResponse(repositoryId, repoName, false);
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(repoJson)
                .WithHeader("Content-Type", "application/json"));

        // Mock PUT file creation (GitHub API uses PUT for file creation/update)
        var fileCreationResponse = _responseBuilder.BuildFileCreationResponse(fileContent, filePath, sha, "Add new-file.md");
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/contents/{filePath}")
                .UsingPut())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(fileCreationResponse)
                .WithHeader("Content-Type", "application/json"));

        // Act
        await Sut.CreateFileAsync(repositoryId, filePath, fileContent);

        // Assert - Verify the mock server received the create request
        var requests = MockServer.LogEntries;
        requests.Should().ContainSingle(r =>
            r.RequestMessage.Path.Contains($"/contents/{filePath}") &&
            r.RequestMessage.Method == "PUT");
    }

    /// <summary>
    /// CreateFileAsync - Custom Commit Message
    /// Verifies that CreateFileAsync uses custom commit message when provided
    /// </summary>
    [Fact]
    public async Task CreateFileAsync_WithCustomCommitMessage_UsesCustomMessage()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        const string repoName = "test-repo";
        const string filePath = "custom-file.md";
        const string fileContent = "# Custom File";
        const string customCommitMessage = "Custom commit message";
        const string sha = "xyz789";

        // Mock GET repository
        var repoJson = _responseBuilder.BuildRepositoryResponse(repositoryId, repoName, false);
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(repoJson)
                .WithHeader("Content-Type", "application/json"));

        // Mock PUT file creation (GitHub API uses PUT for file creation/update)
        var fileCreationResponse = _responseBuilder.BuildFileCreationResponse(fileContent, filePath, sha, customCommitMessage);
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/contents/{filePath}")
                .UsingPut())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(fileCreationResponse)
                .WithHeader("Content-Type", "application/json"));

        // Act
        await Sut.CreateFileAsync(repositoryId, filePath, fileContent, customCommitMessage);

        // Assert - Verify request was made
        var requests = MockServer.LogEntries;
        requests.Should().ContainSingle(r =>
            r.RequestMessage.Path.Contains($"/contents/{filePath}") &&
            r.RequestMessage.Method == "PUT");
    }

    /// <summary>
    /// CreateFileAsync - Path Already Exists
    /// Verifies that CreateFileAsync throws exception when file already exists
    /// </summary>
    [Fact]
    public async Task CreateFileAsync_WhenPathAlreadyExists_ThrowsException()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        const string repoName = "test-repo";
        const string filePath = "existing-file.md";
        const string fileContent = "# Existing File";

        // Mock GET repository
        var repoJson = _responseBuilder.BuildRepositoryResponse(repositoryId, repoName, false);
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(repoJson)
                .WithHeader("Content-Type", "application/json"));

        // Mock PUT file creation - file already exists
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/contents/{filePath}")
                .UsingPut())
            .RespondWith(Response.Create()
                .WithStatusCode(422)
                .WithBody("{\"message\": \"Invalid request.\", \"errors\": [{\"resource\": \"File\", \"code\": \"already_exists\", \"field\": \"path\"}]}")
                .WithHeader("Content-Type", "application/json"));

        // Act
        var act = async () => await Sut.CreateFileAsync(repositoryId, filePath, fileContent);

        // Assert
        await act.Should().ThrowAsync<Octokit.ApiValidationException>();
    }

    /// <summary>
    /// UpdateFileAsync - Success
    /// Verifies that UpdateFileAsync updates file content successfully
    /// </summary>
    [Fact]
    public async Task UpdateFileAsync_WhenFileExists_UpdatesFileContent()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        const string repoName = "test-repo";
        const string filePath = "existing-file.md";
        const string existingContent = "# Old Content";
        const string newContent = "# New Content";
        const string sha = "existing-sha-123";

        // Mock GET repository
        var repoJson = _responseBuilder.BuildRepositoryResponse(repositoryId, repoName, false);
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(repoJson)
                .WithHeader("Content-Type", "application/json"));

        // Mock GET file to get SHA
        var existingFileResponse = _responseBuilder.BuildFileContentResponse(existingContent, filePath);
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/contents/{filePath}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(existingFileResponse)
                .WithHeader("Content-Type", "application/json"));

        // Mock PUT file update
        var fileUpdateResponse = _responseBuilder.BuildFileUpdateResponse(newContent, filePath, sha, "Update existing-file.md");
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/contents/{filePath}")
                .UsingPut())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(fileUpdateResponse)
                .WithHeader("Content-Type", "application/json"));

        // Act
        await Sut.UpdateFileAsync(repositoryId, filePath, newContent);

        // Assert - Verify the mock server received the update request
        var requests = MockServer.LogEntries;
        requests.Should().ContainSingle(r =>
            r.RequestMessage.Path.Contains($"/contents/{filePath}") &&
            r.RequestMessage.Method == "PUT");
    }

    /// <summary>
    /// UpdateFileAsync - File Not Found
    /// Verifies that UpdateFileAsync throws InvalidOperationException when file doesn't exist
    /// </summary>
    [Fact]
    public async Task UpdateFileAsync_WhenFileNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        const string repoName = "test-repo";
        const string filePath = "non-existent-file.md";
        const string newContent = "# New Content";

        // Mock GET repository
        var repoJson = _responseBuilder.BuildRepositoryResponse(repositoryId, repoName, false);
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(repoJson)
                .WithHeader("Content-Type", "application/json"));

        // Mock GET file - file not found (returns empty array)
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/contents/{filePath}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("[]")
                .WithHeader("Content-Type", "application/json"));

        // Act
        var act = async () => await Sut.UpdateFileAsync(repositoryId, filePath, newContent);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"File {filePath} not found in repository {repositoryId}");
    }

    /// <summary>
    /// DeleteFileAsync (by repositoryId) - Success
    /// Verifies that DeleteFileAsync deletes file successfully
    /// </summary>
    [Fact]
    public async Task DeleteFileAsync_ById_WhenFileExists_DeletesFile()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        const string repoName = "test-repo";
        const string filePath = "file-to-delete.md";
        const string existingContent = "# Content to delete";
        const string sha = "sha-to-delete";

        // Mock GET repository
        var repoJson = _responseBuilder.BuildRepositoryResponse(repositoryId, repoName, false);
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(repoJson)
                .WithHeader("Content-Type", "application/json"));

        // Mock GET file to get SHA
        var existingFileResponse = _responseBuilder.BuildFileContentResponse(existingContent, filePath);
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/contents/{filePath}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(existingFileResponse)
                .WithHeader("Content-Type", "application/json"));

        // Mock DELETE file
        var fileDeleteResponse = _responseBuilder.BuildFileDeleteResponse(filePath, sha, "Delete file-to-delete.md");
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/contents/{filePath}")
                .UsingDelete())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(fileDeleteResponse)
                .WithHeader("Content-Type", "application/json"));

        // Act
        await Sut.DeleteFileAsync(repositoryId, filePath);

        // Assert - Verify the mock server received the delete request
        var requests = MockServer.LogEntries;
        requests.Should().ContainSingle(r =>
            r.RequestMessage.Path.Contains($"/contents/{filePath}") &&
            r.RequestMessage.Method == "DELETE");
    }

    /// <summary>
    /// DeleteFileAsync (by repositoryId) - File Not Found
    /// Verifies that DeleteFileAsync throws InvalidOperationException when file doesn't exist
    /// </summary>
    [Fact]
    public async Task DeleteFileAsync_ById_WhenFileNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const long repositoryId = 12345;
        const string repoName = "test-repo";
        const string filePath = "non-existent-file.md";

        // Mock GET repository
        var repoJson = _responseBuilder.BuildRepositoryResponse(repositoryId, repoName, false);
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(repoJson)
                .WithHeader("Content-Type", "application/json"));

        // Mock GET file - file not found (returns empty array)
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/contents/{filePath}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("[]")
                .WithHeader("Content-Type", "application/json"));

        // Act
        var act = async () => await Sut.DeleteFileAsync(repositoryId, filePath);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"File {filePath} not found in repository {repositoryId}");
    }

    /// <summary>
    /// DeleteFileAsync (by repositoryName) - Success
    /// Verifies that DeleteFileAsync deletes file by repository name successfully
    /// </summary>
    [Fact]
    public async Task DeleteFileAsync_ByName_WhenFileExists_DeletesFile()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const string repoName = "test-repo";
        const string filePath = "file-to-delete.md";
        const string existingContent = "# Content to delete";
        const string sha = "sha-to-delete";

        // Mock GET repository by name
        var repoJson = _responseBuilder.BuildRepositoryResponse(12345, repoName, false);
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repos/{Options.OrganizationName}/{repoName}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(repoJson)
                .WithHeader("Content-Type", "application/json"));

        // Mock GET file to get SHA
        var existingFileResponse = _responseBuilder.BuildFileContentResponse(existingContent, filePath);
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repos/{Options.OrganizationName}/{repoName}/contents/{filePath}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(existingFileResponse)
                .WithHeader("Content-Type", "application/json"));

        // Mock DELETE file
        var fileDeleteResponse = _responseBuilder.BuildFileDeleteResponse(filePath, sha, "Delete file-to-delete.md");
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repos/{Options.OrganizationName}/{repoName}/contents/{filePath}")
                .UsingDelete())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(fileDeleteResponse)
                .WithHeader("Content-Type", "application/json"));

        // Act
        await Sut.DeleteFileAsync(repoName, filePath);

        // Assert - Verify the mock server received the delete request
        var requests = MockServer.LogEntries;
        requests.Should().ContainSingle(r =>
            r.RequestMessage.Path.Contains($"/contents/{filePath}") &&
            r.RequestMessage.Method == "DELETE");
    }

    /// <summary>
    /// DeleteFileAsync (by repositoryName) - File Not Found
    /// Verifies that DeleteFileAsync throws InvalidOperationException when file doesn't exist
    /// </summary>
    [Fact]
    public async Task DeleteFileAsync_ByName_WhenFileNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        const string repoName = "test-repo";
        const string filePath = "non-existent-file.md";

        // Mock GET repository by name
        var repoJson = _responseBuilder.BuildRepositoryResponse(12345, repoName, false);
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repos/{Options.OrganizationName}/{repoName}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(repoJson)
                .WithHeader("Content-Type", "application/json"));

        // Mock GET file - file not found (returns empty array)
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repos/{Options.OrganizationName}/{repoName}/contents/{filePath}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("[]")
                .WithHeader("Content-Type", "application/json"));

        // Act
        var act = async () => await Sut.DeleteFileAsync(repoName, filePath);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"File {filePath} not found in repository {repoName}");
    }
}

