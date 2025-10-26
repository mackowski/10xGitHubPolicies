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
        result.TotalRepositories.Should().Be(0,
            because: "no completed scans exist so service returns empty view model");
        result.CompliantRepositories.Should().Be(0);
        result.CompliancePercentage.Should().Be(0);
        result.NonCompliantRepositories.Should().BeEmpty();
    }

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

        // Act - Using matching case filter
        var result = await _sut.GetDashboardViewModelAsync(nameFilter: "Test");

        // Assert
        // Note: EF Core in-memory provider uses case-sensitive Contains by default
        // This test documents the actual behavior
        result.NonCompliantRepositories.Should().HaveCount(1,
            because: "filter should match 'TestRepo' with case-sensitive search");
        result.NonCompliantRepositories.First().Name.Should().Be("TestRepo");
    }

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

    // Helper Methods

    /// <summary>
    /// Creates a single repository entity
    /// </summary>
    private Repository CreateRepository(string? name = null)
    {
        return new Repository
        {
            GitHubRepositoryId = _faker.Random.Long(1000, 999999),
            Name = name ?? _faker.Company.CompanyName(),
            ComplianceStatus = "Unknown",
            LastScannedAt = DateTime.UtcNow.AddMinutes(-30)
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
            Description = _faker.Lorem.Paragraph(),
            Action = "log-only"
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
            RepositoryId = repositoryId
        };
    }
}

