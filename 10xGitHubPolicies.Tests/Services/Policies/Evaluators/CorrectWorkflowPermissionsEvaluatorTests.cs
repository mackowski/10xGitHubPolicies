using _10xGitHubPolicies.App.Services.GitHub;
using _10xGitHubPolicies.App.Services.Policies.Evaluators;
using Bogus;
using FluentAssertions;
using NSubstitute;
using Octokit;
using Xunit;
using OctokitRepository = Octokit.Repository;

namespace _10xGitHubPolicies.Tests.Services.Policies.Evaluators;

[Trait("Category", "Unit")]
[Trait("Service", "CorrectWorkflowPermissionsEvaluator")]
public class CorrectWorkflowPermissionsEvaluatorTests
{
    private readonly IGitHubService _gitHubService;
    private readonly CorrectWorkflowPermissionsEvaluator _sut;
    private readonly Faker _faker;

    public CorrectWorkflowPermissionsEvaluatorTests()
    {
        _gitHubService = Substitute.For<IGitHubService>();
        _sut = new CorrectWorkflowPermissionsEvaluator(_gitHubService);
        _faker = new Faker();
    }

    [Fact]
    public void PolicyType_WhenAccessed_ReturnsCorrectValue()
    {
        // Act
        var policyType = _sut.PolicyType;

        // Assert
        policyType.Should().Be("correct_workflow_permissions");
    }

    [Fact]
    public async Task EvaluateAsync_WhenPermissionsAreRead_ReturnsNull()
    {
        // Arrange
        var repository = CreateMockRepository();
        _gitHubService.GetWorkflowPermissionsAsync(repository.Id)
            .Returns("read");

        // Act
        var result = await _sut.EvaluateAsync(repository);

        // Assert
        result.Should().BeNull(because: "permissions are set to 'read' (secure setting)");

        await _gitHubService.Received(1).GetWorkflowPermissionsAsync(repository.Id);
    }

    [Fact]
    public async Task EvaluateAsync_WhenPermissionsAreWrite_ReturnsViolation()
    {
        // Arrange
        var repository = CreateMockRepository();
        _gitHubService.GetWorkflowPermissionsAsync(repository.Id)
            .Returns("write");

        // Act
        var result = await _sut.EvaluateAsync(repository);

        // Assert
        result.Should().NotBeNull(because: "permissions are 'write' (insecure setting)");
        result!.PolicyType.Should().Be("correct_workflow_permissions");

        await _gitHubService.Received(1).GetWorkflowPermissionsAsync(repository.Id);
    }

    [Fact]
    public async Task EvaluateAsync_WhenPermissionsAreNull_ReturnsNull()
    {
        // Arrange
        var repository = CreateMockRepository();
        _gitHubService.GetWorkflowPermissionsAsync(repository.Id)
            .Returns((string?)null);

        // Act
        var result = await _sut.EvaluateAsync(repository);

        // Assert
        result.Should().BeNull(because: "null permissions means GitHub Actions is disabled (compliant)");

        await _gitHubService.Received(1).GetWorkflowPermissionsAsync(repository.Id);
    }

    [Theory]
    [InlineData("read", false)]      // Compliant
    [InlineData("write", true)]      // Violation
    [InlineData("admin", true)]      // Violation
    [InlineData("none", true)]       // Violation (not "read")
    [InlineData("unknown", true)]    // Violation (not "read")
    public async Task EvaluateAsync_WhenVariousPermissions_EvaluatesCorrectly(
        string permissions,
        bool expectViolation)
    {
        // Arrange
        var repository = CreateMockRepository();
        _gitHubService.GetWorkflowPermissionsAsync(repository.Id)
            .Returns(permissions);

        // Act
        var result = await _sut.EvaluateAsync(repository);

        // Assert
        if (expectViolation)
        {
            result.Should().NotBeNull(because: $"permissions '{permissions}' should be a violation");
            result!.PolicyType.Should().Be("correct_workflow_permissions");
        }
        else
        {
            result.Should().BeNull(because: $"permissions '{permissions}' should be compliant");
        }
    }

    [Theory]
    [InlineData("read")]   // Lowercase - compliant
    [InlineData("Read")]   // Capital R - violation
    [InlineData("READ")]   // Uppercase - violation
    [InlineData("ReAd")]   // Mixed case - violation
    public async Task EvaluateAsync_WhenPermissionsCaseDiffers_IsCaseSensitive(string permissions)
    {
        // Arrange
        var repository = CreateMockRepository();
        _gitHubService.GetWorkflowPermissionsAsync(repository.Id)
            .Returns(permissions);

        // Act
        var result = await _sut.EvaluateAsync(repository);

        // Assert
        if (permissions == "read")
        {
            result.Should().BeNull(because: "lowercase 'read' is the correct value");
        }
        else
        {
            result.Should().NotBeNull(because: "comparison is case-sensitive");
        }
    }

    [Fact]
    public async Task EvaluateAsync_WhenCalled_UsesCorrectRepositoryId()
    {
        // Arrange
        var expectedRepoId = _faker.Random.Long(1000, 999999);
        var repository = CreateMockRepository(id: expectedRepoId);
        _gitHubService.GetWorkflowPermissionsAsync(expectedRepoId)
            .Returns("read");

        // Act
        await _sut.EvaluateAsync(repository);

        // Assert
        await _gitHubService.Received(1).GetWorkflowPermissionsAsync(expectedRepoId);
    }

    /// <summary>
    /// Creates a real Octokit.Repository instance for testing using JSON deserialization
    /// </summary>
    private OctokitRepository CreateMockRepository(long id = 12345, string name = "test-repo")
    {
        // Create Repository via JSON deserialization (the way Octokit does it internally)
        var json = $$"""
        {
            "id": {{id}},
            "node_id": "R_{{id}}",
            "name": "{{name}}",
            "full_name": "owner/{{name}}",
            "private": false,
            "owner": {
                "login": "owner",
                "id": 1,
                "node_id": "U_1",
                "avatar_url": "",
                "url": "https://api.github.com/users/owner",
                "html_url": "https://github.com/owner",
                "type": "User"
            },
            "html_url": "https://github.com/owner/{{name}}",
            "description": "Test repository",
            "fork": false,
            "url": "https://api.github.com/repos/owner/{{name}}",
            "created_at": "2024-01-01T00:00:00Z",
            "updated_at": "2024-01-01T00:00:00Z",
            "pushed_at": "2024-01-01T00:00:00Z",
            "size": 100,
            "stargazers_count": 0,
            "watchers_count": 0,
            "language": "C#",
            "forks_count": 0,
            "open_issues_count": 0,
            "default_branch": "main",
            "visibility": "public"
        }
        """;

        var repository = Newtonsoft.Json.JsonConvert.DeserializeObject<OctokitRepository>(json)!;
        
        // Use reflection to set the Id property if deserialization didn't work
        if (repository.Id == 0 && id != 0)
        {
            var idProperty = typeof(OctokitRepository).GetProperty("Id");
            if (idProperty != null && idProperty.CanWrite)
            {
                idProperty.SetValue(repository, id);
            }
            else
            {
                // If property is not writable, use backing field
                var idField = typeof(OctokitRepository).GetField("<Id>k__BackingField", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                idField?.SetValue(repository, id);
            }
        }
        
        return repository;
    }
}

