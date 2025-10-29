using Microsoft.Playwright.NUnit;
using Microsoft.Playwright;
using FluentAssertions;
using _10xGitHubPolicies.Tests.E2E.Infrastructure;

namespace _10xGitHubPolicies.Tests.E2E.Smoke;

[TestFixture]
public class AuthTests : E2ETestBase
{
    [Test]
    [Category("E2E-Auth")]
    public async Task SecurityBoundaryValidation_Success()
    {
        Console.WriteLine("🔒 Starting Security Boundary Validation Test");
        
        // Act & Assert - Test Unauthenticated URLs
        Console.WriteLine("🔍 Testing unauthenticated URLs...");
        
        var unauthenticatedUrls = new[]
        {
            "/login",
            "/logout", 
            "/access-denied",
            "/onboarding"
        };
        
        foreach (var url in unauthenticatedUrls)
        {
            var response = await Page.GotoAsync($"{TestConstants.BaseUrl}{url}");
            response.Should().NotBeNull($"Should be able to access {url}");
            
            // For logout, it redirects to login (expected behavior)
            // For other URLs, they should be accessible without authentication
            var urlAfterNavigation = Page.Url;
            if (url == "/logout")
            {
                urlAfterNavigation.Should().Contain("/login", $"{url} should redirect to login");
            }
            else
            {
                urlAfterNavigation.Should().Contain(url, $"{url} should be accessible without authentication");
            }
            
            Console.WriteLine($"✅ {url} - Status: {response?.Status} - URL: {urlAfterNavigation}");
        }
        
        // Test Authenticated URLs (should be protected)
        Console.WriteLine("🔒 Testing authenticated URLs (should be protected)...");
        
        var authenticatedUrls = new[]
        {
            ("/", "redirect"),
            ("/debug", "redirect"),
            ("/hangfire", "401")
        };
        
        foreach (var (url, expectedBehavior) in authenticatedUrls)
        {
            var response = await Page.GotoAsync($"{TestConstants.BaseUrl}{url}");
            response.Should().NotBeNull($"Should be able to access {url}");
            
            if (expectedBehavior == "redirect")
            {
                // Should redirect to login for protected URLs
                var redirectedUrl = Page.Url;
                redirectedUrl.Should().Contain("/login", $"{url} should redirect to login when not authenticated");
                Console.WriteLine($"✅ {url} - Status: {response?.Status} - Redirected to: {redirectedUrl}");
            }
            else if (expectedBehavior == "401")
            {
                // Should return 401 for API endpoints
                response?.Status.Should().Be(401, $"{url} should return 401 Unauthorized when not authenticated");
                Console.WriteLine($"✅ {url} - Status: {response?.Status} - Correctly returns 401 Unauthorized");
            }
        }
        
        // Test Login Flow
        Console.WriteLine("🔐 Testing login flow...");
        
        await Page.GotoAsync($"{BaseUrl}/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // Take screenshot of login page
        await Page.ScreenshotAsync(new() { 
            Path = $"test-results/screenshots/{TestContext.CurrentContext.Test.Name}-02-login-page.png",
            FullPage = true 
        });
        
        // Check if login page loads correctly (status 200 and contains login-related content)
        var currentUrl = Page.Url;
        currentUrl.Should().Contain("/login", "Should be on login page");
        
        // Check for GitHub login button
        var hasGitHubLogin = await Page.Locator("text=Login with GitHub").IsVisibleAsync();
        hasGitHubLogin.Should().BeTrue("Login page should show 'Login with GitHub' button");
        
        Console.WriteLine($"✅ Login page is accessible at: {currentUrl}");
        
        // Test Dashboard Access After Login (simulate)
        Console.WriteLine("📊 Testing dashboard access...");
        
        // Navigate to dashboard (will redirect to login)
        await Page.GotoAsync($"{TestConstants.BaseUrl}/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        var redirectedToLogin = Page.Url.Contains("/login");
        redirectedToLogin.Should().BeTrue("Dashboard should redirect to login when not authenticated");
        
        // Take screenshot of login page after redirect
        await Page.ScreenshotAsync(new() { 
            Path = $"test-results/screenshots/{TestContext.CurrentContext.Test.Name}-03-login-page-redirected.png",
            FullPage = true 
        });
        Console.WriteLine("✅ Dashboard properly redirects to login when not authenticated");
        
        Console.WriteLine("🎉 Security Boundary Validation Test PASSED!");
    }
}

