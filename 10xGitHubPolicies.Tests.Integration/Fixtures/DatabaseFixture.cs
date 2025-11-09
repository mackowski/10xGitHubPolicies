using _10xGitHubPolicies.App.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace _10xGitHubPolicies.Tests.Integration.Fixtures;

/// <summary>
/// Provides an ephemeral SQLite in-memory database for integration tests.
/// The database is created once per test collection and shared across all tests in the collection.
/// 
/// Performance Notes:
/// - SQLite in-memory is much faster than SQL Server containers (milliseconds vs minutes)
/// - Database is created instantly, no container startup required
/// - Migrations run very quickly
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    private SqliteConnection? _connection;
    public string ConnectionString { get; private set; } = null!;
    public ApplicationDbContext DbContext { get; private set; } = null!;

    public DatabaseFixture()
    {
        Console.WriteLine($"[DatabaseFixture] Constructor called at {DateTime.UtcNow:HH:mm:ss.fff}");

        // Create SQLite in-memory database connection
        // Using shared cache so multiple DbContext instances can access the same in-memory database
        // The connection must stay open for the lifetime of the in-memory database
        _connection = new SqliteConnection("Data Source=:memory:;Cache=Shared");
        ConnectionString = _connection.ConnectionString;

        Console.WriteLine($"[DatabaseFixture] SQLite in-memory database created at {DateTime.UtcNow:HH:mm:ss.fff}");
    }

    public async Task InitializeAsync()
    {
        Console.WriteLine($"[DatabaseFixture] InitializeAsync started at {DateTime.UtcNow:HH:mm:ss.fff}");

        try
        {
            // Open connection - SQLite in-memory databases require the connection to stay open
            await _connection!.OpenAsync();
            Console.WriteLine($"[DatabaseFixture] Connection opened at {DateTime.UtcNow:HH:mm:ss.fff}");

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            DbContext = new ApplicationDbContext(options);
            Console.WriteLine($"[DatabaseFixture] DbContext created at {DateTime.UtcNow:HH:mm:ss.fff}");

            // Create database schema using EnsureCreated (faster than migrations for tests)
            // Note: Migrations contain SQL Server-specific syntax, so we use EnsureCreated for SQLite
            Console.WriteLine($"[DatabaseFixture] Creating database schema at {DateTime.UtcNow:HH:mm:ss.fff}");
            await DbContext.Database.EnsureCreatedAsync();
            Console.WriteLine($"[DatabaseFixture] Database schema created at {DateTime.UtcNow:HH:mm:ss.fff}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseFixture] ERROR in InitializeAsync: {ex.GetType().Name} - {ex.Message}");
            Console.WriteLine($"[DatabaseFixture] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        Console.WriteLine($"[DatabaseFixture] DisposeAsync started at {DateTime.UtcNow:HH:mm:ss.fff}");
        try
        {
            if (DbContext != null)
            {
                await DbContext.DisposeAsync();
                Console.WriteLine($"[DatabaseFixture] DbContext disposed at {DateTime.UtcNow:HH:mm:ss.fff}");
            }

            if (_connection != null)
            {
                await _connection.DisposeAsync();
                Console.WriteLine($"[DatabaseFixture] Connection disposed at {DateTime.UtcNow:HH:mm:ss.fff}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseFixture] ERROR in DisposeAsync: {ex.GetType().Name} - {ex.Message}");
        }
    }
}

