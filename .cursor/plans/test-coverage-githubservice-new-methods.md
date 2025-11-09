# Test Coverage Plan: GitHubService New Methods

## Overview
This document outlines the test coverage plan for 9 new methods added to `GitHubService` (marked as "E2E Testing Methods"). These methods require comprehensive coverage across three test types following existing patterns.

## New Methods Summary

### Repository Operations
1. **CreateRepositoryAsync** - Creates a new repository in the organization
2. **DeleteRepositoryAsync** - Deletes a repository by name
3. **UnarchiveRepositoryAsync** - Unarchives a previously archived repository

### File Operations
4. **CreateFileAsync** - Creates a new file in a repository
5. **UpdateFileAsync** - Updates an existing file in a repository
6. **DeleteFileAsync** (2 overloads) - Deletes a file by repositoryId or repositoryName

### Issue Operations
7. **CloseIssueAsync** - Closes an issue by number
8. **GetRepositoryIssuesAsync** - Gets all issues for a repository by name

### Workflow Operations
9. **UpdateWorkflowPermissionsAsync** - Updates workflow permissions for a repository

---

## Test Coverage Strategy

### 1. Unit Tests (`GitHubServiceTests.cs`)

**Pattern**: Unit tests focus on constructor, initialization, and testable components. Most GitHubService methods are difficult to unit test due to Octokit's sealed classes.

**New Tests Needed**: None (E2E methods follow same pattern as existing methods)

**Rationale**: These methods are thin wrappers around Octokit API calls, similar to existing methods. Unit tests would require extensive mocking that doesn't add value.

---

### 2. Integration Tests (`10xGitHubPolicies.Tests.Integration/GitHub/`)

**Pattern**: 
- Inherit from `GitHubServiceIntegrationTestBase`
- Use `SetupGitHubAppAuthentication()` in Arrange
- Use `GitHubApiResponseBuilder` for response JSON
- Mock WireMock endpoints with `/api/v3/` prefix
- Test success paths, error cases (404, exceptions), edge cases
- Use FluentAssertions for assertions
- Include test case IDs (TC-XXX-XXX) in comments

**New Test Files Needed**:

#### A. `RepositoryCrudOperationsTests.cs` (NEW FILE)
Tests for repository creation, deletion, and unarchiving operations.

```csharp
[Trait("Category", "Integration")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "RepositoryCrudOperations")]
public class RepositoryCrudOperationsTests : GitHubServiceIntegrationTestBase
```

**Tests**:
1. **CreateRepositoryAsync_WhenCalled_CreatesRepository**
   - Arrange: Setup auth, mock POST `/api/v3/orgs/{org}/repos`
   - Act: Create repository with name, description, isPrivate
   - Assert: Repository returned with correct properties
   - Assert: Repository.DefaultBranch set correctly

2. **CreateRepositoryAsync_WhenNameAlreadyExists_ThrowsException**
   - Arrange: Mock 422 response for duplicate name
   - Act: Create repository with duplicate name
   - Assert: Throws appropriate exception

3. **DeleteRepositoryAsync_WhenRepositoryExists_DeletesRepository**
   - Arrange: Setup auth, mock DELETE `/api/v3/repos/{org}/{repo}`
   - Act: Delete repository by name
   - Assert: No exception thrown, verify DELETE request made

4. **DeleteRepositoryAsync_WhenRepositoryNotFound_ThrowsNotFoundException**
   - Arrange: Mock 404 response
   - Act: Delete non-existent repository
   - Assert: Throws NotFoundException

5. **UnarchiveRepositoryAsync_WhenRepositoryArchived_UnarchivesRepository**
   - Arrange: Setup auth, mock PATCH `/api/v3/repositories/{id}` with archived=false
   - Act: Unarchive repository
   - Assert: No exception, verify PATCH request made

6. **UnarchiveRepositoryAsync_WhenRepositoryNotFound_ThrowsNotFoundException**
   - Arrange: Mock 404 response
   - Act: Unarchive non-existent repository
   - Assert: Throws NotFoundException

#### B. `FileCrudOperationsTests.cs` (NEW FILE)
Tests for file create, update, and delete operations.

```csharp
[Trait("Category", "Integration")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "FileCrudOperations")]
public class FileCrudOperationsTests : GitHubServiceIntegrationTestBase
```

