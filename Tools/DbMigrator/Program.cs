using _10xGitHubPolicies.App.Data;

using Microsoft.EntityFrameworkCore;

// Reads ConnectionStrings__DefaultConnection from environment/appsettings and runs EF Core migrations.
// Designed to run in CI with Azure Web App Managed Identity (Authentication=Active Directory Managed Identity).

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("ConnectionStrings__DefaultConnection environment variable is not set.");
    return 1;
}

Console.WriteLine("Starting database migrations...");

var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer(connectionString)
    .Options;

await using var db = new ApplicationDbContext(options);
await db.Database.MigrateAsync();

Console.WriteLine("Database migrations completed successfully.");
return 0;


