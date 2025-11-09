using _10xGitHubPolicies.App.Data.Entities;
using _10xGitHubPolicies.App.Services.Configuration.Models;
using _10xGitHubPolicies.Tests.Integration.Builders;
using _10xGitHubPolicies.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace _10xGitHubPolicies.Tests.Integration.Action;

[Collection("ActionService Integration Tests")]
[Trait("Category", "Integration")]
[Trait("Service", "ActionService")]
[Trait("Feature", "ArchiveAction")]
public class ActionServiceArchiveTests : ActionServiceIntegrationTestBase
{
    private readonly GitHubApiResponseBuilder _responseBuilder;

    public ActionServiceArchiveTests(GitHubApiFixture gitHubApiFixture, DatabaseFixture databaseFixture)
        : base(gitHubApiFixture, databaseFixture)
    {
        _responseBuilder = new GitHubApiResponseBuilder();
    }

    /// <summary>
    /// ProcessActionsForScanAsync - Archive Repository Success
    /// Verifies that archive action successfully archives repository and logs action to database
    /// </summary>
    [Fact]
    public async Task ProcessActionsForScanAsync_WhenArchiveRepoAction_ArchivesRepositoryAndLogsAction()
    {
        Console.WriteLine($"[Test] ProcessActionsForScanAsync_WhenArchiveRepoAction_ArchivesRepositoryAndLogsAction started at {DateTime.UtcNow:HH:mm:ss.fff}");

        // Arrange
        Console.WriteLine($"[Test] Setting up GitHub authentication at {DateTime.UtcNow:HH:mm:ss.fff}");
        SetupGitHubAppAuthentication();

        var scan = await CreateScanAsync();
        var policy = await CreatePolicyAsync("test-policy", "archive-repo");
        var repository = await CreateRepositoryAsync("test-repo", 12345);
        var violation = await CreateViolationAsync(scan.ScanId, policy.PolicyId, repository.RepositoryId);

        // Mock repository settings (not archived)
        Console.WriteLine($"[Test] Setting up WireMock stubs at {DateTime.UtcNow:HH:mm:ss.fff}");
        var repoSettingsJson = _responseBuilder.BuildRepositoryResponse(repository.GitHubRepositoryId, repository.Name, archived: false);
        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{repository.GitHubRepositoryId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(repoSettingsJson)
                .WithHeader("Content-Type", "application/json"));

        // Also mock with /api/v3/ prefix in case Octokit prepends it
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repository.GitHubRepositoryId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(repoSettingsJson)
                .WithHeader("Content-Type", "application/json"));

