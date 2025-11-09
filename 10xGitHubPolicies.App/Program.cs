using System.Security.Claims;

using _10xGitHubPolicies.App.Authorization;
using _10xGitHubPolicies.App.Data;
using _10xGitHubPolicies.App.Middleware;
using _10xGitHubPolicies.App.Options;
using _10xGitHubPolicies.App.Services.Action;
using _10xGitHubPolicies.App.Services.Authorization;
using _10xGitHubPolicies.App.Services.Configuration;
using _10xGitHubPolicies.App.Services.Dashboard;
using _10xGitHubPolicies.App.Services.GitHub;
using _10xGitHubPolicies.App.Services.Policies;
using _10xGitHubPolicies.App.Services.Policies.Evaluators;
using _10xGitHubPolicies.App.Services.Scanning;
using _10xGitHubPolicies.App.Services.Webhooks;

using Hangfire;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Add Hangfire services.
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add the processing server as IHostedService
builder.Services.AddHangfireServer();

// Configure GitHub App options first
builder.Services.Configure<GitHubAppOptions>(builder.Configuration.GetSection(GitHubAppOptions.GitHubApp));

// Configure Test Mode options
builder.Services.Configure<TestModeOptions>(builder.Configuration.GetSection(TestModeOptions.TestMode));

// Get Test Mode options to check if test mode is enabled
var testModeOptions = builder.Configuration.GetSection(TestModeOptions.TestMode).Get<TestModeOptions>() ?? new TestModeOptions();

// Configure data protection for OAuth state handling
builder.Services.AddDataProtection();

// Add authentication services only if not in test mode
if (!testModeOptions.Enabled)
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = "GitHub";
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.SlidingExpiration = false;
    })
    .AddGitHub(options =>
    {
        options.ClientId = builder.Configuration["GitHub:ClientId"] ?? throw new InvalidOperationException("GitHub:ClientId is not configured");
        options.ClientSecret = builder.Configuration["GitHub:ClientSecret"] ?? throw new InvalidOperationException("GitHub:ClientSecret is not configured");
        options.CallbackPath = "/signin-github";
        options.Scope.Add("read:org");
        options.SaveTokens = true; // Save access token for team verification

        // Configure OAuth events to handle failures gracefully
        options.Events.OnRemoteFailure = context =>
        {
            // Log OAuth failures for debugging
            var error = context.Failure?.Message ?? "Unknown error";
            context.Response.Redirect($"/login?error={Uri.EscapeDataString(error)}");
            context.HandleResponse();
            return Task.CompletedTask;
        };
    });
}
else
{
    // In test mode, add minimal authentication services
    builder.Services.AddAuthentication("TestMode")
        .AddScheme<AuthenticationSchemeOptions, TestModeAuthenticationHandler>("TestMode", options => { });
}

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddServerSideBlazor();
builder.Services.AddFluentUIComponents();
builder.Services.AddScoped<IDashboardService, DashboardService>();


builder.Services.AddMemoryCache();

// Register GitHub client factory with optional base URL from configuration
builder.Services.AddSingleton<IGitHubClientFactory>(sp =>
{
    var options = sp.GetRequiredService<IOptions<GitHubAppOptions>>();
    return new GitHubClientFactory(options.Value.BaseUrl);
});

builder.Services.AddSingleton<IGitHubService, GitHubService>();
builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();
builder.Services.AddScoped<IScanningService, ScanningService>();

builder.Services.AddScoped<IPolicyEvaluationService, PolicyEvaluationService>();
builder.Services.AddScoped<IPolicyEvaluator, HasAgentsMdEvaluator>();
builder.Services.AddScoped<IPolicyEvaluator, HasCatalogInfoYamlEvaluator>();
builder.Services.AddScoped<IPolicyEvaluator, CatalogInfoHasOwnerEvaluator>();
builder.Services.AddScoped<IPolicyEvaluator, CorrectWorkflowPermissionsEvaluator>();
builder.Services.AddScoped<IActionService, ActionService>();
builder.Services.AddScoped<_10xGitHubPolicies.App.Services.Authorization.IAuthorizationService, _10xGitHubPolicies.App.Services.Authorization.AuthorizationService>();
builder.Services.AddScoped<IWebhookService, WebhookService>();
builder.Services.AddScoped<IPullRequestWebhookHandler, PullRequestWebhookHandler>();

// Configure HTTPS redirection for development
if (builder.Environment.IsDevelopment())
{
    builder.Services.Configure<HttpsRedirectionOptions>(options =>
    {
        options.HttpsPort = 7040;
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Add test mode authentication middleware if in test mode
if (testModeOptions.Enabled)
{
    app.UseTestModeAuthentication();
}

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

// Configure recurring jobs
RecurringJob.AddOrUpdate<IScanningService>(
    "daily-scan",
    service => service.PerformScanAsync(),
    "0 0 * * *", // Daily at midnight UTC
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.Utc
    });

// Add OAuth challenge endpoint
app.MapGet("/challenge", async (HttpContext context) =>
{
    await context.ChallengeAsync("GitHub", new AuthenticationProperties
    {
        RedirectUri = "/"
    });
});

app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();