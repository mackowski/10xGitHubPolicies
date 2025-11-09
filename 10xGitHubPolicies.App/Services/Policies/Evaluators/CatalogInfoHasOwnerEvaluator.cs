using System.Text;

using _10xGitHubPolicies.App.Data.Entities;
using _10xGitHubPolicies.App.Services.GitHub;

using YamlDotNet.Serialization;

namespace _10xGitHubPolicies.App.Services.Policies.Evaluators;

public class CatalogInfoHasOwnerEvaluator : IPolicyEvaluator
{
    private readonly IGitHubService _githubService;
    private readonly ILogger<CatalogInfoHasOwnerEvaluator> _logger;

    public CatalogInfoHasOwnerEvaluator(IGitHubService githubService, ILogger<CatalogInfoHasOwnerEvaluator> logger)
    {
        _githubService = githubService;
        _logger = logger;
    }

    public string PolicyType => "catalog_info_has_owner";

    public async Task<PolicyViolation?> EvaluateAsync(Octokit.Repository repository)
    {
        // Get file content (returns base64-encoded string or null)
        var base64Content = await _githubService.GetFileContentAsync(repository.Name, "catalog-info.yaml");

        // If file doesn't exist (null), return null (covered by has_catalog_info_yaml policy)
        // Empty string means file exists but is empty, which should be treated as a violation
        if (base64Content == null)
        {
            return null;
        }

        try
        {
            // Decode base64 to UTF-8
            var yamlBytes = Convert.FromBase64String(base64Content);
            var yamlContent = Encoding.UTF8.GetString(yamlBytes);

            // Check if YAML content is empty or whitespace
            if (string.IsNullOrWhiteSpace(yamlContent))
            {
                _logger.LogWarning("catalog-info.yaml in repository {RepoName} is empty or contains only whitespace", repository.Name);
                return new PolicyViolation
                {
                    PolicyType = PolicyType
                };
            }

            // Parse YAML using dynamic deserialization
            var deserializer = new DeserializerBuilder()
                .Build();

            var yamlData = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

            // Navigate to spec.owner field
            if (yamlData == null || !yamlData.ContainsKey("spec"))
            {
                _logger.LogWarning("catalog-info.yaml in repository {RepoName} is missing 'spec' section", repository.Name);
                return new PolicyViolation
                {
                    PolicyType = PolicyType
                };
            }

            // Handle nested dictionary - YamlDotNet may return Dictionary<object, object> for nested structures
            object? specObj = yamlData["spec"];
            if (specObj == null)
            {
                _logger.LogWarning("catalog-info.yaml in repository {RepoName} has null 'spec' section", repository.Name);
                return new PolicyViolation
                {
                    PolicyType = PolicyType
                };
            }

            // Try to get owner from nested dictionary structure
            string? owner = null;
            if (specObj is Dictionary<object, object> specDict)
            {
                if (!specDict.ContainsKey("owner"))
                {
                    _logger.LogWarning("catalog-info.yaml in repository {RepoName} is missing 'owner' field in 'spec' section", repository.Name);
                    return new PolicyViolation
                    {
                        PolicyType = PolicyType
                    };
                }
                owner = specDict["owner"]?.ToString();
            }
            else if (specObj is Dictionary<string, object> specStringDict)
            {
                if (!specStringDict.ContainsKey("owner"))
                {
                    _logger.LogWarning("catalog-info.yaml in repository {RepoName} is missing 'owner' field in 'spec' section", repository.Name);
                    return new PolicyViolation
                    {
                        PolicyType = PolicyType
                    };
                }
                owner = specStringDict["owner"]?.ToString();
            }
            else
            {
                _logger.LogWarning("catalog-info.yaml in repository {RepoName} has 'spec' section that is not a valid object", repository.Name);
                return new PolicyViolation
                {
                    PolicyType = PolicyType
                };
            }

            // Validate that owner exists and is not null/empty/whitespace
            if (string.IsNullOrWhiteSpace(owner))
            {
                _logger.LogWarning("catalog-info.yaml in repository {RepoName} has empty or whitespace-only 'owner' field", repository.Name);
                return new PolicyViolation
                {
                    PolicyType = PolicyType
                };
            }

            // Repository is compliant
            return null;
        }
        catch (Exception ex)
        {
            // Invalid YAML: Log error and return violation (malformed file)
            _logger.LogError(ex, "Failed to parse catalog-info.yaml in repository {RepoName}. The file may be malformed.", repository.Name);
            return new PolicyViolation
            {
                PolicyType = PolicyType
            };
        }
    }
}