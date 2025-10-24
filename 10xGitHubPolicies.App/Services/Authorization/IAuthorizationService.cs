using System.Security.Claims;

namespace _10xGitHubPolicies.App.Services.Authorization;

public interface IAuthorizationService
{
    Task<bool> IsUserAuthorizedAsync(ClaimsPrincipal user);
    Task<string?> GetAuthorizedTeamAsync();
}
