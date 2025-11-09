using System.Security.Cryptography;
using System.Text;

using _10xGitHubPolicies.App.Services.Webhooks;

using Microsoft.AspNetCore.Mvc;

namespace _10xGitHubPolicies.App.Controllers;

/// <summary>
/// Minimal webhook controller for testing webhook infrastructure.
/// This controller validates webhook signatures and logs events.
/// Full PR processing will be implemented in subsequent steps.
/// </summary>
[ApiController]
[Route("api/webhooks/github")]
public class WebhookController : ControllerBase
{
    private readonly ILogger<WebhookController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebhookService _webhookService;

    public WebhookController(
        ILogger<WebhookController> logger,
        IConfiguration configuration,
        IWebhookService webhookService)
    {
        _logger = logger;
        _configuration = configuration;
        _webhookService = webhookService;
    }

    [HttpPost]
    public async Task<IActionResult> HandleWebhook()
    {
        // Enable buffering BEFORE reading body (critical for signature verification)
        Request.EnableBuffering();

        // Get webhook secret from configuration
        var webhookSecret = _configuration["GitHubApp:WebhookSecret"];
        if (string.IsNullOrEmpty(webhookSecret))
        {
            _logger.LogWarning("Webhook secret is not configured. Webhook validation will fail.");
            return Unauthorized(new { error = "Webhook secret not configured" });
        }

        // Get signature from headers
        var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
        if (string.IsNullOrEmpty(signature))
        {
            _logger.LogWarning("Missing X-Hub-Signature-256 header");
            return Unauthorized(new { error = "Missing signature" });
        }

        // Read request body as raw bytes to preserve exact content
        Request.Body.Position = 0;
        using var memoryStream = new MemoryStream();
        await Request.Body.CopyToAsync(memoryStream);
        var bodyBytes = memoryStream.ToArray();
        Request.Body.Position = 0;

        // Convert to string for processing (using UTF8 encoding)
        var body = Encoding.UTF8.GetString(bodyBytes);

        // Log signature and secret info for debugging (first 10 chars only for security)
        _logger.LogDebug(
            "Verifying signature: Signature={SignaturePrefix}..., SecretLength={SecretLength}, BodyLength={BodyLength}",
            signature.Length > 10 ? signature[..10] : signature,
            webhookSecret.Length,
            bodyBytes.Length);

        // Verify signature and log computed signature for comparison
        var (isValid, computedSignature) = VerifySignatureWithDebug(bodyBytes, signature, webhookSecret);
        if (!isValid)
        {
            _logger.LogWarning(
                "Invalid webhook signature. Received={ReceivedSignaturePrefix}..., Computed={ComputedSignaturePrefix}..., BodyLength={BodyLength}, SecretConfigured={SecretConfigured}",
                signature.Length > 10 ? signature[..10] : signature,
                computedSignature?.Length > 10 ? computedSignature[..10] : computedSignature ?? "null",
                bodyBytes.Length,
                !string.IsNullOrEmpty(webhookSecret));
            return Unauthorized(new { error = "Invalid signature" });
        }

        _logger.LogDebug("Signature verification successful");

        // Get event type and metadata
        var eventType = Request.Headers["X-GitHub-Event"].FirstOrDefault();
        var deliveryId = Request.Headers["X-GitHub-Delivery"].FirstOrDefault();
        var action = Request.Headers["X-GitHub-Event-Action"].FirstOrDefault();

        _logger.LogInformation(
            "✅ Webhook received: Event={EventType}, Delivery={DeliveryId}, Action={Action}",
            eventType,
            deliveryId,
            action ?? "N/A");

        // Log the payload (be careful with sensitive data in production)
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Webhook payload: {Payload}", body);
        }

        // Handle different event types
        switch (eventType)
        {
            case "pull_request":
                _logger.LogInformation("📝 Processing pull_request event (Action: {Action})", action);
                // Process PR event asynchronously via webhook service
                await _webhookService.ProcessPullRequestEventAsync(eventType, action, body, deliveryId);
                break;
            case "ping":
                _logger.LogInformation("🏓 Received ping event - webhook is configured correctly!");
                return Ok(new { message = "pong", eventType, deliveryId });
            default:
                _logger.LogInformation("ℹ️ Unhandled event type: {EventType} (Action: {Action})", eventType, action);
                break;
        }

        return Ok(new { received = true, eventType, deliveryId, action });
    }

    private static (bool isValid, string? computedSignature) VerifySignatureWithDebug(byte[] payloadBytes, string signature, string secret)
    {
        if (string.IsNullOrEmpty(signature) || !signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            return (false, null);
        }

        try
        {
            // Extract hex signature (remove "sha256=" prefix)
            var signatureHex = signature["sha256=".Length..];
            var signatureBytes = Convert.FromHexString(signatureHex);

            // Convert secret to bytes
            var secretBytes = Encoding.UTF8.GetBytes(secret);

            // Compute HMAC-SHA256 hash of payload
            using var hmac = new HMACSHA256(secretBytes);
            var hash = hmac.ComputeHash(payloadBytes);

            // Convert computed hash to hex string for comparison
            var computedSignatureHex = Convert.ToHexString(hash).ToLowerInvariant();
            var computedSignature = "sha256=" + computedSignatureHex;

            // Compare signatures using constant-time comparison
            var isValid = CryptographicOperations.FixedTimeEquals(hash, signatureBytes);

            return (isValid, computedSignature);
        }
        catch
        {
            // Log exception for debugging (but don't expose details in production)
            return (false, null);
        }
    }
}