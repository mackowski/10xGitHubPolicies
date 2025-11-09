# ActionService Unit Tests - Implementation Plan

## Overview

**Target Class**: `ActionService` (`10xGitHubPolicies.App/Services/Action/ActionService.cs`)

**Test Project**: `10xGitHubPolicies.Tests`

**Test File**: `10xGitHubPolicies.Tests/Services/Action/ActionServiceTests.cs`

**Testing Framework**: xUnit + NSubstitute + FluentAssertions + Bogus

## Related Test Cases

This implementation covers the following test cases from `.ai/test-plan.md`:

- **TC-ACTION-001**: Issue Creation
- **TC-ACTION-002**: Repository Archiving
- **TC-ACTION-003**: Action Failure Handling
- **TC-ACTION-004**: Duplicate Issue Prevention Logic
- **TC-ACTION-005**: Partial Failure Isolation

## Dependencies to Mock

```csharp
private readonly ApplicationDbContext _dbContext;           // Mock with in-memory provider
private readonly IGitHubService _gitHubService;            // Mock with NSubstitute
private readonly IConfigurationService _configService;      // Mock with NSubstitute
private readonly ILogger<ActionService> _logger;           // Mock with NSubstitute
private readonly ActionService _sut;                       // System Under Test
private readonly Faker _faker;                             // Test data generation
```

## Test Class Structure

```csharp
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

    // Test methods here...
}
```

## Test Scenarios

### 1. ProcessActionsForScanAsync - No Violations

**Test Case**: `ProcessActionsForScanAsync_WhenNoViolations_CompletesSuccessfully`

**Objective**: Verify service handles empty violations gracefully

**Related Test Case**: TC-ACTION-003 (partial coverage)

```csharp
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
    
    // Verify appropriate log message
    _logger.ReceivedWithAnyArgs().LogInformation(default, default);
}
```

### 2. ProcessActionsForScanAsync - Create Issue Action

**Test Case**: `ProcessActionsForScanAsync_WhenCreateIssueAction_CreatesIssueSuccessfully`

**Objective**: Verify issue creation workflow

**Related Test Case**: TC-ACTION-001

```csharp
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
        Arg.Is<string>(t => t.Contains(policyConfig.Name ?? policyConfig.Type)),
        Arg.Any<string>(),
        Arg.Any<IEnumerable<string>>());
    
    // Verify action log was created
    var actionLog = await _dbContext.ActionsLogs.FirstOrDefaultAsync();
    actionLog.Should().NotBeNull();
    actionLog!.ActionType.Should().Be("create-issue");
    actionLog.Status.Should().Be("Success");
    actionLog.Details.Should().Contain($"#{mockIssue.Number}");
}
```

### 3. ProcessActionsForScanAsync - Duplicate Issue Prevention

**Test Case**: `ProcessActionsForScanAsync_WhenDuplicateIssueExists_SkipsCreation`

**Objective**: Verify duplicate issue prevention (US-010 requirement)

**Related Test Case**: TC-ACTION-004

```csharp
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
```

### 4. ProcessActionsForScanAsync - Archive Repository Action

**Test Case**: `ProcessActionsForScanAsync_WhenArchiveRepoAction_ArchivesRepository`

**Objective**: Verify repository archiving workflow

**Related Test Case**: TC-ACTION-002

```csharp
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
```

### 5. ProcessActionsForScanAsync - Log Only Action

**Test Case**: `ProcessActionsForScanAsync_WhenLogOnlyAction_LogsWithoutActions`

**Objective**: Verify log-only action doesn't trigger GitHub API calls

**Related Test Case**: Implicit in TC-ACTION-003

```csharp
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
```

### 6. ProcessActionsForScanAsync - Unknown Action Type

**Test Case**: `ProcessActionsForScanAsync_WhenUnknownAction_LogsWarningAndContinues`

**Objective**: Verify unknown action types are handled gracefully

**Related Test Case**: TC-ACTION-003

```csharp
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
    
    // Verify warning was logged
    _logger.ReceivedWithAnyArgs().LogWarning(default(string), default, default);
    
    // Verify no GitHub API calls
    await _gitHubService.DidNotReceive().CreateIssueAsync(
        Arg.Any<long>(),
        Arg.Any<string>(),
        Arg.Any<string>(),
        Arg.Any<IEnumerable<string>>());
    await _gitHubService.DidNotReceive().ArchiveRepositoryAsync(Arg.Any<long>());
}
```

### 7. ProcessActionsForScanAsync - Missing Policy Configuration

