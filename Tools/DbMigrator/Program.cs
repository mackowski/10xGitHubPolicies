using _10xGitHubPolicies.App.Data;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// Reads ConnectionStrings__DefaultConnection from environment/appsettings and runs EF Core migrations.
// Supports both Managed Identity (when running in Azure) and Azure AD token authentication (when running in CI/CD).
// If AZURE_SQL_TOKEN is set, it will be used instead of Managed Identity.

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("ConnectionStrings__DefaultConnection environment variable is not set.");
    return 1;
}

Console.WriteLine("Starting database migrations...");

// If Azure SQL token is provided (e.g., from GitHub Actions), use it for authentication
var sqlToken = Environment.GetEnvironmentVariable("AZURE_SQL_TOKEN");

DbContextOptions<ApplicationDbContext> options;

if (!string.IsNullOrWhiteSpace(sqlToken))
{
    // Use token-based authentication for CI/CD scenarios
    Console.WriteLine("Using Azure AD token authentication for SQL connection.");
    
    // Remove Authentication parameter from connection string when using token
    var builder = new SqlConnectionStringBuilder(connectionString);
    builder.Remove("Authentication"); // Remove Authentication parameter as AccessToken will be used
    
    // Create connection with access token (AccessToken takes precedence over Authentication parameter)
    var connection = new SqlConnection(builder.ConnectionString)
    {
        AccessToken = sqlToken
    };
    
    options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlServer(connection, sqlOptions => sqlOptions.EnableRetryOnFailure())
        .Options;
}
else
{
    // Use Managed Identity (for Azure Web App) or connection string authentication
    Console.WriteLine("Using connection string authentication (Managed Identity or SQL Auth).");
    options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlServer(connectionString, sqlOptions => sqlOptions.EnableRetryOnFailure())
        .Options;
}

await using var db = new ApplicationDbContext(options);
await db.Database.MigrateAsync();

Console.WriteLine("Database migrations completed successfully.");
return 0;