**Tests**:
1. **CreateFileAsync_WhenCalled_CreatesFileWithContent**
   - Arrange: Setup auth, mock GET repository (for default branch), POST `/api/v3/repositories/{id}/contents/{path}`
   - Act: Create file with content
   - Assert: File created, verify POST request body contains Base64 content

2. **CreateFileAsync_WithCustomCommitMessage_UsesCustomMessage**
   - Arrange: Mock POST with commit message in body
   - Act: Create file with custom commit message
   - Assert: Request body contains custom message

3. **CreateFileAsync_WhenPathAlreadyExists_ThrowsException**
   - Arrange: Mock 422 response for existing file
   - Act: Create file at existing path
   - Assert: Throws appropriate exception

4. **UpdateFileAsync_WhenFileExists_UpdatesFileContent**
   - Arrange: Setup auth, mock GET repository, GET file (for SHA), PUT `/api/v3/repositories/{id}/contents/{path}`
   - Act: Update file with new content
   - Assert: File updated, verify PUT request contains SHA and new content

5. **UpdateFileAsync_WhenFileNotFound_ThrowsInvalidOperationException**
   - Arrange: Mock GET file returns empty or 404
   - Act: Update non-existent file
   - Assert: Throws InvalidOperationException

6. **DeleteFileAsync_ById_WhenFileExists_DeletesFile**
   - Arrange: Setup auth, mock GET repository, GET file (for SHA), DELETE `/api/v3/repositories/{id}/contents/{path}`
   - Act: Delete file by repositoryId
   - Assert: File deleted, verify DELETE request contains SHA

7. **DeleteFileAsync_ById_WhenFileNotFound_ThrowsInvalidOperationException**
   - Arrange: Mock GET file returns empty
   - Act: Delete non-existent file
   - Assert: Throws InvalidOperationException

8. **DeleteFileAsync_ByName_WhenFileExists_DeletesFile**
   - Arrange: Setup auth, mock GET repository by name, GET file (for SHA), DELETE `/api/v3/repos/{org}/{repo}/contents/{path}`
   - Act: Delete file by repositoryName
   - Assert: File deleted, verify DELETE request made

9. **DeleteFileAsync_ByName_WhenFileNotFound_ThrowsInvalidOperationException**
   - Arrange: Mock GET file returns empty
   - Act: Delete non-existent file
   - Assert: Throws InvalidOperationException

#### C. `IssueManagementTests.cs` (NEW FILE)
Tests for issue closing and retrieval operations.

```csharp
[Trait("Category", "Integration")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "IssueManagement")]
public class IssueManagementTests : GitHubServiceIntegrationTestBase
```

**Tests**:
1. **CloseIssueAsync_WhenIssueExists_ClosesIssue**
   - Arrange: Setup auth, mock PATCH `/api/v3/repositories/{id}/issues/{number}` with state=closed
   - Act: Close issue
   - Assert: No exception, verify PATCH request made

2. **CloseIssueAsync_WhenIssueNotFound_ThrowsNotFoundException**
   - Arrange: Mock 404 response
   - Act: Close non-existent issue
   - Assert: Throws NotFoundException

3. **CloseIssueAsync_WhenIssueAlreadyClosed_DoesNotThrow**
   - Arrange: Mock PATCH returns 200 with closed state
   - Act: Close already-closed issue
   - Assert: No exception

4. **GetRepositoryIssuesAsync_WhenIssuesExist_ReturnsAllIssues**
   - Arrange: Setup auth, mock GET `/api/v3/repos/{org}/{repo}/issues`
   - Act: Get repository issues
   - Assert: Returns list of issues with correct properties

5. **GetRepositoryIssuesAsync_WhenNoIssuesExist_ReturnsEmptyList**
   - Arrange: Mock GET returns empty array
   - Act: Get repository issues
   - Assert: Returns empty list

6. **GetRepositoryIssuesAsync_WhenRepositoryNotFound_ThrowsNotFoundException**
   - Arrange: Mock 404 response
   - Act: Get issues for non-existent repository
   - Assert: Throws NotFoundException

#### D. Update `WorkflowPermissionsTests.cs` (EXISTING FILE)
Add tests for UpdateWorkflowPermissionsAsync.

**Additional Tests**:
1. **UpdateWorkflowPermissionsAsync_WhenCalled_UpdatesPermissionsToRead**
   - Arrange: Setup auth, mock PATCH `/api/v3/repositories/{id}/actions/permissions/workflow`
   - Act: Update permissions to "read"
   - Assert: No exception, verify PATCH request body contains "read"

