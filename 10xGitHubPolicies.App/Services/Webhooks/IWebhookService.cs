namespace _10xGitHubPolicies.App.Services.Webhooks;

/// <summary>
/// Service for processing GitHub webhook events.
/// Handles routing webhook events to appropriate handlers.
/// </summary>
public interface IWebhookService
{
    /// <summary>
    /// Processes a pull request webhook event.
    /// </summary>
    /// <param name="eventType">The webhook event type (e.g., "pull_request").</param>
    /// <param name="action">The action that triggered the event (e.g., "opened", "synchronize").</param>
    /// <param name="payload">The JSON payload of the webhook event.</param>
    /// <param name="deliveryId">The unique delivery ID for this webhook event.</param>
    Task ProcessPullRequestEventAsync(string eventType, string? action, string payload, string? deliveryId);
}