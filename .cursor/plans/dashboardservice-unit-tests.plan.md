# DashboardService Unit Tests - Implementation Plan

## Overview

**Target Class**: `DashboardService` (`10xGitHubPolicies.App/Services/Dashboard/DashboardService.cs`)

**Test Project**: `10xGitHubPolicies.Tests`

**Test File**: `10xGitHubPolicies.Tests/Services/Dashboard/DashboardServiceTests.cs`

**Testing Framework**: xUnit + NSubstitute + FluentAssertions + Bogus

## Related Test Cases

This implementation covers the following test cases from `.ai/test-plan.md`:

- **TC-DASH-001**: Compliance Dashboard Display
- **TC-DASH-002**: Repository Filtering

## Dependencies to Mock

```csharp
private readonly ApplicationDbContext _dbContext;    // Use in-memory database
private readonly DashboardService _sut;              // System Under Test
private readonly Faker _faker;                       // Test data generation
```

## Test Class Structure

```csharp
using _10xGitHubPolicies.App.Data;
using _10xGitHubPolicies.App.Data.Entities;
using _10xGitHubPolicies.App.Services.Dashboard;
using _10xGitHubPolicies.App.ViewModels;
using Bogus;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace _10xGitHubPolicies.Tests.Services.Dashboard;

[Trait("Category", "Unit")]
[Trait("Service", "DashboardService")]
public class DashboardServiceTests : IAsyncLifetime
{
    private readonly ApplicationDbContext _dbContext;
    private readonly DashboardService _sut;
    private readonly Faker _faker;

    public DashboardServiceTests()
    {
        // Arrange - Create in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ApplicationDbContext(options);
        
        _faker = new Faker();

        // Create system under test
        _sut = new DashboardService(_dbContext);
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

### 1. GetDashboardViewModelAsync - No Completed Scans

**Test Case**: `GetDashboardViewModelAsync_WhenNoCompletedScans_ReturnsEmptyViewModel`

**Objective**: Verify empty state when no scans have been completed

**Related Test Case**: TC-DASH-001 (edge case)

```csharp
[Fact]
public async Task GetDashboardViewModelAsync_WhenNoCompletedScans_ReturnsEmptyViewModel()
{
    // Arrange - No scans in database

    // Act
    var result = await _sut.GetDashboardViewModelAsync();

    // Assert
    result.Should().NotBeNull(because: "service should always return a view model");
    result.TotalRepositories.Should().Be(0);
    result.CompliantRepositories.Should().Be(0);
    result.CompliancePercentage.Should().Be(0);
    result.NonCompliantRepositories.Should().BeEmpty();
}
```

### 2. GetDashboardViewModelAsync - No Violations (100% Compliance)

**Test Case**: `GetDashboardViewModelAsync_WhenNoViolations_Returns100PercentCompliance`

**Objective**: Verify 100% compliance when all repositories are compliant

**Related Test Case**: TC-DASH-001

```csharp
[Fact]
public async Task GetDashboardViewModelAsync_WhenNoViolations_Returns100PercentCompliance()
{
    // Arrange
    var repositories = CreateRepositories(5);
    await _dbContext.Repositories.AddRangeAsync(repositories);
    
    var scan = CreateCompletedScan();
    await _dbContext.Scans.AddAsync(scan);
    
    await _dbContext.SaveChangesAsync();

    // Act
    var result = await _sut.GetDashboardViewModelAsync();

    // Assert
    result.TotalRepositories.Should().Be(5);
    result.CompliantRepositories.Should().Be(5);
    result.CompliancePercentage.Should().Be(100.0, 
        because: "all repositories are compliant");
    result.NonCompliantRepositories.Should().BeEmpty();
}
```

### 3. GetDashboardViewModelAsync - Some Violations

**Test Case**: `GetDashboardViewModelAsync_WhenSomeViolations_CalculatesComplianceCorrectly`

**Objective**: Verify correct compliance calculation with violations

**Related Test Case**: TC-DASH-001

```csharp
[Fact]
public async Task GetDashboardViewModelAsync_WhenSomeViolations_CalculatesComplianceCorrectly()
{
    // Arrange - 5 repos, 2 with violations = 60% compliance
    var repositories = CreateRepositories(5);
    await _dbContext.Repositories.AddRangeAsync(repositories);
    
    var scan = CreateCompletedScan();
    await _dbContext.Scans.AddAsync(scan);
    
    var policy = CreatePolicy("test-policy");
    await _dbContext.Policies.AddAsync(policy);
    
    // Create violations for 2 repositories
    var violation1 = CreateViolation(scan.ScanId, policy.PolicyId, repositories[0].RepositoryId);
    var violation2 = CreateViolation(scan.ScanId, policy.PolicyId, repositories[1].RepositoryId);
    await _dbContext.PolicyViolations.AddRangeAsync(violation1, violation2);
    
    await _dbContext.SaveChangesAsync();

    // Act
    var result = await _sut.GetDashboardViewModelAsync();

    // Assert
    result.TotalRepositories.Should().Be(5);
    result.CompliantRepositories.Should().Be(3, 
        because: "2 out of 5 repositories have violations");
    result.CompliancePercentage.Should().BeApproximately(60.0, 0.01, 
        because: "3/5 = 60%");
    result.NonCompliantRepositories.Should().HaveCount(2);
}
```

### 4. GetDashboardViewModelAsync - All Repositories Violate

**Test Case**: `GetDashboardViewModelAsync_WhenAllViolate_ReturnsZeroCompliance`

**Objective**: Verify 0% compliance when all repositories violate policies

**Related Test Case**: TC-DASH-001

```csharp
[Fact]
public async Task GetDashboardViewModelAsync_WhenAllViolate_ReturnsZeroCompliance()
{
    // Arrange - 3 repos, all with violations
    var repositories = CreateRepositories(3);
    await _dbContext.Repositories.AddRangeAsync(repositories);
    
    var scan = CreateCompletedScan();
    await _dbContext.Scans.AddAsync(scan);
    
    var policy = CreatePolicy("test-policy");
    await _dbContext.Policies.AddAsync(policy);
    
    // Create violations for all repositories
    foreach (var repo in repositories)
    {
        var violation = CreateViolation(scan.ScanId, policy.PolicyId, repo.RepositoryId);
        await _dbContext.PolicyViolations.AddAsync(violation);
    }
    
    await _dbContext.SaveChangesAsync();

    // Act
    var result = await _sut.GetDashboardViewModelAsync();

    // Assert
    result.TotalRepositories.Should().Be(3);
    result.CompliantRepositories.Should().Be(0);
    result.CompliancePercentage.Should().Be(0.0, 
        because: "all repositories are non-compliant");
    result.NonCompliantRepositories.Should().HaveCount(3);
}
```

### 5. GetDashboardViewModelAsync - Repository with Multiple Violations

**Test Case**: `GetDashboardViewModelAsync_WhenRepoHasMultipleViolations_ListsAllPolicies`

**Objective**: Verify all violated policies are listed for a repository

**Related Test Case**: TC-DASH-001

```csharp
[Fact]
public async Task GetDashboardViewModelAsync_WhenRepoHasMultipleViolations_ListsAllPolicies()
{
    // Arrange
    var repository = CreateRepository();
    await _dbContext.Repositories.AddAsync(repository);
    
    var scan = CreateCompletedScan();
    await _dbContext.Scans.AddAsync(scan);
    
    var policy1 = CreatePolicy("has_agents_md");
    var policy2 = CreatePolicy("has_catalog_info_yaml");
    var policy3 = CreatePolicy("correct_workflow_permissions");
    await _dbContext.Policies.AddRangeAsync(policy1, policy2, policy3);
    
    // Repository violates all 3 policies
    var violation1 = CreateViolation(scan.ScanId, policy1.PolicyId, repository.RepositoryId);
    var violation2 = CreateViolation(scan.ScanId, policy2.PolicyId, repository.RepositoryId);
    var violation3 = CreateViolation(scan.ScanId, policy3.PolicyId, repository.RepositoryId);
    await _dbContext.PolicyViolations.AddRangeAsync(violation1, violation2, violation3);
    
    await _dbContext.SaveChangesAsync();

    // Act
    var result = await _sut.GetDashboardViewModelAsync();

    // Assert
    result.NonCompliantRepositories.Should().HaveCount(1);
    
    var nonCompliantRepo = result.NonCompliantRepositories.First();
    nonCompliantRepo.ViolatedPolicies.Should().HaveCount(3);
    nonCompliantRepo.ViolatedPolicies.Should().Contain("has_agents_md");
    nonCompliantRepo.ViolatedPolicies.Should().Contain("has_catalog_info_yaml");
    nonCompliantRepo.ViolatedPolicies.Should().Contain("correct_workflow_permissions");
}
```

### 6. GetDashboardViewModelAsync - Latest Scan Selection

**Test Case**: `GetDashboardViewModelAsync_WhenMultipleScans_UsesLatestCompletedScan`

**Objective**: Verify service uses the most recent completed scan

**Related Test Case**: TC-DASH-001

```csharp
[Fact]
public async Task GetDashboardViewModelAsync_WhenMultipleScans_UsesLatestCompletedScan()
{
    // Arrange
    var repository = CreateRepository();
    await _dbContext.Repositories.AddAsync(repository);
    
    var policy = CreatePolicy("test-policy");
    await _dbContext.Policies.AddAsync(policy);
    
    // Older scan with violation
    var olderScan = CreateCompletedScan(completedAt: DateTime.UtcNow.AddDays(-2));
    await _dbContext.Scans.AddAsync(olderScan);
    var oldViolation = CreateViolation(olderScan.ScanId, policy.PolicyId, repository.RepositoryId);
    await _dbContext.PolicyViolations.AddAsync(oldViolation);
    
    // Newer scan with no violations
    var newerScan = CreateCompletedScan(completedAt: DateTime.UtcNow.AddDays(-1));
    await _dbContext.Scans.AddAsync(newerScan);
    
    await _dbContext.SaveChangesAsync();

    // Act
    var result = await _sut.GetDashboardViewModelAsync();

    // Assert
    result.CompliancePercentage.Should().Be(100.0, 
        because: "latest scan has no violations");
    result.NonCompliantRepositories.Should().BeEmpty(
        because: "latest scan should be used, not older scan");
}
```

### 7. GetDashboardViewModelAsync - Ignores In-Progress Scans

**Test Case**: `GetDashboardViewModelAsync_WhenInProgressScan_IgnoresIt`

**Objective**: Verify only completed scans are considered

**Related Test Case**: TC-DASH-001

```csharp
[Fact]
public async Task GetDashboardViewModelAsync_WhenInProgressScan_IgnoresIt()
{
    // Arrange
    var repository = CreateRepository();
    await _dbContext.Repositories.AddAsync(repository);
    
    // Completed scan with no violations
    var completedScan = CreateCompletedScan(completedAt: DateTime.UtcNow.AddDays(-2));
    await _dbContext.Scans.AddAsync(completedScan);
    
    // In-progress scan with violations (should be ignored)
    var inProgressScan = CreateScan(status: "InProgress");
    await _dbContext.Scans.AddAsync(inProgressScan);
    
    var policy = CreatePolicy("test-policy");
    await _dbContext.Policies.AddAsync(policy);
    var violation = CreateViolation(inProgressScan.ScanId, policy.PolicyId, repository.RepositoryId);
    await _dbContext.PolicyViolations.AddAsync(violation);
    
    await _dbContext.SaveChangesAsync();

    // Act
    var result = await _sut.GetDashboardViewModelAsync();

    // Assert
    result.CompliancePercentage.Should().Be(100.0, 
        because: "in-progress scan should be ignored");
    result.NonCompliantRepositories.Should().BeEmpty();
}
```

### 8. GetDashboardViewModelAsync - Failed Scans Ignored

**Test Case**: `GetDashboardViewModelAsync_WhenFailedScan_IgnoresIt`

**Objective**: Verify failed scans are not used

**Related Test Case**: TC-DASH-001

```csharp
[Fact]
public async Task GetDashboardViewModelAsync_WhenFailedScan_IgnoresIt()
{
    // Arrange
    var repository = CreateRepository();
    await _dbContext.Repositories.AddAsync(repository);
    
    // Failed scan (should be ignored)
    var failedScan = CreateScan(status: "Failed");
    await _dbContext.Scans.AddAsync(failedScan);
    
    await _dbContext.SaveChangesAsync();

    // Act
    var result = await _sut.GetDashboardViewModelAsync();

    // Assert
    result.TotalRepositories.Should().Be(1);
    result.CompliantRepositories.Should().Be(0, 
        because: "no completed scans exist");
    result.CompliancePercentage.Should().Be(0);
    result.NonCompliantRepositories.Should().BeEmpty();
}
```

### 9. GetDashboardViewModelAsync - Name Filter (Exact Match)

**Test Case**: `GetDashboardViewModelAsync_WhenNameFilterExactMatch_ReturnsMatchingRepo`

**Objective**: Verify name filtering returns matching repositories

**Related Test Case**: TC-DASH-002

```csharp
[Fact]
public async Task GetDashboardViewModelAsync_WhenNameFilterExactMatch_ReturnsMatchingRepo()
{
    // Arrange
    var repo1 = CreateRepository(name: "test-repo-1");
    var repo2 = CreateRepository(name: "test-repo-2");
    var repo3 = CreateRepository(name: "other-repo");
    await _dbContext.Repositories.AddRangeAsync(repo1, repo2, repo3);
    
    var scan = CreateCompletedScan();
    await _dbContext.Scans.AddAsync(scan);
    
    var policy = CreatePolicy("test-policy");
    await _dbContext.Policies.AddAsync(policy);
    
    // All repos have violations
    var violation1 = CreateViolation(scan.ScanId, policy.PolicyId, repo1.RepositoryId);
    var violation2 = CreateViolation(scan.ScanId, policy.PolicyId, repo2.RepositoryId);
    var violation3 = CreateViolation(scan.ScanId, policy.PolicyId, repo3.RepositoryId);
    await _dbContext.PolicyViolations.AddRangeAsync(violation1, violation2, violation3);
    
    await _dbContext.SaveChangesAsync();

    // Act
    var result = await _sut.GetDashboardViewModelAsync(nameFilter: "test-repo-1");

    // Assert
    result.TotalRepositories.Should().Be(3, 
        because: "total count is not affected by filter");
    result.NonCompliantRepositories.Should().HaveCount(1);
    result.NonCompliantRepositories.First().Name.Should().Be("test-repo-1");
}
```

### 10. GetDashboardViewModelAsync - Name Filter (Partial Match)

**Test Case**: `GetDashboardViewModelAsync_WhenNameFilterPartialMatch_ReturnsMatchingRepos`

**Objective**: Verify partial name matching works correctly

**Related Test Case**: TC-DASH-002

```csharp
[Fact]
public async Task GetDashboardViewModelAsync_WhenNameFilterPartialMatch_ReturnsMatchingRepos()
{
    // Arrange
    var repo1 = CreateRepository(name: "frontend-app");
    var repo2 = CreateRepository(name: "backend-api");
    var repo3 = CreateRepository(name: "frontend-lib");
    await _dbContext.Repositories.AddRangeAsync(repo1, repo2, repo3);
    
    var scan = CreateCompletedScan();
    await _dbContext.Scans.AddAsync(scan);
    
    var policy = CreatePolicy("test-policy");
    await _dbContext.Policies.AddAsync(policy);
    
    // All repos have violations
    var violation1 = CreateViolation(scan.ScanId, policy.PolicyId, repo1.RepositoryId);
    var violation2 = CreateViolation(scan.ScanId, policy.PolicyId, repo2.RepositoryId);
    var violation3 = CreateViolation(scan.ScanId, policy.PolicyId, repo3.RepositoryId);
    await _dbContext.PolicyViolations.AddRangeAsync(violation1, violation2, violation3);
    
    await _dbContext.SaveChangesAsync();

    // Act
    var result = await _sut.GetDashboardViewModelAsync(nameFilter: "frontend");

    // Assert
    result.NonCompliantRepositories.Should().HaveCount(2, 
        because: "2 repos contain 'frontend'");
    result.NonCompliantRepositories.Should().Contain(r => r.Name == "frontend-app");
    result.NonCompliantRepositories.Should().Contain(r => r.Name == "frontend-lib");
    result.NonCompliantRepositories.Should().NotContain(r => r.Name == "backend-api");
}
```

### 11. GetDashboardViewModelAsync - Name Filter (Case Sensitivity)

**Test Case**: `GetDashboardViewModelAsync_WhenNameFilterDifferentCase_MatchesCorrectly`

**Objective**: Verify case sensitivity behavior of name filter

**Related Test Case**: TC-DASH-002

```csharp
[Fact]
public async Task GetDashboardViewModelAsync_WhenNameFilterDifferentCase_MatchesCorrectly()
{
    // Arrange
    var repo1 = CreateRepository(name: "TestRepo");
    var repo2 = CreateRepository(name: "OtherRepo");
    await _dbContext.Repositories.AddRangeAsync(repo1, repo2);
    
    var scan = CreateCompletedScan();
    await _dbContext.Scans.AddAsync(scan);
    
    var policy = CreatePolicy("test-policy");
    await _dbContext.Policies.AddAsync(policy);
    
    var violation1 = CreateViolation(scan.ScanId, policy.PolicyId, repo1.RepositoryId);
    var violation2 = CreateViolation(scan.ScanId, policy.PolicyId, repo2.RepositoryId);
    await _dbContext.PolicyViolations.AddRangeAsync(violation1, violation2);
    
    await _dbContext.SaveChangesAsync();

    // Act - Using lowercase filter
    var result = await _sut.GetDashboardViewModelAsync(nameFilter: "test");

    // Assert
    // Note: EF Core in-memory provider uses case-sensitive Contains by default
    // This test documents the actual behavior
    result.NonCompliantRepositories.Should().HaveCount(1);
    result.NonCompliantRepositories.First().Name.Should().Be("TestRepo");
}
```

### 12. GetDashboardViewModelAsync - Empty Name Filter

**Test Case**: `GetDashboardViewModelAsync_WhenEmptyNameFilter_ReturnsAllRepos`

**Objective**: Verify empty string filter returns all repositories

**Related Test Case**: TC-DASH-002

```csharp
[Theory]
[InlineData(null)]
[InlineData("")]
public async Task GetDashboardViewModelAsync_WhenEmptyNameFilter_ReturnsAllRepos(string? nameFilter)
{
    // Arrange
    var repo1 = CreateRepository(name: "repo-1");
    var repo2 = CreateRepository(name: "repo-2");
    var repo3 = CreateRepository(name: "repo-3");
    await _dbContext.Repositories.AddRangeAsync(repo1, repo2, repo3);
    
    var scan = CreateCompletedScan();
    await _dbContext.Scans.AddAsync(scan);
    
    var policy = CreatePolicy("test-policy");
    await _dbContext.Policies.AddAsync(policy);
    
    // All repos have violations
    var violation1 = CreateViolation(scan.ScanId, policy.PolicyId, repo1.RepositoryId);
    var violation2 = CreateViolation(scan.ScanId, policy.PolicyId, repo2.RepositoryId);
    var violation3 = CreateViolation(scan.ScanId, policy.PolicyId, repo3.RepositoryId);
    await _dbContext.PolicyViolations.AddRangeAsync(violation1, violation2, violation3);
    
    await _dbContext.SaveChangesAsync();

    // Act
    var result = await _sut.GetDashboardViewModelAsync(nameFilter: nameFilter);

    // Assert
    result.NonCompliantRepositories.Should().HaveCount(3, 
        because: "null or empty filter should return all repos");
}
```

### 13. GetDashboardViewModelAsync - Name Filter No Matches

**Test Case**: `GetDashboardViewModelAsync_WhenNameFilterNoMatch_ReturnsEmpty`

**Objective**: Verify empty result when filter matches no repositories

**Related Test Case**: TC-DASH-002

```csharp
[Fact]
public async Task GetDashboardViewModelAsync_WhenNameFilterNoMatch_ReturnsEmpty()
{
    // Arrange
    var repo1 = CreateRepository(name: "repo-1");
    var repo2 = CreateRepository(name: "repo-2");
    await _dbContext.Repositories.AddRangeAsync(repo1, repo2);
    
    var scan = CreateCompletedScan();
    await _dbContext.Scans.AddAsync(scan);
    
    var policy = CreatePolicy("test-policy");
    await _dbContext.Policies.AddAsync(policy);
    
    var violation1 = CreateViolation(scan.ScanId, policy.PolicyId, repo1.RepositoryId);
    var violation2 = CreateViolation(scan.ScanId, policy.PolicyId, repo2.RepositoryId);
    await _dbContext.PolicyViolations.AddRangeAsync(violation1, violation2);
    
    await _dbContext.SaveChangesAsync();

    // Act
    var result = await _sut.GetDashboardViewModelAsync(nameFilter: "nonexistent");

    // Assert
    result.TotalRepositories.Should().Be(2, 
        because: "total count is not affected by filter");
    result.NonCompliantRepositories.Should().BeEmpty(
        because: "no repositories match the filter");
}
```

### 14. GetDashboardViewModelAsync - Zero Repositories Edge Case

**Test Case**: `GetDashboardViewModelAsync_WhenZeroRepositories_Returns100PercentCompliance`

**Objective**: Verify edge case when no repositories exist

**Related Test Case**: TC-DASH-001

```csharp
[Fact]
public async Task GetDashboardViewModelAsync_WhenZeroRepositories_Returns100PercentCompliance()
{
    // Arrange - Scan exists but no repositories
    var scan = CreateCompletedScan();
    await _dbContext.Scans.AddAsync(scan);
    await _dbContext.SaveChangesAsync();

    // Act
    var result = await _sut.GetDashboardViewModelAsync();

    // Assert
    result.TotalRepositories.Should().Be(0);
    result.CompliantRepositories.Should().Be(0);
    result.CompliancePercentage.Should().Be(100.0, 
        because: "0/0 should default to 100% compliance per code logic");
    result.NonCompliantRepositories.Should().BeEmpty();
}
```

### 15. GetDashboardViewModelAsync - Repository URL Format

**Test Case**: `GetDashboardViewModelAsync_WhenReposReturned_FormatsUrlCorrectly`

**Objective**: Verify GitHub URL is formatted correctly

**Related Test Case**: TC-DASH-001

```csharp
[Fact]
public async Task GetDashboardViewModelAsync_WhenReposReturned_FormatsUrlCorrectly()
{
    // Arrange
    var repository = CreateRepository(name: "owner/repo-name");
    await _dbContext.Repositories.AddAsync(repository);
    
    var scan = CreateCompletedScan();
    await _dbContext.Scans.AddAsync(scan);
    
    var policy = CreatePolicy("test-policy");
    await _dbContext.Policies.AddAsync(policy);
    
    var violation = CreateViolation(scan.ScanId, policy.PolicyId, repository.RepositoryId);
    await _dbContext.PolicyViolations.AddAsync(violation);
    
    await _dbContext.SaveChangesAsync();

    // Act
    var result = await _sut.GetDashboardViewModelAsync();

    // Assert
    var nonCompliantRepo = result.NonCompliantRepositories.First();
    nonCompliantRepo.Url.Should().Be("https://github.com/owner/repo-name");
}
```

### 16. GetDashboardViewModelAsync - Compliance Percentage Precision

**Test Case**: `GetDashboardViewModelAsync_WhenCalculatingPercentage_UsesPrecision`

**Objective**: Verify percentage calculation precision

**Related Test Case**: TC-DASH-001

```csharp
[Fact]
public async Task GetDashboardViewModelAsync_WhenCalculatingPercentage_UsesPrecision()
{
    // Arrange - 3 repos, 1 violates = 66.666...% compliance
    var repositories = CreateRepositories(3);
    await _dbContext.Repositories.AddRangeAsync(repositories);
    
    var scan = CreateCompletedScan();
    await _dbContext.Scans.AddAsync(scan);
    
    var policy = CreatePolicy("test-policy");
    await _dbContext.Policies.AddAsync(policy);
    
    var violation = CreateViolation(scan.ScanId, policy.PolicyId, repositories[0].RepositoryId);
    await _dbContext.PolicyViolations.AddAsync(violation);
    
    await _dbContext.SaveChangesAsync();

    // Act
    var result = await _sut.GetDashboardViewModelAsync();

    // Assert
    result.CompliancePercentage.Should().BeApproximately(66.666, 0.01, 
        because: "2/3 = 66.666...%");
}
```

### 17. GetDashboardViewModelAsync - Filter Only Affects Non-Compliant List

**Test Case**: `GetDashboardViewModelAsync_WhenFiltered_MetricsUnaffected`

**Objective**: Verify filter only affects non-compliant list, not metrics

**Related Test Case**: TC-DASH-002

```csharp
[Fact]
public async Task GetDashboardViewModelAsync_WhenFiltered_MetricsUnaffected()
{
    // Arrange
    var repo1 = CreateRepository(name: "frontend-app");
    var repo2 = CreateRepository(name: "backend-api");
    await _dbContext.Repositories.AddRangeAsync(repo1, repo2);
    
    var scan = CreateCompletedScan();
    await _dbContext.Scans.AddAsync(scan);
    
    var policy = CreatePolicy("test-policy");
    await _dbContext.Policies.AddAsync(policy);
    
    // Both repos have violations
    var violation1 = CreateViolation(scan.ScanId, policy.PolicyId, repo1.RepositoryId);
    var violation2 = CreateViolation(scan.ScanId, policy.PolicyId, repo2.RepositoryId);
    await _dbContext.PolicyViolations.AddRangeAsync(violation1, violation2);
    
    await _dbContext.SaveChangesAsync();

    // Act - Filter for one repo
    var result = await _sut.GetDashboardViewModelAsync(nameFilter: "frontend");

    // Assert
    result.TotalRepositories.Should().Be(2, 
        because: "total count includes all repositories");
    result.CompliantRepositories.Should().Be(0, 
        because: "compliance calculation includes all repositories");
    result.CompliancePercentage.Should().Be(0.0, 
        because: "percentage calculation includes all repositories");
    result.NonCompliantRepositories.Should().HaveCount(1, 
        because: "only the list is filtered");
}
```

### 18. GetDashboardViewModelAsync - Large Dataset Performance

**Test Case**: `GetDashboardViewModelAsync_WhenLargeDataset_PerformsEfficiently`

**Objective**: Verify service handles large datasets efficiently

**Related Test Case**: Performance consideration

```csharp
[Fact]
public async Task GetDashboardViewModelAsync_WhenLargeDataset_PerformsEfficiently()
{
    // Arrange - 100 repos, 50 with violations
    var repositories = CreateRepositories(100);
    await _dbContext.Repositories.AddRangeAsync(repositories);
    
    var scan = CreateCompletedScan();
    await _dbContext.Scans.AddAsync(scan);
    
    var policy = CreatePolicy("test-policy");
    await _dbContext.Policies.AddAsync(policy);
    
    // First 50 repos have violations
    for (int i = 0; i < 50; i++)
    {
        var violation = CreateViolation(scan.ScanId, policy.PolicyId, repositories[i].RepositoryId);
        await _dbContext.PolicyViolations.AddAsync(violation);
    }
    
    await _dbContext.SaveChangesAsync();

    // Act
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    var result = await _sut.GetDashboardViewModelAsync();
    stopwatch.Stop();

    // Assert
    result.TotalRepositories.Should().Be(100);
    result.CompliantRepositories.Should().Be(50);
    result.CompliancePercentage.Should().Be(50.0);
    result.NonCompliantRepositories.Should().HaveCount(50);
    
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, 
        because: "query should be efficient even with large datasets");
}
```

## Helper Methods

```csharp
/// <summary>
/// Creates a single repository entity
/// </summary>
private Repository CreateRepository(string? name = null)
{
    return new Repository
    {
        GitHubRepositoryId = _faker.Random.Long(1000, 999999),
        Name = name ?? _faker.Company.CompanyName(),
        Owner = _faker.Internet.UserName(),
        IsArchived = false,
        CreatedAt = DateTime.UtcNow.AddDays(-_faker.Random.Int(1, 365)),
        UpdatedAt = DateTime.UtcNow
    };
}

