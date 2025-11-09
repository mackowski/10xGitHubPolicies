using _10xGitHubPolicies.App.Data;
using _10xGitHubPolicies.App.Data.Entities;
using _10xGitHubPolicies.App.Services.Configuration;
using _10xGitHubPolicies.App.Services.GitHub;

using Microsoft.EntityFrameworkCore;

using Octokit;

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

            // 4. Execute all actions based on configuration
            // Process each action independently - one failure doesn't block others
            foreach (var action in policyConfig.Actions)
            {
                try
                {
                    var normalizedAction = NormalizeActionName(action);
                    switch (normalizedAction)
                    {
                        case "create-issue":
                            await CreateIssueForViolationAsync(violation, policyConfig);
                            break;
                        case "archive-repo":
                            await ArchiveRepositoryForViolationAsync(violation, policyConfig);
                            break;
                        case "comment-on-prs":
                        case "comment_on_prs":
                            await CommentOnPullRequestsForViolationAsync(violation, policyConfig);
                            break;
                        case "block-prs":
                        case "block_prs":
                            await BlockPullRequestsForViolationAsync(violation, policyConfig);
                            break;
                        case "log-only":
                            _logger.LogInformation("Log-only action for violation ID: {ViolationId} in repository {RepositoryName}",
                                violation.ViolationId, violation.Repository.Name);
                            await LogActionAsync(violation, "log-only", "Success", "Violation logged as configured");
                            break;
                        default:
                            _logger.LogWarning("Unknown action type: {ActionType} for violation ID: {ViolationId}",
                                action, violation.ViolationId);
                            await LogActionAsync(violation, action, "Failed", $"Unknown action type: {action}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue processing other actions
                    _logger.LogError(ex, "Error processing action {ActionType} for violation ID: {ViolationId}. Continuing with other actions.",
                        action, violation.ViolationId);
                    await LogActionAsync(violation, action, "Failed", $"Exception: {ex.Message}");
                }
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
            // Check if repository is already archived (duplicate prevention)
            var repositorySettings = await _gitHubService.GetRepositorySettingsAsync(violation.Repository.GitHubRepositoryId);

            if (repositorySettings.Archived)
            {
                _logger.LogInformation("Repository {RepositoryName} (ID: {RepositoryId}) is already archived. Skipping archive action for violation ID: {ViolationId}",
                    violation.Repository.Name, violation.Repository.GitHubRepositoryId, violation.ViolationId);
                await LogActionAsync(violation, "archive-repo", "Skipped", $"Repository is already archived");
                return;
            }

            await _gitHubService.ArchiveRepositoryAsync(violation.Repository.GitHubRepositoryId);

            _logger.LogInformation("Successfully archived repository {RepositoryName} (ID: {RepositoryId}) due to policy violation {PolicyName} (Violation ID: {ViolationId})",
                violation.Repository.Name, violation.Repository.GitHubRepositoryId, policyConfig.Name, violation.ViolationId);

            await LogActionAsync(violation, "archive-repo", "Success", $"Repository archived due to {policyConfig.Name} policy violation");
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Repository {RepositoryName} (ID: {RepositoryId}) not found when attempting to archive for violation ID: {ViolationId}",
                violation.Repository.Name, violation.Repository.GitHubRepositoryId, violation.ViolationId);
            await LogActionAsync(violation, "archive-repo", "Failed", $"Repository not found: {ex.Message}");
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogWarning(ex, "Insufficient permissions to archive repository {RepositoryName} (ID: {RepositoryId}) for violation ID: {ViolationId}",
                violation.Repository.Name, violation.Repository.GitHubRepositoryId, violation.ViolationId);
            await LogActionAsync(violation, "archive-repo", "Failed", $"Insufficient permissions: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to archive repository {RepositoryName} (ID: {RepositoryId}) for violation ID: {ViolationId} due to policy {PolicyName}",
                violation.Repository.Name, violation.Repository.GitHubRepositoryId, violation.ViolationId, policyConfig.Name);
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

    /// <summary>
    /// Comments on a pull request for policy violations (webhook path).
    /// </summary>
    public async Task CommentOnPullRequestAsync(long repositoryId, int pullRequestNumber, Configuration.Models.PolicyConfig policyConfig, List<PolicyViolation> violations)
    {
        try
        {
            if (!violations.Any())
            {
                _logger.LogInformation("No violations for PR #{PrNumber} in repository {RepositoryId}. Skipping comment.", pullRequestNumber, repositoryId);
                return;
            }

            // Build comment message
            var message = policyConfig.PrCommentDetails?.Message;
            if (string.IsNullOrEmpty(message))
            {
                // Default message format
                // Use PolicyType (available from evaluation) or Policy.PolicyKey (if loaded from DB)
                var violationList = string.Join("\n", violations.Select(v => $"- {v.PolicyType ?? v.Policy?.PolicyKey ?? "Unknown"}"));
                message = $"⚠️ **Policy Compliance Violations Detected**\n\nThis pull request is associated with a repository that violates the following policies:\n\n{violationList}\n\nPlease address these violations before merging.";
            }

            // Check for duplicate comments (check if bot already commented with similar message)
            var existingComments = await _gitHubService.GetPullRequestCommentsAsync(repositoryId, pullRequestNumber);
            var botComments = existingComments.Where(c =>
                c.User != null &&
                (c.User.Type == AccountType.Bot ||
                 (c.User.Login != null && (c.User.Login.EndsWith("[bot]", StringComparison.OrdinalIgnoreCase) ||
                                           c.User.Login.Contains("bot", StringComparison.OrdinalIgnoreCase))))).ToList();

            // Check if we already commented with a similar message (check first 50 chars to avoid exact match requirement)
            var messagePrefix = message.Length > 50 ? message[..50] : message;
            var hasDuplicateComment = botComments.Any(c => c.Body.Contains(messagePrefix, StringComparison.OrdinalIgnoreCase));

            if (hasDuplicateComment)
            {
                _logger.LogInformation("Bot already commented on PR #{PrNumber} in repository {RepositoryId} with similar message. Skipping duplicate comment.", pullRequestNumber, repositoryId);
                return;
            }

            await _gitHubService.CreatePullRequestCommentAsync(repositoryId, pullRequestNumber, message);

            _logger.LogInformation("Successfully commented on PR #{PrNumber} in repository {RepositoryId}", pullRequestNumber, repositoryId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to comment on PR #{PrNumber} in repository {RepositoryId}", pullRequestNumber, repositoryId);
            throw;
        }
    }

    /// <summary>
    /// Creates or updates a status check for a pull request based on policy violations (webhook path).
    /// </summary>
    public async Task UpdatePullRequestStatusCheckAsync(long repositoryId, string headSha, List<PolicyViolation> violations, Configuration.Models.PolicyConfig policyConfig)
    {
        try
        {
            var statusCheckName = policyConfig.BlockPrsDetails?.StatusCheckName ?? "Policy Compliance Check";

            // Determine status and conclusion based on violations
            var hasViolations = violations.Any();
            var status = "completed";
            var conclusion = hasViolations ? "failure" : "success";

            // Check if status check already exists and update it instead of creating new one
            var existingCheckRuns = await _gitHubService.GetCheckRunsForRefAsync(repositoryId, headSha);
            var existingCheckRun = existingCheckRuns.FirstOrDefault(cr => cr.Name?.Equals(statusCheckName, StringComparison.OrdinalIgnoreCase) == true);

            if (existingCheckRun != null)
            {
                _logger.LogInformation(
                    "Status check '{StatusCheckName}' already exists for SHA {HeadSha}. Updating existing check run {CheckRunId}.",
                    statusCheckName,
                    headSha,
                    existingCheckRun.Id);

                await _gitHubService.UpdateStatusCheckAsync(
                    repositoryId,
                    existingCheckRun.Id,
                    status,
                    conclusion,
                    detailsUrl: null);
            }
            else
            {
                await _gitHubService.CreateStatusCheckAsync(
                    repositoryId,
                    headSha,
                    statusCheckName,
                    status,
                    conclusion,
                    detailsUrl: null);
            }

            _logger.LogInformation(
                "Successfully created status check '{StatusCheckName}' for SHA {HeadSha} in repository {RepositoryId} with conclusion: {Conclusion}",
                statusCheckName,
                headSha,
                repositoryId,
                conclusion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create/update status check for SHA {HeadSha} in repository {RepositoryId}", headSha, repositoryId);
            throw;
        }
    }

    /// <summary>
    /// Comments on all open pull requests for a violation (scan-based path, backward compatibility).
    /// </summary>
    private async Task CommentOnPullRequestsForViolationAsync(PolicyViolation violation, Configuration.Models.PolicyConfig policyConfig)
    {
        try
        {
            var openPRs = await _gitHubService.GetOpenPullRequestsAsync(violation.Repository.GitHubRepositoryId);

            if (!openPRs.Any())
            {
                _logger.LogInformation("No open PRs found for repository {RepositoryName}. Skipping comment action.", violation.Repository.Name);
                await LogActionAsync(violation, "comment-on-prs", "Skipped", "No open pull requests found");
                return;
            }

            var violationsList = new List<PolicyViolation> { violation };

            foreach (var pr in openPRs)
            {
                try
                {
                    await CommentOnPullRequestAsync(violation.Repository.GitHubRepositoryId, pr.Number, policyConfig, violationsList);
                    await LogActionAsync(violation, "comment-on-prs", "Success", $"Commented on PR #{pr.Number}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to comment on PR #{PrNumber} in repository {RepositoryName}", pr.Number, violation.Repository.Name);
                    await LogActionAsync(violation, "comment-on-prs", "Failed", $"Failed to comment on PR #{pr.Number}: {ex.Message}");
                    // Continue processing other PRs
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing comment-on-prs action for violation ID: {ViolationId}", violation.ViolationId);
            await LogActionAsync(violation, "comment-on-prs", "Failed", $"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Blocks all open pull requests for a violation (scan-based path, backward compatibility).
    /// </summary>
    private async Task BlockPullRequestsForViolationAsync(PolicyViolation violation, Configuration.Models.PolicyConfig policyConfig)
    {
        try
        {
            var openPRs = await _gitHubService.GetOpenPullRequestsAsync(violation.Repository.GitHubRepositoryId);

            if (!openPRs.Any())
            {
                _logger.LogInformation("No open PRs found for repository {RepositoryName}. Skipping block action.", violation.Repository.Name);
                await LogActionAsync(violation, "block-prs", "Skipped", "No open pull requests found");
                return;
            }

            var violationsList = new List<PolicyViolation> { violation };

            foreach (var pr in openPRs)
            {
                try
                {
                    if (string.IsNullOrEmpty(pr.Head.Sha))
                    {
                        _logger.LogWarning("PR #{PrNumber} in repository {RepositoryName} has no head SHA. Skipping.", pr.Number, violation.Repository.Name);
                        continue;
                    }

                    await UpdatePullRequestStatusCheckAsync(violation.Repository.GitHubRepositoryId, pr.Head.Sha, violationsList, policyConfig);
                    await LogActionAsync(violation, "block-prs", "Success", $"Created/updated status check for PR #{pr.Number}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create status check for PR #{PrNumber} in repository {RepositoryName}", pr.Number, violation.Repository.Name);
                    await LogActionAsync(violation, "block-prs", "Failed", $"Failed to block PR #{pr.Number}: {ex.Message}");
                    // Continue processing other PRs
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing block-prs action for violation ID: {ViolationId}", violation.ViolationId);
            await LogActionAsync(violation, "block-prs", "Failed", $"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Normalizes action names to handle both hyphenated and underscored formats.
    /// </summary>
    private static string NormalizeActionName(string action)
    {
        return action?.Replace("_", "-", StringComparison.Ordinal) ?? string.Empty;
    }
}