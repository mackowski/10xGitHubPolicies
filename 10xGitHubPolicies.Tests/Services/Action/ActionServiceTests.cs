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
using OctokitRepository = Octokit.Repository;

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
        _logger.ReceivedWithAnyArgs().LogInformation(default(string)!, default!);
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

        var mockRepository = CreateMockRepository(archived: false);
        _gitHubService.GetRepositorySettingsAsync(violation.Repository.GitHubRepositoryId)
            .Returns(mockRepository);
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
        await _gitHubService.Received(1).GetRepositorySettingsAsync(violation.Repository.GitHubRepositoryId);
        await _gitHubService.Received(1).ArchiveRepositoryAsync(violation.Repository.GitHubRepositoryId);

        // Verify action log
        var actionLog = await _dbContext.ActionsLogs.FirstOrDefaultAsync();
        actionLog.Should().NotBeNull();
        actionLog!.ActionType.Should().Be("archive-repo");
        actionLog.Status.Should().Be("Success");
        actionLog.Details.Should().Contain(policyConfig.Name);
    }

    [Fact]
    public async Task ProcessActionsForScanAsync_WhenRepositoryAlreadyArchived_SkipsArchive()
    {
        // Arrange
        var (scanId, violation, policyConfig) = await SetupViolationWithActionAsync("archive-repo");

        var mockRepository = CreateMockRepository(archived: true);
        _gitHubService.GetRepositorySettingsAsync(violation.Repository.GitHubRepositoryId)
            .Returns(mockRepository);

        var config = new AppConfig
        {
            AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
            Policies = new List<PolicyConfig> { policyConfig }
        };
        _configService.GetConfigAsync().Returns(config);

        // Act
        await _sut.ProcessActionsForScanAsync(scanId);

        // Assert - GetRepositorySettingsAsync called, but ArchiveRepositoryAsync should not be called
        await _gitHubService.Received(1).GetRepositorySettingsAsync(violation.Repository.GitHubRepositoryId);
        await _gitHubService.DidNotReceive().ArchiveRepositoryAsync(Arg.Any<long>());

        // Verify action logged as "Skipped"
        var actionLog = await _dbContext.ActionsLogs.FirstOrDefaultAsync();
        actionLog.Should().NotBeNull();
        actionLog!.ActionType.Should().Be("archive-repo");
        actionLog.Status.Should().Be("Skipped");
        actionLog.Details.Should().Contain("already archived");
    }

    [Fact]
    public async Task ProcessActionsForScanAsync_WhenArchiveFailsWithNotFoundException_LogsErrorAndContinues()
    {
        // Arrange
        var (scanId, violation, policyConfig) = await SetupViolationWithActionAsync("archive-repo");

        _gitHubService.GetRepositorySettingsAsync(violation.Repository.GitHubRepositoryId)
            .Throws(new NotFoundException("Repository not found", System.Net.HttpStatusCode.NotFound));

        var config = new AppConfig
        {
            AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
            Policies = new List<PolicyConfig> { policyConfig }
        };
        _configService.GetConfigAsync().Returns(config);

        // Act
        var act = async () => await _sut.ProcessActionsForScanAsync(scanId);

        // Assert
        await act.Should().NotThrowAsync(because: "NotFoundException should be handled gracefully");

        // Verify warning was logged
        _logger.ReceivedWithAnyArgs().LogWarning(
            default(Exception),
            default(string),
            default,
            default,
            default);

        // Verify action logged as "Failed"
        var actionLog = await _dbContext.ActionsLogs.FirstOrDefaultAsync();
        actionLog.Should().NotBeNull();
        actionLog!.Status.Should().Be("Failed");
        actionLog.Details.Should().Contain("Repository not found");
    }

    [Fact]
    public async Task ProcessActionsForScanAsync_WhenArchiveFailsWithForbidden_LogsErrorAndContinues()
    {
        // Arrange
        var (scanId, violation, policyConfig) = await SetupViolationWithActionAsync("archive-repo");

        var mockRepository = CreateMockRepository(archived: false);
        _gitHubService.GetRepositorySettingsAsync(violation.Repository.GitHubRepositoryId)
            .Returns(mockRepository);
        _gitHubService.ArchiveRepositoryAsync(violation.Repository.GitHubRepositoryId)
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
        await act.Should().NotThrowAsync(because: "Forbidden exception should be handled gracefully");

        // Verify warning was logged
        _logger.ReceivedWithAnyArgs().LogWarning(
            default(Exception),
            default(string),
            default,
            default,
            default);

        // Verify action logged as "Failed"
        var actionLog = await _dbContext.ActionsLogs.FirstOrDefaultAsync();
        actionLog.Should().NotBeNull();
        actionLog!.Status.Should().Be("Failed");
        actionLog.Details.Should().Contain("Insufficient permissions");
    }

    [Fact]
    public async Task ProcessActionsForScanAsync_WhenArchiveActionFails_OtherActionsStillProcess()
    {
        // Arrange - Create 2 violations: one archive (fails), one create-issue (succeeds)
        var scanId = _faker.Random.Int(1, 1000);
        var scan = new Scan { ScanId = scanId, StartedAt = DateTime.UtcNow, Status = "InProgress" };
        await _dbContext.Scans.AddAsync(scan);

        var archivePolicy = new Policy
        {
            PolicyKey = "archive-policy",
            Description = "Archive Policy",
            Action = "archive-repo"
        };
        var issuePolicy = new Policy
        {
            PolicyKey = "issue-policy",
            Description = "Issue Policy",
            Action = "create-issue"
        };
        await _dbContext.Policies.AddRangeAsync(archivePolicy, issuePolicy);

        var repo1 = CreateRepository();
        var repo2 = CreateRepository();
        await _dbContext.Repositories.AddRangeAsync(repo1, repo2);

        var violation1 = CreateViolation(scanId, archivePolicy.PolicyId, repo1.RepositoryId);
        var violation2 = CreateViolation(scanId, issuePolicy.PolicyId, repo2.RepositoryId);
        await _dbContext.PolicyViolations.AddRangeAsync(violation1, violation2);
        await _dbContext.SaveChangesAsync();

        var archivePolicyConfig = new PolicyConfig
        {
            Type = "archive-policy",
            Name = "Archive Policy",
            Actions = new List<string> { "archive-repo" }
        };
        var issuePolicyConfig = new PolicyConfig
        {
            Type = "issue-policy",
            Name = "Issue Policy",
            Actions = new List<string> { "create-issue" },
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
            Policies = new List<PolicyConfig> { archivePolicyConfig, issuePolicyConfig }
        };
        _configService.GetConfigAsync().Returns(config);

        // Mock: Archive fails, issue succeeds
        var mockRepository = CreateMockRepository(archived: false);
        _gitHubService.GetRepositorySettingsAsync(repo1.GitHubRepositoryId)
            .Returns(mockRepository);
        _gitHubService.ArchiveRepositoryAsync(repo1.GitHubRepositoryId)
            .Throws(new ApiException("Archive failed", System.Net.HttpStatusCode.InternalServerError));

        _gitHubService.GetOpenIssuesAsync(repo2.GitHubRepositoryId, Arg.Any<string>())
            .Returns(new List<Issue>());
        _gitHubService.CreateIssueAsync(repo2.GitHubRepositoryId, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>())
            .Returns(CreateMockIssue());

        // Act
        await _sut.ProcessActionsForScanAsync(scanId);

        // Assert - Both actions attempted
        await _gitHubService.Received(1).ArchiveRepositoryAsync(repo1.GitHubRepositoryId);
        await _gitHubService.Received(1).CreateIssueAsync(repo2.GitHubRepositoryId, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>());

        // Verify 2 action logs created (1 Failed, 1 Success)
        var actionLogs = await _dbContext.ActionsLogs.ToListAsync();
        actionLogs.Should().HaveCount(2);
        actionLogs.Count(a => a.Status == "Failed").Should().Be(1);
        actionLogs.Count(a => a.Status == "Success").Should().Be(1);
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

        var mockRepository = CreateMockRepository(archived: false);
        _gitHubService.GetRepositorySettingsAsync(violation.Repository.GitHubRepositoryId)
            .Returns(mockRepository);
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

        // Verify warning logged (Forbidden exceptions are logged as warnings, not errors)
        _logger.ReceivedWithAnyArgs().LogWarning(
            default(Exception),
            default(string),
            default,
            default,
            default);

        // Verify action logged as "Failed"
        var actionLog = await _dbContext.ActionsLogs.FirstOrDefaultAsync();
        actionLog.Should().NotBeNull();
        actionLog!.Status.Should().Be("Failed");
        actionLog.Details.Should().Contain("Insufficient permissions");
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
            Actions = new List<string> { "create-issue" },
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
            var mockRepository = CreateMockRepository(archived: false);
            _gitHubService.GetRepositorySettingsAsync(Arg.Any<long>())
                .Returns(mockRepository);
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
            Actions = new List<string> { "create-issue" },
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
            Actions = new List<string> { "create-issue" },
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
            Actions = new List<string> { actionType },
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

    [Fact]
    public async Task ProcessActionsForScanAsync_WhenMultipleActions_ExecutesAllActions()
    {
        // Arrange
        var (scanId, violation, _) = await SetupViolationWithActionAsync("create-issue");

        var policyConfig = new PolicyConfig
        {
            Type = "test-policy",
            Name = "Test Policy",
            Actions = new List<string> { "create-issue", "log-only" },
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

        _gitHubService.GetOpenIssuesAsync(violation.Repository.GitHubRepositoryId, Arg.Any<string>())
            .Returns(new List<Issue>());
        _gitHubService.CreateIssueAsync(
            violation.Repository.GitHubRepositoryId,
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IEnumerable<string>>())
            .Returns(CreateMockIssue());

        // Act
        await _sut.ProcessActionsForScanAsync(scanId);

        // Assert - Both actions should be executed
        await _gitHubService.Received(1).CreateIssueAsync(
            violation.Repository.GitHubRepositoryId,
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IEnumerable<string>>());

        // Verify 2 action logs created (one for each action)
        var actionLogs = await _dbContext.ActionsLogs.ToListAsync();
        actionLogs.Should().HaveCount(2);
        actionLogs.Should().Contain(a => a.ActionType == "create-issue" && a.Status == "Success");
        actionLogs.Should().Contain(a => a.ActionType == "log-only" && a.Status == "Success");
    }

    [Fact]
    public async Task ProcessActionsForScanAsync_WhenMultipleActions_OneFails_OthersContinue()
    {
        // Arrange
        var (scanId, violation, _) = await SetupViolationWithActionAsync("create-issue");

        var policyConfig = new PolicyConfig
        {
            Type = "test-policy",
            Name = "Test Policy",
            Actions = new List<string> { "create-issue", "archive-repo", "log-only" },
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

        // Mock: create-issue succeeds, archive-repo fails, log-only succeeds
        _gitHubService.GetOpenIssuesAsync(violation.Repository.GitHubRepositoryId, Arg.Any<string>())
            .Returns(new List<Issue>());
        _gitHubService.CreateIssueAsync(
            violation.Repository.GitHubRepositoryId,
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IEnumerable<string>>())
            .Returns(CreateMockIssue());

        var mockRepository = CreateMockRepository(archived: false);
        _gitHubService.GetRepositorySettingsAsync(violation.Repository.GitHubRepositoryId)
            .Returns(mockRepository);
        _gitHubService.ArchiveRepositoryAsync(violation.Repository.GitHubRepositoryId)
            .Throws(new ApiException("Archive failed", System.Net.HttpStatusCode.InternalServerError));

        // Act
        await _sut.ProcessActionsForScanAsync(scanId);

        // Assert - All 3 actions attempted
        await _gitHubService.Received(1).CreateIssueAsync(
            violation.Repository.GitHubRepositoryId,
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IEnumerable<string>>());
        await _gitHubService.Received(1).ArchiveRepositoryAsync(violation.Repository.GitHubRepositoryId);

        // Verify 3 action logs created (2 Success, 1 Failed)
        var actionLogs = await _dbContext.ActionsLogs.ToListAsync();
        actionLogs.Should().HaveCount(3);
        actionLogs.Should().Contain(a => a.ActionType == "create-issue" && a.Status == "Success");
        actionLogs.Should().Contain(a => a.ActionType == "archive-repo" && a.Status == "Failed");
        actionLogs.Should().Contain(a => a.ActionType == "log-only" && a.Status == "Success");
    }

    [Fact]
    public async Task ProcessActionsForScanAsync_WhenSingleAction_BackwardCompatible()
    {
        // Arrange - Test backward compatibility with single action
        var (scanId, violation, _) = await SetupViolationWithActionAsync("create-issue");

        var policyConfig = new PolicyConfig
        {
            Type = "test-policy",
            Name = "Test Policy",
            Actions = new List<string> { "create-issue" }, // Single action in list format
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

        _gitHubService.GetOpenIssuesAsync(violation.Repository.GitHubRepositoryId, Arg.Any<string>())
            .Returns(new List<Issue>());
        _gitHubService.CreateIssueAsync(
            violation.Repository.GitHubRepositoryId,
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IEnumerable<string>>())
            .Returns(CreateMockIssue());

        // Act
        await _sut.ProcessActionsForScanAsync(scanId);

        // Assert - Single action executed successfully
        await _gitHubService.Received(1).CreateIssueAsync(
            violation.Repository.GitHubRepositoryId,
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IEnumerable<string>>());

        var actionLogs = await _dbContext.ActionsLogs.ToListAsync();
        actionLogs.Should().HaveCount(1);
        actionLogs[0].ActionType.Should().Be("create-issue");
        actionLogs[0].Status.Should().Be("Success");
    }

    [Fact]
    public async Task ProcessActionsForScanAsync_WhenEmptyActionsList_HandlesGracefully()
    {
        // Arrange
        var (scanId, violation, _) = await SetupViolationWithActionAsync("create-issue");

        var policyConfig = new PolicyConfig
        {
            Type = "test-policy",
            Name = "Test Policy",
            Actions = new List<string>(), // Empty actions list
            IssueDetails = null
        };

        var config = new AppConfig
        {
            AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
            Policies = new List<PolicyConfig> { policyConfig }
        };
        _configService.GetConfigAsync().Returns(config);

        // Act
        await _sut.ProcessActionsForScanAsync(scanId);

        // Assert - No actions executed, no errors thrown
        await _gitHubService.DidNotReceive().CreateIssueAsync(
            Arg.Any<long>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IEnumerable<string>>());
        await _gitHubService.DidNotReceive().ArchiveRepositoryAsync(Arg.Any<long>());

        var actionLogs = await _dbContext.ActionsLogs.ToListAsync();
        actionLogs.Should().BeEmpty();
    }

    // PR Action Tests (Webhook Path)

    [Fact]
    public async Task CommentOnPullRequestAsync_WhenViolationsExist_CreatesCommentWithDefaultMessage()
    {
        // Arrange
        var repositoryId = _faker.Random.Long(1000, 999999);
        var pullRequestNumber = _faker.Random.Int(1, 100);
        var violation = await CreateViolationWithPolicyAsync();

        var policyConfig = new PolicyConfig
        {
            Type = "test-policy",
            Name = "Test Policy",
            Actions = new List<string> { "comment-on-pr" },
            PrCommentDetails = null // Use default message
        };

        var violations = new List<PolicyViolation> { violation };

        _gitHubService.GetPullRequestCommentsAsync(repositoryId, pullRequestNumber)
            .Returns(new List<IssueComment>());

        var mockComment = CreateMockIssueComment();
        _gitHubService.CreatePullRequestCommentAsync(repositoryId, pullRequestNumber, Arg.Any<string>())
            .Returns(mockComment);

        // Act
        await _sut.CommentOnPullRequestAsync(repositoryId, pullRequestNumber, policyConfig, violations);

        // Assert
        await _gitHubService.Received(1).GetPullRequestCommentsAsync(repositoryId, pullRequestNumber);
        await _gitHubService.Received(1).CreatePullRequestCommentAsync(
            repositoryId,
            pullRequestNumber,
            Arg.Is<string>(msg => msg.Contains("Policy Compliance Violations Detected") && msg.Contains("test-policy")));
    }

    [Fact]
    public async Task CommentOnPullRequestAsync_WhenCustomMessageProvided_UsesCustomMessage()
    {
        // Arrange
        var repositoryId = _faker.Random.Long(1000, 999999);
        var pullRequestNumber = _faker.Random.Int(1, 100);
        var customMessage = _faker.Lorem.Paragraph();
        var violation = await CreateViolationWithPolicyAsync();

        var policyConfig = new PolicyConfig
        {
            Type = "test-policy",
            Name = "Test Policy",
            Actions = new List<string> { "comment-on-pr" },
            PrCommentDetails = new PrCommentDetails { Message = customMessage }
        };

        var violations = new List<PolicyViolation> { violation };

        _gitHubService.GetPullRequestCommentsAsync(repositoryId, pullRequestNumber)
            .Returns(new List<IssueComment>());

        var mockComment = CreateMockIssueComment();
        _gitHubService.CreatePullRequestCommentAsync(repositoryId, pullRequestNumber, Arg.Any<string>())
            .Returns(mockComment);

        // Act
        await _sut.CommentOnPullRequestAsync(repositoryId, pullRequestNumber, policyConfig, violations);

        // Assert
        await _gitHubService.Received(1).CreatePullRequestCommentAsync(repositoryId, pullRequestNumber, customMessage);
    }

    [Fact]
    public async Task CommentOnPullRequestAsync_WhenNoViolations_SkipsComment()
    {
        // Arrange
        var repositoryId = _faker.Random.Long(1000, 999999);
        var pullRequestNumber = _faker.Random.Int(1, 100);
        var policyConfig = new PolicyConfig
        {
            Type = "test-policy",
            Name = "Test Policy",
            Actions = new List<string> { "comment-on-pr" }
        };

        var violations = new List<PolicyViolation>(); // Empty violations

        // Act
        await _sut.CommentOnPullRequestAsync(repositoryId, pullRequestNumber, policyConfig, violations);

        // Assert
        await _gitHubService.DidNotReceive().GetPullRequestCommentsAsync(Arg.Any<long>(), Arg.Any<int>());
        await _gitHubService.DidNotReceive().CreatePullRequestCommentAsync(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<string>());
    }

    [Fact]
    public async Task UpdatePullRequestStatusCheckAsync_WhenViolationsExist_CreatesFailureStatusCheck()
    {
        // Arrange
        var repositoryId = _faker.Random.Long(1000, 999999);
        var headSha = _faker.Random.AlphaNumeric(40);
        var violation = await CreateViolationWithPolicyAsync();

        var policyConfig = new PolicyConfig
        {
            Type = "test-policy",
            Name = "Test Policy",
            Actions = new List<string> { "block-prs" },
            BlockPrsDetails = null // Use default status check name
        };

        var violations = new List<PolicyViolation> { violation };

        _gitHubService.GetCheckRunsForRefAsync(repositoryId, headSha)
            .Returns(new List<CheckRun>());

        var mockCheckRun = CreateMockCheckRun();
        _gitHubService.CreateStatusCheckAsync(
            repositoryId,
            headSha,
            Arg.Any<string>(),
            "completed",
            "failure",
            Arg.Any<string>())
            .Returns(mockCheckRun);

        // Act
        await _sut.UpdatePullRequestStatusCheckAsync(repositoryId, headSha, violations, policyConfig);

        // Assert
        await _gitHubService.Received(1).GetCheckRunsForRefAsync(repositoryId, headSha);
        await _gitHubService.Received(1).CreateStatusCheckAsync(
            repositoryId,
            headSha,
            "Policy Compliance Check",
            "completed",
            "failure",
            null);
    }

    [Fact]
    public async Task UpdatePullRequestStatusCheckAsync_WhenNoViolations_CreatesSuccessStatusCheck()
    {
        // Arrange
        var repositoryId = _faker.Random.Long(1000, 999999);
        var headSha = _faker.Random.AlphaNumeric(40);

        var policyConfig = new PolicyConfig
        {
            Type = "test-policy",
            Name = "Test Policy",
            Actions = new List<string> { "block-prs" },
            BlockPrsDetails = null
        };

        var violations = new List<PolicyViolation>(); // No violations

        _gitHubService.GetCheckRunsForRefAsync(repositoryId, headSha)
            .Returns(new List<CheckRun>());

        var mockCheckRun = CreateMockCheckRun();
        _gitHubService.CreateStatusCheckAsync(
            repositoryId,
            headSha,
            Arg.Any<string>(),
            "completed",
            "success",
            Arg.Any<string>())
            .Returns(mockCheckRun);

        // Act
        await _sut.UpdatePullRequestStatusCheckAsync(repositoryId, headSha, violations, policyConfig);

        // Assert
        await _gitHubService.Received(1).CreateStatusCheckAsync(
            repositoryId,
            headSha,
            "Policy Compliance Check",
            "completed",
            "success",
            null);
    }

    [Fact]
    public async Task UpdatePullRequestStatusCheckAsync_WhenCustomStatusCheckName_UsesCustomName()
    {
        // Arrange
        var repositoryId = _faker.Random.Long(1000, 999999);
        var headSha = _faker.Random.AlphaNumeric(40);
        var customStatusCheckName = "Custom Policy Check";
        var violation = await CreateViolationWithPolicyAsync();

        var policyConfig = new PolicyConfig
        {
            Type = "test-policy",
            Name = "Test Policy",
            Actions = new List<string> { "block-prs" },
            BlockPrsDetails = new BlockPrsDetails { StatusCheckName = customStatusCheckName }
        };

        var violations = new List<PolicyViolation> { violation };

        _gitHubService.GetCheckRunsForRefAsync(repositoryId, headSha)
            .Returns(new List<CheckRun>());

        var mockCheckRun = CreateMockCheckRun();
        _gitHubService.CreateStatusCheckAsync(
            repositoryId,
            headSha,
            Arg.Any<string>(),
            "completed",
            "failure",
            Arg.Any<string>())
            .Returns(mockCheckRun);

        // Act
        await _sut.UpdatePullRequestStatusCheckAsync(repositoryId, headSha, violations, policyConfig);

        // Assert
        await _gitHubService.Received(1).CreateStatusCheckAsync(
            repositoryId,
            headSha,
            customStatusCheckName,
            "completed",
            "failure",
            null);
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

    /// <summary>
    /// Creates a mock Octokit.Repository for testing
    /// </summary>
    private OctokitRepository CreateMockRepository(long id = 12345, string name = "test-repo", bool archived = false)
    {
        // Create Repository via JSON deserialization (the way Octokit does it internally)
        var json = $$"""
        {
            "id": {{id}},
            "node_id": "R_{{id}}",
            "name": "{{name}}",
            "full_name": "owner/{{name}}",
            "private": false,
            "archived": {{archived.ToString().ToLower()}},
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

        // Use reflection to ensure Archived property is set correctly
        if (repository.Archived != archived)
        {
            var archivedProperty = typeof(OctokitRepository).GetProperty("Archived");
            if (archivedProperty != null && archivedProperty.CanWrite)
            {
                archivedProperty.SetValue(repository, archived);
            }
            else
            {
                // If property is not writable, use backing field
                var archivedField = typeof(OctokitRepository).GetField("<Archived>k__BackingField",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                archivedField?.SetValue(repository, archived);
            }
        }

        return repository;
    }

    /// <summary>
    /// Creates a violation with associated policy for PR action tests
    /// </summary>
    private async Task<PolicyViolation> CreateViolationWithPolicyAsync()
    {
        var policy = new Policy
        {
            PolicyKey = "test-policy",
            Description = "Test Policy",
            Action = "comment-on-pr"
        };
        await _dbContext.Policies.AddAsync(policy);

        var repository = CreateRepository();
        await _dbContext.Repositories.AddAsync(repository);

        var scan = new Scan { ScanId = _faker.Random.Int(1, 1000), StartedAt = DateTime.UtcNow, Status = "InProgress" };
        await _dbContext.Scans.AddAsync(scan);

        var violation = CreateViolation(scan.ScanId, policy.PolicyId, repository.RepositoryId);
        violation.PolicyType = "test-policy"; // Set PolicyType for webhook path
        await _dbContext.PolicyViolations.AddAsync(violation);

        await _dbContext.SaveChangesAsync();

        // Load navigation properties
        await _dbContext.Entry(violation).Reference(v => v.Policy).LoadAsync();
        await _dbContext.Entry(violation).Reference(v => v.Repository).LoadAsync();

        return violation;
    }

    /// <summary>
    /// Creates a mock Octokit.IssueComment for testing
    /// </summary>
    private IssueComment CreateMockIssueComment(int id = 1, string body = "Test comment")
    {
        // Use JSON deserialization since IssueComment doesn't have a public constructor
        var commentJson = $$"""
        {
          "id": {{id}},
          "node_id": "IC_{{id}}",
          "url": "https://api.github.com/repos/test/repo/issues/comments/{{id}}",
          "html_url": "https://github.com/test/repo/issues/comments/{{id}}",
          "body": "{{body.Replace("\"", "\\\"")}}",
          "created_at": "{{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ssZ}}",
          "updated_at": "{{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ssZ}}",
          "user": {
            "login": "test-user",
            "id": 1,
            "type": "User"
          },
          "author_association": "NONE"
        }
        """;

        return Newtonsoft.Json.JsonConvert.DeserializeObject<IssueComment>(commentJson)!;
    }

    /// <summary>
    /// Creates a mock Octokit.CheckRun for testing
    /// </summary>
    private CheckRun CreateMockCheckRun(long id = 1, string name = "Test Check", CheckStatus status = CheckStatus.Completed, CheckConclusion? conclusion = CheckConclusion.Success)
    {
        // Use JSON deserialization since CheckRun doesn't have a public constructor
        var conclusionStr = conclusion.HasValue ? $"\"{conclusion.Value.ToString().ToLower()}\"" : "null";
        var checkRunJson = $$"""
        {
          "id": {{id}},
          "name": "{{name}}",
          "status": "{{status.ToString().ToLower()}}",
          "conclusion": {{conclusionStr}},
          "head_sha": "{{_faker.Random.AlphaNumeric(40)}}",
          "html_url": "https://github.com/test/repo/runs/{{id}}"
        }
        """;

        return Newtonsoft.Json.JsonConvert.DeserializeObject<CheckRun>(checkRunJson)!;
    }
}