/// <summary>
/// Creates multiple repository entities
/// </summary>
private List<Repository> CreateRepositories(int count)
{
    var repositories = new List<Repository>();
    for (int i = 0; i < count; i++)
    {
        repositories.Add(CreateRepository());
    }
    return repositories;
}

/// <summary>
/// Creates a completed scan entity
/// </summary>
private Scan CreateCompletedScan(DateTime? completedAt = null)
{
    return new Scan
    {
        StartedAt = (completedAt ?? DateTime.UtcNow).AddMinutes(-30),
        CompletedAt = completedAt ?? DateTime.UtcNow,
        Status = "Completed"
    };
}

/// <summary>
/// Creates a scan entity with specified status
/// </summary>
private Scan CreateScan(string status)
{
    return new Scan
    {
        StartedAt = DateTime.UtcNow.AddMinutes(-30),
        CompletedAt = status == "Completed" ? DateTime.UtcNow : null,
        Status = status
    };
}

/// <summary>
/// Creates a policy entity
/// </summary>
private Policy CreatePolicy(string policyKey)
{
    return new Policy
    {
        PolicyKey = policyKey,
        Name = _faker.Lorem.Sentence(3),
        Description = _faker.Lorem.Paragraph()
    };
}

/// <summary>
/// Creates a violation entity
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
```

## Test Execution Order

1. **No completed scans** - Baseline edge case
2. **No violations** - Happy path (100% compliance)
3. **Some violations** - Core calculation testing
4. **All violations** - Edge case (0% compliance)
5. **Zero repositories** - Division by zero edge case
6. **Multiple violations per repo** - Complex scenario
7. **Latest scan selection** - Verify ordering logic
8. **Ignore in-progress/failed scans** - Status filtering
9. **Name filter exact match** - Simple filtering
10. **Name filter partial match** - Complex filtering
11. **Name filter case sensitivity** - String comparison
12. **Empty name filter** - Null/empty handling
13. **Name filter no matches** - Empty result
14. **Filter doesn't affect metrics** - Verify calculation independence
15. **URL formatting** - Output validation
16. **Compliance percentage precision** - Math accuracy
17. **Large dataset** - Performance validation

## Code Coverage Expectations

**Target Coverage**: 85-90%

**What to Cover**:

- ✅ All compliance calculation paths
- ✅ Scan selection logic (latest completed only)
- ✅ Name filtering behavior
- ✅ Edge cases (no scans, no repos, all compliant, all non-compliant)
- ✅ Multiple violations per repository
- ✅ Repository URL formatting
- ✅ Division by zero handling (0 repositories)

**What NOT to Cover**:

- ❌ Entity Framework internal query generation
- ❌ LINQ internal implementation
- ❌ In-memory database provider internals

## Implementation Notes

### In-Memory Database Considerations

```csharp
// Use unique database per test
var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
    .Options;
