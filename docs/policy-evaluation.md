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
builder.Services.AddScoped<IPolicyEvaluator, CatalogInfoHasOwnerEvaluator>();
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

  # Example: Catalog Info Owner Policy
  - name: 'Verify catalog-info.yaml has owner'
    type: 'catalog_info_has_owner'
    action: 'create-issue'
    issue_details:
      title: 'Compliance: catalog-info.yaml missing owner'
      body: 'The catalog-info.yaml file exists but does not have an owner assigned in the spec.owner field. Please add an owner to comply with organization standards.'
      labels: ['policy-violation', 'backstage']

  # Example: Workflow Permissions Policy
  - name: 'Verify Workflow Permissions'
    type: 'correct_workflow_permissions'
    action: 'create-issue'
    issue_details:
      title: 'Security: Workflow permissions should be read-only'
      body: 'This repository has GitHub Actions workflow permissions set to write. For security, please change the default workflow permissions to "Read repository contents and packages permissions" in Settings > Actions > General.'
      labels: ['policy-violation', 'security']
```

Once these steps are completed, the `ScanningService` will automatically pick up and execute your new policy evaluator during the next scan.

### Example: CatalogInfoHasOwnerEvaluator

Here's a real-world example of a policy evaluator that validates YAML file content:

```csharp
using System.Text;
using _10xGitHubPolicies.App.Data.Entities;
using _10xGitHubPolicies.App.Services.GitHub;
using YamlDotNet.Serialization;

namespace _10xGitHubPolicies.App.Services.Policies.Evaluators;

public class CatalogInfoHasOwnerEvaluator : IPolicyEvaluator
{
    private readonly IGitHubService _githubService;
    private readonly ILogger<CatalogInfoHasOwnerEvaluator> _logger;

    public CatalogInfoHasOwnerEvaluator(IGitHubService githubService, ILogger<CatalogInfoHasOwnerEvaluator> logger)
    {
        _githubService = githubService;
        _logger = logger;
    }

    public string PolicyType => "catalog_info_has_owner";

    public async Task<PolicyViolation?> EvaluateAsync(Octokit.Repository repository)
    {
        // Get file content (returns base64-encoded string or null)
        var base64Content = await _githubService.GetFileContentAsync(repository.Name, "catalog-info.yaml");

        // If file doesn't exist, return null (covered by has_catalog_info_yaml policy)
        if (string.IsNullOrEmpty(base64Content))
        {
            return null;
        }

        try
        {
            // Decode base64 to UTF-8
            var yamlBytes = Convert.FromBase64String(base64Content);
            var yamlContent = Encoding.UTF8.GetString(yamlBytes);

            // Parse YAML using dynamic deserialization
            var deserializer = new DeserializerBuilder().Build();
            var yamlData = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

            // Navigate to spec.owner field and validate
            if (yamlData == null || !yamlData.ContainsKey("spec"))
            {
                _logger.LogWarning("catalog-info.yaml in repository {RepoName} is missing 'spec' section", repository.Name);
                return new PolicyViolation { PolicyType = PolicyType };
            }

            var spec = yamlData["spec"] as Dictionary<object, object>;
            if (spec == null || !spec.ContainsKey("owner"))
            {
                _logger.LogWarning("catalog-info.yaml in repository {RepoName} is missing 'owner' field", repository.Name);
                return new PolicyViolation { PolicyType = PolicyType };
            }

            var owner = spec["owner"]?.ToString();
            if (string.IsNullOrWhiteSpace(owner))
            {
                _logger.LogWarning("catalog-info.yaml in repository {RepoName} has empty 'owner' field", repository.Name);
                return new PolicyViolation { PolicyType = PolicyType };
            }

            // Repository is compliant
            return null;
        }
        catch (Exception ex)
        {
            // Invalid YAML: Log error and return violation
            _logger.LogError(ex, "Failed to parse catalog-info.yaml in repository {RepoName}", repository.Name);
            return new PolicyViolation { PolicyType = PolicyType };
        }
    }
}
```

**Key Points from this example:**
- **File Content Retrieval**: Uses `IGitHubService.GetFileContentAsync()` which returns base64-encoded content
- **YAML Parsing**: Uses `YamlDotNet` to parse and navigate the YAML structure
- **Error Handling**: Handles invalid YAML gracefully by logging and returning a violation
- **Edge Cases**: Handles missing file (returns null), missing sections, and empty values
- **Logging**: Uses structured logging for debugging and observability

---

## Testing Policy Evaluators

When creating new policy evaluators, follow the multi-level testing strategy:

### Unit Testing
Test the evaluator logic in isolation by mocking `IGitHubService`:

```csharp
public class YourNewPolicyEvaluatorTests
{
    private readonly IGitHubService _mockGitHubService;
    private readonly YourNewPolicyEvaluator _sut;

    public YourNewPolicyEvaluatorTests()
    {
        _mockGitHubService = Substitute.For<IGitHubService>();
        _sut = new YourNewPolicyEvaluator(_mockGitHubService);
    }

    [Fact]
    public async Task EvaluateAsync_WhenConditionNotMet_ReturnsViolation()
    {
        // Arrange
        var repository = new Repository();
        _mockGitHubService.CheckSomethingAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);

        // Act
        var result = await _sut.EvaluateAsync(repository);

        // Assert
        result.Should().NotBeNull();
        result.PolicyType.Should().Be("your_policy_type");
    }

    [Fact]
    public async Task EvaluateAsync_WhenConditionMet_ReturnsNull()
    {
        // Arrange
        var repository = new Repository();
        _mockGitHubService.CheckSomethingAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);

        // Act
        var result = await _sut.EvaluateAsync(repository);

        // Assert
        result.Should().BeNull();
    }
}
```

### Integration Testing
Test the full policy evaluation workflow with a real database and mocked GitHub API:

```csharp
public class PolicyEvaluationIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer;
    private readonly WireMockServer _wireMockServer;

    [Fact]
    public async Task PerformScanAsync_WithViolations_SavesViolationsToDatabase()
    {
        // Arrange - Setup WireMock to return repository data
        _wireMockServer
            .Given(Request.Create().WithPath("/orgs/*/repos"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyFromFile("fixtures/repositories.json"));

        // Act - Trigger scan
        await _scanningService.PerformScanAsync();

        // Assert - Verify violations in database
        var violations = await _dbContext.PolicyViolations.ToListAsync();
        violations.Should().NotBeEmpty();
    }
}
```