**Test Case**: `ProcessActionsForScanAsync_WhenPolicyConfigMissing_SkipsViolation`

**Objective**: Verify handling when policy configuration is missing

**Related Test Case**: TC-ACTION-003

```csharp
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
    // Verify warning was logged
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
```

### 8. ProcessActionsForScanAsync - Create Issue Failure

**Test Case**: `ProcessActionsForScanAsync_WhenCreateIssueFails_LogsErrorAndContinues`

**Objective**: Verify error handling when issue creation fails

**Related Test Case**: TC-ACTION-003, TC-ACTION-005

```csharp
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
        .Returns(Task.FromException<Issue>(new ApiException("GitHub API error", System.Net.HttpStatusCode.InternalServerError)));

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
    
    // Verify error was logged
    _logger.ReceivedWithAnyArgs().LogError(default(Exception), default(string), default, default);
    
    // Verify action logged as "Failed"
    var actionLog = await _dbContext.ActionsLogs.FirstOrDefaultAsync();
    actionLog.Should().NotBeNull();
    actionLog!.Status.Should().Be("Failed");
    actionLog.Details.Should().Contain("Exception");
}
```

### 9. ProcessActionsForScanAsync - Archive Repository Failure

**Test Case**: `ProcessActionsForScanAsync_WhenArchiveFails_LogsErrorAndContinues`

**Objective**: Verify error handling when repository archiving fails

**Related Test Case**: TC-ACTION-003, TC-ACTION-005

```csharp
[Fact]
public async Task ProcessActionsForScanAsync_WhenArchiveFails_LogsErrorAndContinues()
{
    // Arrange
    var (scanId, violation, policyConfig) = await SetupViolationWithActionAsync("archive-repo");
    
    _gitHubService.ArchiveRepositoryAsync(Arg.Any<long>())
        .Returns(Task.FromException(new ApiException("Insufficient permissions", System.Net.HttpStatusCode.Forbidden)));

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
    
    // Verify error logged
    _logger.ReceivedWithAnyArgs().LogError(default(Exception), default(string), default, default, default);
    
    // Verify action logged as "Failed"
    var actionLog = await _dbContext.ActionsLogs.FirstOrDefaultAsync();
    actionLog.Should().NotBeNull();
    actionLog!.Status.Should().Be("Failed");
    actionLog.Details.Should().Contain("Exception");
}
```

### 10. ProcessActionsForScanAsync - Multiple Violations with Partial Failure

**Test Case**: `ProcessActionsForScanAsync_WhenOneActionFails_OthersStillProcess`

**Objective**: Verify individual action failures don't block other violations (US-010 requirement)

**Related Test Case**: TC-ACTION-005

```csharp
[Fact]
public async Task ProcessActionsForScanAsync_WhenOneActionFails_OthersStillProcess()
{
    // Arrange - Create 3 violations
    var scanId = _faker.Random.Int(1, 1000);
    var scan = new Scan { ScanId = scanId, StartedAt = DateTime.UtcNow, Status = "InProgress" };
    await _dbContext.Scans.AddAsync(scan);
    
    var policy = new Policy { PolicyKey = "test-policy", Name = "Test Policy" };
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
        .Returns(Task.FromException<Issue>(new ApiException("API Error", System.Net.HttpStatusCode.InternalServerError)));
    
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
```

### 11. ProcessActionsForScanAsync - Action Type Variations (Underscore vs Hyphen)

**Test Case**: `ProcessActionsForScanAsync_WhenActionTypeVariations_HandlesCorrectly`

**Objective**: Verify both underscore and hyphen action formats work

**Related Test Case**: Implementation detail testing

```csharp
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
```

### 12. ProcessActionsForScanAsync - Custom Issue Details

**Test Case**: `ProcessActionsForScanAsync_WhenCustomIssueDetails_UsesConfiguredValues`

**Objective**: Verify custom issue title, body, and labels are used

**Related Test Case**: TC-ACTION-001

```csharp
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
```

### 13. ProcessActionsForScanAsync - Default Issue Details

**Test Case**: `ProcessActionsForScanAsync_WhenNoIssueDetails_UsesDefaults`

**Objective**: Verify default values are used when IssueDetails is null

**Related Test Case**: TC-ACTION-001

```csharp
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
```

## Helper Methods

