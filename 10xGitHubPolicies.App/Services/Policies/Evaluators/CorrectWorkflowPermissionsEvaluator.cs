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
        var permissions = await _githubService.GetWorkflowPermissionsAsync(repository.Id);
        
        // If permissions is null, Actions might be disabled - consider this compliant
        if (permissions == null)
        {
            return null;
        }
        
        // Check if permissions are set to "read" (the secure, restrictive setting)
        if (permissions != "read")
        {
            return new PolicyViolation
            {
                PolicyType = PolicyType
            };
        }
        
        return null;
    }
}