```

### Testing Query Ordering

The service uses `OrderByDescending(s => s.CompletedAt)` to get the latest scan. Test this by creating multiple scans with different completion times:

```csharp
var olderScan = CreateCompletedScan(completedAt: DateTime.UtcNow.AddDays(-2));
var newerScan = CreateCompletedScan(completedAt: DateTime.UtcNow.AddDays(-1));
```

### Testing Includes

The service uses `.Include()` to load related entities. In-memory database handles this automatically, but verify the relationships are set correctly in test data.

### Name Filter Case Sensitivity

EF Core in-memory provider uses case-sensitive `Contains()` by default. This differs from SQL Server which is case-insensitive. Document this behavior in tests.

### Compliance Percentage Edge Cases

The service has special logic for division by zero:

```csharp
viewModel.CompliancePercentage = totalRepositories > 0
    ? (double)viewModel.CompliantRepositories / totalRepositories * 100
    : 100;
```

Test both branches explicitly.

### ViewModels vs Entities

Service returns `DashboardViewModel` which contains `NonCompliantRepositoryViewModel`. Ensure test assertions use these view models, not entities.

## Performance Considerations

### Query Efficiency

The service makes several queries:

1. Get latest completed scan
2. Count total repositories
3. Get violations for scan (with includes)
4. Get non-compliant repositories (with filter)

Consider testing with larger datasets (100+ repositories) to verify reasonable performance.

### N+1 Query Prevention

The service uses `Include()` to prevent N+1 queries. Verify that all required data is loaded in a single query per entity type.

## Running Tests

```bash
# Run all DashboardService tests
dotnet test --filter FullyQualifiedName~DashboardServiceTests

