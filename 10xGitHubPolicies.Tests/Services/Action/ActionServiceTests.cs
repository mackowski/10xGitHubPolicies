using _10xGitHubPolicies.App.Data;
using _10xGitHubPolicies.App.Data.Entities;
using _10xGitHubPolicies.App.Services.Action;
using _10xGitHubPolicies.App.Services.Configuration;
using _10xGitHubPolicies.App.Services.Configuration.Models;
using _10xGitHubPolicies.App.Services.GitHub;
using Bogus;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Octokit;
using Xunit;

namespace _10xGitHubPolicies.Tests.Services.Action;

[Trait("Category", "Unit")]
[Trait("Service", "ActionService")]
public class ActionServiceTests : IAsyncLifetime
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IGitHubService _gitHubService;
    private readonly IConfigurationService _configService;
    private readonly ILogger<ActionService> _logger;
    private readonly ActionService _sut;
    private readonly Faker _faker;

    public ActionServiceTests()
    {
        // Arrange - Create in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ApplicationDbContext(options);

        // Arrange - Create mocks
        _gitHubService = Substitute.For<IGitHubService>();
        _configService = Substitute.For<IConfigurationService>();
        _logger = Substitute.For<ILogger<ActionService>>();
        _faker = new Faker();

        // Create system under test
        _sut = new ActionService(_dbContext, _gitHubService, _configService, _logger);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task ProcessActionsForScanAsync_WhenNoViolations_CompletesSuccessfully()
    {
        // Arrange
        var scanId = _faker.Random.Int(1, 1000);
        // No violations in database

        // Act
        var act = async () => await _sut.ProcessActionsForScanAsync(scanId);

        // Assert
        await act.Should().NotThrowAsync(because: "empty violations should be handled gracefully");

        // Verify no GitHub API calls were made
        await _gitHubService.DidNotReceive().CreateIssueAsync(
            Arg.Any<long>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IEnumerable<string>>());
        await _gitHubService.DidNotReceive().ArchiveRepositoryAsync(Arg.Any<long>());

        // Verify appropriate log message was logged (use ReceivedWithAnyArgs for structured logging)
        _logger.ReceivedWithAnyArgs().LogInformation(default(string)!, default);
    }

    [Fact]
    public async Task ProcessActionsForScanAsync_WhenCreateIssueAction_CreatesIssueSuccessfully()
    {
        // Arrange
        var (scanId, violation, policyConfig) = await SetupViolationWithActionAsync("create-issue");

        var mockIssue = CreateMockIssue();
        _gitHubService.CreateIssueAsync(
            violation.Repository.GitHubRepositoryId,
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IEnumerable<string>>())
            .Returns(mockIssue);

        _gitHubService.GetOpenIssuesAsync(
            violation.Repository.GitHubRepositoryId,
            Arg.Any<string>())
            .Returns(new List<Issue>());

        var config = new AppConfig
        {
            AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
            Policies = new List<PolicyConfig> { policyConfig }
        };
        _configService.GetConfigAsync().Returns(config);

        // Act
        await _sut.ProcessActionsForScanAsync(scanId);

        // Assert
        await _gitHubService.Received(1).CreateIssueAsync(
            violation.Repository.GitHubRepositoryId,
            policyConfig.IssueDetails!.Title,
            Arg.Any<string>(),
            Arg.Any<IEnumerable<string>>());

        // Verify action log was created
        var actionLog = await _dbContext.ActionsLogs.FirstOrDefaultAsync();
        actionLog.Should().NotBeNull();
        actionLog!.ActionType.Should().Be("create-issue");
        actionLog.Status.Should().Be("Success");
        actionLog.Details.Should().Contain($"#{mockIssue.Number}");
    }

    [Fact]
    public async Task ProcessActionsForScanAsync_WhenDuplicateIssueExists_SkipsCreation()
    {
        // Arrange
        var (scanId, violation, policyConfig) = await SetupViolationWithActionAsync("create-issue");

        var expectedTitle = policyConfig.IssueDetails?.Title ?? $"Compliance Violation: {violation.Policy.PolicyKey}";
        var primaryLabel = policyConfig.IssueDetails?.Labels?.FirstOrDefault() ?? "policy-violation";

        // Mock existing issue with same title
        var existingIssue = CreateMockIssue(title: expectedTitle);
        _gitHubService.GetOpenIssuesAsync(
            violation.Repository.GitHubRepositoryId,
            primaryLabel)
            .Returns(new List<Issue> { existingIssue });

        var config = new AppConfig
        {
            AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
            Policies = new List<PolicyConfig> { policyConfig }
        };
        _configService.GetConfigAsync().Returns(config);

        // Act
        await _sut.ProcessActionsForScanAsync(scanId);

        // Assert - No new issue created
        await _gitHubService.DidNotReceive().CreateIssueAsync(
            Arg.Any<long>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IEnumerable<string>>());

        // Verify action logged as "Skipped"
        var actionLog = await _dbContext.ActionsLogs.FirstOrDefaultAsync();
        actionLog.Should().NotBeNull();
        actionLog!.Status.Should().Be("Skipped");
        actionLog.Details.Should().Contain(existingIssue.HtmlUrl);
    }

    [Fact]
    public async Task ProcessActionsForScanAsync_WhenArchiveRepoAction_ArchivesRepository()
    {
        // Arrange
        var (scanId, violation, policyConfig) = await SetupViolationWithActionAsync("archive-repo");

        _gitHubService.ArchiveRepositoryAsync(violation.Repository.GitHubRepositoryId)
            .Returns(Task.CompletedTask);

        var config = new AppConfig
        {
            AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
            Policies = new List<PolicyConfig> { policyConfig }
        };
        _configService.GetConfigAsync().Returns(config);

        // Act
        await _sut.ProcessActionsForScanAsync(scanId);

        // Assert
        await _gitHubService.Received(1).ArchiveRepositoryAsync(violation.Repository.GitHubRepositoryId);

        // Verify action log
        var actionLog = await _dbContext.ActionsLogs.FirstOrDefaultAsync();
        actionLog.Should().NotBeNull();
        actionLog!.ActionType.Should().Be("archive-repo");
        actionLog.Status.Should().Be("Success");
    }

    [Fact]
    public async Task ProcessActionsForScanAsync_WhenLogOnlyAction_LogsWithoutActions()
    {
        // Arrange
        var (scanId, violation, policyConfig) = await SetupViolationWithActionAsync("log-only");

        var config = new AppConfig
        {
            AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
            Policies = new List<PolicyConfig> { policyConfig }
        };
        _configService.GetConfigAsync().Returns(config);

        // Act
        await _sut.ProcessActionsForScanAsync(scanId);

        // Assert - No GitHub API calls
        await _gitHubService.DidNotReceive().CreateIssueAsync(
            Arg.Any<long>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IEnumerable<string>>());
        await _gitHubService.DidNotReceive().ArchiveRepositoryAsync(Arg.Any<long>());

        // Verify action logged
        var actionLog = await _dbContext.ActionsLogs.FirstOrDefaultAsync();
        actionLog.Should().NotBeNull();
        actionLog!.ActionType.Should().Be("log-only");
        actionLog.Status.Should().Be("Success");
    }

    [Fact]
    public async Task ProcessActionsForScanAsync_WhenUnknownAction_LogsWarningAndContinues()
    {
        // Arrange
        var (scanId, violation, policyConfig) = await SetupViolationWithActionAsync("unknown-action");

        var config = new AppConfig
        {
            AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
            Policies = new List<PolicyConfig> { policyConfig }
        };
        _configService.GetConfigAsync().Returns(config);

        // Act
        var act = async () => await _sut.ProcessActionsForScanAsync(scanId);

        // Assert
        await act.Should().NotThrowAsync(because: "unknown actions should be handled gracefully");

        // Verify warning was logged (use ReceivedWithAnyArgs for structured logging)
        _logger.ReceivedWithAnyArgs().LogWarning(default(string), default, default);

        // Verify no GitHub API calls
        await _gitHubService.DidNotReceive().CreateIssueAsync(
            Arg.Any<long>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IEnumerable<string>>());
        await _gitHubService.DidNotReceive().ArchiveRepositoryAsync(Arg.Any<long>());
    }

    [Fact]
    public async Task ProcessActionsForScanAsync_WhenPolicyConfigMissing_SkipsViolation()
    {
        // Arrange
        var (scanId, violation, _) = await SetupViolationWithActionAsync("create-issue");

        // Config with no policies
        var config = new AppConfig
        {
            AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
            Policies = new List<PolicyConfig>() // Empty
        };
        _configService.GetConfigAsync().Returns(config);

        // Act
        await _sut.ProcessActionsForScanAsync(scanId);

        // Assert
        // Verify warning was logged (use ReceivedWithAnyArgs for structured logging)
        _logger.ReceivedWithAnyArgs().LogWarning(default(string), default, default);

        // Verify no actions executed
        await _gitHubService.DidNotReceive().CreateIssueAsync(
            Arg.Any<long>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IEnumerable<string>>());

        // Verify no action log created
        var actionLogs = await _dbContext.ActionsLogs.ToListAsync();
        actionLogs.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessActionsForScanAsync_WhenCreateIssueFails_LogsErrorAndContinues()
    {
        // Arrange
        var (scanId, violation, policyConfig) = await SetupViolationWithActionAsync("create-issue");

        _gitHubService.GetOpenIssuesAsync(Arg.Any<long>(), Arg.Any<string>())
            .Returns(new List<Issue>());

        _gitHubService.CreateIssueAsync(
            Arg.Any<long>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IEnumerable<string>>())
            .Throws(new ApiException("GitHub API error", System.Net.HttpStatusCode.InternalServerError));

        var config = new AppConfig
        {
            AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
            Policies = new List<PolicyConfig> { policyConfig }
        };
        _configService.GetConfigAsync().Returns(config);

        // Act
        var act = async () => await _sut.ProcessActionsForScanAsync(scanId);

        // Assert - Should not throw
        await act.Should().NotThrowAsync(because: "individual action failures should be isolated");

        // Verify error was logged - we can't verify exact exception match, just that LogError was called
        _logger.ReceivedWithAnyArgs().LogError(
            default(Exception),
            default(string),
            default,
            default);

        // Verify action logged as "Failed"
        var actionLog = await _dbContext.ActionsLogs.FirstOrDefaultAsync();
        actionLog.Should().NotBeNull();
        actionLog!.Status.Should().Be("Failed");
        actionLog.Details.Should().Contain("Exception");
    }

    [Fact]
    public async Task ProcessActionsForScanAsync_WhenArchiveFails_LogsErrorAndContinues()
    {
        // Arrange
        var (scanId, violation, policyConfig) = await SetupViolationWithActionAsync("archive-repo");

        _gitHubService.ArchiveRepositoryAsync(Arg.Any<long>())
            .Throws(new ApiException("Insufficient permissions", System.Net.HttpStatusCode.Forbidden));

        var config = new AppConfig
        {
            AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
            Policies = new List<PolicyConfig> { policyConfig }
        };
        _configService.GetConfigAsync().Returns(config);

        // Act
        var act = async () => await _sut.ProcessActionsForScanAsync(scanId);

        // Assert
        await act.Should().NotThrowAsync(because: "individual action failures should be isolated");

        // Verify error logged - we can't verify exact exception match, just that LogError was called
        _logger.ReceivedWithAnyArgs().LogError(
            default(Exception),
            default(string),
            default,
            default,
            default);

        // Verify action logged as "Failed"
        var actionLog = await _dbContext.ActionsLogs.FirstOrDefaultAsync();
        actionLog.Should().NotBeNull();
        actionLog!.Status.Should().Be("Failed");
        actionLog.Details.Should().Contain("Exception");
    }

    [Fact]
    public async Task ProcessActionsForScanAsync_WhenOneActionFails_OthersStillProcess()
    {
        // Arrange - Create 3 violations
        var scanId = _faker.Random.Int(1, 1000);
        var scan = new Scan { ScanId = scanId, StartedAt = DateTime.UtcNow, Status = "InProgress" };
        await _dbContext.Scans.AddAsync(scan);

        var policy = new Policy 
        { 
            PolicyKey = "test-policy", 
            Description = "Test Policy",
            Action = "create-issue"
        };
        await _dbContext.Policies.AddAsync(policy);

        var repo1 = CreateRepository();
        var repo2 = CreateRepository();
        var repo3 = CreateRepository();
        await _dbContext.Repositories.AddRangeAsync(repo1, repo2, repo3);

        var violation1 = CreateViolation(scanId, policy.PolicyId, repo1.RepositoryId);
        var violation2 = CreateViolation(scanId, policy.PolicyId, repo2.RepositoryId);
        var violation3 = CreateViolation(scanId, policy.PolicyId, repo3.RepositoryId);
        await _dbContext.PolicyViolations.AddRangeAsync(violation1, violation2, violation3);
        await _dbContext.SaveChangesAsync();

        var policyConfig = new PolicyConfig
        {
            Type = "test-policy",
            Name = "Test Policy",
            Action = "create-issue",
            IssueDetails = new IssueDetails
            {
                Title = "Test Issue",
                Body = "Test body",
                Labels = new List<string> { "test" }
            }
        };

        var config = new AppConfig
        {
            AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
            Policies = new List<PolicyConfig> { policyConfig }
        };
        _configService.GetConfigAsync().Returns(config);

        // Mock: First succeeds, second fails, third succeeds
        _gitHubService.GetOpenIssuesAsync(Arg.Any<long>(), Arg.Any<string>())
            .Returns(new List<Issue>());

        _gitHubService.CreateIssueAsync(repo1.GitHubRepositoryId, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>())
            .Returns(CreateMockIssue(number: 1));

        _gitHubService.CreateIssueAsync(repo2.GitHubRepositoryId, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>())
            .Throws(new ApiException("API Error", System.Net.HttpStatusCode.InternalServerError));

        _gitHubService.CreateIssueAsync(repo3.GitHubRepositoryId, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>())
            .Returns(CreateMockIssue(number: 3));

        // Act
        await _sut.ProcessActionsForScanAsync(scanId);

        // Assert - All 3 GitHub API calls attempted
        await _gitHubService.Received(1).CreateIssueAsync(repo1.GitHubRepositoryId, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>());
        await _gitHubService.Received(1).CreateIssueAsync(repo2.GitHubRepositoryId, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>());
        await _gitHubService.Received(1).CreateIssueAsync(repo3.GitHubRepositoryId, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>());

        // Verify 3 action logs created (2 Success, 1 Failed)
        var actionLogs = await _dbContext.ActionsLogs.ToListAsync();
        actionLogs.Should().HaveCount(3);
        actionLogs.Count(a => a.Status == "Success").Should().Be(2);
        actionLogs.Count(a => a.Status == "Failed").Should().Be(1);
    }

    [Theory]
    [InlineData("create-issue")]
    [InlineData("create_issue")]
    [InlineData("archive-repo")]
    [InlineData("archive_repo")]
    [InlineData("log-only")]
    [InlineData("log_only")]
    public async Task ProcessActionsForScanAsync_WhenActionTypeVariations_HandlesCorrectly(string actionType)
    {
        // Arrange
        var (scanId, violation, policyConfig) = await SetupViolationWithActionAsync(actionType);

        if (actionType.StartsWith("create"))
        {
            _gitHubService.GetOpenIssuesAsync(Arg.Any<long>(), Arg.Any<string>())
                .Returns(new List<Issue>());
            _gitHubService.CreateIssueAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>())
                .Returns(CreateMockIssue());
        }
        else if (actionType.StartsWith("archive"))
        {
            _gitHubService.ArchiveRepositoryAsync(Arg.Any<long>())
                .Returns(Task.CompletedTask);
        }

        var config = new AppConfig
        {
            AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
            Policies = new List<PolicyConfig> { policyConfig }
        };
        _configService.GetConfigAsync().Returns(config);

        // Act
        var act = async () => await _sut.ProcessActionsForScanAsync(scanId);

        // Assert
        await act.Should().NotThrowAsync(because: "both underscore and hyphen formats should work");

        // Verify action log created
        var actionLog = await _dbContext.ActionsLogs.FirstOrDefaultAsync();
        actionLog.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessActionsForScanAsync_WhenCustomIssueDetails_UsesConfiguredValues()
    {
        // Arrange
        var customTitle = _faker.Lorem.Sentence();
        var customBody = _faker.Lorem.Paragraph();
        var customLabels = new List<string> { "custom-label-1", "custom-label-2" };

        var policyConfig = new PolicyConfig
        {
            Type = "test-policy",
            Name = "Test Policy",
            Action = "create-issue",
            IssueDetails = new IssueDetails
            {
                Title = customTitle,
                Body = customBody,
                Labels = customLabels
            }
        };

        var (scanId, violation, _) = await SetupViolationWithActionAsync("create-issue");

        _gitHubService.GetOpenIssuesAsync(Arg.Any<long>(), Arg.Any<string>())
            .Returns(new List<Issue>());

        _gitHubService.CreateIssueAsync(
            violation.Repository.GitHubRepositoryId,
            customTitle,
            customBody,
            Arg.Is<IEnumerable<string>>(l => l.SequenceEqual(customLabels)))
            .Returns(CreateMockIssue());

        var config = new AppConfig
        {
            AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
            Policies = new List<PolicyConfig> { policyConfig }
        };
        _configService.GetConfigAsync().Returns(config);

        // Act
        await _sut.ProcessActionsForScanAsync(scanId);

        // Assert
        await _gitHubService.Received(1).CreateIssueAsync(
            violation.Repository.GitHubRepositoryId,
            customTitle,
            customBody,
            Arg.Is<IEnumerable<string>>(l => l.SequenceEqual(customLabels)));
    }

    [Fact]
    public async Task ProcessActionsForScanAsync_WhenNoIssueDetails_UsesDefaults()
    {
        // Arrange
        var policyConfig = new PolicyConfig
        {
            Type = "test-policy",
            Name = "Test Policy",
            Action = "create-issue",
            IssueDetails = null // No custom details
        };

        var (scanId, violation, _) = await SetupViolationWithActionAsync("create-issue");

        _gitHubService.GetOpenIssuesAsync(Arg.Any<long>(), Arg.Any<string>())
            .Returns(new List<Issue>());

        _gitHubService.CreateIssueAsync(
            Arg.Any<long>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IEnumerable<string>>())
            .Returns(CreateMockIssue());

        var config = new AppConfig
        {
            AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
            Policies = new List<PolicyConfig> { policyConfig }
        };
        _configService.GetConfigAsync().Returns(config);

        // Act
        await _sut.ProcessActionsForScanAsync(scanId);

        // Assert - Verify default values used
        await _gitHubService.Received(1).CreateIssueAsync(
            violation.Repository.GitHubRepositoryId,
            Arg.Is<string>(t => t.Contains("Compliance Violation") && t.Contains(violation.Policy.PolicyKey)),
            Arg.Is<string>(b => b.Contains("violates") && b.Contains(violation.Policy.PolicyKey)),
            Arg.Is<IEnumerable<string>>(l => l.Contains("policy-violation") && l.Contains("compliance")));
    }

    // Helper Methods

    /// <summary>
    /// Creates a test violation with associated scan, policy, and repository
    /// </summary>
    private async Task<(int scanId, PolicyViolation violation, PolicyConfig policyConfig)>
        SetupViolationWithActionAsync(string actionType)
    {
        var scanId = _faker.Random.Int(1, 1000);
        var scan = new Scan { ScanId = scanId, StartedAt = DateTime.UtcNow, Status = "InProgress" };
        await _dbContext.Scans.AddAsync(scan);

        var policy = new Policy 
        { 
            PolicyKey = "test-policy", 
            Description = "Test Policy",
            Action = actionType
        };
        await _dbContext.Policies.AddAsync(policy);

        var repository = CreateRepository();
        await _dbContext.Repositories.AddAsync(repository);

        var violation = CreateViolation(scanId, policy.PolicyId, repository.RepositoryId);
        await _dbContext.PolicyViolations.AddAsync(violation);

        await _dbContext.SaveChangesAsync();

        var policyConfig = new PolicyConfig
        {
            Type = "test-policy",
            Name = "Test Policy",
            Action = actionType,
            IssueDetails = new IssueDetails
            {
                Title = _faker.Lorem.Sentence(),
                Body = _faker.Lorem.Paragraph(),
                Labels = new List<string> { "policy-violation", "test" }
            }
        };

        return (scanId, violation, policyConfig);
    }

    /// <summary>
    /// Creates a test repository entity
    /// </summary>
    private _10xGitHubPolicies.App.Data.Entities.Repository CreateRepository()
    {
        return new _10xGitHubPolicies.App.Data.Entities.Repository
        {
            GitHubRepositoryId = _faker.Random.Long(1000, 999999),
            Name = _faker.Company.CompanyName(),
            ComplianceStatus = "Unknown",
            LastScannedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a test violation entity
    /// </summary>
    private PolicyViolation CreateViolation(int scanId, int policyId, int repositoryId)
    {
        return new PolicyViolation
        {
            ScanId = scanId,
            PolicyId = policyId,
            RepositoryId = repositoryId
        };
    }

    /// <summary>
    /// Creates a mock Octokit.Issue for testing
    /// </summary>
    private Issue CreateMockIssue(int number = 1, string title = "Test Issue")
    {
        var issue = new Issue(
            url: $"https://api.github.com/repos/test/repo/issues/{number}",
            htmlUrl: $"https://github.com/test/repo/issues/{number}",
            commentsUrl: $"https://api.github.com/repos/test/repo/issues/{number}/comments",
            eventsUrl: $"https://api.github.com/repos/test/repo/issues/{number}/events",
            number: number,
            state: ItemState.Open,
            title: title,
            body: "Test body",
            closedBy: null,
            user: null,
            labels: null,
            assignee: null,
            assignees: null,
            milestone: null,
            comments: 0,
            pullRequest: null,
            closedAt: null,
            createdAt: DateTimeOffset.UtcNow,
            updatedAt: null,
            id: number,
            nodeId: $"issue_{number}",
            locked: false,
            repository: null,
            reactions: null,
            activeLockReason: null,
            stateReason: null);
        
        return issue;
    }
}

