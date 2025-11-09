namespace _10xGitHubPolicies.App.Services.Webhooks;

/// <summary>
/// Handler for processing pull request webhook events.
/// Evaluates policies and executes PR actions (comments, status checks).
/// </summary>
public interface IPullRequestWebhookHandler
{
    /// <summary>
    /// Handles a pull request webhook event.
    /// Evaluates repository policies and executes configured actions.
    /// </summary>
    /// <param name="eventType">The webhook event type (e.g., "pull_request").</param>
    /// <param name="action">The action that triggered the event (e.g., "opened", "synchronize").</param>
    /// <param name="payload">The JSON payload of the webhook event.</param>
    /// <param name="deliveryId">The unique delivery ID for this webhook event.</param>
    Task HandlePullRequestEventAsync(string eventType, string? action, string payload, string? deliveryId);
}