# Run specific test
dotnet test --filter FullyQualifiedName~DashboardServiceTests.GetDashboardViewModelAsync_WhenNoCompletedScans_ReturnsEmptyViewModel

# Run with coverage
dotnet test --filter FullyQualifiedName~DashboardServiceTests /p:CollectCoverage=true

# Watch mode for TDD
dotnet watch test --filter FullyQualifiedName~DashboardServiceTests

# Run only filtering tests
dotnet test --filter "FullyQualifiedName~DashboardServiceTests&FullyQualifiedName~Filter"

# Run only compliance calculation tests
dotnet test --filter "FullyQualifiedName~DashboardServiceTests&FullyQualifiedName~Compliance"
```

## Success Criteria

- ✅ All 18 test scenarios pass
- ✅ Code coverage > 85%
- ✅ All test cases from test plan (TC-DASH-001, TC-DASH-002) covered
- ✅ Test execution time < 10 seconds total (including large dataset test)
- ✅ No flaky tests (tests pass consistently)
- ✅ Proper test isolation (tests can run in any order)
- ✅ Clear test names following `MethodName_WhenCondition_ExpectedBehavior` pattern
- ✅ Comprehensive assertion messages with `because` parameter
- ✅ Edge cases thoroughly tested (no scans, no repos, zero division)

## Integration with Existing Tests

### Dependencies Tested Elsewhere

- **ApplicationDbContext**: Entity configurations tested separately
- **DashboardViewModel**: Data transfer object (no logic to test)

### Related Test Coverage

This test suite focuses on:

- Dashboard data aggregation logic
- Compliance calculation correctness
- Name filtering behavior
- Scan selection logic

Component tests (bUnit) should cover:

- Dashboard UI rendering
- Real-time filtering interaction
- Compliance percentage display

## Next Steps After Implementation

1. Review test coverage report
2. Add Theory tests for edge cases if needed
3. Consider adding integration tests with real SQL database
4. Plan bUnit component tests for Index.razor (dashboard UI)
5. Update dashboard documentation with calculation examples
6. Consider adding performance benchmarks for large organizations

## Common Pitfalls to Avoid

❌ **Don't**: Assume SQL Server case-insensitivity matches in-memory provider

✅ **Do**: Document case-sensitivity behavior in tests

❌ **Don't**: Test LINQ query syntax

✅ **Do**: Test query results and business logic

❌ **Don't**: Hard-code repository counts in assertions

✅ **Do**: Use variables to make test data setup clear

❌ **Don't**: Forget to test filter independence from metrics

✅ **Do**: Verify filter only affects NonCompliantRepositories list

❌ **Don't**: Skip edge cases (0 repos, 0 scans)

✅ **Do**: Explicitly test boundary conditions

❌ **Don't**: Test EF Core internals

✅ **Do**: Test service behavior and correctness of returned data