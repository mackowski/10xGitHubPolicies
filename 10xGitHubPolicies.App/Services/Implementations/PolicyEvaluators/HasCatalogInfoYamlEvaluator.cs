namespace _10xGitHubPolicies.App.Services.Implementations.PolicyEvaluators;

public class HasCatalogInfoYamlEvaluator : IPolicyEvaluator
{
    private readonly IGitHubService _githubService;

    public HasCatalogInfoYamlEvaluator(IGitHubService githubService)
    {
        _githubService = githubService;
    }

    public string PolicyType => "has_catalog_info_yaml";

    public async Task<Data.Models.PolicyViolation?> EvaluateAsync(Octokit.Repository repository)
    {
        var fileExists = await _githubService.FileExistsAsync(repository.Id, "catalog-info.yaml");
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
