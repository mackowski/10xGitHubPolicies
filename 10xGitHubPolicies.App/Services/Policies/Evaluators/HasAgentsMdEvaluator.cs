using _10xGitHubPolicies.App.Data.Entities;
using _10xGitHubPolicies.App.Services.GitHub;

namespace _10xGitHubPolicies.App.Services.Policies.Evaluators;

public class HasAgentsMdEvaluator : IPolicyEvaluator
{
    private readonly IGitHubService _githubService;

    public HasAgentsMdEvaluator(IGitHubService githubService)
    {
        _githubService = githubService;
    }

    public string PolicyType => "has_agents_md";

    public async Task<PolicyViolation?> EvaluateAsync(Octokit.Repository repository)
    {
        var fileExists = await _githubService.FileExistsAsync(repository.Id, "AGENTS.md");
        if (!fileExists)
        {
            return new PolicyViolation
            {
                PolicyType = PolicyType
            };
        }

        return null;
    }
}
