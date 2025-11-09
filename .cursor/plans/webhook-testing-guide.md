# Webhook Infrastructure Testing Guide

This guide provides a step-by-step approach to test the webhook infrastructure **before** implementing the full PR comment/block functionality.

## Testing Strategy

We'll implement a **minimal webhook endpoint** first that:
1. ✅ Validates webhook signatures
2. ✅ Logs incoming webhook events
3. ✅ Returns proper HTTP responses
4. ✅ Handles different event types

Once this works, we can proceed with the full implementation.

## Step 1: Create Minimal Webhook Controller

Create a basic webhook controller that validates signatures and logs events:

**File**: `10xGitHubPolicies.App/Controllers/WebhookController.cs`

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace _10xGitHubPolicies.App.Controllers;

[ApiController]
[Route("api/webhooks/github")]
public class WebhookController : ControllerBase
{
    private readonly ILogger<WebhookController> _logger;
    private readonly IConfiguration _configuration;

    public WebhookController(ILogger<WebhookController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [HttpPost]
    public async Task<IActionResult> HandleWebhook()
    {
        // Get webhook secret from configuration
        var webhookSecret = _configuration["GitHubApp:WebhookSecret"];
        if (string.IsNullOrEmpty(webhookSecret))
        {
            _logger.LogWarning("Webhook secret is not configured. Webhook validation will fail.");
            return Unauthorized("Webhook secret not configured");
        }

        // Get signature from headers
        var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
        if (string.IsNullOrEmpty(signature))
        {
            _logger.LogWarning("Missing X-Hub-Signature-256 header");
            return Unauthorized("Missing signature");
        }

        // Read request body
        Request.EnableBuffering();
        var body = await new StreamReader(Request.Body, Encoding.UTF8).ReadToEndAsync();
        Request.Body.Position = 0;

        // Verify signature
        if (!VerifySignature(body, signature, webhookSecret))
        {
            _logger.LogWarning("Invalid webhook signature");
            return Unauthorized("Invalid signature");
        }

        // Get event type
        var eventType = Request.Headers["X-GitHub-Event"].FirstOrDefault();
        var deliveryId = Request.Headers["X-GitHub-Delivery"].FirstOrDefault();

        _logger.LogInformation(
            "Received webhook: Event={EventType}, Delivery={DeliveryId}, Action={Action}",
            eventType,
            deliveryId,
            Request.Headers["X-GitHub-Event-Action"].FirstOrDefault());

        // Log the payload (be careful with sensitive data in production)
        _logger.LogDebug("Webhook payload: {Payload}", body);

        // Handle different event types
        switch (eventType)
        {
            case "pull_request":
                _logger.LogInformation("Processing pull_request event");
                // TODO: Process PR event (will be implemented later)
                break;
            case "ping":
                _logger.LogInformation("Received ping event - webhook is configured correctly");
                return Ok(new { message = "pong" });
            default:
                _logger.LogInformation("Unhandled event type: {EventType}", eventType);
                break;
        }

        return Ok(new { received = true, eventType, deliveryId });
    }

    private static bool VerifySignature(string payload, string signature, string secret)
    {
        if (string.IsNullOrEmpty(signature) || !signature.StartsWith("sha256="))
        {
            return false;
        }

        var signatureBytes = Convert.FromHexString(signature["sha256=".Length..]);
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(secretBytes);
        var hash = hmac.ComputeHash(payloadBytes);

        return CryptographicOperations.FixedTimeEquals(hash, signatureBytes);
    }
}
```

## Step 2: Add Webhook Secret to Configuration

Add the webhook secret to your configuration:

**For local development** (using .NET Secret Manager):
```bash
cd 10xGitHubPolicies.App
dotnet user-secrets set "GitHubApp:WebhookSecret" "YOUR_WEBHOOK_SECRET"
```

**In `appsettings.Development.json`** (optional, for testing):
```json
{
  "GitHubApp": {
    "WebhookSecret": "your-webhook-secret-here"
  }
}
```

## Step 3: Update GitHubAppOptions

Add WebhookSecret to the options class:

**File**: `10xGitHubPolicies.App/Options/GitHubAppOptions.cs`

```csharp
public class GitHubAppOptions
{
    public const string GitHubApp = "GitHubApp";

    public long AppId { get; set; }
    public string PrivateKey { get; set; } = string.Empty;
    public long InstallationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public string? WebhookSecret { get; set; }  // Add this

