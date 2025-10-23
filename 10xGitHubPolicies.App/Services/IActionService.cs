namespace _10xGitHubPolicies.App.Services;

/// <summary>
/// Executes the automated actions based on violations found during a scan.
/// </summary>
public interface IActionService
{
    /// <summary>
    /// [Background Job] Processes all violations for a completed scan and executes the configured actions.
    /// </summary>
    /// <param name="scanId">The ID of the scan whose violations should be processed.</param>
    Task ProcessActionsForScanAsync(int scanId);
}
