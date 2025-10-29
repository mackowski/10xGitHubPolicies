using Microsoft.Playwright;

namespace _10xGitHubPolicies.Tests.E2E.Helpers;

/// <summary>
/// Helper class for taking screenshots in E2E tests with consistent naming.
/// Extracted from WorkflowTests to centralize screenshot patterns.
/// </summary>
public static class ScreenshotHelper
{
    /// <summary>
    /// Takes a full-page screenshot with the specified filename.
    /// Extracted from WorkflowTests screenshot pattern.
    /// </summary>
    /// <param name="page">The Playwright page</param>
    /// <param name="fileName">The filename (without path - will be saved to test-results/screenshots/)</param>
    public static async Task TakeScreenshotAsync(IPage page, string fileName)
    {
        await page.ScreenshotAsync(new()
        {
            Path = $"test-results/screenshots/{fileName}",
            FullPage = true
        });
    }

    /// <summary>
    /// Takes a full-page screenshot with auto-generated filename based on test context.
    /// Extracted from WorkflowTests screenshot pattern using TestContext.CurrentContext.Test.Name.
    /// </summary>
    /// <param name="page">The Playwright page</param>
    /// <param name="suffix">The suffix to append to test name (e.g., "01-initial-dashboard-state")</param>
    /// <param name="testName">The test name (typically from TestContext.CurrentContext.Test.Name)</param>
    public static async Task TakeScreenshotAsync(IPage page, string testName, string suffix)
    {
        var fileName = $"{testName}-{suffix}.png";
        await TakeScreenshotAsync(page, fileName);
    }
}

