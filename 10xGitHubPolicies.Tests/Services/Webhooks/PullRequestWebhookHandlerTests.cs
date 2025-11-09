using _10xGitHubPolicies.App.Data.Entities;
using _10xGitHubPolicies.App.Services.Action;
using _10xGitHubPolicies.App.Services.Configuration;
using _10xGitHubPolicies.App.Services.Configuration.Models;
using _10xGitHubPolicies.App.Services.GitHub;
using _10xGitHubPolicies.App.Services.Policies;
using _10xGitHubPolicies.App.Services.Webhooks;
using Bogus;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Octokit;
using Xunit;
using OctokitRepository = Octokit.Repository;

namespace _10xGitHubPolicies.Tests.Services.Webhooks;

[Trait("Category", "Unit")]
[Trait("Service", "PullRequestWebhookHandler")]
public class PullRequestWebhookHandlerTests
{
    private readonly IConfigurationService _configurationService;
    private readonly IPolicyEvaluationService _policyEvaluationService;
    private readonly IGitHubService _gitHubService;
    private readonly IActionService _actionService;
    private readonly ILogger<PullRequestWebhookHandler> _logger;
    private readonly PullRequestWebhookHandler _sut;
    private readonly Faker _faker;

    public PullRequestWebhookHandlerTests()
    {
        _configurationService = Substitute.For<IConfigurationService>();
        _policyEvaluationService = Substitute.For<IPolicyEvaluationService>();
        _gitHubService = Substitute.For<IGitHubService>();
        _actionService = Substitute.For<IActionService>();
        _logger = Substitute.For<ILogger<PullRequestWebhookHandler>>();
        _faker = new Faker();

        _sut = new PullRequestWebhookHandler(
            _configurationService,
            _policyEvaluationService,
            _gitHubService,
            _actionService,
            _logger);
    }

    [Fact]
    public async Task HandlePullRequestEventAsync_WhenViolationsExist_CommentsAndBlocksPR()
    {
        // Arrange
        var repositoryId = _faker.Random.Long(1000, 999999);
        var prNumber = _faker.Random.Int(1, 100);
        var headSha = _faker.Random.AlphaNumeric(40);
        var payload = CreatePullRequestWebhookPayload(repositoryId, prNumber, headSha, "opened");

        var repository = CreateMockRepository(repositoryId);
        _gitHubService.GetRepositorySettingsAsync(repositoryId).Returns(repository);

        var policyConfig = new PolicyConfig
        {
            Type = "test-policy",
            Name = "Test Policy",
            Actions = new List<string> { "comment-on-prs", "block-prs" }
        };

        var config = new AppConfig
        {
            AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
            Policies = new List<PolicyConfig> { policyConfig }
        };
        _configurationService.GetConfigAsync().Returns(config);

        var violations = new List<PolicyViolation>
        {
            new PolicyViolation { PolicyType = "test-policy" }
        };
        _policyEvaluationService.EvaluateRepositoryAsync(repository, config.Policies).Returns(violations);

        // Act
        await _sut.HandlePullRequestEventAsync("pull_request", "opened", payload, "delivery-123");

        // Assert
        await _actionService.Received(1).CommentOnPullRequestAsync(
            repositoryId,
            prNumber,
            policyConfig,
            Arg.Is<List<PolicyViolation>>(v => v.Count == 1 && v[0].PolicyType == "test-policy"));

        await _actionService.Received(1).UpdatePullRequestStatusCheckAsync(
            repositoryId,
            headSha,
            Arg.Is<List<PolicyViolation>>(v => v.Count == 1 && v[0].PolicyType == "test-policy"),
            policyConfig);
    }

    [Fact]
    public async Task HandlePullRequestEventAsync_WhenNoViolations_DoesNotCommentOrBlock()
    {
        // Arrange
        var repositoryId = _faker.Random.Long(1000, 999999);
        var prNumber = _faker.Random.Int(1, 100);
        var headSha = _faker.Random.AlphaNumeric(40);
        var payload = CreatePullRequestWebhookPayload(repositoryId, prNumber, headSha, "opened");

        var repository = CreateMockRepository(repositoryId);
        _gitHubService.GetRepositorySettingsAsync(repositoryId).Returns(repository);

        var policyConfig = new PolicyConfig
        {
            Type = "test-policy",
            Name = "Test Policy",
            Actions = new List<string> { "comment-on-prs", "block-prs" }
        };

        var config = new AppConfig
        {
            AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
            Policies = new List<PolicyConfig> { policyConfig }
        };
        _configurationService.GetConfigAsync().Returns(config);

        var violations = new List<PolicyViolation>(); // No violations
        _policyEvaluationService.EvaluateRepositoryAsync(repository, config.Policies).Returns(violations);

        // Act
        await _sut.HandlePullRequestEventAsync("pull_request", "opened", payload, "delivery-123");

        // Assert
        // Should not comment when there are no violations
        await _actionService.DidNotReceive().CommentOnPullRequestAsync(
            Arg.Any<long>(),
            Arg.Any<int>(),
            Arg.Any<PolicyConfig>(),
            Arg.Any<List<PolicyViolation>>());

        // Should update status check to success when there are no violations (to unblock PR if previously blocked)
        await _actionService.Received(1).UpdatePullRequestStatusCheckAsync(
            repositoryId,
            headSha,
            Arg.Is<List<PolicyViolation>>(v => v.Count == 0),
            policyConfig);
    }

