using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace _10xGitHubPolicies.App.Controllers;

[Route("[controller]")]
public class AccountController : Controller
{
    [HttpGet("Logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login", "Account");
    }

    [HttpGet("Login")]
    public IActionResult Login()
    {
        return Redirect("/login");
    }
}