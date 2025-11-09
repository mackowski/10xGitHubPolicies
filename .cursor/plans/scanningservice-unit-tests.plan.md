<!-- 67d8a2c6-ff30-4ce0-8b66-491f45831694 e520b022-8f19-4639-9ea0-829266da438d -->
# Unit Test Implementation Plan for ScanningService

## Overview

Create comprehensive unit tests for `ScanningService.cs` following xUnit + NSubstitute + FluentAssertions patterns established in the existing test suite.

## Approach: EF Core InMemory Database

Given that `ScanningService` has extensive database operations (Scan creation, Policy sync, Repository sync, PolicyViolation tracking), we'll use EF Core InMemory database instead of mocking `ApplicationDbContext`. This provides:

- Fast, isolated tests (< 100ms per test)
- Easier testing of complex LINQ queries and database operations
- Follows the pragmatic approach shown in `ConfigurationServiceTests` (uses real MemoryCache)

## File Structure

**New File**: `10xGitHubPolicies.Tests/Services/Scanning/ScanningServiceTests.cs`

## Test Class Structure

```csharp
public class ScanningServiceTests : IAsyncLifetime
{
    private readonly IGitHubService _githubService;
    private readonly IConfigurationService _configurationService;
    private readonly IPolicyEvaluationService _policyEvaluationService;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<ScanningService> _logger;
    private readonly ApplicationDbContext _dbContext;
    private readonly ScanningService _sut;
    private readonly Faker _faker;

    public ScanningServiceTests()
    {
        // Mock external dependencies
        _githubService = Substitute.For<IGitHubService>();
        _configurationService = Substitute.For<IConfigurationService>();
        _policyEvaluationService = Substitute.For<IPolicyEvaluationService>();
        _backgroundJobClient = Substitute.For<IBackgroundJobClient>();
        _logger = Substitute.For<ILogger<ScanningService>>();
        _faker = new Faker();

        // Use InMemory database for isolated testing
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ApplicationDbContext(options);

        _sut = new ScanningService(
            _githubService,
            _configurationService,
            _policyEvaluationService,
            _dbContext,
            _backgroundJobClient,
            _logger);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    
    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }
}
```

## Test Cases to Implement

### 1. PerformScanAsync - Happy Path (No Violations)

**Test**: `PerformScanAsync_WhenNoViolationsFound_CompletesSuccessfully`

- Arrange: Mock config with policies, GitHub repos (2-3 repos), policy evaluations return empty violations
- Act: Call PerformScanAsync()
- Assert:
  - Scan record created with status "InProgress" → "Completed"
  - Scan has StartedAt and CompletedAt timestamps
  - Repositories synced to database
  - Policies synced to database
  - No PolicyViolations created
  - Action job NOT enqueued (no violations)
  - Logger called with appropriate messages

### 2. PerformScanAsync - With Violations

**Test**: `PerformScanAsync_WhenViolationsFound_CreatesViolationsAndEnqueuesJob`

- Arrange: Mock 2 repos with violations (repo1: 1 violation, repo2: 2 violations)
- Act: Call PerformScanAsync()
- Assert:
  - Scan status is "Completed"
  - 3 PolicyViolations created with correct ScanId, RepositoryId, PolicyId
  - Action processing job enqueued via `_backgroundJobClient.Enqueue<IActionService>()`
  - Verify job parameter is correct scanId
  - Logger logs violation count

### 3. PerformScanAsync - Configuration Service Throws Exception

**Test**: `PerformScanAsync_WhenConfigServiceFails_SetsScanStatusToFailed`

- Arrange: Mock `_configurationService.GetConfigAsync()` to throw exception
- Act: Call PerformScanAsync()
- Assert:
  - Scan record created with status "Failed"
  - Exception logged
  - CompletedAt timestamp set
  - Action job NOT enqueued

### 4. PerformScanAsync - GitHub Service Throws Exception

**Test**: `PerformScanAsync_WhenGitHubServiceFails_SetsScanStatusToFailed`

- Arrange: Mock `_githubService.GetOrganizationRepositoriesAsync()` to throw exception
- Act: Call PerformScanAsync()
- Assert:
  - Scan status set to "Failed"
  - Exception logged
  - CompletedAt timestamp set

### 5. PerformScanAsync - Policy Evaluation Throws Exception

**Test**: `PerformScanAsync_WhenPolicyEvaluationFails_SetsScanStatusToFailed`

- Arrange: Mock `_policyEvaluationService.EvaluateRepositoryAsync()` to throw exception
- Act: Call PerformScanAsync()
- Assert:
  - Scan status set to "Failed"
  - Exception logged

### 6. PerformScanAsync - Creates Scan Record

**Test**: `PerformScanAsync_WhenCalled_CreatesScanRecordWithCorrectStatus`

- Arrange: Setup minimal mocks
- Act: Call PerformScanAsync()
- Assert:
  - Scan record exists in database
  - Scan.Status transitions from "InProgress" to "Completed"
  - StartedAt < CompletedAt
  - Timestamps are UTC

### 7. PerformScanAsync - Syncs Policies Correctly

**Test**: `PerformScanAsync_WhenNewPolicies_AddsPolicies` (via SyncPoliciesAsync)

- Arrange: Config with 2 policies (has_agents_md, has_catalog_info_yaml)
- Act: Call PerformScanAsync()
- Assert:
  - 2 Policy records created in database
  - PolicyKey matches config type
  - Action matches config action

### 8. PerformScanAsync - Reuses Existing Policies

**Test**: `PerformScanAsync_WhenPoliciesExist_ReusesExistingPolicies`

