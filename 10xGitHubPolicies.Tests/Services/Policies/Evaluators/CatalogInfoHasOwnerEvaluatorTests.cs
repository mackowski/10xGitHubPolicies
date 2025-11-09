using System.Text;
using _10xGitHubPolicies.App.Services.GitHub;
using _10xGitHubPolicies.App.Services.Policies.Evaluators;
using Bogus;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Octokit;
using Xunit;
using OctokitRepository = Octokit.Repository;

namespace _10xGitHubPolicies.Tests.Services.Policies.Evaluators;

[Trait("Category", "Unit")]
[Trait("Service", "CatalogInfoHasOwnerEvaluator")]
public class CatalogInfoHasOwnerEvaluatorTests
{
    private readonly IGitHubService _gitHubService;
    private readonly ILogger<CatalogInfoHasOwnerEvaluator> _logger;
    private readonly CatalogInfoHasOwnerEvaluator _sut;
    private readonly Faker _faker;

    public CatalogInfoHasOwnerEvaluatorTests()
    {
        _gitHubService = Substitute.For<IGitHubService>();
        _logger = Substitute.For<ILogger<CatalogInfoHasOwnerEvaluator>>();
        _sut = new CatalogInfoHasOwnerEvaluator(_gitHubService, _logger);
        _faker = new Faker();
    }

    [Fact]
    public void PolicyType_WhenAccessed_ReturnsCorrectValue()
    {
        // Act
        var policyType = _sut.PolicyType;

        // Assert
        policyType.Should().Be("catalog_info_has_owner");
    }

    [Fact]
    public async Task EvaluateAsync_WhenFileDoesNotExist_ReturnsNull()
    {
        // Arrange
        var repository = CreateMockRepository();
        _gitHubService.GetFileContentAsync(repository.Name, "catalog-info.yaml")
            .Returns((string?)null);

        // Act
        var result = await _sut.EvaluateAsync(repository);

        // Assert
        result.Should().BeNull(because: "file doesn't exist, covered by has_catalog_info_yaml policy");

        await _gitHubService.Received(1).GetFileContentAsync(repository.Name, "catalog-info.yaml");
    }

    [Fact]
    public async Task EvaluateAsync_WhenOwnerExistsAndNotEmpty_ReturnsNull()
    {
        // Arrange
        var repository = CreateMockRepository();
        var validYaml = @"
apiVersion: backstage.io/v1alpha1
kind: Component
spec:
  owner: appsec
";
        var base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(validYaml));
        _gitHubService.GetFileContentAsync(repository.Name, "catalog-info.yaml")
            .Returns(base64Content);

        // Act
        var result = await _sut.EvaluateAsync(repository);

