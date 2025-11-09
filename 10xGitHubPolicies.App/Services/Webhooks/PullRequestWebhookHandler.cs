using System.Text.Json;

using _10xGitHubPolicies.App.Data.Entities;
using _10xGitHubPolicies.App.Services.Action;
using _10xGitHubPolicies.App.Services.Configuration;
using _10xGitHubPolicies.App.Services.Configuration.Models;
using _10xGitHubPolicies.App.Services.GitHub;
using _10xGitHubPolicies.App.Services.Policies;

using Octokit;

namespace _10xGitHubPolicies.App.Services.Webhooks;

/// <summary>
/// Handler for processing pull request webhook events.
/// Evaluates repository policies and executes PR actions (comments, status checks).
/// </summary>
public class PullRequestWebhookHandler : IPullRequestWebhookHandler
{
    private readonly IConfigurationService _configurationService;
    private readonly IPolicyEvaluationService _policyEvaluationService;
    private readonly IGitHubService _gitHubService;
    private readonly IActionService _actionService;
    private readonly ILogger<PullRequestWebhookHandler> _logger;

    public PullRequestWebhookHandler(
        IConfigurationService configurationService,
        IPolicyEvaluationService policyEvaluationService,
        IGitHubService gitHubService,
        IActionService actionService,
        ILogger<PullRequestWebhookHandler> logger)
    {
        _configurationService = configurationService;
        _policyEvaluationService = policyEvaluationService;
        _gitHubService = gitHubService;
        _actionService = actionService;
        _logger = logger;
    }

    public async Task HandlePullRequestEventAsync(string eventType, string? action, string payload, string? deliveryId)
    {
        _logger.LogInformation(
            "Processing pull request webhook: Event={EventType}, Action={Action}, Delivery={DeliveryId}",
            eventType,
            action,
            deliveryId);

        try
        {
            // Parse webhook payload
            using var jsonDoc = JsonDocument.Parse(payload);
            var root = jsonDoc.RootElement;

            // Extract PR information
            if (!root.TryGetProperty("pull_request", out var pullRequestElement))
            {
                _logger.LogWarning("Webhook payload does not contain 'pull_request' property");
                return;
            }

            if (!root.TryGetProperty("repository", out var repositoryElement))
            {
                _logger.LogWarning("Webhook payload does not contain 'repository' property");
                return;
            }

            // Extract repository ID and PR number
            var repositoryId = repositoryElement.GetProperty("id").GetInt64();
            var prNumber = pullRequestElement.GetProperty("number").GetInt32();
            var headSha = pullRequestElement.GetProperty("head").GetProperty("sha").GetString();

            if (string.IsNullOrEmpty(headSha))
            {
                _logger.LogWarning("PR head SHA is missing");
                return;
            }

            _logger.LogInformation(
                "Processing PR #{PrNumber} in repository {RepositoryId} (SHA: {HeadSha}, Action: {Action})",
                prNumber,
                repositoryId,
                headSha,
                action ?? "unknown");

            // Process all PR actions to re-evaluate policies on any change
            // This ensures policies are re-evaluated when PRs are edited, converted to draft,
            // made ready for review, or any other state change occurs
            _logger.LogInformation("Processing PR action '{Action}' - re-evaluating policies", action ?? "unknown");

            // Get repository object
            var repository = await _gitHubService.GetRepositorySettingsAsync(repositoryId);

            // Get configuration and evaluate policies
            var config = await _configurationService.GetConfigAsync();
            var violations = (await _policyEvaluationService.EvaluateRepositoryAsync(repository, config.Policies)).ToList();

            _logger.LogInformation(
                "Evaluated repository {RepositoryId} for PR #{PrNumber}: Found {ViolationCount} violations",
                repositoryId,
                prNumber,
                violations.Count);

            // Group violations by policy config to execute actions per policy
            var violationsByPolicy = violations
                .GroupBy(v => v.PolicyType)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Process all policies that have PR actions configured
            // For status checks: process even if no violations (to update to success when violations are fixed)
            // For comments: only process when violations exist (no need to comment when compliant)
            foreach (var policyConfig in config.Policies)
            {
                // Get violations for this policy (empty list if no violations)
                var policyViolations = violationsByPolicy.GetValueOrDefault(policyConfig.Type, new List<PolicyViolation>());

                // Execute PR actions based on policy configuration
                foreach (var actionType in policyConfig.Actions)
                {
                    var normalizedAction = NormalizeActionName(actionType);
                    try
                    {
                        switch (normalizedAction)
                        {
                            case "comment-on-prs":
                            case "comment_on_prs":
                                // Only comment when there are violations
                                if (policyViolations.Any())
                                {
                                    await _actionService.CommentOnPullRequestAsync(repositoryId, prNumber, policyConfig, policyViolations);
                                }
                                break;
                            case "block-prs":
                            case "block_prs":
                                // Always update status check (even when no violations, to set success status)
                                await _actionService.UpdatePullRequestStatusCheckAsync(repositoryId, headSha, policyViolations, policyConfig);
                                break;
                            default:
                                _logger.LogDebug("Policy {PolicyType} has action {Action} which is not a PR action. Skipping.", policyConfig.Type, actionType);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing PR action {Action} for policy {PolicyType} on PR #{PrNumber}", actionType, policyConfig.Type, prNumber);
                        // Continue processing other actions
                    }
                }
            }

            _logger.LogInformation(
                "Completed processing PR webhook: PR #{PrNumber}, Repository {RepositoryId}",
                prNumber,
                repositoryId);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse webhook payload JSON");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pull request webhook: Delivery={DeliveryId}", deliveryId);
        }
    }

    private static string NormalizeActionName(string action)
    {
        return action?.Replace("_", "-", StringComparison.Ordinal) ?? string.Empty;
    }
}