- Arrange: Pre-populate database with policy, config returns same policy
- Act: Call PerformScanAsync()
- Assert:
  - No duplicate policies created
  - Existing policy is reused (verify count remains 1)

### 9. PerformScanAsync - Syncs Repositories

**Test**: `PerformScanAsync_WhenNewRepositories_AddsRepositories` (via SyncRepositoriesAsync)

- Arrange: Mock GitHub returns 3 repositories
- Act: Call PerformScanAsync()
- Assert:
  - 3 Repository records created
  - GitHubRepositoryId matches
  - Name (FullName) is set correctly
  - ComplianceStatus set to "Pending"

### 10. PerformScanAsync - Skips Existing Repositories

**Test**: `PerformScanAsync_WhenRepositoriesExist_SkipsExisting`

- Arrange: Pre-populate database with 2 repos, GitHub returns same 2 repos
- Act: Call PerformScanAsync()
- Assert:
  - No duplicate repositories created
  - Repository count remains 2

### 11. PerformScanAsync - Links Violations Correctly

**Test**: `PerformScanAsync_WhenViolationsFound_LinksToCorrectEntities`

- Arrange: Mock 1 repo with 1 violation
- Act: Call PerformScanAsync()
- Assert:
  - PolicyViolation.ScanId matches created Scan
  - PolicyViolation.RepositoryId matches synced Repository
  - PolicyViolation.PolicyId matches synced Policy

### 12. PerformScanAsync - Concurrent Scan Safety (Optional - Lower Priority)

**Test**: `PerformScanAsync_WhenCalledConcurrently_CreatesSeparateScans`

- Note: This test is optional depending on whether concurrent scans should be allowed
- If needed, test that concurrent calls create separate scan records

### 13. SyncPoliciesAsync - Edge Cases

**Test**: `PerformScanAsync_WhenNoPoliciesConfigured_HandlesEmptyPolicies`

- Arrange: Config returns empty policy list
- Act: Call PerformScanAsync()
- Assert:
  - No policies created
  - Scan completes successfully

### 14. SyncRepositoriesAsync - Empty Organization

**Test**: `PerformScanAsync_WhenNoRepositories_HandlesEmptyOrganization`

- Arrange: GitHub returns empty repository list
- Act: Call PerformScanAsync()
- Assert:
  - No repositories created
  - Scan completes successfully
  - Action job NOT enqueued (no repositories to violate policies)

## Test Traits

All tests should include:

```csharp
[Fact]
[Trait("Category", "Unit")]
[Trait("Feature", "Scanning")]
```

## Helper Methods

Create helper methods to reduce boilerplate:

```csharp
private AppConfig CreateTestConfig(params PolicyConfig[] policies)
{
    return new AppConfig
    {
        AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
        Policies = policies.ToList()
    };
}

private Octokit.Repository CreateTestRepository(long id, string name)
{
    return new Octokit.Repository(
        // Use Bogus or minimal required parameters
        id: id,
        name: name,
        fullName: $"org/{name}",
        // ... other required fields
    );
}

private PolicyViolation CreateTestViolation(string policyType, string reason)
{
    return new PolicyViolation
    {
        PolicyType = policyType,
        Reason = reason,
        DetectedAt = DateTime.UtcNow
    };
}
```

## Mocking Patterns

### ConfigurationService Mock

```csharp
var config = CreateTestConfig(
    new PolicyConfig { Type = "has_agents_md", Action = "create-issue" }
);
_configurationService.GetConfigAsync(Arg.Any<bool>()).Returns(config);
```

### GitHubService Mock

```csharp
var repos = new List<Octokit.Repository>
{
    CreateTestRepository(1, "repo1"),
    CreateTestRepository(2, "repo2")
};
_githubService.GetOrganizationRepositoriesAsync().Returns(repos);
```

### PolicyEvaluationService Mock

```csharp
// No violations
_policyEvaluationService.EvaluateRepositoryAsync(
    Arg.Any<Octokit.Repository>(),
    Arg.Any<IEnumerable<PolicyConfig>>()
).Returns(new List<PolicyViolation>());

// With violations
_policyEvaluationService.EvaluateRepositoryAsync(
    Arg.Is<Octokit.Repository>(r => r.Id == 1),
    Arg.Any<IEnumerable<PolicyConfig>>()
).Returns(new List<PolicyViolation>
{
    CreateTestViolation("has_agents_md", "File missing")
});
```

### BackgroundJobClient Mock

```csharp
// Verify job was enqueued
_backgroundJobClient.Received(1).Enqueue<IActionService>(
    Arg.Is<Expression<Action<IActionService>>>(
        expr => expr.ToString().Contains("ProcessActionsForScanAsync")
    )
);
```

## Code Coverage Target

Aim for 85-90% coverage of ScanningService:

- All public methods tested
- Happy paths and error paths
- Edge cases (empty collections, null handling)
- Focus on business logic, not EF Core internals

## Dependencies to Add (if not already present)

- `Microsoft.EntityFrameworkCore.InMemory` package
- Ensure xUnit, NSubstitute, FluentAssertions, Bogus are available

## Testing Philosophy

- Fast tests (< 100ms each, InMemory DB is fast)
- Isolated (each test uses new DB instance via Guid.NewGuid())
- Independent (tests can run in any order)
- Readable (descriptive test names, clear assertions with "because" clauses)
- Comprehensive (cover all branches and error conditions)

## Reference Files

- Follow patterns from: `10xGitHubPolicies.Tests/Services/Configuration/ConfigurationServiceTests.cs`
- Adhere to: `.cursor/rules/unit-testing.mdc`
- Align with: `.ai/test-plan.md` (Section 4.6 - Background Job Processing)