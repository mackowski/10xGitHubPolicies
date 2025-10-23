using _10xGitHubPolicies.App.Services.Configuration.Models;

namespace _10xGitHubPolicies.App.Services.Configuration;

public interface IConfigurationService
{
    Task<AppConfig> GetConfigAsync(bool forceRefresh = false);
}