2. **UpdateWorkflowPermissionsAsync_WhenCalled_UpdatesPermissionsToWrite**
   - Arrange: Mock PATCH with "write" in body
   - Act: Update permissions to "write"
   - Assert: No exception, verify request body

3. **UpdateWorkflowPermissionsAsync_WhenRepositoryNotFound_ThrowsNotFoundException**
   - Arrange: Mock 404 response
   - Act: Update permissions for non-existent repository
   - Assert: Throws NotFoundException

---

### 3. Contract Tests (`10xGitHubPolicies.Tests.Contracts/GitHub/`)

**Pattern**:
- Inherit from `GitHubContractTestBase`
- Use `SetupGitHubAppAuthentication()` in Arrange
- Verify response structure matches GitHub API JSON schema
- Use anonymous objects for mock responses
- Test that required fields are present and correctly typed
- Use FluentAssertions to verify schema compliance

**New Test Files Needed**:

#### A. `RepositoryCrudContractTests.cs` (NEW FILE)
Schema validation for repository creation and deletion responses.

```csharp
[Trait("Category", "Contract")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "RepositoryCrudContract")]
public class RepositoryCrudContractTests : GitHubContractTestBase
```

**Tests**:
1. **CreateRepositoryAsync_ResponseMatchesSchema**
   - Arrange: Mock POST response with full repository object
   - Act: Create repository
   - Assert: Response has required fields (id, name, full_name, owner, private, archived, default_branch)
   - Assert: Field types match schema (id: long, name: string, etc.)

2. **DeleteRepositoryAsync_NoResponseBody_ValidatesStatusCode**
   - Arrange: Mock DELETE returns 204 No Content
   - Act: Delete repository
   - Assert: No exception (DELETE typically has no body)

#### B. `FileCrudContractTests.cs` (NEW FILE)
Schema validation for file operation responses.

```csharp
[Trait("Category", "Contract")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "FileCrudContract")]
public class FileCrudContractTests : GitHubContractTestBase
```

**Tests**:
1. **CreateFileAsync_ResponseMatchesSchema**
   - Arrange: Mock POST response with content response object
   - Act: Create file
   - Assert: Response has required fields (content, commit, name, path, sha)
   - Assert: Commit object has required fields (sha, message, author)

2. **UpdateFileAsync_ResponseMatchesSchema**
   - Arrange: Mock PUT response with content response object
   - Act: Update file
   - Assert: Response matches schema (same as CreateFileAsync)

3. **DeleteFileAsync_ResponseMatchesSchema**
   - Arrange: Mock DELETE response with content response object
   - Act: Delete file
   - Assert: Response matches schema (same as CreateFileAsync)

#### C. `IssueManagementContractTests.cs` (NEW FILE)
Schema validation for issue management responses.

```csharp
[Trait("Category", "Contract")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "IssueManagementContract")]
public class IssueManagementContractTests : GitHubContractTestBase
```

**Tests**:
1. **CloseIssueAsync_ResponseMatchesSchema**
   - Arrange: Mock PATCH response with issue object
   - Act: Close issue
   - Assert: Response has required fields (id, number, title, state, html_url)
   - Assert: State is "closed"

2. **GetRepositoryIssuesAsync_ResponseMatchesSchema**
   - Arrange: Mock GET response with array of issues
   - Act: Get repository issues
   - Assert: Response is array
   - Assert: Each issue has required fields (id, number, title, state, html_url)

#### D. Update `WorkflowPermissionsContractTests.cs` (EXISTING FILE)
Add contract test for UpdateWorkflowPermissionsAsync.

**Additional Tests**:
1. **UpdateWorkflowPermissionsAsync_ResponseMatchesSchema**
   - Arrange: Mock PATCH response with workflow permissions object
   - Act: Update workflow permissions
   - Assert: Response has required fields (default_workflow_permissions, can_approve_pull_request_reviews)
   - Assert: default_workflow_permissions is enum ["read", "write"]

#### E. Update `GitHubApiSnapshotTests.cs` (EXISTING FILE)
Add snapshot tests for new methods.

**Additional Tests**:
1. **CreateRepositoryAsync_StructureRemainsStable**
   - Capture repository creation response structure
   - Scrub dynamic fields (id, created_at, updated_at, pushed_at)

2. **CreateFileAsync_StructureRemainsStable**
   - Capture file creation response structure
   - Scrub dynamic fields (sha, commit.sha, commit.author.date)

