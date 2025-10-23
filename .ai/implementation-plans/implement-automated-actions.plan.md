# Implement Automated Actions for Policy Violations

## Overview

Replace the `LoggingActionService` with a full implementation that executes configured actions (create-issue, archive-repo) for policy violations, with proper duplicate prevention, error handling, and audit logging.

## Implementation Steps

### 1. Create IssueDetails Model

**File**: `10xGitHubPolicies.App/Services/Configuration/Models/IssueDetails.cs`

Create a new model class to represent issue configuration:

```csharp
public class IssueDetails
{
    [YamlMember(Alias = "title")]
    public string Title { get; set; }
    
    [YamlMember(Alias = "body")]
    public string Body { get; set; }
    
    [YamlMember(Alias = "labels")]
    public List<string> Labels { get; set; } = new();
}
```

### 2. Update PolicyConfig Model

**File**: `10xGitHubPolicies.App/Services/Configuration/Models/PolicyConfig.cs`

Add missing properties to match the documented configuration format:

```csharp
public class PolicyConfig
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; }
    
    [YamlMember(Alias = "type")]
    public string Type { get; set; }
    
    [YamlMember(Alias = "action")]
    public string Action { get; set; }
    
    [YamlMember(Alias = "issue_details")]
    public IssueDetails IssueDetails { get; set; }
}
```

### 3. Implement ActionService

**File**: `10xGitHubPolicies.App/Services/Action/ActionService.cs`

Create a new implementation that replaces `LoggingActionService`:

**Key responsibilities:**

- Retrieve violations for the scan from database
- Load policy configuration to determine actions
- For each violation:
  - Match violation to policy configuration by PolicyType
  - Execute the configured action (create-issue or archive-repo)
  - Log the action to ActionLog table
  - Handle errors gracefully

**Core logic:**

```csharp
public async Task ProcessActionsForScanAsync(int scanId)
{
    // 1. Load violations with related entities (Repository, Policy)
    var violations = await _dbContext.PolicyViolations
        .Include(v => v.Repository)
        .Include(v => v.Policy)
        .Where(v => v.ScanId == scanId)
        .ToListAsync();
    
    // 2. Get configuration to determine actions
    var config = await _configurationService.GetConfigAsync();
    
    // 3. Process each violation
    foreach (var violation in violations)
    {
        var policyConfig = config.Policies
            .FirstOrDefault(p => p.Type == violation.Policy.PolicyKey);
        
        if (policyConfig == null) continue;
        
        // 4. Execute action based on configuration
        if (policyConfig.Action == "create-issue")
        {
            await CreateIssueForViolationAsync(violation, policyConfig);
        }
        else if (policyConfig.Action == "archive-repo")
        {
            await ArchiveRepositoryForViolationAsync(violation, policyConfig);
        }
    }
}
```

**Duplicate Prevention for Issues (US-010):**

- Before creating an issue, check for existing open issues with the same title and label
- Use `IGitHubService` to search existing issues in the repository
- Skip issue creation if duplicate found, log the skip action

**Error Handling:**

- Wrap each action in try-catch to prevent one failure from blocking others
- Log failures with details to ActionLog table with Status = "Failed"
- Continue processing remaining violations even if one fails

### 4. Update Service Registration

**File**: `10xGitHubPolicies.App/Program.cs`

Replace the service registration:

```csharp
// Change from:
builder.Services.AddScoped<IActionService, LoggingActionService>();

// To:
builder.Services.AddScoped<IActionService, ActionService>();
```

### 5. Add GitHub Service Methods (if missing)

**File**: `10xGitHubPolicies.App/Services/GitHub/IGitHubService.cs` and `GitHubService.cs`

Verify these methods exist (they should based on documentation):

- `Task<Issue> CreateIssueAsync(long repositoryId, string title, string body, IEnumerable<string> labels)`
- `Task ArchiveRepositoryAsync(long repositoryId)`

If a method to check for existing issues doesn't exist, add:

- `Task<IReadOnlyList<Issue>> GetOpenIssuesAsync(long repositoryId, string label)`

### 6. Update ActionLog Usage

Ensure ActionLog entries are created for each action with:

- `ActionType`: "create-issue" or "archive-repo"
- `Status`: "Success", "Failed", or "Skipped"
- `Details`: JSON or text with action details/error messages
- `Timestamp`: DateTime.UtcNow

### 7. Delete LoggingActionService

**File**: `10xGitHubPolicies.App/Services/Action/LoggingActionService.cs`

Remove this file after ActionService is implemented and registered.

### 8. Cleanup Test Endpoints

**File**: `10xGitHubPolicies.App/Program.cs`

Remove the test/verification endpoints that are no longer needed:

- Remove `/verify-scan` endpoint (lines 78-90) - functionality exists in UI via "Scan Now" button
- Remove `/log-job` endpoint (lines 93-97) - was only for testing Hangfire

## Key Considerations

**Dependencies:**

- `IGitHubService`: For GitHub API operations
- `IConfigurationService`: To retrieve policy configurations
- `ApplicationDbContext`: For database operations
- `ILogger<ActionService>`: For logging

**Service Lifetime:** Scoped (matches current registration)

**Thread Safety:** Not required - Hangfire processes jobs sequentially per worker

**Idempotency:** Actions should be safe to retry (duplicate prevention for issues, archive is idempotent)

**Testing:** After implementation, test via:

1. Use the "Scan Now" button in the UI dashboard to trigger a scan
2. Monitor Hangfire dashboard at `/hangfire` for job execution
3. Check ActionLog table for action records
4. Verify GitHub issues created and repositories archived

### To-dos

- [ ] Create IssueDetails model class for YAML configuration
- [ ] Add Name and IssueDetails properties to PolicyConfig model
- [ ] Create ActionService with create-issue and archive-repo logic
- [ ] Implement duplicate issue detection logic in ActionService
- [ ] Replace LoggingActionService with ActionService in Program.cs
- [ ] Verify and add any missing GitHub service methods for issue checking
- [ ] Delete LoggingActionService.cs file
- [ ] Remove test endpoints (/verify-scan and /log-job) from Program.cs