using System.Text.Json;

using Hangfire;

namespace _10xGitHubPolicies.App.Services.Webhooks;

/// <summary>
/// Service for processing GitHub webhook events.
/// Routes webhook events to appropriate handlers using Hangfire for async processing.
/// </summary>
public class WebhookService : IWebhookService
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<WebhookService> _logger;

    public WebhookService(
        IBackgroundJobClient backgroundJobClient,
        ILogger<WebhookService> logger)
    {
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    public Task ProcessPullRequestEventAsync(string eventType, string? action, string payload, string? deliveryId)
    {
        // Remove line breaks/carriage returns from user input to prevent log forging
        var safeEventType = eventType?.Replace("\r", string.Empty).Replace("\n", string.Empty);
        var safeAction = action?.Replace("\r", string.Empty).Replace("\n", string.Empty);
        var safeDeliveryId = deliveryId?.Replace("\r", string.Empty).Replace("\n", string.Empty);
        _logger.LogInformation(
            "Enqueuing pull request webhook processing: Event={EventType}, Action={Action}, Delivery={DeliveryId}",
            safeEventType,
            safeAction,
            safeDeliveryId);

        // Enqueue background job for async processing
        // This ensures webhook response is returned quickly to GitHub
        _backgroundJobClient.Enqueue<IPullRequestWebhookHandler>(
            handler => handler.HandlePullRequestEventAsync(eventType, action, payload, deliveryId));

        return Task.CompletedTask;
    }
}