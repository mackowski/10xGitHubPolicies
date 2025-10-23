using _10xGitHubPolicies.App.Data.Entities;
using _10xGitHubPolicies.App.Services.Configuration.Models;

namespace _10xGitHubPolicies.App.Services.Policies;

/// <summary>
/// Evaluates a single repository against all configured policies using the Strategy pattern.
/// </summary>
public interface IPolicyEvaluationService
{
    /// <summary>
    /// Evaluates a single repository against all active policies.
    /// </summary>
    /// <param name="repository">The repository to evaluate.</param>
    /// <param name="policies">The list of policies to check against.</param>
    /// <returns>A list of policy violations found for the repository.</returns>
    Task<IEnumerable<PolicyViolation>> EvaluateRepositoryAsync(Octokit.Repository repository, IEnumerable<PolicyConfig> policies);
}