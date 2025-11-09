# Webhook Signature Verification Troubleshooting

## Common Issues and Solutions

### Issue: "Invalid signature" Error

This is the most common issue. Here are the steps to debug and fix it:

### 1. Verify Webhook Secret Matches

**Check GitHub App Settings:**
1. Go to your GitHub App settings: https://github.com/settings/apps
2. Click on your app
3. Go to "Webhook" section
4. Click on the webhook
5. The webhook secret is shown (you can regenerate it if needed)

**Check Your Configuration:**
```bash
cd 10xGitHubPolicies.App
dotnet user-secrets list
```

Look for `GitHubApp:WebhookSecret` - it should match exactly what's in GitHub App settings.

**Common Mistakes:**
- ❌ Extra whitespace (leading/trailing spaces)
- ❌ Wrong secret (using OAuth secret instead of webhook secret)
- ❌ Secret not set at all
- ❌ Secret was regenerated in GitHub but not updated in config

**Fix:**
```bash
# Remove old secret
dotnet user-secrets remove "GitHubApp:WebhookSecret"

# Set correct secret (copy EXACTLY from GitHub App settings, no extra spaces)
dotnet user-secrets set "GitHubApp:WebhookSecret" "your-secret-here"
```

### 2. Check Body Reading

The body must be read as raw bytes before any processing. The updated controller now:
- Enables buffering first
- Reads body as raw bytes
- Preserves exact content for signature verification

**If using ngrok or a proxy:**
- Make sure the proxy doesn't modify the body
- Check that Content-Type is `application/json`
- Verify the body isn't being compressed or modified

### 3. Enable Debug Logging

Add this to `appsettings.Development.json` to see detailed logs:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "_10xGitHubPolicies.App.Controllers.WebhookController": "Debug"
    }
  }
}
```

This will show:
- Signature prefix (first 10 chars)
- Secret length
- Body length
- Whether secret is configured

### 4. Test Signature Verification Manually

You can test the signature verification logic manually:

```csharp
// Test code (for debugging only)
var secret = "your-webhook-secret";
var payload = "{\"action\":\"opened\",...}"; // Your JSON payload
var signature = "sha256=..."; // From X-Hub-Signature-256 header

var secretBytes = Encoding.UTF8.GetBytes(secret);
var payloadBytes = Encoding.UTF8.GetBytes(payload);

using var hmac = new HMACSHA256(secretBytes);
var hash = hmac.ComputeHash(payloadBytes);
var computedSignature = "sha256=" + Convert.ToHexString(hash).ToLower();

Console.WriteLine($"Computed: {computedSignature}");
Console.WriteLine($"Received: {signature}");
Console.WriteLine($"Match: {computedSignature == signature}");
```

### 5. Check Request Headers

Verify these headers are present:
- `X-Hub-Signature-256`: The signature (format: `sha256=...`)
- `X-GitHub-Event`: Event type (e.g., `pull_request`)
- `X-GitHub-Delivery`: Delivery ID
- `Content-Type`: Should be `application/json`

### 6. Verify Body Content

The body must be the **exact** JSON that GitHub sent. Common issues:
- Body was modified by middleware
- Body was read multiple times
- Encoding issues (should be UTF-8)
- Body was compressed/decompressed

**Check in logs:**
The controller now logs body length. Compare it with what GitHub sent.

### 7. Test with GitHub's Webhook Delivery

1. Go to GitHub App → Webhook settings
2. Click on a recent delivery
3. Click "Redeliver" to resend the exact same payload
4. Check your logs for the new attempt

### 8. Temporary Bypass for Testing (NOT FOR PRODUCTION)

If you need to test webhook processing without signature verification, you can temporarily add:

```csharp
// TEMPORARY - FOR TESTING ONLY - REMOVE BEFORE PRODUCTION
var skipVerification = _configuration.GetValue<bool>("GitHubApp:SkipWebhookVerification", false);
if (!skipVerification && !VerifySignature(bodyBytes, signature, webhookSecret))
{
    _logger.LogWarning("Invalid webhook signature");
    return Unauthorized(new { error = "Invalid signature" });
}
```

Then in `appsettings.Development.json`:
```json
{
  "GitHubApp": {
    "SkipWebhookVerification": true
  }
}
```

**⚠️ WARNING: Never enable this in production!**

## Quick Checklist

- [ ] Webhook secret in config matches GitHub App settings exactly
- [ ] No extra whitespace in webhook secret
- [ ] Body is read as raw bytes (not string first)
- [ ] `EnableBuffering()` is called before reading body
- [ ] Content-Type is `application/json`
- [ ] No middleware is modifying the body
- [ ] Debug logging is enabled to see signature details
- [ ] Tested with GitHub's "Redeliver" feature

## Still Not Working?

1. **Check Application Logs:**
   - Look for the debug log showing signature prefix, secret length, body length
   - Check if secret is configured (should show `SecretConfigured=true`)

2. **Verify ngrok/Proxy:**
   - If using ngrok, make sure it's not modifying requests
   - Try testing directly (if possible) without proxy

3. **Compare with GitHub Documentation:**
   - GitHub webhook signature verification: https://docs.github.com/en/webhooks/using-webhooks/validating-webhook-deliveries

4. **Test with Simple Payload:**
   - Create a simple test webhook payload
   - Verify signature manually using the test code above

## Success Indicators

✅ **Signature verification is working when:**
- No "Invalid signature" errors in logs
- Webhook events are processed successfully
- Logs show successful signature verification
- GitHub App webhook deliveries show "200 OK" status

