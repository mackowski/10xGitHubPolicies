using _10xGitHubPolicies.App.Data;
using Microsoft.EntityFrameworkCore;

namespace _10xGitHubPolicies.App.Services.Implementations;

public class LoggingActionService : IActionService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<LoggingActionService> _logger;

    public LoggingActionService(ApplicationDbContext dbContext, ILogger<LoggingActionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task ProcessActionsForScanAsync(int scanId)
    {
        _logger.LogInformation("Processing actions for Scan ID: {ScanId}", scanId);

        var violations = await _dbContext.PolicyViolations
            .Where(v => v.ScanId == scanId)
            .ToListAsync();

        if (!violations.Any())
        {
            _logger.LogInformation("No violations found for Scan ID: {ScanId}. No actions to process.", scanId);
            return;
        }

        _logger.LogInformation("Found {ViolationCount} violations for Scan ID: {ScanId}. Logging intended actions...", violations.Count, scanId);

        foreach (var violation in violations)
        {
            // In a real implementation, we'd look up the policy's configured action.
            _logger.LogWarning("Action Required: Policy Violation ID {ViolationId} in Repository ID {RepositoryId} for Policy ID {PolicyId}. Intended action: Log to console.",
                violation.ViolationId,
                violation.RepositoryId,
                violation.PolicyId);
        }
    }
}