        // Assert
        result.Should().BeNull(because: "owner exists and is not empty, repository is compliant");
    }

    [Fact]
    public async Task EvaluateAsync_WhenOwnerMissing_ReturnsViolation()
    {
        // Arrange
        var repository = CreateMockRepository();
        var yamlWithoutOwner = @"
apiVersion: backstage.io/v1alpha1
kind: Component
spec:
  type: service
";
        var base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(yamlWithoutOwner));
        _gitHubService.GetFileContentAsync(repository.Name, "catalog-info.yaml")
            .Returns(base64Content);

        // Act
        var result = await _sut.EvaluateAsync(repository);

        // Assert
        result.Should().NotBeNull(because: "owner field is missing");
        result!.PolicyType.Should().Be("catalog_info_has_owner");
    }

    [Fact]
    public async Task EvaluateAsync_WhenOwnerEmpty_ReturnsViolation()
    {
        // Arrange
        var repository = CreateMockRepository();
        var yamlWithEmptyOwner = @"
apiVersion: backstage.io/v1alpha1
kind: Component
spec:
  owner: ''
";
        var base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(yamlWithEmptyOwner));
        _gitHubService.GetFileContentAsync(repository.Name, "catalog-info.yaml")
            .Returns(base64Content);

        // Act
        var result = await _sut.EvaluateAsync(repository);

        // Assert
        result.Should().NotBeNull(because: "owner field is empty");
        result!.PolicyType.Should().Be("catalog_info_has_owner");
    }

    [Fact]
    public async Task EvaluateAsync_WhenOwnerWhitespace_ReturnsViolation()
    {
        // Arrange
        var repository = CreateMockRepository();
        var yamlWithWhitespaceOwner = @"
apiVersion: backstage.io/v1alpha1
kind: Component
spec:
  owner: '   '
";
        var base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(yamlWithWhitespaceOwner));
        _gitHubService.GetFileContentAsync(repository.Name, "catalog-info.yaml")
            .Returns(base64Content);

        // Act
        var result = await _sut.EvaluateAsync(repository);

        // Assert
        result.Should().NotBeNull(because: "owner field contains only whitespace");
        result!.PolicyType.Should().Be("catalog_info_has_owner");
    }

    [Fact]
    public async Task EvaluateAsync_WhenSpecSectionMissing_ReturnsViolation()
    {
        // Arrange
        var repository = CreateMockRepository();
        var yamlWithoutSpec = @"
apiVersion: backstage.io/v1alpha1
kind: Component
metadata:
  name: test-service
";
        var base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(yamlWithoutSpec));
        _gitHubService.GetFileContentAsync(repository.Name, "catalog-info.yaml")
            .Returns(base64Content);

        // Act
        var result = await _sut.EvaluateAsync(repository);

        // Assert
        result.Should().NotBeNull(because: "spec section is missing");
        result!.PolicyType.Should().Be("catalog_info_has_owner");
    }

    [Fact]
    public async Task EvaluateAsync_WhenInvalidYaml_ReturnsViolation()
    {
        // Arrange
        var repository = CreateMockRepository();
        var invalidYaml = @"
apiVersion: backstage.io/v1alpha1
kind: Component
spec:
  owner: appsec
  invalid: [unclosed bracket
";
        var base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(invalidYaml));
        _gitHubService.GetFileContentAsync(repository.Name, "catalog-info.yaml")
            .Returns(base64Content);

        // Act
        var result = await _sut.EvaluateAsync(repository);

        // Assert
        result.Should().NotBeNull(because: "YAML is malformed");
        result!.PolicyType.Should().Be("catalog_info_has_owner");
    }

    [Fact]
    public async Task EvaluateAsync_WhenCalled_UsesCorrectRepositoryName()
    {
        // Arrange
        var expectedRepoName = _faker.Random.Word();
        var repository = CreateMockRepository(name: expectedRepoName);
        var validYaml = @"
apiVersion: backstage.io/v1alpha1
kind: Component
spec:
  owner: appsec
";
        var base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(validYaml));
        _gitHubService.GetFileContentAsync(expectedRepoName, "catalog-info.yaml")
            .Returns(base64Content);

        // Act
        await _sut.EvaluateAsync(repository);

        // Assert
        await _gitHubService.Received(1).GetFileContentAsync(expectedRepoName, "catalog-info.yaml");
    }

    [Fact]
    public async Task EvaluateAsync_WhenOwnerIsNull_ReturnsViolation()
    {
        // Arrange
        var repository = CreateMockRepository();
        var yamlWithNullOwner = @"
apiVersion: backstage.io/v1alpha1
kind: Component
spec:
  owner: null
";
        var base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(yamlWithNullOwner));
        _gitHubService.GetFileContentAsync(repository.Name, "catalog-info.yaml")
            .Returns(base64Content);

        // Act
        var result = await _sut.EvaluateAsync(repository);

        // Assert
        result.Should().NotBeNull(because: "owner field is null");
        result!.PolicyType.Should().Be("catalog_info_has_owner");
    }

    [Fact]
    public async Task EvaluateAsync_WhenOwnerIsValidTeamName_ReturnsNull()
    {
        // Arrange
        var repository = CreateMockRepository();
        var validYaml = @"
apiVersion: backstage.io/v1alpha1
kind: Component
spec:
  owner: platform-team
";
        var base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(validYaml));
        _gitHubService.GetFileContentAsync(repository.Name, "catalog-info.yaml")
            .Returns(base64Content);

        // Act
        var result = await _sut.EvaluateAsync(repository);

        // Assert
        result.Should().BeNull(because: "owner is a valid team name");
    }

    [Fact]
    public async Task EvaluateAsync_WhenSpecIsNotDictionary_ReturnsViolation()
    {
        // Arrange
        var repository = CreateMockRepository();
        var yamlWithInvalidSpec = @"
apiVersion: backstage.io/v1alpha1
kind: Component
spec: 'not an object'
";
        var base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(yamlWithInvalidSpec));
        _gitHubService.GetFileContentAsync(repository.Name, "catalog-info.yaml")
            .Returns(base64Content);

        // Act
        var result = await _sut.EvaluateAsync(repository);

        // Assert
        result.Should().NotBeNull(because: "spec is not a valid object/dictionary");
        result!.PolicyType.Should().Be("catalog_info_has_owner");
    }

    [Fact]
    public async Task EvaluateAsync_WhenFileIsEmpty_ReturnsViolation()
    {
        // Arrange
        var repository = CreateMockRepository();
        var emptyYaml = "";
        var base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(emptyYaml));
        _gitHubService.GetFileContentAsync(repository.Name, "catalog-info.yaml")
            .Returns(base64Content);

        // Act
        var result = await _sut.EvaluateAsync(repository);

        // Assert
        // Empty YAML deserializes to null or empty dictionary, which should return a violation
        // because it doesn't contain the 'spec' section
        result.Should().NotBeNull(because: "empty YAML file should not contain 'spec' section and return violation");
        result!.PolicyType.Should().Be("catalog_info_has_owner");
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

        // Use reflection to set the Name property if deserialization didn't work
        if (string.IsNullOrEmpty(repository.Name))
        {
            var nameProperty = typeof(OctokitRepository).GetProperty("Name");
            if (nameProperty != null && nameProperty.CanWrite)
            {
                nameProperty.SetValue(repository, name);
            }
            else
            {
                // If property is not writable, use backing field
                var nameField = typeof(OctokitRepository).GetField("<Name>k__BackingField",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                nameField?.SetValue(repository, name);
            }
        }

        return repository;
    }
}

