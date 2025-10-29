using Microsoft.Playwright.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;
using FluentAssertions;
using _10xGitHubPolicies.Tests.E2E.Pages;
using Microsoft.Extensions.DependencyInjection;
using Octokit;
using Microsoft.EntityFrameworkCore;
using _10xGitHubPolicies.App.Data.Entities;
using _10xGitHubPolicies.Tests.E2E.Infrastructure;
using _10xGitHubPolicies.Tests.E2E.Fixtures;
using _10xGitHubPolicies.Tests.E2E.Helpers;

namespace _10xGitHubPolicies.Tests.E2E.Workflow;

[TestFixture]
public class WorkflowTests : E2ETestBase
{
    private DashboardPage _dashboardPage = null!;
    private ITestDataManager _testDataManager = null!;
    private ITestCleanupService _cleanupService = null!;
    private List<string> _createdRepositories = new();

    [SetUp]
    public new async Task SetupAsync()
    {
        await base.SetupAsync();
        _dashboardPage = new DashboardPage(Page);
        _testDataManager = ServiceProvider.GetRequiredService<ITestDataManager>();
        _cleanupService = ServiceProvider.GetRequiredService<ITestCleanupService>();
    }

    [TearDown]
    public new async Task TearDownAsync()
    {
        // Clean up any remaining repositories
        await RepositoryHelper.CleanupRepositoriesAsync(GitHubService, _createdRepositories);
        _createdRepositories.Clear();

        // Call base teardown to properly dispose of the test host
        await base.TearDownAsync();
    }

