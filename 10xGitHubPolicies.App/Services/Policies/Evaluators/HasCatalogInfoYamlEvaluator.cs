using _10xGitHubPolicies.App.Data.Entities;
using _10xGitHubPolicies.App.Services.GitHub;

namespace _10xGitHubPolicies.App.Services.Policies.Evaluators;

public class HasCatalogInfoYamlEvaluator : IPolicyEvaluator
{
    private readonly IGitHubService _githubService;

    public HasCatalogInfoYamlEvaluator(IGitHubService githubService)
    {
        _githubService = githubService;
    }

    public string PolicyType => "has_catalog_info_yaml";

    public async Task<PolicyViolation?> EvaluateAsync(Octokit.Repository repository)
    {
        var fileExists = await _githubService.FileExistsAsync(repository.Id, "catalog-info.yaml");
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