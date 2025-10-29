using Microsoft.Playwright;
using FluentAssertions;
using _10xGitHubPolicies.Tests.E2E.Models;
using _10xGitHubPolicies.Tests.E2E.Infrastructure;

namespace _10xGitHubPolicies.Tests.E2E.Pages;

public class DashboardPage
{
    private readonly IPage _page;

    public DashboardPage(IPage page)
    {
        _page = page;
    }

    public async Task WaitForPageLoad()
    {
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        // Wait for the dashboard to be fully loaded
        await _page.WaitForSelectorAsync(".kpi-grid", new() { Timeout = 10000 });
    }

    public async Task<bool> IsDashboardVisible()
    {
        try
        {
            await _page.WaitForSelectorAsync(".kpi-grid", new() { Timeout = 5000 });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetPageTitle()
    {
        return await _page.TitleAsync();
    }

    public async Task NavigateToDashboard()
    {
        await _page.GotoAsync($"{TestConstants.BaseUrl}/");
        await WaitForPageLoad();
    }

    public async Task<DashboardMetrics> GetDashboardMetrics()
    {
        await WaitForPageLoad();

        var compliancePercentage = await GetCompliancePercentage();
        var totalRepositories = await GetTotalRepositories();
        var compliantRepositories = await GetCompliantRepositories();
        var nonCompliantRepositories = await GetNonCompliantRepositoriesCount();

        return new DashboardMetrics
        {
            CompliancePercentage = compliancePercentage,
            TotalRepositories = totalRepositories,
            CompliantRepositories = compliantRepositories,
            NonCompliantRepositories = nonCompliantRepositories
        };
    }

    public async Task<double> GetCompliancePercentage()
    {
        var complianceElement = await _page.WaitForSelectorAsync(".kpi-card:has-text('Overall Compliance') .kpi-value");
        var complianceText = await complianceElement?.TextContentAsync();
        var percentage = complianceText?.Replace("%", "").Trim();
        return double.TryParse(percentage, out var result) ? result : 0;
    }

    public async Task<int> GetTotalRepositories()
    {
        var totalElement = await _page.WaitForSelectorAsync(".kpi-card:has-text('Total Repositories') .kpi-value");
        var totalText = await totalElement?.TextContentAsync();
        return int.TryParse(totalText, out var result) ? result : 0;
    }

    public async Task<int> GetCompliantRepositories()
    {
        var compliantElement = await _page.WaitForSelectorAsync(".kpi-card.compliant:has-text('Compliant') .kpi-value");
        var compliantText = await compliantElement?.TextContentAsync();
        return int.TryParse(compliantText, out var result) ? result : 0;
    }

    public async Task<int> GetNonCompliantRepositoriesCount()
    {
        var nonCompliantElement = await _page.WaitForSelectorAsync(".kpi-card.non-compliant:has-text('Non-Compliant') .kpi-value");
        var nonCompliantText = await nonCompliantElement?.TextContentAsync();
        return int.TryParse(nonCompliantText, out var result) ? result : 0;
    }

    /// <summary>
    /// Clicks the scan button with robust error handling and waiting logic.
    /// Extracted from WorkflowTests to ensure consistent behavior.
    /// </summary>
    /// <param name="errorScreenshotFileName">Optional filename for error screenshot. If null, screenshot is skipped.</param>
    public async Task ClickScanButton(string? errorScreenshotFileName = null)
    {
        IElementHandle? scanButton = null;
        try
        {
            // Check if page is still accessible
            Console.WriteLine("🔍 Checking if page is still accessible...");
            var pageTitle = await _page.TitleAsync();
            Console.WriteLine($"✅ Page title: {pageTitle}");

            // Wait for scan button to be available and clickable
            Console.WriteLine("🔍 Waiting for scan button...");
            scanButton = await _page.Locator("[data-testid='scan-button']").ElementHandleAsync();
            scanButton.Should().NotBeNull("Scan button should be visible");

            // Wait for button to be enabled
            Console.WriteLine("🔍 Waiting for scan button to be enabled...");
            await _page.WaitForFunctionAsync("() => document.querySelector('[data-testid=\"scan-button\"]')?.disabled === false", new PageWaitForFunctionOptions { Timeout = 5000 });

            Console.WriteLine("✅ Scan button is ready, clicking...");
            await scanButton!.ClickAsync();
            Console.WriteLine("✅ Scan button clicked successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to click scan button: {ex.Message}");
            Console.WriteLine($"❌ Error type: {ex.GetType().Name}");

            // Take a screenshot for debugging if filename provided
            if (!string.IsNullOrEmpty(errorScreenshotFileName))
            {
                await _page.ScreenshotAsync(new()
                {
                    Path = $"test-results/screenshots/{errorScreenshotFileName}",
                    FullPage = true
                });
            }

            Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Waits for scan to start and then complete.
    /// Extracted from WorkflowTests to ensure consistent behavior.
    /// </summary>
    public async Task WaitForScanComplete()
    {
        // Wait for scanning to start
        await _page.Locator("[data-testid='scan-button']:has-text('Scanning...')").WaitForAsync(new() { Timeout = 5000 });
        Console.WriteLine("✅ Scan started successfully");

        // Wait for scanning to complete (the button text changes back to "Scan Now")
        await _page.Locator("[data-testid='scan-button']:has-text('Scan Now')").WaitForAsync(new() { Timeout = TestConstants.ScanTimeoutSeconds * 1000 });
        Console.WriteLine("✅ Scan completed successfully");
    }

    public async Task<bool> IsScanning()
    {
        try
        {
            var scanButton = await _page.WaitForSelectorAsync("[data-testid='scan-button']", new() { Timeout = 1000 });
            var buttonText = await scanButton.TextContentAsync();
            return buttonText?.Contains("Scanning...") == true;
        }
        catch
        {
            return false;
        }
    }

    public async Task SetRepositoryFilter(string filter)
    {
        var filterInput = await _page.WaitForSelectorAsync("input[placeholder='Filter by name...']");
        await filterInput?.FillAsync(filter);
        await Task.Delay(500); // Wait for filter to apply
    }



    public async Task<List<NonCompliantRepository>> GetNonCompliantRepositories()
    {
        await WaitForPageLoad();

        var repositories = new List<NonCompliantRepository>();

        try
        {
            // Check if there's a data grid with non-compliant repositories
            var dataGrid = await _page.WaitForSelectorAsync("[data-testid='non-compliant-repositories-grid']", new() { Timeout = 2000 });

            // Get all repository rows
            var rows = await _page.QuerySelectorAllAsync("fluent-data-grid fluent-data-grid-row");

            foreach (var row in rows)
            {
                try
                {
                    // Get repository name (first column)
                    var nameElement = await row.QuerySelectorAsync("fluent-data-grid-cell:first-child fluent-anchor");
                    var name = await nameElement?.TextContentAsync() ?? "";

                    // Get violations (second column)
                    var violationsElement = await row.QuerySelectorAsync("fluent-data-grid-cell:nth-child(2)");
                    var violations = new List<string>();

                    if (violationsElement != null)
                    {
                        var badges = await violationsElement.QuerySelectorAllAsync("fluent-badge");
                        foreach (var badge in badges)
                        {
                            var violation = await badge.TextContentAsync();
                            if (!string.IsNullOrEmpty(violation))
                            {
                                violations.Add(violation.Trim());
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(name))
                    {
                        repositories.Add(new NonCompliantRepository
                        {
                            Name = name.Trim(),
                            Violations = violations
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing repository row: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No non-compliant repositories found or error reading data grid: {ex.Message}");
        }

        return repositories;
    }



    public async Task<bool> IsRepositoryVisible(string repositoryName)
    {
        var repoLocator = _page.Locator("[data-testid='repository-name']").Filter(new() { HasText = repositoryName });
        return await repoLocator.IsVisibleAsync();
    }





    /// <summary>
    /// Gets the violations for a repository from the UI by iterating through table rows,
    /// finding the one that contains the repository name, and extracting violation badges.
    /// Based on actual DOM structure: table > tbody > tr > td (with repository-name) and td (with repository-violations)
    /// </summary>
    public async Task<List<string>> GetRepositoryViolations(string fullRepoName)
    {
        // Wait for the grid to be available
        await _page.Locator("[data-testid='non-compliant-repositories-grid']").WaitForAsync(new() { Timeout = 10000 });

        // Get all table rows (tbody > tr elements, excluding header row)
        var rows = _page.Locator("[data-testid='non-compliant-repositories-grid'] tbody tr");
        var rowCount = await rows.CountAsync();

        Console.WriteLine($"🔍 Searching through {rowCount} rows for repository: {fullRepoName}");

        for (int i = 0; i < rowCount; i++)
        {
            var row = rows.Nth(i);

            // Get the repository name from the first column's fluent-anchor
            var repoNameElement = row.Locator("[data-testid='repository-name']").First;

            // Check if this element exists
            var elementCount = await repoNameElement.CountAsync();
            if (elementCount == 0)
            {
                continue; // Skip rows without repository names
            }

            var repoNameText = await repoNameElement.TextContentAsync();

            if (repoNameText != null && repoNameText.Contains(fullRepoName, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"✅ Found repository in row {i}: {repoNameText}");

                // Found the matching repository row, get violations from second column
                // The violations container is in the same row
                var violationsContainer = row.Locator("[data-testid='repository-violations']");

                // Wait for violations container to be available
                var containerCount = await violationsContainer.CountAsync();
                if (containerCount == 0)
                {
                    Console.WriteLine($"⚠️ Warning: No violations container found for repository {fullRepoName}");
                    return new List<string>();
                }

                await violationsContainer.WaitForAsync(new() { Timeout = 5000 });

                // Get all violation badges
                var violationBadges = violationsContainer.Locator("[data-testid='violation-badge']");
                var badgeCount = await violationBadges.CountAsync();

                var violations = new List<string>();
                for (int j = 0; j < badgeCount; j++)
                {
                    var badgeText = await violationBadges.Nth(j).TextContentAsync();
                    if (!string.IsNullOrWhiteSpace(badgeText))
                    {
                        violations.Add(badgeText.Trim());
                    }
                }

                Console.WriteLine($"✅ Found {violations.Count} violations: {string.Join(", ", violations)}");
                return violations;
            }
        }

        // If we get here, repository was not found - list all repository names for debugging
        Console.WriteLine($"❌ Repository {fullRepoName} not found. Listing all repository names:");
        for (int i = 0; i < rowCount; i++)
        {
            var row = rows.Nth(i);
            var repoNameElement = row.Locator("[data-testid='repository-name']").First;
            var elementCount = await repoNameElement.CountAsync();
            if (elementCount > 0)
            {
                var repoNameText = await repoNameElement.TextContentAsync();
                Console.WriteLine($"  - Row {i}: {repoNameText}");
            }
        }

        throw new Exception($"Repository {fullRepoName} not found in the non-compliant repositories list");
    }

    /// <summary>
    /// Waits for the non-compliant repositories grid to load.
    /// Extracted from WorkflowTests to ensure consistent behavior.
    /// </summary>
    public async Task WaitForNonCompliantRepositoriesGrid()
    {
        // Wait for the non-compliant repositories grid to be visible
        Console.WriteLine("🔍 Waiting for non-compliant repositories grid to load...");
        await _page.Locator("[data-testid='non-compliant-repositories-grid']").WaitForAsync(new() { Timeout = 10000 });

        // Additional wait to ensure all repository names are loaded
        Console.WriteLine("🔍 Waiting for repository names to load...");
        await Task.Delay(3000);
    }



    /// <summary>
    /// Reloads the page and waits for the non-compliant repositories grid to be ready.
    /// Extracted from WorkflowTests to ensure consistent behavior.
    /// </summary>
    public async Task ReloadAndWaitForGrid()
    {
        // Refresh the page to show updated scan results
        Console.WriteLine("🔄 Refreshing page to show updated scan results...");
        await _page.ReloadAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Navigate to dashboard to show updated scan results
        Console.WriteLine("🔄 Navigating to dashboard to show updated scan results...");
        await _page.GotoAsync(TestConstants.BaseUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait a bit for the page to fully load
        await Task.Delay(2000);

        // Wait for the non-compliant repositories grid to be visible
        await WaitForNonCompliantRepositoriesGrid();

        Console.WriteLine("✅ Page refreshed successfully");
    }


}