    [Test]
    [Category("E2E-Workflow")]
    public async Task CompletePolicyEnforcementWorkflow_ShouldWorkEndToEnd()
    {
        Console.WriteLine("🚀 Starting Complete Policy Enforcement Workflow Test");

        // Step 1: Create test repositories (compliant and non-compliant)
        Console.WriteLine("📁 Step 1: Create test repositories");

        var compliantRepo = await _testDataManager.CreateCompliantRepositoryAsync("workflow-test-compliant");
        var nonCompliantRepo = await _testDataManager.CreateNonCompliantRepositoryAsync("workflow-test-non-compliant", new[] { "has_agents_md" });

        _createdRepositories.Add(compliantRepo.Name);
        _createdRepositories.Add(nonCompliantRepo.Name);

        Console.WriteLine($"✅ Created repositories: {compliantRepo.Name} (compliant), {nonCompliantRepo.Name} (non-compliant)");

        // Wait for repositories to be fully available in GitHub API
        Console.WriteLine("⏳ Waiting for repositories to be fully available in GitHub API...");
        await Task.Delay(TestConstants.RepositoryPropagationDelaySeconds * 1000);

        // Verify our repositories are visible to the GitHub API
        var foundRepos = await RepositoryHelper.VerifyRepositoriesVisibleAsync(
            GitHubService,
            compliantRepo.Name,
            nonCompliantRepo.Name);

        var ourCompliantRepo = foundRepos[0];
        var ourNonCompliantRepo = foundRepos[1];

        // Step 2: Navigate to dashboard and verify initial state
        Console.WriteLine("📊 Step 2: Navigate to dashboard and verify initial state");

        // Navigate to the web application (this should already be verified in SetupAsync)
        await Page.GotoAsync(BaseUrl);
        await _dashboardPage.WaitForPageLoad();

        Console.WriteLine("✅ Successfully navigated to dashboard");

        // Take screenshot of initial state
        await ScreenshotHelper.TakeScreenshotAsync(Page, TestContext.CurrentContext.Test.Name, "01-initial-dashboard-state");

        // Step 3: Run initial scan
        Console.WriteLine("🔍 Step 3: Run initial scan");

        // Click scan button with error handling
        await _dashboardPage.ClickScanButton($"{TestContext.CurrentContext.Test.Name}-error-scan-button");

        // Wait for scan to complete
        await _dashboardPage.WaitForScanComplete();

        // Debug: Check what repositories the web application can see BEFORE refresh
        Console.WriteLine("🔍 Debug: Checking what repositories the web application can see...");
        var webAppRepos = await GitHubService.GetOrganizationRepositoriesAsync();
        var testRepoNames = new[] { compliantRepo.Name, nonCompliantRepo.Name };
        var foundTestRepos = webAppRepos.Where(r => testRepoNames.Contains(r.Name)).ToList();
        Console.WriteLine($"🔍 Web app found {webAppRepos.Count} total repositories");
        Console.WriteLine($"🔍 Web app found {foundTestRepos.Count} test repositories: {string.Join(", ", foundTestRepos.Select(r => r.Name))}");

        // Check if our specific test repositories are in the list
        var foundCompliantRepo = webAppRepos.FirstOrDefault(r => r.Name == compliantRepo.Name);
        var foundNonCompliantRepo = webAppRepos.FirstOrDefault(r => r.Name == nonCompliantRepo.Name);
        Console.WriteLine($"🔍 Compliant repo found: {foundCompliantRepo != null} (ID: {foundCompliantRepo?.Id})");
        Console.WriteLine($"🔍 Non-compliant repo found: {foundNonCompliantRepo != null} (ID: {foundNonCompliantRepo?.Id})");

        // Wait for scan results to be fully processed and stored in database
        var latestScan = await DatabaseHelper.WaitForScanCompleteAsync(DbContext, ourNonCompliantRepo.Id);


        // Refresh the page to show updated scan results
        await _dashboardPage.ReloadAndWaitForGrid();

        await ScreenshotHelper.TakeScreenshotAsync(Page, TestContext.CurrentContext.Test.Name, "02-after-initial-scan");

        // Step 4: Verify compliance status and violations
        Console.WriteLine("✅ Step 4: Verify compliance status and violations");

        // Take a screenshot to see the current state
        await ScreenshotHelper.TakeScreenshotAsync(Page, TestContext.CurrentContext.Test.Name, "04-compliance-check");

        // The UI displays full repository names with organization prefix (e.g., "mackowski-corp/repo-name")
        var fullNonCompliantRepoName = $"mackowski-corp/{nonCompliantRepo.Name}";
        var fullCompliantRepoName = $"mackowski-corp/{compliantRepo.Name}";

        // Verify both repositories are in the non-compliant list
        Console.WriteLine($"🔍 Verifying both repositories are in the non-compliant list");

        var isNonCompliantRepoVisible = await _dashboardPage.IsRepositoryVisible(fullNonCompliantRepoName);
        var isCompliantRepoVisible = await _dashboardPage.IsRepositoryVisible(fullCompliantRepoName);

        Console.WriteLine($"🔍 Non-compliant repository visibility: {isNonCompliantRepoVisible}");
        Console.WriteLine($"🔍 Compliant repository visibility: {isCompliantRepoVisible}");


        isNonCompliantRepoVisible.Should().BeTrue($"Repository {fullNonCompliantRepoName} should be visible in non-compliant list");
        isCompliantRepoVisible.Should().BeTrue($"Repository {fullCompliantRepoName} should be visible in non-compliant list");

        // Verify violations for non-compliant repository (missing AGENTS.md)
        // Should have: has_catalog_info_yaml, correct_workflow_permissions, has_agents_md
        Console.WriteLine($"🔍 Verifying violations for non-compliant repository: {fullNonCompliantRepoName}");
        var nonCompliantViolations = await _dashboardPage.GetRepositoryViolations(fullNonCompliantRepoName);
        Console.WriteLine($"🔍 Found violations: {string.Join(", ", nonCompliantViolations)}");

        nonCompliantViolations.Should().Contain("has_catalog_info_yaml", "Non-compliant repo should have has_catalog_info_yaml violation");
        nonCompliantViolations.Should().Contain("correct_workflow_permissions", "Non-compliant repo should have correct_workflow_permissions violation");
        nonCompliantViolations.Should().Contain("has_agents_md", "Non-compliant repo should have has_agents_md violation (missing AGENTS.md)");

        // Verify violations for compliant repository (has AGENTS.md)
        // Should have: has_catalog_info_yaml, correct_workflow_permissions (but NOT has_agents_md)
        Console.WriteLine($"🔍 Verifying violations for compliant repository: {fullCompliantRepoName}");
        var compliantViolations = await _dashboardPage.GetRepositoryViolations(fullCompliantRepoName);
        Console.WriteLine($"🔍 Found violations: {string.Join(", ", compliantViolations)}");

        compliantViolations.Should().Contain("has_catalog_info_yaml", "Compliant repo should have has_catalog_info_yaml violation");
        compliantViolations.Should().Contain("correct_workflow_permissions", "Compliant repo should have correct_workflow_permissions violation");
        compliantViolations.Should().NotContain("has_agents_md", "Compliant repo should NOT have has_agents_md violation (has AGENTS.md)");

        Console.WriteLine($"✅ Compliance verification passed - Both repositories are in non-compliant list with correct violations");

        // Step 5: Verify GitHub issues were created
        Console.WriteLine("📝 Step 5: Verify GitHub issues were created");

        // Wait for action processing to complete
        await DatabaseHelper.WaitForActionLogsAsync(DbContext, ourNonCompliantRepo.Id);

        // Wait for GitHub issues to be visible (GitHub API propagation delay)
        var policyViolationIssues = await RepositoryHelper.WaitForPolicyViolationIssuesAsync(
            GitHubService,
            nonCompliantRepo.Name);

        policyViolationIssues.Should().NotBeEmpty($"Policy violation issues should be created for {nonCompliantRepo.Name}");

        // Step 6: Make compliant repository non-compliant
        Console.WriteLine("🔄 Step 6: Make compliant repository non-compliant");

        // Delete the AGENTS.md file from the compliant repository
        await GitHubService.DeleteFileAsync(compliantRepo.Name, "AGENTS.md", "Remove AGENTS.md to make repository non-compliant for testing");
        Console.WriteLine($"✅ Removed AGENTS.md from {compliantRepo.Name}");

        // Step 7: Make non-compliant repository compliant
        Console.WriteLine("🔄 Step 7: Make non-compliant repository compliant");

        // Add AGENTS.md file to the non-compliant repository
        await GitHubService.CreateFileAsync(nonCompliantRepo.Id, "AGENTS.md", "# AGENTS.md\n\nThis repository is now compliant with the policy.", "Add AGENTS.md to make repository compliant");
        Console.WriteLine($"✅ Added AGENTS.md to {nonCompliantRepo.Name}");

        // Step 8: Run re-scan
        Console.WriteLine("🔍 Step 8: Run re-scan");

        // Click scan button with error handling
        await _dashboardPage.ClickScanButton($"{TestContext.CurrentContext.Test.Name}-error-scan-button-rescan");

        // Wait for scan to complete
        await _dashboardPage.WaitForScanComplete();

        // Wait for re-scan results to be fully processed and stored in database
        var rescanLatestScan = await DatabaseHelper.WaitForRescanCompleteAsync(DbContext, latestScan?.ScanId, compliantRepo.Id);

        // Navigate to dashboard to show updated scan results
        await Page.GotoAsync(BaseUrl);
        await _dashboardPage.WaitForPageLoad();

        await ScreenshotHelper.TakeScreenshotAsync(Page, TestContext.CurrentContext.Test.Name, "03-after-rescan");

        // Step 9: Verify updated compliance status and violations
        Console.WriteLine("✅ Step 9: Verify updated compliance status and violations");

        // Re-declare the full repository names (they were scoped to Step 4)
        var fullCompliantRepoNameAfterRescan = $"mackowski-corp/{compliantRepo.Name}";
        var fullNonCompliantRepoNameAfterRescan = $"mackowski-corp/{nonCompliantRepo.Name}";

        // Verify both repositories are still in the non-compliant list
        Console.WriteLine($"🔍 Verifying both repositories are still in the non-compliant list");
        var isPreviouslyCompliantNowNonCompliant = await _dashboardPage.IsRepositoryVisible(fullCompliantRepoNameAfterRescan);
        var isPreviouslyNonCompliantStillVisible = await _dashboardPage.IsRepositoryVisible(fullNonCompliantRepoNameAfterRescan);

        Console.WriteLine($"🔍 Previously compliant repository visibility: {isPreviouslyCompliantNowNonCompliant}");
        Console.WriteLine($"🔍 Previously non-compliant repository visibility: {isPreviouslyNonCompliantStillVisible}");

        isPreviouslyCompliantNowNonCompliant.Should().BeTrue($"Repository {fullCompliantRepoNameAfterRescan} should be visible in non-compliant list");
        isPreviouslyNonCompliantStillVisible.Should().BeTrue($"Repository {fullNonCompliantRepoNameAfterRescan} should be visible in non-compliant list");

        // Verify violations for previously compliant repository (we removed AGENTS.md)
        // Should now have: has_catalog_info_yaml, correct_workflow_permissions, has_agents_md
        Console.WriteLine($"🔍 Verifying violations for previously compliant repository (AGENTS.md removed): {fullCompliantRepoNameAfterRescan}");
        var previouslyCompliantViolations = await _dashboardPage.GetRepositoryViolations(fullCompliantRepoNameAfterRescan);
        Console.WriteLine($"🔍 Found violations: {string.Join(", ", previouslyCompliantViolations)}");

        previouslyCompliantViolations.Should().Contain("has_catalog_info_yaml", "Previously compliant repo should have has_catalog_info_yaml violation");
        previouslyCompliantViolations.Should().Contain("correct_workflow_permissions", "Previously compliant repo should have correct_workflow_permissions violation");
        previouslyCompliantViolations.Should().Contain("has_agents_md", "Previously compliant repo should now have has_agents_md violation (AGENTS.md was removed)");

        // Verify violations for previously non-compliant repository (we added AGENTS.md)
        // Should now have: has_catalog_info_yaml, correct_workflow_permissions (but NOT has_agents_md)
        Console.WriteLine($"🔍 Verifying violations for previously non-compliant repository (AGENTS.md added): {fullNonCompliantRepoNameAfterRescan}");
        var previouslyNonCompliantViolations = await _dashboardPage.GetRepositoryViolations(fullNonCompliantRepoNameAfterRescan);
        Console.WriteLine($"🔍 Found violations: {string.Join(", ", previouslyNonCompliantViolations)}");

        previouslyNonCompliantViolations.Should().Contain("has_catalog_info_yaml", "Previously non-compliant repo should have has_catalog_info_yaml violation");
        previouslyNonCompliantViolations.Should().Contain("correct_workflow_permissions", "Previously non-compliant repo should have correct_workflow_permissions violation");
        previouslyNonCompliantViolations.Should().NotContain("has_agents_md", "Previously non-compliant repo should NOT have has_agents_md violation (AGENTS.md was added)");

        Console.WriteLine($"✅ Updated compliance verification passed - Violations updated correctly after re-scan");

        // Step 10: Verify new GitHub issues were created
        Console.WriteLine("📝 Step 10: Verify new GitHub issues were created");

        // Wait for action processing to complete for the re-scan
        await DatabaseHelper.WaitForActionLogsAsync(DbContext, compliantRepo.Id);

        // Wait for GitHub issues to be visible (GitHub API propagation delay)
        var newPolicyViolationIssues = await RepositoryHelper.WaitForPolicyViolationIssuesAsync(
            GitHubService,
            compliantRepo.Name);

        newPolicyViolationIssues.Should().NotBeEmpty($"New policy violation issues should be created for {compliantRepo.Name}");

        await ScreenshotHelper.TakeScreenshotAsync(Page, TestContext.CurrentContext.Test.Name, "04-final-dashboard-state");

        Console.WriteLine("🎉 Complete Policy Enforcement Workflow Test PASSED!");
    }
}

