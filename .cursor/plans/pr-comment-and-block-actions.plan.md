<!-- 045c09b9-78d7-4bab-b027-7231ee060ad4 16a8ae75-7304-45a6-bfa4-a4c1d1415034 -->
# PR Comment and Block Actions Implementation Plan

## Overview

Add two new action types to the Action Service:

1. **`comment-on-prs`** - Adds comments to all open pull requests in a repository when policy violations are detected
2. **`block-prs`** - Blocks all pull requests in a repository by creating failing status checks when policy violations are detected

## Implementation Tasks

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

**New Method**: `CommentOnPullRequestsForViolationAsync(PolicyViolation violation, PolicyConfig policyConfig)`

**Features**:

- Retrieve all open PRs for the violating repository
- Add a comment to each PR with policy violation details
- Use configurable comment message from `PolicyConfig` or default format
- Implement duplicate prevention: Check if bot already commented (by checking existing comments)
- Log each comment action separately (one log entry per PR)
- Handle errors gracefully - continue processing other PRs if one fails
- Support both action name formats: `"comment-on-prs"` and `"comment_on_prs"`

**Comment Content**:

- Default: Include policy name, violation details, and link to repository
- Configurable via new `PrCommentDetails` model in `PolicyConfig` (similar to `IssueDetails`)

### 3. Add Block PRs Action to ActionService

**Location**: `10xGitHubPolicies.App/Services/Action/ActionService.cs`

**New Method**: `BlockPullRequestsForViolationAsync(PolicyViolation violation, PolicyConfig policyConfig)`

**Features**:

- Retrieve all open PRs for the violating repository
- For each PR, create a failing status check on the PR's head SHA
- Status check name: Configurable or default to "Policy Compliance Check"
- Status check conclusion: `"failure"` to block merging
- Status check details: Include policy violation information
- Implement duplicate prevention: Check if status check already exists for this violation
- Log each status check creation separately
- Handle errors gracefully - continue processing other PRs if one fails
- Support both action name formats: `"block-prs"` and `"block_prs"`

**Status Check Details**:

- Name: Configurable via `PolicyConfig` or default format
- Conclusion: Always `"failure"` to block merging
- Output: Include policy name and violation details
- External URL: Optional link to repository or dashboard

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

**Add new action handlers**:

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

## Testing Implementation

### Level 1: Unit Tests

**Location**: `10xGitHubPolicies.Tests/Services/Action/ActionServiceTests.cs`

**New Tests for Comment-on-PRs**:

- `ProcessActionsForScanAsync_WhenCommentOnPrsAction_CommentsOnAllOpenPRs` - Tests successful commenting
- `ProcessActionsForScanAsync_WhenCommentOnPrsAction_NoOpenPRs_LogsInfo` - Tests when no PRs exist
- `ProcessActionsForScanAsync_WhenCommentOnPrsAction_DuplicateComment_Skips` - Tests duplicate prevention
- `ProcessActionsForScanAsync_WhenCommentOnPrsAction_PartialFailure_ContinuesProcessing` - Tests error handling
- `ProcessActionsForScanAsync_WhenCommentOnPrsAction_UsesCustomMessage` - Tests configurable message

**New Tests for Block-PRs**:

- `ProcessActionsForScanAsync_WhenBlockPrsAction_CreatesFailingStatusChecks` - Tests successful blocking
- `ProcessActionsForScanAsync_WhenBlockPrsAction_NoOpenPRs_LogsInfo` - Tests when no PRs exist
- `ProcessActionsForScanAsync_WhenBlockPrsAction_DuplicateStatusCheck_Skips` - Tests duplicate prevention
- `ProcessActionsForScanAsync_WhenBlockPrsAction_PartialFailure_ContinuesProcessing` - Tests error handling
- `ProcessActionsForScanAsync_WhenBlockPrsAction_UsesCustomStatusCheckName` - Tests configurable name

**Test Data Requirements**:

- Mock `IGitHubService` to return list of PRs
- Mock PR comment creation and status check creation
- Verify action log entries are created correctly for each PR

### Level 2: Integration Tests

**Location**: `10xGitHubPolicies.Tests.Integration/GitHub/`

**New Test Files**:

