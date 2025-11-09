<!-- 045c09b9-78d7-4bab-b027-7231ee060ad4 16a8ae75-7304-45a6-bfa4-a4c1d1415034 -->
# PR Comment and Block Actions Implementation Plan

## Overview

Add two new action types to the Action Service with **webhook-based real-time processing**:

1. **`comment-on-prs`** - Adds comments to pull requests when policy violations are detected (via webhooks)
2. **`block-prs`** - Blocks pull requests by creating failing status checks when policy violations are detected (via webhooks)

## Architecture Decision: Webhooks vs Scanning

**Why Webhooks?**
- **Real-time response**: PRs are commented/blocked immediately when opened or updated, not waiting for the next scan (up to 24 hours)
- **Better UX**: Developers get immediate feedback when opening PRs
- **Status check updates**: When violations are fixed, status checks can be updated immediately on PR sync
- **Centralized control**: No need to add GitHub Actions workflows to every repository
- **Aligns with existing architecture**: The app is already a GitHub App, which supports webhooks

**Hybrid Approach**:
- **Scanning** (existing): Continues to detect repository-level violations and trigger actions like `create-issue` and `archive-repo`
- **Webhooks** (new): Handle PR-level actions (`comment-on-prs`, `block-prs`) in real-time

## Implementation Tasks

### 0. Add Webhook Infrastructure (NEW - CRITICAL)

**Location**: `10xGitHubPolicies.App/Controllers/` and `10xGitHubPolicies.App/Services/Webhooks/`

**Testing First**: Before implementing full webhook processing, test the infrastructure using the minimal webhook controller. See `.cursor/plans/webhook-testing-guide.md` for detailed testing instructions.

**New Components Needed**:

1. **Webhook Controller** (`WebhookController.cs`): ✅ **CREATED** (minimal version for testing)
   - Endpoint: `POST /api/webhooks/github`
   - Validates webhook signature using GitHub App webhook secret
   - Logs incoming webhook events
   - Returns 200 OK immediately (webhook processing will be async in full implementation)

2. **Webhook Service** (`IWebhookService.cs` and `WebhookService.cs`):
   - Processes `pull_request` events (opened, synchronize, reopened)
   - Processes `check_run` events (optional, for status check updates)
   - Enqueues background jobs for policy evaluation and PR actions

3. **PR Webhook Handler** (`IPullRequestWebhookHandler.cs` and `PullRequestWebhookHandler.cs`):
   - Evaluates repository policies for the PR's repository
   - Determines if PR should be commented/blocked based on violations
   - Creates comments or status checks as needed
   - Updates existing status checks when violations are fixed

**Implementation Details**:

- Use `Octokit.Webhooks` NuGet package or implement manual webhook signature verification
- Webhook secret should be stored in configuration (GitHub App webhook secret)
- Use Hangfire to enqueue webhook processing jobs (avoid blocking webhook response)
- Handle webhook delivery retries gracefully (idempotent operations)
- Log all webhook events for debugging

**GitHub App Configuration**:
- Enable webhook delivery in GitHub App settings
- Subscribe to `pull_request` events (opened, synchronize, reopened, closed)
- Set webhook URL to: `https://your-domain.com/api/webhooks/github`
- Configure webhook secret in application configuration

### 1. Add PR Methods to GitHubService

**Location**: `10xGitHubPolicies.App/Services/GitHub/IGitHubService.cs` and `GitHubService.cs`

**New Methods Needed**:

- `Task<IReadOnlyList<PullRequest>> GetOpenPullRequestsAsync(long repositoryId)` - Retrieve all open PRs for a repository
- `Task<IssueComment> CreatePullRequestCommentAsync(long repositoryId, int pullRequestNumber, string comment)` - Add comment to a specific PR
- `Task<CheckRun> CreateStatusCheckAsync(long repositoryId, string headSha, string name, string status, string conclusion, string? detailsUrl = null)` - Create a status check (for blocking PRs)

