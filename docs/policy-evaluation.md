# Policy Evaluation Engine

The 10x GitHub Policy Enforcer uses a flexible and extensible engine to evaluate repositories against a set of defined policies. This document explains the architecture of the engine and how to create new policy evaluators.

## Architecture

The policy evaluation engine is built around the **Strategy Pattern**. This design pattern allows the application to select the appropriate algorithm (in this case, a policy check) at runtime. It promotes separation of concerns and makes the system easier to maintain and extend.

The key components of the architecture are:

*   **`IPolicyEvaluationService`**: The main service that orchestrates the evaluation process for a single repository. It iterates through all the configured policies and delegates the actual evaluation to the appropriate `IPolicyEvaluator`.
*   **`IPolicyEvaluator`**: An interface representing a single, specific policy check (a "strategy"). Each implementation of this interface is responsible for evaluating one type of policy (e.g., checking for the existence of a file, verifying a repository setting).
*   **`ScanningService`**: This service is responsible for the high-level scanning process. It fetches all repositories, retrieves the policy configuration, and then uses the `IPolicyEvaluationService` to check each repository for violations.

### How it Works

1.  The `ScanningService` initiates a scan.
2.  It retrieves the policy configuration from the `.github/config.yaml` file.
3.  For each repository in the organization, it calls `IPolicyEvaluationService.EvaluateRepositoryAsync()`.
4.  The `PolicyEvaluationService` uses dependency injection to get all registered `IPolicyEvaluator` implementations.
5.  It matches the `type` of each policy in the configuration with the `PolicyType` property of the available evaluators.
6.  When a match is found, it executes the `EvaluateAsync` method of that specific evaluator.
7.  If the evaluator finds a violation, it returns a `PolicyViolation` object.
8.  The `ScanningService` collects all violations and saves them to the database.

---

## Creating a New Policy Evaluator

To add a new policy check to the application, you need to create a new class that implements the `IPolicyEvaluator` interface and register it in the dependency injection container.

### Step 1: Create the Evaluator Class

1.  Create a new C# class file in the `Services/Implementations/PolicyEvaluators/` directory. Name it descriptively, e.g., `BranchProtectionEvaluator.cs`.
2.  Implement the `IPolicyEvaluator` interface.

Here is a template for a new evaluator:

```csharp
using _10xGitHubPolicies.App.Data.Models;
using _10xGitHubPolicies.App.Services;
using Octokit;

namespace _10xGitHubPolicies.App.Services.Implementations.PolicyEvaluators;

public class YourNewPolicyEvaluator : IPolicyEvaluator
{
    private readonly IGitHubService _githubService;

    // The PolicyType must match the 'type' field in your config.yaml
    public string PolicyType => "your_policy_type";

    public YourNewPolicyEvaluator(IGitHubService githubService)
    {
        _githubService = githubService;
    }

    public async Task<PolicyViolation?> EvaluateAsync(Octokit.Repository repository)
    {
        // Your policy evaluation logic goes here.
        // For example, check for a specific condition.
        var someConditionIsMet = await _githubService.CheckSomethingAsync(repository.Owner.Login, repository.Name);

        if (!someConditionIsMet)
        {
            // If the policy is violated, return a new PolicyViolation object.
            return new PolicyViolation
            {
                PolicyType = this.PolicyType,
                // You can add more details to be stored, though most are set by the ScanningService
            };
        }

        // If the repository is compliant, return null.
        return null;
    }
}
```

**Key Points:**

*   **`PolicyType`**: This string property is crucial. It must **exactly match** the `type` identifier you will use in your `.github/config.yaml` file.
*   **`EvaluateAsync`**: This method contains the core logic for your policy check. It receives the `Octokit.Repository` object, which gives you access to all the repository's details.
*   **Return Value**: The method should return a `PolicyViolation` object if the policy is violated, and `null` if the repository is compliant. The `ScanningService` will handle filling in the `ScanId`, `RepositoryId`, and `PolicyId` fields.

### Step 2: Register the Evaluator

After creating your evaluator class, you need to register it with the dependency injection container so the `PolicyEvaluationService` can find it.

1.  Open `Program.cs`.
2.  Find the section where services are registered.
3.  Add your new evaluator as a scoped service.

```csharp
// Program.cs

// ... other service registrations

builder.Services.AddScoped<IPolicyEvaluationService, PolicyEvaluationService>();
builder.Services.AddScoped<IPolicyEvaluator, HasAgentsMdEvaluator>();
builder.Services.AddScoped<IPolicyEvaluator, HasCatalogInfoYamlEvaluator>();
builder.Services.AddScoped<IPolicyEvaluator, CorrectWorkflowPermissionsEvaluator>();
// Add your new evaluator here
builder.Services.AddScoped<IPolicyEvaluator, YourNewPolicyEvaluator>();

// ... other service registrations
```

### Step 3: Update the Configuration

Finally, add a new policy definition to your `.github/config.yaml` file to enable your new check.

```yaml
# .github/config.yaml

policies:
  # ... other policies
  - name: 'Your New Policy Name'
    type: 'your_policy_type' # This must match the PolicyType in your class
    action: 'create-issue'   # or 'archive-repo'
    issue_details:
      title: 'Compliance: Your new policy violation'
      body: 'Details about why this is a violation.'
      labels: ['policy-violation', 'your-label']
```

Once these steps are completed, the `ScanningService` will automatically pick up and execute your new policy evaluator during the next scan.
