using _10xGitHubPolicies.App.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using _10xGitHubPolicies.App.Options;
using Microsoft.Extensions.Options;
using _10xGitHubPolicies.App.Services;
using _10xGitHubPolicies.App.Services.Mock;
using Hangfire;

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
builder.Services.AddMudServices();
builder.Services.AddScoped<IDashboardService, MockDashboardService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IGitHubService, GitHubService>();
builder.Services.AddScoped<IScanningService, ScanningService>();

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

// Verification endpoint
app.MapGet("/verify-scan", async (IScanningService scanningService, ILogger<Program> logger) =>
{
    try
    {
        await scanningService.PerformScanAsync();
        return Results.Ok("Scan completed successfully. Check logs for details.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to perform scan.");
        return Results.Problem("Failed to perform scan.");
    }
});

// Endpoint to enqueue a test job
app.MapGet("/log-job", (IBackgroundJobClient backgroundJobClient) =>
{
    var jobId = backgroundJobClient.Enqueue(() => Console.WriteLine("Hello from a Hangfire job!"));
    return Results.Ok($"Job '{jobId}' enqueued.");
});

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();