- `PullRequestOperationsTests.cs` - Test GitHubService PR methods with WireMock
                                                                - `GetOpenPullRequestsAsync_WhenCalled_ReturnsOpenPRs`
                                                                - `CreatePullRequestCommentAsync_WhenCalled_CreatesComment`
                                                                - `CreateStatusCheckAsync_WhenCalled_CreatesFailingCheck`
                                                                - `CreateStatusCheckAsync_WhenRepositoryNotFound_ThrowsNotFoundException`

- `Action/PullRequestActionTests.cs` - Test ActionService PR actions with database
                                                                - `CommentOnPrsAction_WhenViolationExists_CommentsOnAllPRs`
                                                                - `BlockPrsAction_WhenViolationExists_BlocksAllPRs`
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

1. **Add GitHubService PR Methods** - Implement `GetOpenPullRequestsAsync`, `CreatePullRequestCommentAsync`, `CreateStatusCheckAsync`
2. **Add Configuration Models** - Create `PrCommentDetails` and `BlockPrsDetails` models, update `PolicyConfig`
3. **Implement Comment-on-PRs Action** - Add `CommentOnPullRequestsForViolationAsync` method
4. **Implement Block-PRs Action** - Add `BlockPullRequestsForViolationAsync` method
5. **Update ProcessActionsForScanAsync** - Add action handlers for new actions
6. **Unit Tests** - Add unit tests for both actions
7. **Integration Tests** - Add integration tests for GitHubService and ActionService
8. **Contract Tests** - Add contract tests for PR API responses
9. **Documentation** - Update all relevant documentation
10. **Code Review** - Verify all tests pass and implementation follows patterns

## Success Criteria

- `comment-on-prs` action successfully comments on all open PRs in violating repositories
- `block-prs` action successfully creates failing status checks on all open PRs
- Both actions implement duplicate prevention
- Both actions handle errors gracefully without blocking other actions
- All unit tests pass (existing + new)
- All integration tests pass (existing + new)
- All contract tests pass (existing + new)
- Action logs are correctly persisted for all scenarios
- Documentation is updated and accurate
- Code follows existing patterns and conventions
- Both action name formats supported (`kebab-case` and `snake_case`)

## Considerations

- **Rate Limiting**: PR actions may create many API calls (one per PR). Consider rate limit handling and potential batching if needed.
- **Status Check Permissions**: Creating status checks requires appropriate GitHub App permissions. Ensure the app has `checks: write` permission.
- **Comment Permissions**: PR comments require `pull_requests: write` permission.
- **Duplicate Prevention**: For comments, check if bot user already commented. For status checks, check if status check with same name already exists.
- **Performance**: Processing many PRs may take time. Consider async processing and logging progress.
- **Status Check Visibility**: Status checks appear in PR checks section. Ensure the name clearly indicates policy violation.

### To-dos

- [ ] Add GetOpenPullRequestsAsync, CreatePullRequestCommentAsync, and CreateStatusCheckAsync methods to IGitHubService and GitHubService
- [ ] Create PrCommentDetails and BlockPrsDetails models, update PolicyConfig to include new properties
- [ ] Implement CommentOnPullRequestsForViolationAsync method in ActionService with duplicate prevention and error handling
- [ ] Implement BlockPullRequestsForViolationAsync method in ActionService with duplicate prevention and error handling
- [ ] Update ProcessActionsForScanAsync to handle comment-on-prs and block-prs actions
- [ ] Add unit tests for comment-on-prs action: successful commenting, no PRs scenario, duplicate prevention, partial failures, custom message
- [ ] Add unit tests for block-prs action: successful blocking, no PRs scenario, duplicate prevention, partial failures, custom status check name
- [ ] Add integration tests for GitHubService PR methods: GetOpenPullRequestsAsync, CreatePullRequestCommentAsync, CreateStatusCheckAsync with WireMock
- [ ] Add integration tests for ActionService PR actions with database: CommentOnPrsAction and BlockPrsAction workflows
- [ ] Add contract tests for PR API responses: PullRequest, PR comment, and StatusCheck response schemas with snapshot testing
- [ ] Update action-service.md, github-integration.md, configuration docs, and CHANGELOG.md with new PR actions documentation