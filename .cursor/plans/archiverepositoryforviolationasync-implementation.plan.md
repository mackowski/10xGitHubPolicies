<!-- 61072b0a-e620-4895-852d-660f920b6e4f 4ba05308-aec2-4582-9de1-f1724478345f -->
# ArchiveRepositoryForViolationAsync Implementation Plan

## Current Status

The `ArchiveRepositoryForViolationAsync` method already exists as a private method in `ActionService.cs` (lines 133-150). It:

- Calls `IGitHubService.ArchiveRepositoryAsync()` to archive repositories
- Logs successful and failed archive actions to the database
- Handles exceptions gracefully without blocking other actions

## Implementation Tasks

### 1. Enhance ArchiveRepositoryForViolationAsync Method

**Location**: `10xGitHubPolicies.App/Services/Action/ActionService.cs`

**Enhancements Needed**:

- Add duplicate prevention logic: Check if repository is already archived before attempting to archive
- Use `IGitHubService.GetRepositorySettingsAsync()` to check current archived status
- If already archived, log action as "Skipped" with appropriate message (similar to issue duplicate prevention)
- This prevents unnecessary API calls and improves efficiency
- Improve error handling for specific GitHub API exceptions (e.g., insufficient permissions, repository not found)
- Add structured logging with repository name and policy name for better traceability

**Dependencies**:

- `IGitHubService.GetRepositorySettingsAsync()` (already exists)
- Repository entity must have `Archived` property accessible

### 2. Update ActionService Interface (if needed)

**Location**: `10xGitHubPolicies.App/Services/Action/IActionService.cs`

**Check**: Verify interface doesn't need updates (method is private, so likely no changes needed)

### 3. Database Schema Verification

**Location**: `10xGitHubPolicies.App/Data/Entities/`

**Tasks**:

- Verify `ActionLog` entity supports all required fields for archive-repo action logging
- Ensure `Repository` entity can track archived status if needed for optimization
- Verify no schema changes required

## Testing Implementation

### Level 1: Unit Tests

**Location**: `10xGitHubPolicies.Tests/Services/Action/ActionServiceTests.cs`

**Existing Tests**:

- `ProcessActionsForScanAsync_WhenArchiveRepoAction_ArchivesRepository` - Tests successful archiving

**Additional Tests Needed**:

- Test archive action when repository is already archived (should skip)
- Test archive action failure handling (GitHub API exception)
- Test archive action with invalid repository ID
- Test archive action logging with correct details
- Test archive action doesn't block other actions on failure
- Test archive action with both action name formats ("archive-repo" and "archive_repo")

**Test Data Requirements**:

- Mock `IGitHubService` to return archived repository status
- Mock exceptions for various failure scenarios
- Verify action log entries are created correctly

### Level 2: Integration Tests

**Location**: `10xGitHubPolicies.Tests.Integration/GitHub/RepositoryOperationsTests.cs`

**Existing Tests**:

- `ArchiveRepositoryAsync_WhenCalled_SetsArchivedToTrue` - Tests GitHubService.ArchiveRepositoryAsync
- `ArchiveRepositoryAsync_WhenRepositoryNotFound_ThrowsNotFoundException` - Tests error handling

**Additional Tests Needed**:

- Test archive action through ActionService with real database and mocked GitHub API
- Test action logging persistence in database
- Test multiple violations with archive action (verify isolation)
- Test archive action with rate limit scenarios

**Test Infrastructure**:

- Use WireMock.Net to mock GitHub API responses
- Use Testcontainers for database isolation
- Use Respawn for database cleanup between tests

### Level 3: Contract Tests

**Location**: `10xGitHubPolicies.Tests.Contracts/GitHub/RepositoryResponseContractTests.cs`

**Existing Tests**:

- `ArchiveRepositoryAsync_ResponseMatchesSchema` - Validates response structure

**Additional Tests Needed**:

- Verify archived repository response schema matches expected structure
- Snapshot test for archived repository response to detect API changes

### Level 4: Component Tests

**Location**: `10xGitHubPolicies.Tests/Components/` (if applicable)

**Tasks**:

- Verify dashboard displays archived repositories correctly (if applicable)
- Test UI components that show archive action status (if applicable)

**Note**: Archive action is background process, so UI components may not need direct testing

### Level 5: E2E Tests

Automated E2E test fror this task will be skipped.
Ask user to test this!

## Documentation Updates

### 1. Action Service Documentation

**Location**: `docs/action-service.md`

**Updates Needed**:

- Document archive-repo action behavior
- Document duplicate prevention logic (once implemented)
- Add example configuration for archive-repo action
- Document error handling scenarios

### 2. GitHub Integration Documentation

**Location**: `docs/github-integration.md`

**Updates Needed**:

- Verify ArchiveRepositoryAsync documentation is complete
- Add examples of archive action usage

### 3. CHANGELOG

**Location**: `CHANGELOG.md`

**Updates Needed**:

- Add entry for archive action enhancements (duplicate prevention)
- Document E2E test addition

## Implementation Order

1. **Enhance ArchiveRepositoryForViolationAsync** - Add duplicate prevention and improved error handling
2. **Unit Tests** - Add missing unit test scenarios
3. **Integration Tests** - Add ActionService integration tests with archive action
4. **Documentation** - Update all relevant documentation
5. **Code Review** - Verify all tests pass and implementation follows patterns

## Success Criteria

- ArchiveRepositoryForViolationAsync prevents duplicate archive attempts
- All unit tests pass (existing + new)
- All integration tests pass (existing + new)
- Action logs are correctly persisted for all scenarios
- Documentation is updated and accurate
- Code follows existing patterns and conventions

### To-dos

- [ ] Enhance ArchiveRepositoryForViolationAsync with duplicate prevention (check if already archived) and improved error handling
- [ ] Add unit tests for archive action: already archived scenario, failure handling, invalid repository, logging verification, action isolation
- [ ] Add integration tests for archive action through ActionService with database and mocked GitHub API, test action logging persistence and multiple violations
- [ ] Add contract tests for archived repository response schema validation and snapshot testing
- [ ] Implement E2E test TC-ACTION-002: ArchiveRepositoryWorkflow_ShouldArchiveNonCompliantRepository with complete workflow validation
- [ ] Update action-service.md, testing-strategy.md, testing-e2e-tests.md, github-integration.md, and CHANGELOG.md with archive action details