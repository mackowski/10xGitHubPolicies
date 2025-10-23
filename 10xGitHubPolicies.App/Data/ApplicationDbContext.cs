using _10xGitHubPolicies.App.Data.Entities;

using Microsoft.EntityFrameworkCore;

namespace _10xGitHubPolicies.App.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Scan> Scans { get; set; } = null!;
    public DbSet<Repository> Repositories { get; set; } = null!;
    public DbSet<Policy> Policies { get; set; } = null!;
    public DbSet<PolicyViolation> PolicyViolations { get; set; } = null!;
    public DbSet<ActionLog> ActionsLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Repositories
        modelBuilder.Entity<Repository>()
            .HasIndex(r => r.GitHubRepositoryId)
            .IsUnique();

        modelBuilder.Entity<Repository>()
            .HasIndex(r => r.Name);

        // Policies
        modelBuilder.Entity<Policy>()
            .HasIndex(p => p.PolicyKey)
            .IsUnique();

        // PolicyViolations
        modelBuilder.Entity<PolicyViolation>()
            .HasIndex(pv => new { pv.ScanId, pv.RepositoryId, pv.PolicyId })
            .IsUnique();

        modelBuilder.Entity<PolicyViolation>()
            .HasIndex(pv => pv.RepositoryId);
    }
}