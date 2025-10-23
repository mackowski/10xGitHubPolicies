using _10xGitHubPolicies.App.Data.Entities;

namespace _10xGitHubPolicies.App.Services.Policies;

/// <summary>
/// Represents a single, concrete policy check.
/// </summary>
public interface IPolicyEvaluator
{
    /// <summary>
    /// A key that matches the policy 'type' in config.yaml (e.g., "has_agents_md").
    /// </summary>
    string PolicyType { get; }

    /// <summary>
    /// Evaluates the repository against this specific policy.
    /// </summary>
    /// <param name="repository">The repository to check.</param>
    /// <returns>A PolicyViolation object if the policy is violated, otherwise null.</returns>
    Task<PolicyViolation?> EvaluateAsync(Octokit.Repository repository);
}