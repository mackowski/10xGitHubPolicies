using _10xGitHubPolicies.App.Data.Entities;
using _10xGitHubPolicies.App.Services.Configuration.Models;
using _10xGitHubPolicies.App.Services.Policies;
using Bogus;
using FluentAssertions;
using NSubstitute;
using Octokit;
using Xunit;
using OctokitRepository = Octokit.Repository;

namespace _10xGitHubPolicies.Tests.Services.Policies;

[Trait("Category", "Unit")]
[Trait("Service", "PolicyEvaluationService")]
public class PolicyEvaluationServiceTests
{
    private readonly Faker _faker;

    public PolicyEvaluationServiceTests()
    {
        _faker = new Faker();
    }

    [Fact]
    public async Task EvaluateRepositoryAsync_WhenNoPolicies_ReturnsEmptyList()
    {
        // Arrange
        var evaluators = new List<IPolicyEvaluator>
        {
            Substitute.For<IPolicyEvaluator>()
        };
        var sut = new PolicyEvaluationService(evaluators);
        var repository = CreateMockRepository();
        var policies = new List<PolicyConfig>(); // Empty

        // Act
        var result = await sut.EvaluateRepositoryAsync(repository, policies);

        // Assert
        result.Should().BeEmpty(because: "no policies to evaluate");

        // Verify no evaluators were called
        await evaluators[0].DidNotReceive().EvaluateAsync(Arg.Any<OctokitRepository>());
    }

    [Fact]
    public async Task EvaluateRepositoryAsync_WhenSinglePolicyMatches_InvokesCorrectEvaluator()
    {
        // Arrange
        var mockEvaluator = Substitute.For<IPolicyEvaluator>();
        mockEvaluator.PolicyType.Returns("has_agents_md");
        mockEvaluator.EvaluateAsync(Arg.Any<OctokitRepository>())
            .Returns(new PolicyViolation { PolicyType = "has_agents_md" });

        var evaluators = new List<IPolicyEvaluator> { mockEvaluator };
        var sut = new PolicyEvaluationService(evaluators);

        var repository = CreateMockRepository();
        var policies = new List<PolicyConfig>
        {
            new PolicyConfig { Type = "has_agents_md", Name = "AGENTS.md Required" }
        };

        // Act
        var result = await sut.EvaluateRepositoryAsync(repository, policies);

        // Assert
        result.Should().HaveCount(1);
        result.First().PolicyType.Should().Be("has_agents_md");

        await mockEvaluator.Received(1).EvaluateAsync(repository);
    }

    [Fact]
    public async Task EvaluateRepositoryAsync_WhenMultiplePolicies_EvaluatesAll()
    {
        // Arrange
        var evaluator1 = CreateMockEvaluator("has_agents_md", returnsViolation: true);
        var evaluator2 = CreateMockEvaluator("has_catalog_info_yaml", returnsViolation: true);
        var evaluator3 = CreateMockEvaluator("correct_workflow_permissions", returnsViolation: false);

        var evaluators = new List<IPolicyEvaluator> { evaluator1, evaluator2, evaluator3 };
        var sut = new PolicyEvaluationService(evaluators);

        var repository = CreateMockRepository();
        var policies = new List<PolicyConfig>
        {
            new PolicyConfig { Type = "has_agents_md" },
            new PolicyConfig { Type = "has_catalog_info_yaml" },
            new PolicyConfig { Type = "correct_workflow_permissions" }
        };

        // Act
        var result = await sut.EvaluateRepositoryAsync(repository, policies);

        // Assert
        result.Should().HaveCount(2, because: "2 out of 3 policies returned violations");
        result.Should().Contain(v => v.PolicyType == "has_agents_md");
        result.Should().Contain(v => v.PolicyType == "has_catalog_info_yaml");
        result.Should().NotContain(v => v.PolicyType == "correct_workflow_permissions");

        await evaluator1.Received(1).EvaluateAsync(repository);
        await evaluator2.Received(1).EvaluateAsync(repository);
        await evaluator3.Received(1).EvaluateAsync(repository);
    }

    [Fact]
    public async Task EvaluateRepositoryAsync_WhenNoMatchingEvaluator_SkipsPolicy()
    {
        // Arrange
        var evaluator = CreateMockEvaluator("has_agents_md", returnsViolation: false);
        var evaluators = new List<IPolicyEvaluator> { evaluator };
        var sut = new PolicyEvaluationService(evaluators);

        var repository = CreateMockRepository();
        var policies = new List<PolicyConfig>
        {
            new PolicyConfig { Type = "unknown_policy_type" } // No matching evaluator
        };

        // Act
        var result = await sut.EvaluateRepositoryAsync(repository, policies);

        // Assert
        result.Should().BeEmpty(because: "no evaluator matches the policy type");

        // Verify evaluator was not called
        await evaluator.DidNotReceive().EvaluateAsync(Arg.Any<OctokitRepository>());
    }

