using _10xGitHubPolicies.App.Data;
using _10xGitHubPolicies.App.Options;
using _10xGitHubPolicies.App.Services.Action;
using _10xGitHubPolicies.App.Services.Configuration;
using _10xGitHubPolicies.App.Services.Dashboard;
using _10xGitHubPolicies.App.Services.GitHub;
using _10xGitHubPolicies.App.Services.Policies;
using _10xGitHubPolicies.App.Services.Policies.Evaluators;
using _10xGitHubPolicies.App.Services.Scanning;

using Hangfire;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddFluentUIComponents();
builder.Services.AddScoped<IDashboardService, DashboardService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IGitHubService, GitHubService>();
builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();
builder.Services.AddScoped<IScanningService, ScanningService>();

builder.Services.AddScoped<IPolicyEvaluationService, PolicyEvaluationService>();
builder.Services.AddScoped<IPolicyEvaluator, HasAgentsMdEvaluator>();
builder.Services.AddScoped<IPolicyEvaluator, HasCatalogInfoYamlEvaluator>();
builder.Services.AddScoped<IPolicyEvaluator, CorrectWorkflowPermissionsEvaluator>();
builder.Services.AddScoped<IActionService, ActionService>();


builder.Services.Configure<GitHubAppOptions>(builder.Configuration.GetSection(GitHubAppOptions.GitHubApp));

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

app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();

app.UseHangfireDashboard();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();