        // Mock archive repository endpoint
        var archivedRepoJson = _responseBuilder.BuildRepositoryResponse(repository.GitHubRepositoryId, repository.Name, archived: true);
        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{repository.GitHubRepositoryId}")
                .UsingPatch())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(archivedRepoJson)
                .WithHeader("Content-Type", "application/json"));

        // Also mock with /api/v3/ prefix in case Octokit prepends it
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repository.GitHubRepositoryId}")
                .UsingPatch())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(archivedRepoJson)
                .WithHeader("Content-Type", "application/json"));
        Console.WriteLine($"[Test] WireMock stubs configured at {DateTime.UtcNow:HH:mm:ss.fff}");

        var policyConfig = new PolicyConfig
        {
            Type = policy.PolicyKey,
            Name = "Test Policy",
            Action = "archive-repo"
        };

        var appConfig = new AppConfig
        {
            AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
            Policies = new List<PolicyConfig> { policyConfig }
        };

        ConfigurationService.GetConfigAsync().Returns(appConfig);

        // Act
        await Sut.ProcessActionsForScanAsync(scan.ScanId);

        // Assert - Verify archive request was made
        var requests = MockServer.LogEntries;
        requests.Should().Contain(r =>
            r.RequestMessage.Path.Contains($"/repositories/{repository.GitHubRepositoryId}") &&
            r.RequestMessage.Method == "PATCH");

        // Assert - Verify action log was persisted
        var actionLog = await DbContext.ActionsLogs
            .FirstOrDefaultAsync(a => a.RepositoryId == repository.RepositoryId && a.PolicyId == policy.PolicyId);

        actionLog.Should().NotBeNull();
        actionLog!.ActionType.Should().Be("archive-repo");
        actionLog.Status.Should().Be("Success");
        actionLog.Details.Should().Contain("Repository archived");
        actionLog.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// ProcessActionsForScanAsync - Archive Already Archived Repository
    /// Verifies that archive action skips when repository is already archived
    /// </summary>
    [Fact]
    public async Task ProcessActionsForScanAsync_WhenRepositoryAlreadyArchived_SkipsArchive()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        var scan = await CreateScanAsync();
        var policy = await CreatePolicyAsync("test-policy", "archive-repo");
        var repository = await CreateRepositoryAsync("test-repo", 12345);
        var violation = await CreateViolationAsync(scan.ScanId, policy.PolicyId, repository.RepositoryId);

        // Mock repository settings (already archived)
        var repoSettingsJson = _responseBuilder.BuildRepositoryResponse(repository.GitHubRepositoryId, repository.Name, archived: true);
        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{repository.GitHubRepositoryId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(repoSettingsJson)
                .WithHeader("Content-Type", "application/json"));

        // Also mock with /api/v3/ prefix in case Octokit prepends it
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repository.GitHubRepositoryId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(repoSettingsJson)
                .WithHeader("Content-Type", "application/json"));

        var policyConfig = new PolicyConfig
        {
            Type = policy.PolicyKey,
            Name = "Test Policy",
            Action = "archive-repo"
        };

        var appConfig = new AppConfig
        {
            AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
            Policies = new List<PolicyConfig> { policyConfig }
        };

        ConfigurationService.GetConfigAsync().Returns(appConfig);

        // Act
        await Sut.ProcessActionsForScanAsync(scan.ScanId);

        // Assert - Verify archive request was NOT made (only GET for settings)
        var requests = MockServer.LogEntries;
        requests.Should().NotContain(r =>
            r.RequestMessage.Path.Contains($"/repositories/{repository.GitHubRepositoryId}") &&
            r.RequestMessage.Method == "PATCH");

        // Assert - Verify action log was persisted with "Skipped" status
        var actionLog = await DbContext.ActionsLogs
            .FirstOrDefaultAsync(a => a.RepositoryId == repository.RepositoryId && a.PolicyId == policy.PolicyId);

        actionLog.Should().NotBeNull();
        actionLog!.ActionType.Should().Be("archive-repo");
        actionLog.Status.Should().Be("Skipped");
        actionLog.Details.Should().Contain("already archived");
    }

    /// <summary>
    /// ProcessActionsForScanAsync - Multiple Violations with Archive Action
    /// Verifies that multiple violations are processed independently and logged separately
    /// </summary>
    [Fact]
    public async Task ProcessActionsForScanAsync_WhenMultipleViolations_ProcessesAllAndLogsSeparately()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        var scan = await CreateScanAsync();
        var policy = await CreatePolicyAsync("test-policy", "archive-repo");
        var repo1 = await CreateRepositoryAsync("repo1", 11111);
        var repo2 = await CreateRepositoryAsync("repo2", 22222);
        var repo3 = await CreateRepositoryAsync("repo3", 33333);

        var violation1 = await CreateViolationAsync(scan.ScanId, policy.PolicyId, repo1.RepositoryId);
        var violation2 = await CreateViolationAsync(scan.ScanId, policy.PolicyId, repo2.RepositoryId);
        var violation3 = await CreateViolationAsync(scan.ScanId, policy.PolicyId, repo3.RepositoryId);

        // Mock repository settings for all repos (not archived)
        foreach (var repo in new[] { repo1, repo2, repo3 })
        {
            var repoSettingsJson = _responseBuilder.BuildRepositoryResponse(repo.GitHubRepositoryId, repo.Name, archived: false);
            MockServer
                .Given(Request.Create()
                    .WithPath($"/repositories/{repo.GitHubRepositoryId}")
                    .UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithBody(repoSettingsJson)
                    .WithHeader("Content-Type", "application/json"));

            // Also mock with /api/v3/ prefix in case Octokit prepends it
            MockServer
                .Given(Request.Create()
                    .WithPath($"/api/v3/repositories/{repo.GitHubRepositoryId}")
                    .UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithBody(repoSettingsJson)
                    .WithHeader("Content-Type", "application/json"));

            // Mock archive endpoint
            var archivedRepoJson = _responseBuilder.BuildRepositoryResponse(repo.GitHubRepositoryId, repo.Name, archived: true);
            MockServer
                .Given(Request.Create()
                    .WithPath($"/repositories/{repo.GitHubRepositoryId}")
                    .UsingPatch())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithBody(archivedRepoJson)
                    .WithHeader("Content-Type", "application/json"));

            // Also mock with /api/v3/ prefix in case Octokit prepends it
            MockServer
                .Given(Request.Create()
                    .WithPath($"/api/v3/repositories/{repo.GitHubRepositoryId}")
                    .UsingPatch())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithBody(archivedRepoJson)
                    .WithHeader("Content-Type", "application/json"));
        }

        var policyConfig = new PolicyConfig
        {
            Type = policy.PolicyKey,
            Name = "Test Policy",
            Action = "archive-repo"
        };

        var appConfig = new AppConfig
        {
            AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
            Policies = new List<PolicyConfig> { policyConfig }
        };

        ConfigurationService.GetConfigAsync().Returns(appConfig);

        // Act
        await Sut.ProcessActionsForScanAsync(scan.ScanId);

        // Assert - Verify all three repositories were archived
        var archiveRequests = MockServer.LogEntries
            .Where(r => r.RequestMessage.Method == "PATCH" && r.RequestMessage.Path.Contains("/repositories/"))
            .ToList();

        archiveRequests.Should().HaveCount(3);

        // Assert - Verify three action logs were created
        var actionLogs = await DbContext.ActionsLogs
            .Where(a => a.PolicyId == policy.PolicyId && a.ActionType == "archive-repo")
            .ToListAsync();

        actionLogs.Should().HaveCount(3);
        actionLogs.All(a => a.Status == "Success").Should().BeTrue();
        actionLogs.Select(a => a.RepositoryId).Should().BeEquivalentTo(new[] { repo1.RepositoryId, repo2.RepositoryId, repo3.RepositoryId });
    }

    /// <summary>
    /// ProcessActionsForScanAsync - Archive Action Failure Handling
    /// Verifies that archive action failures are logged but don't block other actions
    /// </summary>
    [Fact]
    public async Task ProcessActionsForScanAsync_WhenArchiveFails_LogsFailureAndContinues()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        var scan = await CreateScanAsync();
        var policy = await CreatePolicyAsync("test-policy", "archive-repo");
        var repo1 = await CreateRepositoryAsync("repo1", 11111);
        var repo2 = await CreateRepositoryAsync("repo2", 22222);

        var violation1 = await CreateViolationAsync(scan.ScanId, policy.PolicyId, repo1.RepositoryId);
        var violation2 = await CreateViolationAsync(scan.ScanId, policy.PolicyId, repo2.RepositoryId);

        // Mock repo1 settings (not archived)
        var repo1SettingsJson = _responseBuilder.BuildRepositoryResponse(repo1.GitHubRepositoryId, repo1.Name, archived: false);
        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{repo1.GitHubRepositoryId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(repo1SettingsJson)
                .WithHeader("Content-Type", "application/json"));

        // Also mock with /api/v3/ prefix in case Octokit prepends it
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repo1.GitHubRepositoryId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(repo1SettingsJson)
                .WithHeader("Content-Type", "application/json"));

        // Mock repo1 archive failure (Forbidden)
        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{repo1.GitHubRepositoryId}")
                .UsingPatch())
            .RespondWith(Response.Create()
                .WithStatusCode(403)
                .WithBody("{\"message\": \"Insufficient permissions\"}")
                .WithHeader("Content-Type", "application/json"));

        // Also mock with /api/v3/ prefix in case Octokit prepends it
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repo1.GitHubRepositoryId}")
                .UsingPatch())
            .RespondWith(Response.Create()
                .WithStatusCode(403)
                .WithBody("{\"message\": \"Insufficient permissions\"}")
                .WithHeader("Content-Type", "application/json"));

        // Mock repo2 settings (not archived)
        var repo2SettingsJson = _responseBuilder.BuildRepositoryResponse(repo2.GitHubRepositoryId, repo2.Name, archived: false);
        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{repo2.GitHubRepositoryId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(repo2SettingsJson)
                .WithHeader("Content-Type", "application/json"));

        // Also mock with /api/v3/ prefix in case Octokit prepends it
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repo2.GitHubRepositoryId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(repo2SettingsJson)
                .WithHeader("Content-Type", "application/json"));

        // Mock repo2 archive success
        var archivedRepo2Json = _responseBuilder.BuildRepositoryResponse(repo2.GitHubRepositoryId, repo2.Name, archived: true);
        MockServer
            .Given(Request.Create()
                .WithPath($"/repositories/{repo2.GitHubRepositoryId}")
                .UsingPatch())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(archivedRepo2Json)
                .WithHeader("Content-Type", "application/json"));

        // Also mock with /api/v3/ prefix in case Octokit prepends it
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repo2.GitHubRepositoryId}")
                .UsingPatch())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(archivedRepo2Json)
                .WithHeader("Content-Type", "application/json"));

        var policyConfig = new PolicyConfig
        {
            Type = policy.PolicyKey,
            Name = "Test Policy",
            Action = "archive-repo"
        };

        var appConfig = new AppConfig
        {
            AccessControl = new AccessControlConfig { AuthorizedTeam = "org/team" },
            Policies = new List<PolicyConfig> { policyConfig }
        };

        ConfigurationService.GetConfigAsync().Returns(appConfig);

        // Act
        await Sut.ProcessActionsForScanAsync(scan.ScanId);

        // Assert - Verify both actions were attempted
        var archiveRequests = MockServer.LogEntries
            .Where(r => r.RequestMessage.Method == "PATCH" && r.RequestMessage.Path.Contains("/repositories/"))
            .ToList();

        archiveRequests.Should().HaveCount(2);

        // Assert - Verify action logs: one Failed, one Success
        var actionLogs = await DbContext.ActionsLogs
            .Where(a => a.PolicyId == policy.PolicyId && a.ActionType == "archive-repo")
            .OrderBy(a => a.RepositoryId)
            .ToListAsync();

        actionLogs.Should().HaveCount(2);
        actionLogs[0].Status.Should().Be("Failed");
        actionLogs[0].Details.Should().Contain("Insufficient permissions");
        actionLogs[1].Status.Should().Be("Success");
    }
}

