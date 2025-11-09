using _10xGitHubPolicies.App.Data.Entities;
using _10xGitHubPolicies.App.Services.Configuration.Models;

namespace _10xGitHubPolicies.App.Services.Action;

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

    /// <summary>
    /// Comments on a pull request for policy violations (webhook path).
    /// </summary>
    Task CommentOnPullRequestAsync(long repositoryId, int pullRequestNumber, PolicyConfig policyConfig, List<PolicyViolation> violations);

    /// <summary>
    /// Creates or updates a status check for a pull request based on policy violations (webhook path).
    /// </summary>
    Task UpdatePullRequestStatusCheckAsync(long repositoryId, string headSha, List<PolicyViolation> violations, PolicyConfig policyConfig);
}