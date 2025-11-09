# Webhook Testing Quick Start

## Prerequisites

1. ✅ Minimal webhook controller created (`WebhookController.cs`)
2. ✅ `GitHubAppOptions` updated with `WebhookSecret` property
3. ✅ Application compiles and runs

## Quick Test Steps

### 1. Configure Webhook Secret

```bash
cd 10xGitHubPolicies.App
dotnet user-secrets set "GitHubApp:WebhookSecret" "YOUR_WEBHOOK_SECRET"
```

### 2. Start Application

```bash
dotnet run --launch-profile https
```

Application runs on: `https://localhost:7040`

### 3. Start ngrok Tunnel

In a new terminal:
```bash
ngrok http 7040
```

Copy the HTTPS URL (e.g., `https://abc123.ngrok.io`)

### 4. Configure GitHub App Webhook

1. Go to: https://github.com/settings/apps
2. Select your app → Webhook section
3. Configure:
   - **Webhook URL**: `https://abc123.ngrok.io/api/webhooks/github`
   - **Content type**: `application/json`
   - **Secret**: Generate and save (use for step 1)
   - **Events**: Select `Pull requests` and `Ping`
4. Save webhook

### 5. Verify Ping Event

GitHub automatically sends a `ping` event. Check your application logs:

```
✅ Webhook received: Event=ping, Delivery=abc-123, Action=N/A
🏓 Received ping event - webhook is configured correctly!
```

### 6. Test PR Event

1. Create a test PR in any repository
2. Check logs for:
```
✅ Webhook received: Event=pull_request, Delivery=xyz-789, Action=opened
📝 Processing pull_request event (Action: opened)
```

## Success Indicators

✅ **Webhook infrastructure is working when:**
- Ping events are received and logged
- PR events are received and logged
- No 401 Unauthorized errors in logs
- Webhook deliveries show success in GitHub App settings

## Troubleshooting

| Issue | Solution |
|-------|----------|
| 401 Unauthorized | Check webhook secret matches in config and GitHub App settings |
| No events received | Verify ngrok is running and URL matches GitHub App webhook URL |
| Signature validation fails | Ensure secret is configured correctly (no extra whitespace) |

## Next Steps

Once webhook infrastructure is verified:
1. Implement `IWebhookService` and `WebhookService`
2. Implement `IPullRequestWebhookHandler`
3. Add policy evaluation logic
4. Implement PR comment and status check actions

See `.cursor/plans/webhook-testing-guide.md` for detailed testing instructions.

