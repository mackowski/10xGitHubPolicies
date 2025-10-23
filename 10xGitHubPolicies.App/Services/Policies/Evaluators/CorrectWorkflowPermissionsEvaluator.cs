using _10xGitHubPolicies.App.Data.Entities;
using _10xGitHubPolicies.App.Services.GitHub;

namespace _10xGitHubPolicies.App.Services.Policies.Evaluators;

public class CorrectWorkflowPermissionsEvaluator : IPolicyEvaluator
{
    private readonly IGitHubService _githubService;

    public CorrectWorkflowPermissionsEvaluator(IGitHubService githubService)
    {
        _githubService = githubService;
    }

    public string PolicyType => "correct_workflow_permissions";

    public async Task<PolicyViolation?> EvaluateAsync(Octokit.Repository repository)
    {
        // TODO: Implement the logic to check workflow permissions.
        // This will likely require a new method in IGitHubService to get repository actions settings.
        // For now, we'll assume it's always compliant.
        await Task.CompletedTask;
        return null;
    }
}