**Implementation Details**:

- Use `client.PullRequest.GetAllForRepository()` with `PullRequestRequest` filtered by `ItemStateFilter.Open`
- Use `client.Issue.Comment.Create()` for PR comments (PRs are issues in GitHub API)
- Use `client.Check.Run.Create()` for status checks with `conclusion: "failure"` to block PRs
- Handle `NotFoundException` and other API exceptions gracefully
- Follow existing patterns for authentication and error handling

### 2. Add PR Comment Action to ActionService

**Location**: `10xGitHubPolicies.App/Services/Action/ActionService.cs`

**New Methods**:
- `CommentOnPullRequestAsync(long repositoryId, int pullRequestNumber, PolicyConfig policyConfig, List<PolicyViolation> violations)` - Comment on a specific PR (called from webhook handler)
- `CommentOnPullRequestsForViolationAsync(PolicyViolation violation, PolicyConfig policyConfig)` - Comment on all open PRs (called from scan-based actions, for backward compatibility)

**Features**:

- Add a comment to the PR with policy violation details
- Use configurable comment message from `PolicyConfig` or default format
- Implement duplicate prevention: Check if bot already commented (by checking existing comments for the same policy)
- Log each comment action separately
- Handle errors gracefully
- Support both action name formats: `"comment-on-prs"` and `"comment_on_prs"`

**Comment Content**:

- Default: Include policy name, violation details, and link to repository
- Configurable via new `PrCommentDetails` model in `PolicyConfig` (similar to `IssueDetails`)
- When multiple violations exist, combine them in a single comment

### 3. Add Block PRs Action to ActionService

**Location**: `10xGitHubPolicies.App/Services/Action/ActionService.cs`

**New Methods**:
- `UpdatePullRequestStatusCheckAsync(long repositoryId, string headSha, List<PolicyViolation> violations, PolicyConfig policyConfig)` - Create/update status check for a specific PR (called from webhook handler)
- `BlockPullRequestsForViolationAsync(PolicyViolation violation, PolicyConfig policyConfig)` - Block all open PRs (called from scan-based actions, for backward compatibility)

**Features**:

- Create or update a status check on the PR's head SHA
- Status check name: Configurable or default to "Policy Compliance Check"
- Status check conclusion: `"failure"` if violations exist, `"success"` if no violations
- Status check details: Include policy violation information (or success message if compliant)
- Implement duplicate prevention: Update existing status check if it exists (same name)
- Log each status check creation/update separately
- Handle errors gracefully
- Support both action name formats: `"block-prs"` and `"block_prs"`

**Status Check Details**:

- Name: Configurable via `PolicyConfig` or default format
- Conclusion: `"failure"` when violations exist, `"success"` when compliant
- Output: Include policy name and violation details (or success message)
- External URL: Optional link to repository or dashboard
- **Important**: Status checks should be updated on every PR sync to reflect current compliance status

### 4. Update PolicyConfig Model

**Location**: `10xGitHubPolicies.App/Services/Configuration/Models/PolicyConfig.cs`

**New Properties**:

- `PrCommentDetails? PrCommentDetails { get; set; }` - Optional configuration for PR comments
- `BlockPrsDetails? BlockPrsDetails { get; set; }` - Optional configuration for blocking PRs

**New Models** (create in same directory):

- `PrCommentDetails.cs` - Contains `Message` property for custom comment text
- `BlockPrsDetails.cs` - Contains `StatusCheckName` property for custom status check name

### 5. Update ActionService.ProcessActionsForScanAsync

**Location**: `10xGitHubPolicies.App/Services/Action/ActionService.cs`

**Add new action handlers** (for backward compatibility with scan-based actions):