    [Theory]
    [InlineData("has_agents_md")]
    [InlineData("HAS_AGENTS_MD")]
    [InlineData("Has_Agents_Md")]
    [InlineData("HAS_agents_MD")]
    public async Task EvaluateRepositoryAsync_WhenPolicyTypeDifferentCase_MatchesCorrectly(string policyType)
    {
        // Arrange
        var evaluator = Substitute.For<IPolicyEvaluator>();
        evaluator.PolicyType.Returns("has_agents_md"); // Lowercase in evaluator
        evaluator.EvaluateAsync(Arg.Any<OctokitRepository>())
            .Returns(new PolicyViolation { PolicyType = "has_agents_md" });

        var evaluators = new List<IPolicyEvaluator> { evaluator };
        var sut = new PolicyEvaluationService(evaluators);

        var repository = CreateMockRepository();
        var policies = new List<PolicyConfig>
        {
            new PolicyConfig { Type = policyType } // Different case
        };

        // Act
        var result = await sut.EvaluateRepositoryAsync(repository, policies);

        // Assert
        result.Should().HaveCount(1, because: "matching should be case-insensitive");
        await evaluator.Received(1).EvaluateAsync(repository);
    }

    [Fact]
    public async Task EvaluateRepositoryAsync_WhenEvaluatorReturnsNull_ExcludesFromResult()
    {
        // Arrange
        var evaluator = Substitute.For<IPolicyEvaluator>();
        evaluator.PolicyType.Returns("has_agents_md");
        evaluator.EvaluateAsync(Arg.Any<OctokitRepository>())
            .Returns((PolicyViolation?)null); // Compliant - no violation

        var evaluators = new List<IPolicyEvaluator> { evaluator };
        var sut = new PolicyEvaluationService(evaluators);

        var repository = CreateMockRepository();
        var policies = new List<PolicyConfig>
        {
            new PolicyConfig { Type = "has_agents_md" }
        };

        // Act
        var result = await sut.EvaluateRepositoryAsync(repository, policies);

        // Assert
        result.Should().BeEmpty(because: "evaluator returned null (compliant)");
        await evaluator.Received(1).EvaluateAsync(repository);
    }

    [Fact]
    public async Task EvaluateRepositoryAsync_WhenMultipleEvaluators_PassesSameRepository()
    {
        // Arrange
        var evaluator1 = Substitute.For<IPolicyEvaluator>();
        var evaluator2 = Substitute.For<IPolicyEvaluator>();

        evaluator1.PolicyType.Returns("policy1");
        evaluator2.PolicyType.Returns("policy2");

        evaluator1.EvaluateAsync(Arg.Any<OctokitRepository>()).Returns((PolicyViolation?)null);
        evaluator2.EvaluateAsync(Arg.Any<OctokitRepository>()).Returns((PolicyViolation?)null);

        var evaluators = new List<IPolicyEvaluator> { evaluator1, evaluator2 };
        var sut = new PolicyEvaluationService(evaluators);

        var repository = CreateMockRepository();
        var policies = new List<PolicyConfig>
        {
            new PolicyConfig { Type = "policy1" },
            new PolicyConfig { Type = "policy2" }
        };

        // Act
        await sut.EvaluateRepositoryAsync(repository, policies);

        // Assert
        await evaluator1.Received(1).EvaluateAsync(repository);
        await evaluator2.Received(1).EvaluateAsync(repository);
    }

    /// <summary>
    /// Creates a real Octokit.Repository instance for testing using JSON deserialization and reflection
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

    /// <summary>
    /// Creates a mock evaluator with specified policy type and violation behavior
    /// </summary>
    private IPolicyEvaluator CreateMockEvaluator(string policyType, bool returnsViolation)
    {
        var evaluator = Substitute.For<IPolicyEvaluator>();
        evaluator.PolicyType.Returns(policyType);

        if (returnsViolation)
        {
            evaluator.EvaluateAsync(Arg.Any<OctokitRepository>())
                .Returns(new PolicyViolation { PolicyType = policyType });
        }
        else
        {
            evaluator.EvaluateAsync(Arg.Any<OctokitRepository>())
                .Returns((PolicyViolation?)null);
        }

        return evaluator;
    }
}

