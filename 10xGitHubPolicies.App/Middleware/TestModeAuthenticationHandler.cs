using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace _10xGitHubPolicies.App.Middleware;

/// <summary>
/// Authentication handler for test mode that always succeeds with a fake user.
/// </summary>
public class TestModeAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestModeAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
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

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);

        // Create authentication properties (no access token needed in test mode)
        var properties = new AuthenticationProperties();

        var ticket = new AuthenticationTicket(principal, properties, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}