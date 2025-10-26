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
[Trait("Service", "HasAgentsMdEvaluator")]
public class HasAgentsMdEvaluatorTests
{
    private readonly IGitHubService _gitHubService;
    private readonly HasAgentsMdEvaluator _sut;
    private readonly Faker _faker;

    public HasAgentsMdEvaluatorTests()
    {
        _gitHubService = Substitute.For<IGitHubService>();
        _sut = new HasAgentsMdEvaluator(_gitHubService);
        _faker = new Faker();
    }

    [Fact]
    public void PolicyType_WhenAccessed_ReturnsCorrectValue()
    {
        // Act
        var policyType = _sut.PolicyType;

        // Assert
        policyType.Should().Be("has_agents_md");
    }

    [Fact]
    public async Task EvaluateAsync_WhenAgentsMdExists_ReturnsNull()
    {
        // Arrange
        var repository = CreateMockRepository();
        _gitHubService.FileExistsAsync(repository.Id, "AGENTS.md")
            .Returns(true);

        // Act
        var result = await _sut.EvaluateAsync(repository);

        // Assert
        result.Should().BeNull(because: "AGENTS.md file exists, so repository is compliant");

        await _gitHubService.Received(1).FileExistsAsync(repository.Id, "AGENTS.md");
    }

    [Fact]
    public async Task EvaluateAsync_WhenAgentsMdMissing_ReturnsViolation()
    {
        // Arrange
        var repository = CreateMockRepository();
        _gitHubService.FileExistsAsync(repository.Id, "AGENTS.md")
            .Returns(false);

        // Act
        var result = await _sut.EvaluateAsync(repository);

        // Assert
        result.Should().NotBeNull(because: "AGENTS.md file is missing");
        result!.PolicyType.Should().Be("has_agents_md");

        await _gitHubService.Received(1).FileExistsAsync(repository.Id, "AGENTS.md");
    }

    [Fact]
    public async Task EvaluateAsync_WhenCalled_UsesCorrectRepositoryId()
    {
        // Arrange
        var expectedRepoId = _faker.Random.Long(1000, 999999);
        var repository = CreateMockRepository(id: expectedRepoId);
        _gitHubService.FileExistsAsync(expectedRepoId, "AGENTS.md")
            .Returns(true);

        // Act
        await _sut.EvaluateAsync(repository);

        // Assert
        await _gitHubService.Received(1).FileExistsAsync(expectedRepoId, "AGENTS.md");
    }

    [Fact]
    public async Task EvaluateAsync_WhenCalled_ChecksExactFileName()
    {
        // Arrange
        var repository = CreateMockRepository();
        _gitHubService.FileExistsAsync(repository.Id, "AGENTS.md")
            .Returns(false);

        // Act
        await _sut.EvaluateAsync(repository);

        // Assert - Verify exact file name is checked
        await _gitHubService.Received(1).FileExistsAsync(
            repository.Id,
            Arg.Is<string>(s => s == "AGENTS.md"));

        // Should NOT check for lowercase variants
        await _gitHubService.DidNotReceive().FileExistsAsync(
            repository.Id,
            Arg.Is<string>(s => s == "agents.md"));
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