3. **UpdateFileAsync_StructureRemainsStable**
   - Capture file update response structure
   - Scrub dynamic fields (same as CreateFileAsync)

4. **DeleteFileAsync_StructureRemainsStable**
   - Capture file deletion response structure
   - Scrub dynamic fields (same as CreateFileAsync)

5. **CloseIssueAsync_StructureRemainsStable**
   - Capture issue closure response structure
   - Scrub dynamic fields (id, number, created_at, updated_at, html_url)

6. **GetRepositoryIssuesAsync_StructureRemainsStable**
   - Capture repository issues response structure
   - Scrub dynamic fields (id, number, created_at, updated_at, html_url)

7. **UpdateWorkflowPermissionsAsync_StructureRemainsStable**
   - Capture workflow permissions update response structure
   - No dynamic fields to scrub

---

## Implementation Checklist

### Integration Tests
- [ ] Create `RepositoryCrudOperationsTests.cs` with 6 tests
- [ ] Create `FileCrudOperationsTests.cs` with 9 tests
- [ ] Create `IssueManagementTests.cs` with 6 tests
- [ ] Update `WorkflowPermissionsTests.cs` with 3 additional tests

### Contract Tests
- [ ] Create `RepositoryCrudContractTests.cs` with 2 tests
- [ ] Create `FileCrudContractTests.cs` with 3 tests
- [ ] Create `IssueManagementContractTests.cs` with 2 tests
- [ ] Update `WorkflowPermissionsContractTests.cs` with 1 additional test
- [ ] Update `GitHubApiSnapshotTests.cs` with 7 additional snapshot tests

### Unit Tests
- [ ] No new unit tests needed (documented in existing test class)

---

## Test Data Requirements

### Response Builders Needed
Add to `GitHubApiResponseBuilder`:
- `BuildRepositoryCreationResponse(long id, string name, bool isPrivate, string defaultBranch)`
- `BuildFileCreationResponse(string content, string path, string sha, string commitMessage)`
- `BuildFileUpdateResponse(string content, string path, string sha, string commitMessage)`
- `BuildFileDeleteResponse(string path, string sha, string commitMessage)`
- `BuildClosedIssueResponse(int number, string title)`
- `BuildRepositoryIssuesResponse(List<Issue> issues)`
- `BuildWorkflowPermissionsUpdateResponse(string permissions)`

### Mock Endpoints Needed
- `POST /api/v3/orgs/{org}/repos` - Create repository
- `DELETE /api/v3/repos/{org}/{repo}` - Delete repository
- `PATCH /api/v3/repositories/{id}` - Update repository (unarchive)
- `POST /api/v3/repositories/{id}/contents/{path}` - Create file
- `PUT /api/v3/repositories/{id}/contents/{path}` - Update file
- `DELETE /api/v3/repositories/{id}/contents/{path}` - Delete file (by ID)
- `DELETE /api/v3/repos/{org}/{repo}/contents/{path}` - Delete file (by name)
- `PATCH /api/v3/repositories/{id}/issues/{number}` - Update issue (close)
- `GET /api/v3/repos/{org}/{repo}/issues` - Get repository issues
- `PATCH /api/v3/repositories/{id}/actions/permissions/workflow` - Update workflow permissions

---

## Notes

1. **Default Branch Handling**: `CreateFileAsync`, `UpdateFileAsync`, and `DeleteFileAsync` all fetch the repository's default branch first. Tests should mock the GET repository call before the file operation.

2. **SHA Requirement**: `UpdateFileAsync` and `DeleteFileAsync` require the file's SHA. Tests should mock the GET file call to retrieve the SHA before the update/delete operation.

3. **Commit Message Defaults**: All file operations accept optional commit messages. Tests should verify both default and custom commit messages.

4. **Error Handling**: File operations throw `InvalidOperationException` when file not found (for update/delete) or when file already exists (for create). Tests should verify these exceptions.

5. **Base64 Encoding**: File content is Base64-encoded in requests. Tests should verify the encoding is correct.

6. **API Path Differences**: 
   - File operations by repositoryId use `/api/v3/repositories/{id}/contents/{path}`
   - File operations by repositoryName use `/api/v3/repos/{org}/{repo}/contents/{path}`
   - Issue operations use `/api/v3/repositories/{id}/issues/{number}`

7. **Contract vs Integration**: Contract tests focus on schema validation, integration tests focus on behavior and error handling.

