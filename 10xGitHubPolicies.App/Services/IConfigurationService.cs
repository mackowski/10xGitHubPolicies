using _10xGitHubPolicies.App.Models.Configuration;

namespace _10xGitHubPolicies.App.Services;

public interface IConfigurationService
{
    Task<AppConfig> GetConfigAsync(bool forceRefresh = false);
}