```csharp
else if (policyConfig.Action == "comment-on-prs" || policyConfig.Action == "comment_on_prs")
{
    await CommentOnPullRequestsForViolationAsync(violation, policyConfig);
}
else if (policyConfig.Action == "block-prs" || policyConfig.Action == "block_prs")
{
    await BlockPullRequestsForViolationAsync(violation, policyConfig);
}
```

**Note**: These scan-based handlers are for backward compatibility. The primary mechanism for PR actions is webhook-based (see Task 0).

## Testing Implementation

### Level 0: Webhook Infrastructure Tests (NEW)

**Location**: `10xGitHubPolicies.Tests/Controllers/` and `10xGitHubPolicies.Tests/Services/Webhooks/`

**New Test Files**:

- `WebhookControllerTests.cs` - Test webhook endpoint
  - `Post_WhenValidSignature_Returns200`
  - `Post_WhenInvalidSignature_Returns401`
  - `Post_WhenPullRequestOpened_EnqueuesProcessingJob`
  - `Post_WhenPullRequestSynchronized_EnqueuesProcessingJob`
  - `Post_WhenUnsupportedEvent_Returns200` (ignore unsupported events)

- `PullRequestWebhookHandlerTests.cs` - Test webhook handler logic
  - `HandlePullRequestOpened_WhenViolationsExist_CommentsAndBlocksPR`
  - `HandlePullRequestOpened_WhenNoViolations_DoesNotCommentOrBlock`
  - `HandlePullRequestSynchronized_WhenViolationsFixed_UpdatesStatusCheckToSuccess`
  - `HandlePullRequestSynchronized_WhenNewViolations_UpdatesStatusCheckToFailure`
  - `HandlePullRequestOpened_WithMultiplePolicies_ProcessesAllActions`

**Test Infrastructure**:
- Mock webhook signature verification
- Use NSubstitute to mock GitHubService and PolicyEvaluationService
- Test webhook payload parsing

### Level 1: Unit Tests

**Location**: `10xGitHubPolicies.Tests/Services/Action/ActionServiceTests.cs`

**New Tests for Comment-on-PRs**:

- `CommentOnPullRequestAsync_WhenViolationsExist_CommentsOnPR` - Tests successful commenting (webhook path)
- `CommentOnPullRequestAsync_WhenNoViolations_DoesNotComment` - Tests when no violations exist
- `CommentOnPullRequestAsync_WhenDuplicateComment_Skips` - Tests duplicate prevention
- `CommentOnPullRequestAsync_WhenError_LogsAndContinues` - Tests error handling
- `CommentOnPullRequestAsync_UsesCustomMessage` - Tests configurable message
- `CommentOnPullRequestsForViolationAsync_WhenCommentOnPrsAction_CommentsOnAllOpenPRs` - Tests scan-based path (backward compatibility)
- `CommentOnPullRequestsForViolationAsync_WhenNoOpenPRs_LogsInfo` - Tests when no PRs exist

**New Tests for Block-PRs**:

- `UpdatePullRequestStatusCheckAsync_WhenViolationsExist_CreatesFailingStatusCheck` - Tests successful blocking (webhook path)
- `UpdatePullRequestStatusCheckAsync_WhenNoViolations_CreatesSuccessStatusCheck` - Tests when compliant
- `UpdatePullRequestStatusCheckAsync_WhenStatusCheckExists_UpdatesExisting` - Tests status check updates
- `UpdatePullRequestStatusCheckAsync_WhenError_LogsAndContinues` - Tests error handling
- `UpdatePullRequestStatusCheckAsync_UsesCustomStatusCheckName` - Tests configurable name
- `BlockPullRequestsForViolationAsync_WhenBlockPrsAction_CreatesFailingStatusChecks` - Tests scan-based path (backward compatibility)
- `BlockPullRequestsForViolationAsync_WhenNoOpenPRs_LogsInfo` - Tests when no PRs exist

**Test Data Requirements**:

- Mock `IGitHubService` to return list of PRs
- Mock PR comment creation and status check creation
- Verify action log entries are created correctly for each PR

