using System.Text;

using _10xGitHubPolicies.App.Exceptions;
using _10xGitHubPolicies.App.Options;
using _10xGitHubPolicies.App.Services.Configuration.Models;
using _10xGitHubPolicies.App.Services.GitHub;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace _10xGitHubPolicies.App.Services.Configuration;

public class ConfigurationService : IConfigurationService
{
    private readonly IGitHubService _githubService;
    private readonly IMemoryCache _cache;
    private readonly GitHubAppOptions _options;
    private readonly ILogger<ConfigurationService> _logger;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private const string AppConfigCacheKey = "AppConfigCacheKey";

    public ConfigurationService(
        IGitHubService githubService,
        IMemoryCache cache,
        IOptions<GitHubAppOptions> options,
        ILogger<ConfigurationService> logger)
    {
        _githubService = githubService;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AppConfig> GetConfigAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && _cache.TryGetValue(AppConfigCacheKey, out AppConfig? cachedConfig) && cachedConfig != null)
        {
            _logger.LogInformation("Configuration found in cache.");
            return cachedConfig;
        }

        await _semaphore.WaitAsync();
        try
        {
            // Double-check locking
            if (!forceRefresh && _cache.TryGetValue(AppConfigCacheKey, out AppConfig? configAfterLock) && configAfterLock != null)
            {
                _logger.LogInformation("Configuration found in cache after acquiring lock.");
                return configAfterLock;
            }

            _logger.LogInformation("Fetching configuration from GitHub repository.");

            var base64Content = await _githubService.GetFileContentAsync(".github", "config.yaml");

            if (string.IsNullOrEmpty(base64Content))
            {
                _logger.LogWarning("'.github/config.yaml' not found in organization repository.");
                throw new ConfigurationNotFoundException("The configuration file '.github/config.yaml' was not found.");
            }

            var yamlContent = Encoding.UTF8.GetString(Convert.FromBase64String(base64Content));

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            AppConfig appConfig;
            try
            {
                appConfig = deserializer.Deserialize<AppConfig>(yamlContent);
            }
            catch (YamlException ex)
            {
                _logger.LogError(ex, "Failed to deserialize 'config.yaml'.");
                throw new InvalidConfigurationException("The 'config.yaml' file is malformed.", ex);
            }

            if (appConfig?.AccessControl == null || string.IsNullOrWhiteSpace(appConfig.AccessControl.AuthorizedTeam))
            {
                _logger.LogError("Configuration is invalid: 'access_control.authorized_team' is missing.");
                throw new InvalidConfigurationException("Configuration is invalid: 'access_control.authorized_team' must be set.");
            }

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(15));

            _cache.Set(AppConfigCacheKey, appConfig, cacheEntryOptions);
            _logger.LogInformation("Configuration has been fetched and cached successfully.");

            return appConfig;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}