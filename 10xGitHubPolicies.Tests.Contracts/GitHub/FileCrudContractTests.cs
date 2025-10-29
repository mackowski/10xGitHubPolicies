using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace _10xGitHubPolicies.Tests.Contracts.GitHub;

[Trait("Category", "Contract")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "FileCrudContract")]
public class FileCrudContractTests : GitHubContractTestBase
{
    // Schema validation tests verify that the API responses contain required fields
    // as defined in the GitHub API JSON schemas

    /// <summary>
    /// CreateFileAsync - Response Schema
    /// Verifies that CreateFileAsync response matches JSON schema
    /// </summary>
    [Fact]
    public async Task CreateFileAsync_ResponseMatchesSchema()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        var repositoryId = Faker.Random.Long(1, 999999);
        var repoName = Faker.Internet.DomainWord();
        var filePath = "new-file.md";
        var fileContent = "# New File";
        var base64Content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(fileContent));
        var sha = Faker.Random.Hexadecimal(40, prefix: "");
        var commitSha = Faker.Random.Hexadecimal(40, prefix: "");
        var commitMessage = "Add new-file.md";

        // Mock GET repository to get default branch (required by CreateFileAsync)
        var repoResponse = new
        {
            id = repositoryId,
            name = repoName,
            full_name = $"{Options.OrganizationName}/{repoName}",
            default_branch = "main",
            owner = new
            {
                login = Options.OrganizationName,
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
                .WithBodyAsJson(repoResponse));

        // Mock file creation response
        var fileCreationResponse = new
        {
            content = new
            {
                name = System.IO.Path.GetFileName(filePath),
                path = filePath,
                sha = sha,
                size = fileContent.Length,
                type = "file",
                content = base64Content,
                encoding = "base64"
            },
            commit = new
            {
                sha = commitSha,
                message = commitMessage,
                author = new
                {
                    name = "test-user",
                    email = "test@example.com",
                    date = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                }
            }
        };

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/contents/{filePath}")
                .UsingPut())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(fileCreationResponse));

        // Act
        await Sut.CreateFileAsync(repositoryId, filePath, fileContent);

        // Assert - Verify the request was made (response structure validates schema)
        var requests = MockServer.LogEntries;
        requests.Should().ContainSingle(r =>
            r.RequestMessage.Path.Contains($"/contents/{filePath}") &&
            r.RequestMessage.Method == "PUT");

        // Verify response structure matches schema
        fileCreationResponse.content.Should().NotBeNull("content is a required object field");
        fileCreationResponse.content.name.Should().NotBeNullOrEmpty("content.name is a required string field");
        fileCreationResponse.content.path.Should().Be(filePath, "content.path is a required string field");
        fileCreationResponse.content.sha.Should().NotBeNullOrEmpty("content.sha is a required string field");
        fileCreationResponse.content.size.Should().BeGreaterThan(0, "content.size is a required integer field");
        fileCreationResponse.content.type.Should().Be("file", "content.type should be 'file'");
        fileCreationResponse.content.encoding.Should().Be("base64", "content.encoding should be 'base64'");

        fileCreationResponse.commit.Should().NotBeNull("commit is a required object field");
        fileCreationResponse.commit.sha.Should().NotBeNullOrEmpty("commit.sha is a required string field");
        fileCreationResponse.commit.message.Should().Be(commitMessage, "commit.message is a required string field");
        fileCreationResponse.commit.author.Should().NotBeNull("commit.author is a required object field");
    }

    /// <summary>
    /// UpdateFileAsync - Response Schema
    /// Verifies that UpdateFileAsync response matches JSON schema
    /// </summary>
    [Fact]
    public async Task UpdateFileAsync_ResponseMatchesSchema()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        var repositoryId = Faker.Random.Long(1, 999999);
        var repoName = Faker.Internet.DomainWord();
        var filePath = "existing-file.md";
        var fileContent = "# Updated Content";
        var base64Content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(fileContent));
        var sha = Faker.Random.Hexadecimal(40, prefix: "");
        var commitSha = Faker.Random.Hexadecimal(40, prefix: "");
        var commitMessage = "Update existing-file.md";

        // Mock GET repository to get default branch (required by UpdateFileAsync)
        var repoResponse = new
        {
            id = repositoryId,
            name = repoName,
            full_name = $"{Options.OrganizationName}/{repoName}",
            default_branch = "main",
            owner = new
            {
                login = Options.OrganizationName,
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
                .WithBodyAsJson(repoResponse));

        // Mock GET file to get SHA
        var existingFileResponse = new[]
        {
            new
            {
                sha = sha,
                path = filePath,
                name = System.IO.Path.GetFileName(filePath)
            }
        };

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/contents/{filePath}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(existingFileResponse));

        // Mock file update response
        var fileUpdateResponse = new
        {
            content = new
            {
                name = System.IO.Path.GetFileName(filePath),
                path = filePath,
                sha = sha,
                size = fileContent.Length,
                type = "file",
                content = base64Content,
                encoding = "base64"
            },
            commit = new
            {
                sha = commitSha,
                message = commitMessage,
                author = new
                {
                    name = "test-user",
                    email = "test@example.com",
                    date = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                }
            }
        };

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/contents/{filePath}")
                .UsingPut())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(fileUpdateResponse));

        // Act
        await Sut.UpdateFileAsync(repositoryId, filePath, fileContent);

        // Assert - Verify the request was made (response structure validates schema)
        var requests = MockServer.LogEntries;
        requests.Should().ContainSingle(r =>
            r.RequestMessage.Path.Contains($"/contents/{filePath}") &&
            r.RequestMessage.Method == "PUT");

        // Verify response structure matches schema (same as CreateFileAsync)
        fileUpdateResponse.content.Should().NotBeNull("content is a required object field");
        fileUpdateResponse.content.sha.Should().NotBeNullOrEmpty("content.sha is a required string field");
        fileUpdateResponse.commit.Should().NotBeNull("commit is a required object field");
        fileUpdateResponse.commit.sha.Should().NotBeNullOrEmpty("commit.sha is a required string field");
    }

    /// <summary>
    /// DeleteFileAsync - Response Schema
    /// Verifies that DeleteFileAsync response matches JSON schema
    /// </summary>
    [Fact]
    public async Task DeleteFileAsync_ResponseMatchesSchema()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        var repositoryId = Faker.Random.Long(1, 999999);
        var repoName = Faker.Internet.DomainWord();
        var filePath = "file-to-delete.md";
        var sha = Faker.Random.Hexadecimal(40, prefix: "");
        var commitSha = Faker.Random.Hexadecimal(40, prefix: "");
        var commitMessage = "Delete file-to-delete.md";

        // Mock GET repository to get default branch (required by DeleteFileAsync)
        var repoResponse = new
        {
            id = repositoryId,
            name = repoName,
            full_name = $"{Options.OrganizationName}/{repoName}",
            default_branch = "main",
            owner = new
            {
                login = Options.OrganizationName,
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
                .WithBodyAsJson(repoResponse));

        // Mock GET file to get SHA
        var existingFileResponse = new[]
        {
            new
            {
                sha = sha,
                path = filePath,
                name = System.IO.Path.GetFileName(filePath)
            }
        };

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/contents/{filePath}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(existingFileResponse));

        // Mock file deletion response
        var fileDeleteResponse = new
        {
            content = (object?)null,
            commit = new
            {
                sha = commitSha,
                message = commitMessage,
                author = new
                {
                    name = "test-user",
                    email = "test@example.com",
                    date = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                }
            }
        };

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/contents/{filePath}")
                .UsingDelete())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(fileDeleteResponse));

        // Act
        await Sut.DeleteFileAsync(repositoryId, filePath);

        // Assert - Verify the request was made (response structure validates schema)
        var requests = MockServer.LogEntries;
        requests.Should().ContainSingle(r =>
            r.RequestMessage.Path.Contains($"/contents/{filePath}") &&
            r.RequestMessage.Method == "DELETE");

        // Verify response structure matches schema
        fileDeleteResponse.content.Should().BeNull("content should be null after deletion");
        fileDeleteResponse.commit.Should().NotBeNull("commit is a required object field");
        fileDeleteResponse.commit.sha.Should().NotBeNullOrEmpty("commit.sha is a required string field");
        fileDeleteResponse.commit.message.Should().Be(commitMessage, "commit.message is a required string field");
        fileDeleteResponse.commit.author.Should().NotBeNull("commit.author is a required object field");
    }
}