### Level 2: Integration Tests

**Location**: `10xGitHubPolicies.Tests.Integration/`

**New Test Files**:

- `GitHub/PullRequestOperationsTests.cs` - Test GitHubService PR methods with WireMock
                                                                - `GetOpenPullRequestsAsync_WhenCalled_ReturnsOpenPRs`
                                                                - `CreatePullRequestCommentAsync_WhenCalled_CreatesComment`
                                                                - `CreateStatusCheckAsync_WhenCalled_CreatesFailingCheck`
                                                                - `CreateStatusCheckAsync_WhenRepositoryNotFound_ThrowsNotFoundException`
  - `UpdateStatusCheckAsync_WhenCalled_UpdatesExistingCheck`

- `Webhooks/WebhookIntegrationTests.cs` - Test webhook processing end-to-end with WireMock
  - `ProcessPullRequestOpenedWebhook_WhenViolationsExist_CommentsAndBlocksPR`
  - `ProcessPullRequestSynchronizedWebhook_WhenViolationsFixed_UpdatesStatusCheck`
  - `ProcessPullRequestWebhook_WithInvalidSignature_RejectsRequest`
  - Test webhook signature verification with real payloads

- `Action/PullRequestActionTests.cs` - Test ActionService PR actions with database
  - `CommentOnPrsAction_WhenViolationExists_CommentsOnPR` (webhook path)
  - `BlockPrsAction_WhenViolationExists_BlocksPR` (webhook path)
                                                                - `CommentOnPrsAction_WithMultipleViolations_ProcessesAll`
                                                                - Test action logging persistence for PR actions

**Test Infrastructure**:

- Use WireMock.Net to mock GitHub API responses for PR endpoints
- Use Testcontainers for database isolation
- Use Respawn for database cleanup between tests

### Level 3: Contract Tests

**Location**: `10xGitHubPolicies.Tests.Contracts/GitHub/`

**New Test Files**:

- `PullRequestResponseContractTests.cs` - Validate PR API response schemas
                                                                - `GetOpenPullRequestsAsync_ResponseMatchesSchema`
                                                                - `CreatePullRequestCommentAsync_ResponseMatchesSchema`
                                                                - `CreateStatusCheckAsync_ResponseMatchesSchema`
                                                                - Snapshot tests for PR and status check responses

**Schemas Needed**:

- Pull request response schema
- PR comment response schema
- Status check response schema

### Level 4: Component Tests

**Location**: `10xGitHubPolicies.Tests/Components/` (if applicable)

**Tasks**:

- Verify dashboard displays PR action status correctly (if applicable)
- Test UI components that show PR comment/block action logs (if applicable)

**Note**: PR actions are background processes, so UI components may not need direct testing unless dashboard shows action logs

### Level 5: E2E Tests

**do not add new E2E tests - it will be covered as separate task**

## Documentation Updates

### 1. Action Service Documentation

**Location**: `docs/services/action-service.md`

**Updates Needed**:

- Document `comment-on-prs` action behavior and configuration
- Document `block-prs` action behavior and configuration
- Add example configurations for both actions
- Document duplicate prevention logic for both actions
- Document error handling scenarios

### 2. GitHub Integration Documentation

**Location**: `docs/services/github-integration.md`

**Updates Needed**:

- Document new PR-related methods in `IGitHubService`
- Add examples of PR comment and status check usage
- Document GitHub API endpoints used (PRs, comments, status checks)

### 3. Configuration Documentation

**Location**: `docs/services/configuration-service.md` (if exists) or create new section

**Updates Needed**:

- Document `PrCommentDetails` configuration format
- Document `BlockPrsDetails` configuration format
- Add YAML examples for both action types

### 3. PRD

**Location**: `docs/prd.md` 

**Add new actions to PRD document**

### 5. CHANGELOG

