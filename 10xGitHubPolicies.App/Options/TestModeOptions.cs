namespace _10xGitHubPolicies.App.Options;

/// <summary>
/// Configuration options for test mode functionality.
/// Used to enable authentication bypass for E2E testing.
/// </summary>
public class TestModeOptions
{
    public const string TestMode = "TestMode";

    /// <summary>
    /// Enables test mode which bypasses user authentication for E2E testing.
    /// When enabled, all routes become accessible without OAuth authentication.
    /// GitHub App functionality remains intact for repository operations.
    /// </summary>
    public bool Enabled { get; set; } = false;
}