    public string? BaseUrl { get; set; }
}
```

## Step 4: Set Up Local Webhook Testing with ngrok

### Install ngrok

**macOS** (using Homebrew):
```bash
brew install ngrok
```

**Or download from**: https://ngrok.com/download

### Start Your Application

```bash
cd 10xGitHubPolicies.App
dotnet run --launch-profile https
```

Your app should be running on `https://localhost:7040`

### Start ngrok Tunnel

In a new terminal:
```bash
ngrok http 7040
```

ngrok will display something like:
```
Forwarding  https://abc123.ngrok.io -> https://localhost:7040
```

**Copy the HTTPS URL** (e.g., `https://abc123.ngrok.io`)

## Step 5: Configure GitHub App Webhook

1. Go to your GitHub App settings: https://github.com/settings/apps
2. Select your app
3. Scroll to "Webhook" section
4. Click "Edit" or "Add webhook"
5. Configure:
   - **Webhook URL**: `https://abc123.ngrok.io/api/webhooks/github` (use your ngrok URL)
   - **Content type**: `application/json`
   - **Secret**: Generate a random secret (save it - you'll need it for configuration)
   - **Events**: Select "Let me select individual events"
     - ✅ Check `Pull requests`
     - ✅ Check `Ping` (for testing)
6. Click "Update webhook" or "Add webhook"

### Get Webhook Secret

After creating the webhook, you can see the secret in the webhook settings. Copy it and add to your configuration:

```bash
dotnet user-secrets set "GitHubApp:WebhookSecret" "your-webhook-secret-here"
```

## Step 6: Test Webhook Delivery

### Test 1: Ping Event (Automatic)

GitHub automatically sends a `ping` event when you create/update a webhook. Check your application logs:

```
[Information] Received webhook: Event=ping, Delivery=abc-123, Action=
[Information] Received ping event - webhook is configured correctly
```

If you see this, **webhook infrastructure is working!** ✅

### Test 2: Manual Ping

1. Go to your GitHub App webhook settings
2. Click on the webhook
3. Scroll to "Recent Deliveries"
4. Find the latest `ping` event
5. Click "Redeliver" to test again

### Test 3: Test with Real PR Event

1. Create a test repository (or use an existing one)
2. Create a new branch
3. Open a pull request
4. Check your application logs - you should see:

```
[Information] Received webhook: Event=pull_request, Delivery=xyz-789, Action=opened
[Information] Processing pull_request event
```

## Step 7: Verify Signature Validation

### Test Invalid Signature

You can test signature validation by sending a request with an invalid signature:

```bash
curl -X POST https://abc123.ngrok.io/api/webhooks/github \
  -H "Content-Type: application/json" \
  -H "X-GitHub-Event: ping" \
  -H "X-Hub-Signature-256: sha256=invalid" \
  -d '{"zen":"test"}'
```

You should get a `401 Unauthorized` response, and logs should show:
```
[Warning] Invalid webhook signature
```

### Test Missing Signature

```bash
curl -X POST https://abc123.ngrok.io/api/webhooks/github \
  -H "Content-Type: application/json" \
  -H "X-GitHub-Event: ping" \
  -d '{"zen":"test"}'
```

You should get a `401 Unauthorized` response.

## Step 8: Monitor Webhook Deliveries

### In Your Application Logs

Watch for webhook events in your console output. You should see:
- ✅ Successful signature validation
- ✅ Event type and delivery ID
- ✅ Payload logging (in Debug mode)

### In GitHub App Settings

1. Go to your GitHub App → Webhook settings
2. Click on the webhook
3. View "Recent Deliveries"
4. Check delivery status:
   - ✅ Green checkmark = Success (200 OK)
   - ❌ Red X = Failure (check response)

## Step 9: Test Different PR Events

Create test scenarios to verify different PR events:

1. **PR Opened**:
   - Create a new PR
   - Should see: `Event=pull_request, Action=opened`

2. **PR Synchronized** (new commits pushed):
   - Push commits to the PR branch
   - Should see: `Event=pull_request, Action=synchronize`

3. **PR Reopened**:
   - Close the PR, then reopen it
   - Should see: `Event=pull_request, Action=reopened`

4. **PR Closed**:
   - Close the PR
   - Should see: `Event=pull_request, Action=closed`

## Step 10: Verify Async Processing (Optional)

If you want to test async processing with Hangfire:

1. Add Hangfire job enqueueing to the webhook controller
2. Check Hangfire dashboard at `/hangfire`
3. Verify jobs are created for webhook events

## Troubleshooting

### Webhook Not Receiving Events

1. **Check ngrok is running**: Make sure ngrok tunnel is active
2. **Check webhook URL**: Verify the URL in GitHub App settings matches your ngrok URL
3. **Check HTTPS**: GitHub requires HTTPS for webhooks
4. **Check firewall**: Ensure port 7040 is accessible

### Signature Validation Failing

1. **Verify secret matches**: The secret in your config must match the one in GitHub App settings
2. **Check encoding**: Ensure the secret is stored correctly (no extra whitespace)
3. **Verify signature format**: GitHub sends `sha256=...` format

### 401 Unauthorized Responses

1. **Check webhook secret**: Must be configured in user secrets or appsettings
2. **Check signature header**: Must be `X-Hub-Signature-256`
3. **Check body reading**: Ensure body is read before signature verification

## Success Criteria

✅ **Webhook infrastructure is working when:**

1. Ping events are received and logged successfully
2. PR events are received and logged successfully
3. Signature validation works (rejects invalid signatures)
4. HTTP responses are correct (200 OK for valid, 401 for invalid)
5. Different PR event types are recognized (opened, synchronize, reopened, closed)
6. Webhook deliveries show as successful in GitHub App settings

## Next Steps

Once webhook infrastructure is verified:

1. ✅ Proceed with implementing `IWebhookService` and `WebhookService`
2. ✅ Implement `IPullRequestWebhookHandler` and `PullRequestWebhookHandler`
3. ✅ Add policy evaluation logic
4. ✅ Implement PR comment and status check actions
5. ✅ Add comprehensive tests

## Quick Test Checklist

- [ ] Webhook controller created and compiles
- [ ] Webhook secret configured in user secrets
- [ ] Application running on HTTPS (port 7040)
- [ ] ngrok tunnel active and forwarding to localhost:7040
- [ ] GitHub App webhook configured with ngrok URL
- [ ] Ping event received and logged successfully
- [ ] PR opened event received and logged successfully
- [ ] Invalid signature rejected (401 response)
- [ ] Webhook deliveries show success in GitHub App settings

## Additional Resources

- [GitHub Webhook Documentation](https://docs.github.com/en/webhooks)
- [GitHub Webhook Events](https://docs.github.com/en/webhooks/webhook-events-and-payloads)
- [ngrok Documentation](https://ngrok.com/docs)
- [Octokit.Webhooks](https://github.com/octokit/webhooks.net) - Alternative library for webhook handling

