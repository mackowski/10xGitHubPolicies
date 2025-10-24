# Implementation Plan: Workflow Permissions Policy Evaluator

## Overview

Implement the `correct_workflow_permissions` policy evaluator to check that repositories have their GitHub Actions default workflow permissions set to "read" (read repository contents and packages permissions) as specified in the PRD.

## Context

According to the PRD (section 3.4, policy #3), the application must verify that repositories have workflow permissions set to 'Read repository contents and packages permissions'. This is a security best practice that prevents workflows from having unnecessary write access.

## Implementation Steps

### 1. Add GitHub API Method for Workflow Permissions

**File**: `10xGitHubPolicies.App/Services/GitHub/IGitHubService.cs`

Add a new method to the interface:

```csharp
Task<string> GetWorkflowPermissionsAsync(long repositoryId);
```

**File**: `10xGitHubPolicies.App/Services/GitHub/GitHubService.cs`

Implement the method using Octokit's REST API client:

```csharp
public async Task<string> GetWorkflowPermissionsAsync(long repositoryId)
{
    var client = await GetAuthenticatedClient();
    try
    {
        // Use the GitHub API endpoint: GET /repos/{owner}/{repo}/actions/permissions/workflow
        var connection = client.Connection;
        var endpoint = new Uri($"repositories/{repositoryId}/actions/permissions/workflow", UriKind.Relative);
        var response = await connection.Get<WorkflowPermissionsResponse>(endpoint, null);
        return response.Body.DefaultWorkflowPermissions;
    }
    catch (NotFoundException)
    {
        _logger.LogWarning("Workflow permissions not found for repository {RepositoryId}. Actions may be disabled.", repositoryId);
        return null;
    }
}

// Add a private class for deserialization
private class WorkflowPermissionsResponse
{
    public string DefaultWorkflowPermissions { get; set; }
    public bool CanApprovePullRequestReviews { get; set; }
}
```

### 2. Implement the Policy Evaluator Logic

**File**: `10xGitHubPolicies.App/Services/Policies/Evaluators/CorrectWorkflowPermissionsEvaluator.cs`

Replace the TODO implementation with actual logic:

```csharp
public async Task<PolicyViolation?> EvaluateAsync(Octokit.Repository repository)
{
    var permissions = await _githubService.GetWorkflowPermissionsAsync(repository.Id);
    
    // If permissions is null, Actions might be disabled - consider this compliant
    if (permissions == null)
    {
        return null;
    }
    
    // Check if permissions are set to "read" (the secure, restrictive setting)
    if (permissions != "read")
    {
        return new PolicyViolation
        {
            PolicyType = PolicyType
        };
    }
    
    return null;
}
```

### 3. Update Documentation

**File**: `docs/github-integration.md`

Add the new method to the IGitHubService methods list (around line 16):

```markdown
- `Task<string> GetWorkflowPermissionsAsync(long repositoryId)`: Gets the default workflow permissions for a repository. Returns "read" or "write", or null if Actions are disabled.
```

Add a usage example in the document:

````markdown
### Example: Checking Workflow Permissions

```csharp
public async Task CheckRepositorySecurityAsync(long repositoryId)
{
    var permissions = await _gitHubService.GetWorkflowPermissionsAsync(repositoryId);
    
    if (permissions == "write")
    {
        _logger.LogWarning("Repository {RepoId} has write permissions for workflows", repositoryId);
    }
}
````

```

**File**: `docs/policy-evaluation.md`

Update the example configuration (around line 119) to include a complete example for the workflow permissions policy:

```yaml
- name: 'Verify Workflow Permissions'
  type: 'correct_workflow_permissions'
  action: 'create-issue'
  issue_details:
    title: 'Security: Workflow permissions should be read-only'
    body: 'This repository has GitHub Actions workflow permissions set to write. For security, please change the default workflow permissions to "Read repository contents and packages permissions" in Settings > Actions > General.'
    labels: ['policy-violation', 'security']
```

## Key Technical Details

- **GitHub API Endpoint**: `GET /repos/{owner}/{repo}/actions/permissions/workflow`
- **Expected Values**: 
  - `"read"` - Read-only permissions (compliant)
  - `"write"` - Read and write permissions (violation)
  - `null` - Actions disabled or permissions not configured (treated as compliant)
- **Octokit Usage**: Uses the low-level `Connection.Get<T>()` method since Octokit.net may not have a high-level wrapper for this endpoint
- **Error Handling**: Returns null for NotFoundException, treating disabled Actions as compliant

## Testing Considerations

After implementation, test with:

1. A repository with read-only workflow permissions (should pass)
2. A repository with write workflow permissions (should fail)
3. A repository with Actions disabled (should pass)

## Dependencies

- Existing `IGitHubService` infrastructure
- Octokit.net library (already installed)
- Entity Framework entities (PolicyViolation)