    [Fact]
    public async Task HandlePullRequestEventAsync_WhenViolationsFixed_UpdatesStatusCheckToSuccess()
    {
        // Arrange
        var repositoryId = _faker.Random.Long(1000, 999999);
        var prNumber = _faker.Random.Int(1, 100);
        var headSha = _faker.Random.AlphaNumeric(40);
        var payload = CreatePullRequestWebhookPayload(repositoryId, prNumber, headSha, "synchronize");

        var repository = CreateMockRepository(repositoryId);
        _gitHubService.GetRepositorySettingsAsync(repositoryId).Returns(repository);

        var policyConfig = new PolicyConfig
        {
            Type = "test-policy",
            Name = "Test Policy",
            Actions = new List<string> { "block-prs" }
        };

        var config = new AppConfig
        {
            AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
            Policies = new List<PolicyConfig> { policyConfig }
        };
        _configurationService.GetConfigAsync().Returns(config);

        var violations = new List<PolicyViolation>(); // No violations (fixed)
        _policyEvaluationService.EvaluateRepositoryAsync(repository, config.Policies).Returns(violations);

        // Act
        await _sut.HandlePullRequestEventAsync("pull_request", "synchronize", payload, "delivery-456");

        // Assert - Status check should be updated to success (no violations)
        await _actionService.Received(1).UpdatePullRequestStatusCheckAsync(
            repositoryId,
            headSha,
            Arg.Is<List<PolicyViolation>>(v => v.Count == 0),
            policyConfig);
    }

    [Fact]
    public async Task HandlePullRequestEventAsync_WithMultiplePolicies_ProcessesAllActions()
    {
        // Arrange
        var repositoryId = _faker.Random.Long(1000, 999999);
        var prNumber = _faker.Random.Int(1, 100);
        var headSha = _faker.Random.AlphaNumeric(40);
        var payload = CreatePullRequestWebhookPayload(repositoryId, prNumber, headSha, "opened");

        var repository = CreateMockRepository(repositoryId);
        _gitHubService.GetRepositorySettingsAsync(repositoryId).Returns(repository);

        var policyConfig1 = new PolicyConfig
        {
            Type = "policy-1",
            Name = "Policy 1",
            Actions = new List<string> { "comment-on-prs" }
        };

        var policyConfig2 = new PolicyConfig
        {
            Type = "policy-2",
            Name = "Policy 2",
            Actions = new List<string> { "block-prs" }
        };

        var config = new AppConfig
        {
            AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
            Policies = new List<PolicyConfig> { policyConfig1, policyConfig2 }
        };
        _configurationService.GetConfigAsync().Returns(config);

        var violations = new List<PolicyViolation>
        {
            new PolicyViolation { PolicyType = "policy-1" },
            new PolicyViolation { PolicyType = "policy-2" }
        };
        _policyEvaluationService.EvaluateRepositoryAsync(repository, config.Policies).Returns(violations);

        // Act
        await _sut.HandlePullRequestEventAsync("pull_request", "opened", payload, "delivery-789");

        // Assert
        await _actionService.Received(1).CommentOnPullRequestAsync(
            repositoryId,
            prNumber,
            policyConfig1,
            Arg.Is<List<PolicyViolation>>(v => v.Count == 1 && v[0].PolicyType == "policy-1"));

        await _actionService.Received(1).UpdatePullRequestStatusCheckAsync(
            repositoryId,
            headSha,
            Arg.Is<List<PolicyViolation>>(v => v.Count == 1 && v[0].PolicyType == "policy-2"),
            policyConfig2);
    }

    [Fact]
    public async Task HandlePullRequestEventAsync_WhenInvalidPayload_LogsErrorAndReturns()
    {
        // Arrange
        var invalidPayload = "{ invalid json }";

        // Act
        await _sut.HandlePullRequestEventAsync("pull_request", "opened", invalidPayload, "delivery-123");

        // Assert
        await _gitHubService.DidNotReceive().GetRepositorySettingsAsync(Arg.Any<long>());
        await _actionService.DidNotReceive().CommentOnPullRequestAsync(
            Arg.Any<long>(),
            Arg.Any<int>(),
            Arg.Any<PolicyConfig>(),
            Arg.Any<List<PolicyViolation>>());
    }