**Location**: `CHANGELOG.md`

**Updates Needed**:

- Add entry for new `comment-on-prs` action
- Add entry for new `block-prs` action
- Document new GitHubService methods
- Document configuration model updates

## Configuration Examples

### Comment on PRs Action

```yaml
policies:
  - name: 'Check for AGENTS.md'
    type: 'has_agents_md'
    action: 'comment-on-prs'
    pr_comment_details:
      message: '⚠️ This repository violates the AGENTS.md policy. Please add the required file before merging.'
```

### Block PRs Action

```yaml
policies:
  - name: 'Verify Workflow Permissions'
    type: 'correct_workflow_permissions'
    action: 'block-prs'
    block_prs_details:
      status_check_name: 'Policy Compliance: Workflow Permissions'
```

## Implementation Order

1. **Add Webhook Infrastructure** (CRITICAL FIRST STEP)
   - Create `WebhookController` with signature verification
   - Create `IWebhookService` and `WebhookService`
   - Create `IPullRequestWebhookHandler` and `PullRequestWebhookHandler`
   - Configure webhook endpoint in `Program.cs`
   - Add webhook secret to configuration

2. **Add GitHubService PR Methods** - Implement `GetOpenPullRequestsAsync`, `CreatePullRequestCommentAsync`, `CreateStatusCheckAsync`, `UpdateStatusCheckAsync`

3. **Add Configuration Models** - Create `PrCommentDetails` and `BlockPrsDetails` models, update `PolicyConfig`

4. **Implement PR Actions in ActionService** - Add `CommentOnPullRequestAsync` and `UpdatePullRequestStatusCheckAsync` methods (webhook path)

5. **Integrate Webhook Handler with ActionService** - Connect webhook handler to ActionService methods

6. **Add Scan-Based Handlers** (backward compatibility) - Add `CommentOnPullRequestsForViolationAsync` and `BlockPullRequestsForViolationAsync` for scan-based actions

7. **Update ProcessActionsForScanAsync** - Add action handlers for new actions (scan-based path)

8. **Unit Tests** - Add unit tests for webhook infrastructure and PR actions

9. **Integration Tests** - Add integration tests for webhooks, GitHubService, and ActionService

10. **Contract Tests** - Add contract tests for PR API responses and webhook payloads

11. **Documentation** - Update all relevant documentation

12. **Code Review** - Verify all tests pass and implementation follows patterns

## Success Criteria

