# Webhook Development and Testing Guide

This guide explains how to test and develop webhook functionality locally using ngrok to expose your local application to GitHub.

> **Note**: This guide is for local development and testing. For production deployment:
> - Webhook URL: `https://wa-10xghpolicies-prod.azurewebsites.net/api/webhooks/github`
> - Webhook Secret: Configure via GitHub Repository Secret `GH_APP_WEBHOOK_SECRET` (see [CI/CD Workflows](./ci-cd-workflows.md) and [Authentication](./authentication.md) documentation)

## Overview

GitHub webhooks require a publicly accessible HTTPS endpoint. For local development, we use **ngrok** to create a secure tunnel from a public URL to your local application.

## Prerequisites

1. ✅ Application running locally on HTTPS (port 7040)
2. ✅ GitHub App created and configured
3. ✅ ngrok installed (see installation instructions below)

## Step 1: Install ngrok

### macOS (using Homebrew)

```bash
brew install ngrok
```

### Windows (using Chocolatey)

```bash
choco install ngrok
```

### Linux

Download from [ngrok.com/download](https://ngrok.com/download) or use package manager:

```bash
# Ubuntu/Debian
sudo snap install ngrok

# Or download binary
wget https://bin.equinox.io/c/bNyj1mQVY4c/ngrok-v3-stable-linux-amd64.tgz
tar -xzf ngrok-v3-stable-linux-amd64.tgz
sudo mv ngrok /usr/local/bin/
```

### Verify Installation

```bash
ngrok version
```

You should see the ngrok version number.

## Step 2: Configure Webhook Secret

Before testing webhooks, you need to configure the webhook secret in your local environment.

```bash
cd 10xGitHubPolicies.App
dotnet user-secrets set "GitHubApp:WebhookSecret" "YOUR_WEBHOOK_SECRET"
```

**Note**: You'll get the webhook secret from GitHub App settings (see Step 4).


## Step 3: Start Your Application

Start the application with the HTTPS profile:

```bash
cd 10xGitHubPolicies.App
dotnet run --launch-profile https
```

Your application should be running on `https://localhost:7040`.

**Important**: Always use the HTTPS profile to ensure OAuth authentication works correctly.

## Step 4: Start ngrok Tunnel

In a **new terminal window**, start ngrok:

```bash
ngrok http https://localhost:7040
```

ngrok will display output like:

```
ngrok

Session Status                online
Account                       Your Name (Plan: Free)
Version                       3.x.x
Region                        United States (us)
Latency                       45ms
Web Interface                 http://127.0.0.1:4040
Forwarding                    https://abc123.ngrok-free.app -> https://localhost:7040

Connections                   ttl     opn     rt1     rt5     p50     p90
                              0       0       0.00    0.00    0.00    0.00
```

**Copy the HTTPS forwarding URL** (e.g., `https://abc123.ngrok-free.app`). You'll need this for the next step.

### ngrok Web Interface

ngrok provides a web interface at `http://127.0.0.1:4040` where you can:
- View all incoming HTTP requests
- Inspect request/response headers and bodies
- Replay requests for testing
- Monitor webhook deliveries in real-time

## Step 5: Configure GitHub App Webhook

1. Go to your GitHub App settings: https://github.com/settings/apps
2. Select your GitHub App
3. Scroll to the **"Webhook"** section
4. Click **"Edit"** or **"Add webhook"**

### Webhook Configuration

Configure the following settings:

- **Webhook URL**: `https://abc123.ngrok-free.app/api/webhooks/github`
  - Replace `abc123.ngrok-free.app` with your ngrok URL
  - The path `/api/webhooks/github` is the webhook endpoint in your application

- **Content type**: `application/json`

- **Secret**: 
  - Click **"Generate"** to create a new secret, or
  - Enter an existing secret if you're updating the webhook
  - **Important**: Save this secret - you'll need it for Step 2

- **Events**: Select **"Let me select individual events"**
  - ✅ Check `Pull requests` (for PR comment/block actions)
  - ✅ Check `Ping` (for testing webhook connectivity)

5. Click **"Update webhook"** or **"Add webhook"**

### Get Webhook Secret

After creating/updating the webhook, you can view the secret:

1. In the webhook settings, the secret is shown (masked)
2. If you need to regenerate it, click **"Regenerate"**
3. Copy the secret and add it to your local configuration (Step 2)

## Step 6: Test Webhook Connectivity

### Test 1: Ping Event (Automatic)

GitHub automatically sends a `ping` event when you create or update a webhook. Check your application logs:

```
[Information] ✅ Webhook received: Event=ping, Delivery=abc-123, Action=N/A
[Information] 🏓 Received ping event - webhook is configured correctly!
```

If you see these log messages, **webhook infrastructure is working!** ✅

### Test 2: Manual Ping

You can manually trigger a ping event:

1. Go to your GitHub App → Webhook settings
2. Click on the webhook
3. Scroll to **"Recent Deliveries"**
4. Find the latest `ping` event
5. Click **"Redeliver"** to test again

### Test 3: Verify in ngrok Web Interface

1. Open `http://127.0.0.1:4040` in your browser
2. You should see the webhook request in the request list
3. Click on it to inspect:
   - Request headers (including `X-GitHub-Event`, `X-Hub-Signature-256`)
   - Request body (webhook payload)
   - Response status and body

## Step 7: Test Pull Request Events

### Create a Test PR

1. Create a test repository (or use an existing one in your organization)
2. Create a new branch
3. Make some changes
4. Open a pull request

### Verify PR Event Processing

Check your application logs - you should see:

```
[Information] ✅ Webhook received: Event=pull_request, Delivery=xyz-789, Action=opened
[Information] 📝 Processing pull_request event (Action: opened)
[Information] Processing Pull Request webhook (DeliveryId: xyz-789)
[Information] Processing PR #1 in repository 12345 (SHA: abc123...)
```

### Test Different PR Actions

Test various PR events to ensure they're processed correctly:

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
   - Should see: `Event=pull_request, Action=closed` (may be ignored by handler)

## Step 8: Monitor Webhook Deliveries

### In Your Application Logs

Watch for webhook events in your console output. You should see:
- ✅ Successful signature validation
- ✅ Event type and delivery ID
- ✅ Payload logging (in Debug mode)
- ✅ Processing status for PR events

### In GitHub App Settings

1. Go to your GitHub App → Webhook settings
2. Click on the webhook
3. View **"Recent Deliveries"**
4. Check delivery status:
   - ✅ Green checkmark = Success (200 OK)
   - ❌ Red X = Failure (click to see response details)

### In ngrok Web Interface

The ngrok web interface (`http://127.0.0.1:4040`) shows:
- All incoming requests in real-time
- Request/response details
- Response times
- Status codes

## Troubleshooting

### Webhook Not Receiving Events

**Symptoms**: No webhook events in application logs or ngrok interface.

**Solutions**:
1. **Check ngrok is running**: Verify ngrok tunnel is active and forwarding to `localhost:7040`
2. **Check webhook URL**: Verify the URL in GitHub App settings matches your ngrok URL exactly
3. **Check HTTPS**: GitHub requires HTTPS for webhooks - ensure you're using the HTTPS ngrok URL
4. **Check firewall**: Ensure port 7040 is accessible locally
5. **Check ngrok status**: Look for errors in ngrok output

### Signature Validation Failing

**Symptoms**: `401 Unauthorized` responses, "Invalid signature" in logs.

**Solutions**:
1. **Verify secret matches**: The secret in your config must **exactly match** the one in GitHub App settings
   ```bash
   # Check your configured secret
   dotnet user-secrets list
   ```
2. **Check encoding**: Ensure the secret is stored correctly (no extra whitespace, newlines, or quotes)
3. **Verify signature format**: GitHub sends `sha256=...` format - ensure your verification handles this
4. **Check body reading**: Ensure request body is read correctly before signature verification
5. **Regenerate secret**: If in doubt, regenerate the webhook secret in GitHub and update your config

### 401 Unauthorized Responses

**Symptoms**: All webhook requests return 401, webhook deliveries show as failed.

**Solutions**:
1. **Check webhook secret**: Must be configured in user secrets or appsettings
2. **Check signature header**: Must be `X-Hub-Signature-256` (not `X-Hub-Signature`)
3. **Check body buffering**: Ensure `Request.EnableBuffering()` is called before reading body
4. **Check secret format**: Secret should be a plain string, not base64 encoded

### ngrok Tunnel Issues

**Symptoms**: ngrok not forwarding requests, connection errors.

**Solutions**:
1. **Check ngrok account**: Free tier has limitations - ensure you're logged in
2. **Check port**: Verify application is running on port 7040
3. **Restart ngrok**: Stop and restart ngrok tunnel
4. **Check ngrok logs**: Look for errors in ngrok output
5. **Try different region**: Use `ngrok http 7040 --region us` or `--region eu`

### Application Not Accessible via ngrok

**Symptoms**: ngrok shows "502 Bad Gateway" or connection errors.

**Solutions**:
1. **Verify application is running**: Check that `dotnet run` is active
2. **Check HTTPS**: Ensure application is running with `--launch-profile https`
3. **Check certificate**: Verify HTTPS certificate is trusted (run `dotnet dev-certs https --trust`)
4. **Check port**: Ensure application is listening on port 7040
5. **Check firewall**: Ensure local firewall allows connections on port 7040

## Development Workflow

### Typical Development Session

1. **Start application**:
   ```bash
   cd 10xGitHubPolicies.App
   dotnet run --launch-profile https
   ```

2. **Start ngrok** (in separate terminal):
   ```bash
   ngrok http 7040
   ```

3. **Update webhook URL** (if ngrok URL changed):
   - Copy new ngrok URL
   - Update GitHub App webhook settings
   - GitHub will send a ping event to verify

4. **Develop and test**:
   - Make code changes
   - Application auto-reloads (if using `dotnet watch`)
   - Test webhook events by creating PRs
   - Monitor logs and ngrok interface

5. **Stop when done**:
   - Stop ngrok (Ctrl+C)
   - Stop application (Ctrl+C)

### Using ngrok with Auto-Reload

If you're using `dotnet watch`, the application will auto-reload on code changes. ngrok will continue forwarding to the same port, so you don't need to restart it.

```bash
# Start with watch mode
dotnet watch run --launch-profile https

# ngrok continues to work during reloads
```

## Testing Webhook Signature Verification

### Test Invalid Signature

You can test signature validation by sending a request with an invalid signature:

```bash
curl -X POST https://abc123.ngrok-free.app/api/webhooks/github \
  -H "Content-Type: application/json" \
  -H "X-GitHub-Event: ping" \
  -H "X-Hub-Signature-256: sha256=invalid_signature" \
  -H "X-GitHub-Delivery: test-delivery-123" \
  -d '{"zen":"test"}'
```

You should get a `401 Unauthorized` response, and logs should show:
```
[Warning] Invalid webhook signature
```

### Test Missing Signature

```bash
curl -X POST https://abc123.ngrok-free.app/api/webhooks/github \
  -H "Content-Type: application/json" \
  -H "X-GitHub-Event: ping" \
  -H "X-GitHub-Delivery: test-delivery-123" \
  -d '{"zen":"test"}'
```

You should get a `401 Unauthorized` response.

## Advanced: Using ngrok with Custom Domain

If you have a paid ngrok account, you can use a custom domain for consistent webhook URLs:

```bash
ngrok http 7040 --domain=your-custom-domain.ngrok.io
```

This ensures your webhook URL doesn't change between ngrok sessions.

## Best Practices

1. **Never commit secrets**: Always use Secret Manager or environment variables
2. **Use HTTPS**: Always run application with HTTPS profile
3. **Monitor ngrok interface**: Use the web interface to debug webhook issues
4. **Test signature validation**: Verify that invalid signatures are rejected
5. **Check GitHub deliveries**: Monitor webhook delivery status in GitHub App settings
6. **Log webhook events**: Enable debug logging to see full webhook payloads
7. **Handle ngrok restarts**: If ngrok restarts, update webhook URL in GitHub App settings

## Security Considerations

⚠️ **Important Security Notes**:

1. **Webhook Secret**: Never expose the webhook secret. Always use Secret Manager or secure configuration.
2. **Signature Verification**: Always verify webhook signatures to prevent unauthorized requests.
3. **HTTPS Only**: GitHub requires HTTPS for webhooks - never use HTTP in production.
4. **ngrok in Production**: ngrok is for development only. Use proper hosting for production.
5. **Payload Logging**: Be careful logging webhook payloads - they may contain sensitive data.

## Success Criteria

✅ **Webhook development environment is working when:**

1. ✅ Ping events are received and logged successfully
2. ✅ PR events are received and logged successfully
3. ✅ Signature validation works (rejects invalid signatures)
4. ✅ HTTP responses are correct (200 OK for valid, 401 for invalid)
5. ✅ Different PR event types are recognized (opened, synchronize, reopened)
6. ✅ Webhook deliveries show as successful in GitHub App settings
7. ✅ ngrok web interface shows incoming requests
8. ✅ Application logs show webhook processing

## Additional Resources

- [GitHub Webhook Documentation](https://docs.github.com/en/webhooks)
- [GitHub Webhook Events](https://docs.github.com/en/webhooks/webhook-events-and-payloads)
- [ngrok Documentation](https://ngrok.com/docs)
- [.NET Secret Manager](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets)
- [Webhook Signature Verification](../.cursor/plans/webhook-signature-troubleshooting.md)

## Quick Reference

### Start Development Environment

```bash
# Terminal 1: Start application
cd 10xGitHubPolicies.App
dotnet run --launch-profile https

# Terminal 2: Start ngrok
ngrok http 7040
```

### Configure Webhook Secret

```bash
dotnet user-secrets set "GitHubApp:WebhookSecret" "your-secret-here"
```

### Check Webhook Secret

```bash
dotnet user-secrets list
```

### View ngrok Requests

Open browser: `http://127.0.0.1:4040`

### Test Webhook Endpoint

```bash
curl -X POST https://your-ngrok-url.ngrok-free.app/api/webhooks/github \
  -H "Content-Type: application/json" \
  -H "X-GitHub-Event: ping" \
  -H "X-Hub-Signature-256: sha256=..." \
  -H "X-GitHub-Delivery: test-123" \
  -d '{"zen":"test"}'
```