    [Fact]
    public async Task HandlePullRequestEventAsync_WhenMissingPullRequestProperty_LogsWarningAndReturns()
    {
        // Arrange
        var payload = """{"repository": {"id": 12345}}""";

        // Act
        await _sut.HandlePullRequestEventAsync("pull_request", "opened", payload, "delivery-123");

        // Assert
        await _gitHubService.DidNotReceive().GetRepositorySettingsAsync(Arg.Any<long>());
        await _actionService.DidNotReceive().CommentOnPullRequestAsync(
            Arg.Any<long>(),
            Arg.Any<int>(),
            Arg.Any<PolicyConfig>(),
            Arg.Any<List<PolicyViolation>>());
    }

    [Fact]
    public async Task HandlePullRequestEventAsync_WhenMissingRepositoryProperty_LogsWarningAndReturns()
    {
        // Arrange
        var payload = """{"pull_request": {"number": 1}}""";

        // Act
        await _sut.HandlePullRequestEventAsync("pull_request", "opened", payload, "delivery-123");

        // Assert
        await _gitHubService.DidNotReceive().GetRepositorySettingsAsync(Arg.Any<long>());
        await _actionService.DidNotReceive().CommentOnPullRequestAsync(
            Arg.Any<long>(),
            Arg.Any<int>(),
            Arg.Any<PolicyConfig>(),
            Arg.Any<List<PolicyViolation>>());
    }

    [Fact]
    public async Task HandlePullRequestEventAsync_WhenActionFails_ContinuesProcessingOtherActions()
    {
        // Arrange
        var repositoryId = _faker.Random.Long(1000, 999999);
        var prNumber = _faker.Random.Int(1, 100);
        var headSha = _faker.Random.AlphaNumeric(40);
        var payload = CreatePullRequestWebhookPayload(repositoryId, prNumber, headSha, "opened");

        var repository = CreateMockRepository(repositoryId);
        _gitHubService.GetRepositorySettingsAsync(repositoryId).Returns(repository);

        var policyConfig = new PolicyConfig
        {
            Type = "test-policy",
            Name = "Test Policy",
            Actions = new List<string> { "comment-on-prs", "block-prs" }
        };

        var config = new AppConfig
        {
            AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
            Policies = new List<PolicyConfig> { policyConfig }
        };
        _configurationService.GetConfigAsync().Returns(config);

        var violations = new List<PolicyViolation>
        {
            new PolicyViolation { PolicyType = "test-policy" }
        };
        _policyEvaluationService.EvaluateRepositoryAsync(repository, config.Policies).Returns(violations);

        // Make comment action fail
        _actionService.CommentOnPullRequestAsync(
            Arg.Any<long>(),
            Arg.Any<int>(),
            Arg.Any<PolicyConfig>(),
            Arg.Any<List<PolicyViolation>>())
            .Throws(new Exception("Comment failed"));

        // Act
        await _sut.HandlePullRequestEventAsync("pull_request", "opened", payload, "delivery-123");

        // Assert - Block action should still be called even though comment failed
        await _actionService.Received(1).UpdatePullRequestStatusCheckAsync(
            repositoryId,
            headSha,
            Arg.Any<List<PolicyViolation>>(),
            policyConfig);
    }

    // Helper Methods

    private static string CreatePullRequestWebhookPayload(long repositoryId, int prNumber, string headSha, string action)
    {
        return $$"""
        {
          "action": "{{action}}",
          "pull_request": {
            "number": {{prNumber}},
            "head": {
              "sha": "{{headSha}}"
            }
          },
          "repository": {
            "id": {{repositoryId}}
          }
        }
        """;
    }

    private OctokitRepository CreateMockRepository(long id)
    {
        var json = $$"""
        {
            "id": {{id}},
            "node_id": "R_{{id}}",
            "name": "test-repo",
            "full_name": "owner/test-repo",
            "private": false,
            "archived": false,
            "owner": {
                "login": "owner",
                "id": 1,
                "node_id": "U_1",
                "avatar_url": "",
                "url": "https://api.github.com/users/owner",
                "html_url": "https://github.com/owner",
                "type": "User"
            },
            "html_url": "https://github.com/owner/test-repo",
            "description": "Test repository",
            "fork": false,
            "url": "https://api.github.com/repos/owner/test-repo",
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

        return Newtonsoft.Json.JsonConvert.DeserializeObject<OctokitRepository>(json)!;
    }
}

