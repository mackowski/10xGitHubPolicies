using _10xGitHubPolicies.App.Data;
using _10xGitHubPolicies.App.Data.Entities;
using _10xGitHubPolicies.App.Services.Configuration;
using _10xGitHubPolicies.App.Services.GitHub;

using Microsoft.EntityFrameworkCore;

namespace _10xGitHubPolicies.App.Services.Action;

public class ActionService : IActionService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IGitHubService _gitHubService;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<ActionService> _logger;

    public ActionService(
        ApplicationDbContext dbContext,
        IGitHubService gitHubService,
        IConfigurationService configurationService,
        ILogger<ActionService> logger)
    {
        _dbContext = dbContext;
        _gitHubService = gitHubService;
        _configurationService = configurationService;
        _logger = logger;
    }

    public async Task ProcessActionsForScanAsync(int scanId)
    {
        _logger.LogInformation("Processing actions for Scan ID: {ScanId}", scanId);

        // 1. Load violations with related entities (Repository, Policy)
        var violations = await _dbContext.PolicyViolations
            .Include(v => v.Repository)
            .Include(v => v.Policy)
            .Where(v => v.ScanId == scanId)
            .ToListAsync();

        if (!violations.Any())
        {
            _logger.LogInformation("No violations found for Scan ID: {ScanId}. No actions to process.", scanId);
            return;
        }

        _logger.LogInformation("Found {ViolationCount} violations for Scan ID: {ScanId}. Processing actions...", violations.Count, scanId);

        // 2. Get configuration to determine actions
        var config = await _configurationService.GetConfigAsync();

        // 3. Process each violation
        foreach (var violation in violations)
        {
            var policyConfig = config.Policies
                .FirstOrDefault(p => p.Type == violation.Policy.PolicyKey);

            if (policyConfig == null)
            {
                _logger.LogWarning("No policy configuration found for PolicyKey: {PolicyKey}. Skipping violation ID: {ViolationId}",
                    violation.Policy.PolicyKey, violation.ViolationId);
                continue;
            }

            // 4. Execute action based on configuration
            if (policyConfig.Action == "create-issue" || policyConfig.Action == "create_issue")
            {
                await CreateIssueForViolationAsync(violation, policyConfig);
            }
            else if (policyConfig.Action == "archive-repo" || policyConfig.Action == "archive_repo")
            {
                await ArchiveRepositoryForViolationAsync(violation, policyConfig);
            }
            else if (policyConfig.Action == "log-only" || policyConfig.Action == "log_only")
            {
                _logger.LogInformation("Log-only action for violation ID: {ViolationId} in repository {RepositoryName}",
                    violation.ViolationId, violation.Repository.Name);
                await LogActionAsync(violation, "log-only", "Success", "Violation logged as configured");
            }
            else
            {
                _logger.LogWarning("Unknown action type: {ActionType} for violation ID: {ViolationId}",
                    policyConfig.Action, violation.ViolationId);
            }
        }

        _logger.LogInformation("Completed processing actions for Scan ID: {ScanId}", scanId);
    }

    private async Task CreateIssueForViolationAsync(PolicyViolation violation, Configuration.Models.PolicyConfig policyConfig)
    {
        try
        {
            // Use configured issue details or provide defaults
            var title = policyConfig.IssueDetails?.Title ?? $"Compliance Violation: {violation.Policy.PolicyKey}";
            var body = policyConfig.IssueDetails?.Body ?? $"This repository violates the {violation.Policy.PolicyKey} policy. Please review and take appropriate action.";
            var labels = policyConfig.IssueDetails?.Labels ?? new List<string> { "policy-violation", "compliance" };

            // Check for duplicate issues (US-010)
            var primaryLabel = labels.FirstOrDefault() ?? "policy-violation";
            var existingIssues = await _gitHubService.GetOpenIssuesAsync(violation.Repository.GitHubRepositoryId, primaryLabel);

            var duplicateIssue = existingIssues.FirstOrDefault(issue =>
                issue.Title.Equals(title, StringComparison.OrdinalIgnoreCase));

            if (duplicateIssue != null)
            {
                _logger.LogInformation("Duplicate issue already exists for repository {RepositoryName} with title '{Title}'. Issue URL: {IssueUrl}. Skipping creation.",
                    violation.Repository.Name, title, duplicateIssue.HtmlUrl);
                await LogActionAsync(violation, "create-issue", "Skipped", $"Duplicate issue already exists: {duplicateIssue.HtmlUrl}");
                return;
            }

            // Create the issue
            var issue = await _gitHubService.CreateIssueAsync(
                violation.Repository.GitHubRepositoryId,
                title,
                body,
                labels);

            _logger.LogInformation("Successfully created issue #{IssueNumber} in repository {RepositoryName} for policy violation. Issue URL: {IssueUrl}",
                issue.Number, violation.Repository.Name, issue.HtmlUrl);

            await LogActionAsync(violation, "create-issue", "Success", $"Created issue #{issue.Number}: {issue.HtmlUrl}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create issue for violation ID: {ViolationId} in repository {RepositoryName}",
                violation.ViolationId, violation.Repository.Name);
            await LogActionAsync(violation, "create-issue", "Failed", $"Exception: {ex.Message}");
        }
    }

    private async Task ArchiveRepositoryForViolationAsync(PolicyViolation violation, Configuration.Models.PolicyConfig policyConfig)
    {
        try
        {
            await _gitHubService.ArchiveRepositoryAsync(violation.Repository.GitHubRepositoryId);

            _logger.LogInformation("Successfully archived repository {RepositoryName} (ID: {RepositoryId}) due to policy violation",
                violation.Repository.Name, violation.Repository.GitHubRepositoryId);

            await LogActionAsync(violation, "archive-repo", "Success", $"Repository archived due to {policyConfig.Name} policy violation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to archive repository {RepositoryName} (ID: {RepositoryId}) for violation ID: {ViolationId}",
                violation.Repository.Name, violation.Repository.GitHubRepositoryId, violation.ViolationId);
            await LogActionAsync(violation, "archive-repo", "Failed", $"Exception: {ex.Message}");
        }
    }

    private async Task LogActionAsync(PolicyViolation violation, string actionType, string status, string details)
    {
        var actionLog = new ActionLog
        {
            RepositoryId = violation.RepositoryId,
            PolicyId = violation.PolicyId,
            ActionType = actionType,
            Status = status,
            Details = details,
            Timestamp = DateTime.UtcNow
        };

        _dbContext.ActionsLogs.Add(actionLog);
        await _dbContext.SaveChangesAsync();
    }
}