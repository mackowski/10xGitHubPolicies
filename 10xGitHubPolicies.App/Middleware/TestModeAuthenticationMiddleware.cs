using System.Security.Claims;

using _10xGitHubPolicies.App.Options;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace _10xGitHubPolicies.App.Middleware;

/// <summary>
/// Middleware that bypasses authentication when test mode is enabled.
/// Creates a fake authenticated user context for E2E testing.
/// </summary>
public class TestModeAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly TestModeOptions _options;

    public TestModeAuthenticationMiddleware(RequestDelegate next, IOptions<TestModeOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only bypass authentication if test mode is enabled
        if (_options.Enabled)
        {
            // Create a fake authenticated user for testing - using real GitHub user that's part of mackowski-corp/appsec team
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "mackowski"),
                new(ClaimTypes.Name, "mackowski"),
                new("login", "mackowski"),
                new("avatar_url", "https://github.com/images/error/octocat_happy.gif"),
                new("html_url", "https://github.com/mackowski"),
                new("type", "User")
            };

            var identity = new ClaimsIdentity(claims, "TestMode");
            var principal = new ClaimsPrincipal(identity);

            context.User = principal;

            // Create authentication properties (no access token needed in test mode)
            var properties = new AuthenticationProperties();

            // Set authentication result
            var authResult = AuthenticateResult.Success(new AuthenticationTicket(principal, properties, "TestMode"));
            context.Features.Set<IAuthenticateResultFeature>(new AuthenticateResultFeature { AuthenticateResult = authResult });
        }

        await _next(context);
    }
}

/// <summary>
/// Feature to hold authentication result for test mode.
/// </summary>
public class AuthenticateResultFeature : IAuthenticateResultFeature
{
    public AuthenticateResult? AuthenticateResult { get; set; }
}

/// <summary>
/// Extension method to register the test mode authentication middleware.
/// </summary>
public static class TestModeAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseTestModeAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TestModeAuthenticationMiddleware>();
    }
}