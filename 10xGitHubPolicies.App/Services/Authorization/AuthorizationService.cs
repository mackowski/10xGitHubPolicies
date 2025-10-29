using System.Security.Claims;

using _10xGitHubPolicies.App.Exceptions;
using _10xGitHubPolicies.App.Options;
using _10xGitHubPolicies.App.Services.Configuration;
using _10xGitHubPolicies.App.Services.GitHub;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace _10xGitHubPolicies.App.Services.Authorization;

public class AuthorizationService : IAuthorizationService
{
    private readonly IGitHubService _gitHubService;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<AuthorizationService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOptions<TestModeOptions> _testModeOptions;

    public AuthorizationService(
        IGitHubService gitHubService,
        IConfigurationService configurationService,
        ILogger<AuthorizationService> logger,
        IHttpContextAccessor httpContextAccessor,
        IOptions<TestModeOptions> testModeOptions)
    {
        _gitHubService = gitHubService;
        _configurationService = configurationService;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _testModeOptions = testModeOptions;
    }

    public async Task<bool> IsUserAuthorizedAsync(ClaimsPrincipal user)
    {
        try
        {
            // In test mode, always authorize the user (skip team membership check)
            if (_testModeOptions.Value.Enabled)
            {
                _logger.LogInformation("Test mode enabled - bypassing team membership check");
                return true;
            }

            // Get the user's access token from authentication properties
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                _logger.LogWarning("No HTTP context available for token retrieval");
                return false;
            }

            var authenticateResult = await httpContext.AuthenticateAsync();
            if (!authenticateResult.Succeeded)
            {
                _logger.LogWarning("User authentication failed");
                return false;
            }

            var accessToken = authenticateResult.Properties?.GetTokenValue("access_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("No access token found in authentication properties");
                return false;
            }

            // Get the authorized team from configuration
            var authorizedTeam = await GetAuthorizedTeamAsync();
            if (string.IsNullOrEmpty(authorizedTeam))
            {
                _logger.LogWarning("No authorized team configured");
                return false;
            }

            // Parse organization and team from the authorized team string (format: "org/team")
            var teamParts = authorizedTeam.Split('/');
            if (teamParts.Length != 2)
            {
                _logger.LogError("Invalid authorized team format: {AuthorizedTeam}", authorizedTeam);
                return false;
            }

            var org = teamParts[0];
            var teamSlug = teamParts[1];

            _logger.LogInformation("Parsed team configuration - Org: '{Org}', Team: '{TeamSlug}'", org, teamSlug);

            // Check if user is a member of the authorized team
            _logger.LogInformation("Checking team membership for user. Org: {Org}, Team: {TeamSlug}", org, teamSlug);
            var isMember = await _gitHubService.IsUserMemberOfTeamAsync(accessToken, org, teamSlug);

            _logger.LogInformation("User team membership check result: {IsMember} for team {Team}", isMember, authorizedTeam);
            return isMember;
        }
        catch (ConfigurationNotFoundException ex)
        {
            _logger.LogError(ex, "Configuration not found while checking user authorization");
            return false;
        }
        catch (InvalidConfigurationException ex)
        {
            _logger.LogError(ex, "Invalid configuration while checking user authorization");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while checking user authorization");
            return false;
        }
    }

    public async Task<string?> GetAuthorizedTeamAsync()
    {
        try
        {
            var config = await _configurationService.GetConfigAsync();
            return config.AccessControl.AuthorizedTeam;
        }
        catch (ConfigurationNotFoundException ex)
        {
            _logger.LogError(ex, "Configuration not found while getting authorized team");
            return null;
        }
        catch (InvalidConfigurationException ex)
        {
            _logger.LogError(ex, "Invalid configuration while getting authorized team");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while getting authorized team");
            return null;
        }
    }
}