- **Webhook Infrastructure**:
  - Webhook endpoint accepts and validates GitHub webhook payloads
  - Webhook signature verification works correctly
  - PR events (opened, synchronize, reopened) trigger policy evaluation
  - Webhook processing is async (doesn't block webhook response)

- **PR Actions**:
  - `comment-on-prs` action comments on PRs immediately when opened/updated (via webhooks)
  - `block-prs` action creates/updates status checks immediately when PRs are opened/updated (via webhooks)
  - Status checks are updated to "success" when violations are fixed
- Both actions implement duplicate prevention
- Both actions handle errors gracefully without blocking other actions

- **Backward Compatibility**:
  - Scan-based actions still work (comment/block all open PRs when violations detected during scan)
  - Existing workflows continue to function

- **Testing**:
- All unit tests pass (existing + new)
- All integration tests pass (existing + new)
- All contract tests pass (existing + new)
  - Webhook tests pass

- **Documentation & Code Quality**:
- Action logs are correctly persisted for all scenarios
- Documentation is updated and accurate
- Code follows existing patterns and conventions
- Both action name formats supported (`kebab-case` and `snake_case`)

## Considerations

### Webhook Infrastructure
- **Webhook Secret**: Store GitHub App webhook secret securely in configuration (not in code)
- **Signature Verification**: Always verify webhook signatures to prevent unauthorized requests
- **Async Processing**: Process webhooks asynchronously using Hangfire to avoid blocking webhook responses
- **Idempotency**: Handle webhook delivery retries gracefully (GitHub may retry failed deliveries)
- **Event Filtering**: Only process relevant events (`pull_request.opened`, `pull_request.synchronize`, `pull_request.reopened`)

### GitHub App Permissions
- **Status Check Permissions**: Creating status checks requires `checks: write` permission
- **Comment Permissions**: PR comments require `pull_requests: write` permission
- **Webhook Events**: Subscribe to `pull_request` events in GitHub App settings

### Performance & Rate Limiting
- **Rate Limiting**: PR actions create API calls per PR. Consider rate limit handling and potential batching if needed
- **Webhook Processing**: Use Hangfire to queue webhook processing jobs (avoid blocking webhook endpoint)
- **Status Check Updates**: Update status checks efficiently (check if update is needed before making API call)

### Duplicate Prevention
- **Comments**: Check if bot user already commented on the PR for the same policy violation
- **Status Checks**: Update existing status check if it exists (same name), don't create duplicates

### Status Check Behavior
- **Status Check Visibility**: Status checks appear in PR checks section. Ensure the name clearly indicates policy violation
- **Status Updates**: Update status checks on every PR sync to reflect current compliance status
- **Success State**: When violations are fixed, update status check to "success" to unblock PR

### Configuration
- **Webhook URL**: Configure webhook URL in GitHub App settings: `https://your-domain.com/api/webhooks/github`
- **Webhook Secret**: Configure webhook secret in application configuration (e.g., `GitHubApp:WebhookSecret`)

### To-dos

- [ ] Create WebhookController with signature verification endpoint
- [ ] Create IWebhookService and WebhookService for webhook processing
- [ ] Create IPullRequestWebhookHandler and PullRequestWebhookHandler
- [ ] Configure webhook endpoint in Program.cs
- [ ] Add webhook secret to configuration (GitHubApp:WebhookSecret)
- [ ] Add unit tests for webhook controller and handlers
- [ ] Add integration tests for webhook processing with WireMock
- [ ] Add GetOpenPullRequestsAsync, CreatePullRequestCommentAsync, CreateStatusCheckAsync, UpdateStatusCheckAsync methods to IGitHubService and GitHubService
- [ ] Add integration tests for GitHubService PR methods with WireMock
- [ ] Create PrCommentDetails and BlockPrsDetails models
- [ ] Update PolicyConfig to include new properties
- [ ] Implement CommentOnPullRequestAsync method (webhook path) with duplicate prevention and error handling
- [ ] Implement UpdatePullRequestStatusCheckAsync method (webhook path) with duplicate prevention and error handling
- [ ] Implement CommentOnPullRequestsForViolationAsync method (scan path, backward compatibility)
- [ ] Implement BlockPullRequestsForViolationAsync method (scan path, backward compatibility)
- [ ] Update ProcessActionsForScanAsync to handle comment-on-prs and block-prs actions
- [ ] Add unit tests for webhook infrastructure: controller, service, handler
- [ ] Add unit tests for comment-on-prs action: successful commenting, no violations scenario, duplicate prevention, error handling, custom message
- [ ] Add unit tests for block-prs action: successful blocking, no violations scenario, status check updates, error handling, custom status check name
- [ ] Add integration tests for webhook processing end-to-end
- [ ] Add integration tests for ActionService PR actions with database
- [ ] Add contract tests for PR API responses: PullRequest, PR comment, and StatusCheck response schemas with snapshot testing
- [ ] Add contract tests for webhook payload schemas
- [ ] Update action-service.md with webhook-based PR actions documentation
- [ ] Update github-integration.md with webhook infrastructure and PR methods
- [ ] Create webhooks.md documentation (new file)
- [ ] Update configuration docs with PrCommentDetails and BlockPrsDetails
- [ ] Update CHANGELOG.md with new PR actions and webhook infrastructure
- [ ] Update PRD with new PR action requirements