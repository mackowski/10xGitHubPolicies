using Hangfire.Dashboard;

namespace _10xGitHubPolicies.App.Authorization;

/// <summary>
/// Custom authorization filter for Hangfire dashboard that requires user authentication.
/// </summary>
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    /// <summary>
    /// Determines whether the current user is authorized to access the Hangfire dashboard.
    /// </summary>
    /// <param name="context">The dashboard context containing HTTP context information.</param>
    /// <returns>True if the user is authenticated, false otherwise.</returns>
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true;
    }
}
