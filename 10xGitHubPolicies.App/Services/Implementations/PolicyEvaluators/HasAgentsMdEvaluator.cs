namespace _10xGitHubPolicies.App.Services.Implementations.PolicyEvaluators;

public class HasAgentsMdEvaluator : IPolicyEvaluator
{
    private readonly IGitHubService _githubService;

    public HasAgentsMdEvaluator(IGitHubService githubService)
    {
        _githubService = githubService;
    }

    public string PolicyType => "has_agents_md";

    public async Task<Data.Models.PolicyViolation?> EvaluateAsync(Octokit.Repository repository)
    {
        var fileExists = await _githubService.FileExistsAsync(repository.Id, "AGENTS.md");
        if (!fileExists)
        {
            return new Data.Models.PolicyViolation
            {
                PolicyType = PolicyType
            };
        }

        return null;
    }
}
