# Policy Evaluation Engine

The 10x GitHub Policy Enforcer uses a flexible and extensible engine to evaluate repositories against a set of defined policies. This document explains the architecture of the engine and how to create new policy evaluators.

> **Note**: This document is part of the [Services Architecture](./architecture.md) documentation.

## Architecture

The policy evaluation engine is built around the **Strategy Pattern**. This design pattern allows the application to select the appropriate algorithm (in this case, a policy check) at runtime. It promotes separation of concerns and makes the system easier to maintain and extend.

The key components of the architecture are:

*   **`IPolicyEvaluationService`**: The main service that orchestrates the evaluation process for a single repository. It iterates through all the configured policies and delegates the actual evaluation to the appropriate `IPolicyEvaluator`.
*   **`IPolicyEvaluator`**: An interface representing a single, specific policy check (a "strategy"). Each implementation of this interface is responsible for evaluating one type of policy (e.g., checking for the existence of a file, verifying a repository setting).
*   **`ScanningService`**: This service is responsible for the high-level scanning process. It fetches all repositories, retrieves the policy configuration, and then uses the `IPolicyEvaluationService` to check each repository for violations.

### How it Works

1.  The `ScanningService` initiates a scan.
2.  It retrieves the policy configuration from the `.github/config.yaml` file.
3.  It synchronizes policies from the configuration file with the database (adds new policies if needed).
4.  It synchronizes repositories between GitHub and the database:
    - Adds new repositories that exist in GitHub but not in the database
    - Updates repository names if repositories were renamed in GitHub
    - Removes repositories that no longer exist in GitHub (with cascading deletion of related PolicyViolations and ActionLogs)
5.  For each repository in the organization, it calls `IPolicyEvaluationService.EvaluateRepositoryAsync()`.
6.  The `PolicyEvaluationService` uses dependency injection to get all registered `IPolicyEvaluator` implementations.
7.  It matches the `type` of each policy in the configuration with the `PolicyType` property of the available evaluators.
8.  When a match is found, it executes the `EvaluateAsync` method of that specific evaluator.
9.  If the evaluator finds a violation, it returns a `PolicyViolation` object.
10. The `ScanningService` collects all violations and saves them to the database.
11. After the scan completes, it enqueues a background job to process automated actions for the violations found.

### Repository Synchronization

The `ScanningService` ensures that the database repository list stays synchronized with the GitHub organization:

- **New Repositories**: Automatically detected and added to the database with status "Pending"
- **Renamed Repositories**: Detection based on GitHub repository ID (which remains constant across renames). Repository names are automatically updated in the database.
- **Deleted Repositories**: Repositories that no longer exist in GitHub are removed from the database, along with their related records:
  - Policy violations associated with the repository
  - Action logs for actions taken on the repository

This synchronization happens automatically during each scan, ensuring the database always reflects the current state of the GitHub organization.

---

## Creating a New Policy Evaluator

To add a new policy check to the application, you need to create a new class that implements the `IPolicyEvaluator` interface and register it in the dependency injection container.

### Step 1: Create the Evaluator Class

1. Create a new class in `Services/Policies/Evaluators/` implementing `IPolicyEvaluator`
2. Set the `PolicyType` property to match the `type` in `config.yaml`
3. Implement `EvaluateAsync(Repository)` to return `PolicyViolation?` (null if compliant)

**Key Requirements:**
- `PolicyType` must exactly match the `type` in your `config.yaml`
- Return `PolicyViolation` if violated, `null` if compliant
- `ScanningService` automatically sets `ScanId`, `RepositoryId`, and `PolicyId`

### Step 2: Register the Evaluator

Register your evaluator in `Program.cs` as a scoped service:

```csharp
builder.Services.AddScoped<IPolicyEvaluator, YourNewPolicyEvaluator>();
```

### Step 3: Update the Configuration

Add your policy to `.github/config.yaml`:

```yaml
policies:
  - name: 'Your New Policy Name'
    type: 'your_policy_type'  # Must match PolicyType property
    action: 'create-issue'    # or 'archive-repo', 'log-only'
    issue_details:
      title: 'Compliance: Your new policy violation'
      body: 'Details about why this is a violation.'
      labels: ['policy-violation', 'your-label']
```

The `ScanningService` will automatically execute your evaluator during the next scan.

### Example Implementation Patterns

**File Content Validation**: Use `IGitHubService.GetFileContentAsync()` to retrieve files (returns base64-encoded content)

**YAML/JSON Parsing**: Parse file contents using `YamlDotNet` or `System.Text.Json` to validate structure

**Error Handling**: Log errors and return violations for invalid data; return `null` for compliant repositories

**Edge Cases**: Handle missing files, missing sections, and empty values appropriately

---

## Testing Policy Evaluators

Follow the multi-level testing strategy:

- **Unit Tests**: Mock `IGitHubService` and test evaluator logic in isolation
- **Integration Tests**: Test full evaluation workflow with database and mocked GitHub API

See [Testing Strategy](../testing/testing-strategy.md) for detailed testing guidelines.

## Related Documentation

- [Scanning Service](./scanning-service.md) - How scans are orchestrated
- [Action Service](./action-service.md) - How violations trigger actions
- [GitHub Integration](./github-integration.md) - GitHub API operations