```csharp
/// <summary>
/// Creates a test violation with associated scan, policy, and repository
/// </summary>
private async Task<(int scanId, PolicyViolation violation, PolicyConfig policyConfig)> 
    SetupViolationWithActionAsync(string actionType)
{
    var scanId = _faker.Random.Int(1, 1000);
    var scan = new Scan { ScanId = scanId, StartedAt = DateTime.UtcNow, Status = "InProgress" };
    await _dbContext.Scans.AddAsync(scan);

    var policy = new Policy { PolicyKey = "test-policy", Name = "Test Policy" };
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
private Repository CreateRepository()
{
    return new Repository
    {
        GitHubRepositoryId = _faker.Random.Long(1000, 999999),
        Name = _faker.Company.CompanyName(),
        Owner = _faker.Internet.UserName(),
        IsArchived = false,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
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
        RepositoryId = repositoryId,
        DetectedAt = DateTime.UtcNow
    };
}

/// <summary>
/// Creates a mock Octokit.Issue for testing
/// </summary>
private Issue CreateMockIssue(int number = 1, string title = "Test Issue")
{
    // Note: Issue is a reference type from Octokit that may need special handling
    // Depending on Octokit version, you may need to use reflection or a test factory
    var issue = Substitute.For<Issue>();
    issue.Number.Returns(number);
    issue.Title.Returns(title);
    issue.HtmlUrl.Returns($"https://github.com/test/repo/issues/{number}");
    issue.State.Returns(ItemState.Open);
    return issue;
}
```

## Test Execution Order

1. **No violations** - Baseline test
2. **Log-only action** - Simplest action type
3. **Create issue success** - Happy path for issue creation
4. **Duplicate issue prevention** - Critical US-010 requirement
5. **Archive repository success** - Happy path for archiving
6. **Custom vs default issue details** - Configuration variations
7. **Action type variations** - Underscore vs hyphen
8. **Unknown action type** - Error handling
9. **Missing policy config** - Error handling
10. **Create issue failure** - Exception handling
11. **Archive failure** - Exception handling
12. **Multiple violations with partial failure** - Isolation testing (TC-ACTION-005)

## Code Coverage Expectations

**Target Coverage**: 85-90%

**What to Cover**:

- ✅ All action types (create-issue, archive-repo, log-only)
- ✅ Duplicate issue prevention logic
- ✅ Error handling for each action type
- ✅ Configuration variations (custom vs default)
- ✅ Multiple violations processing
- ✅ Partial failure isolation

**What NOT to Cover**:

- ❌ Entity Framework internal methods
- ❌ Logger internal implementation
- ❌ Octokit.Issue internal properties (tested via mocking)

## Implementation Notes

### In-Memory Database Considerations

```csharp
// Use unique database per test to avoid conflicts
var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
    .Options;
```

### Mocking Octokit.Issue

The `Issue` class from Octokit may be a sealed or complex type. Use NSubstitute's `Substitute.For<Issue>()` or create a test factory pattern if needed.

### Testing Multiple Violations

When testing multiple violations, ensure:

1. Each violation has a unique repository ID
2. GitHub API mocks differentiate by repository ID
3. Action logs are properly associated with violations

### Logging Verification

Use NSubstitute's `ReceivedWithAnyArgs()` for flexible log verification:

```csharp
_logger.ReceivedWithAnyArgs().LogInformation(default, default);
_logger.ReceivedWithAnyArgs().LogError(default(Exception), default(string), default, default);
```

## Running Tests

```bash
# Run all ActionService tests
dotnet test --filter FullyQualifiedName~ActionServiceTests

# Run specific test
dotnet test --filter FullyQualifiedName~ActionServiceTests.ProcessActionsForScanAsync_WhenDuplicateIssueExists_SkipsCreation

# Run with coverage
dotnet test --filter FullyQualifiedName~ActionServiceTests /p:CollectCoverage=true

# Watch mode for TDD
dotnet watch test --filter FullyQualifiedName~ActionServiceTests
```

## Success Criteria

- ✅ All 13 test scenarios pass
- ✅ Code coverage > 85%
- ✅ All test cases from test plan (TC-ACTION-001 through TC-ACTION-005) covered
- ✅ Test execution time < 10 seconds total
- ✅ No flaky tests (tests pass consistently)
- ✅ Proper test isolation (tests can run in any order)
- ✅ Clear test names following `MethodName_WhenCondition_ExpectedBehavior` pattern
- ✅ Comprehensive assertion messages with `because` parameter

## Next Steps After Implementation

1. Review test coverage report
2. Add Theory tests for edge cases if needed
3. Update documentation if new patterns emerge
4. Consider integration tests for database operations
5. Plan component tests for UI that displays action logs