# Contract Testing Guide

## Overview

Contract tests verify that your application correctly handles responses from external APIs (in this case, the **GitHub API**). They protect you from breaking changes when:

1. GitHub changes their API response format
2. The Octokit.NET library changes how it parses responses
3. Your code makes assumptions about the API that might not be true

Contract tests sit at **Level 3** of the [multi-level testing strategy](./testing-strategy.md) - positioned between integration tests and component tests.

## Architecture

The contract tests use **three complementary approaches**:

### 1. Schema Validation (JSON Schema)
- Defines the expected structure of API responses
- Located in: `10xGitHubPolicies.Tests.Contracts/Schemas/*.json`
- Uses: **NJsonSchema** package
- Purpose: Validates that required fields exist with correct types

### 2. Snapshot Testing (Verify.NET)
- Captures the actual API response and compares it to a "golden master"
- Located in: `10xGitHubPolicies.Tests.Contracts/GitHub/Snapshots/*.verified.txt`
- Uses: **Verify.Xunit** package
- Purpose: Detects any changes in response structure over time

### 3. Assertion-Based Testing (FluentAssertions)
- Explicitly verifies critical fields exist and have correct types
- Located in: Test methods themselves
- Uses: **FluentAssertions** package
- Purpose: Validates business-critical fields with readable assertions

## Key Components

### Package Dependencies

```xml
<!-- Contract Testing Stack -->
<PackageReference Include="NJsonSchema" Version="11.0.2" />
<PackageReference Include="Verify.Xunit" Version="28.5.1" />
<PackageReference Include="WireMock.Net" Version="1.5.59" />

<!-- Testing Utilities -->
<PackageReference Include="FluentAssertions" Version="6.12.0" />
<PackageReference Include="Bogus" Version="35.4.0" />
```

## How It Works

### Step 1: Test Base Setup

Every contract test inherits from `GitHubContractTestBase`, which provides:
- **WireMock server** - Fake HTTP server on `localhost:random_port`
- **Real GitHubService** - Points to WireMock instead of real GitHub
- **Real Octokit client** - Handles serialization/deserialization
- **Test credentials** - Valid RSA keys and fake GitHub App credentials

### Step 2: Mock GitHub Authentication

Before each test, call `SetupGitHubAppAuthentication()` which configures WireMock to respond to GitHub App token requests.

### Step 3: Mock API Endpoint

For each test, mock the specific GitHub API endpoint you're testing using WireMock.

### Step 4: Execute Real Code

Call your actual production code - the real GitHubService makes HTTP requests to WireMock, and Octokit parses the JSON responses.

### Step 5: Validate the Contract

Three validation approaches are used:

#### A) Assertion-Based Validation
Explicitly verify each required field:
```csharp
result.Should().NotBeNull();
result.Id.Should().Be(repositoryId, "id is a required integer field");
result.Name.Should().Be(repoName, "name is a required string field");
```

#### B) Snapshot Testing
Capture the entire response structure:
```csharp
await Verify(result)
    .ScrubMembers("CreatedAt", "UpdatedAt", "PushedAt"); // Scrub dynamic date values
```

**First run:** Verify.NET serializes the result to a `.verified.txt` file  
**Subsequent runs:** Verify.NET compares current result to the saved snapshot

#### C) JSON Schema Validation
Define the contract formally using JSON Schema (see `Schemas/` directory), then validate:
```csharp
var schema = await JsonSchema.FromFileAsync("Schemas/github-repository-response.json");
var validationErrors = schema.Validate(responseJson);
validationErrors.Should().BeEmpty();
```

**Example**: See `Tests.Contracts/GitHub/RepositoryResponseContractTests.cs` for complete examples.

## What Problems Does This Solve?

### Problem 1: Breaking API Changes
**Scenario:** GitHub adds a required field `visibility` to repository responses

**Without contract tests:**
- Your app crashes in production
- `result.Visibility` throws `NullReferenceException`

**With contract tests:**
- Snapshot test fails immediately
- You see the diff showing the new field
- You can proactively update your code

### Problem 2: Octokit Parsing Changes
**Scenario:** Octokit.NET v9.0 changes how it parses `archived` field

**Without contract tests:**
- Your policy checks stop working
- No archived repos are detected

**With contract tests:**
- Assertion test fails: `result.Archived.Should().BeFalse()`
- You know exactly which field broke

### Problem 3: Undocumented API Behavior
**Scenario:** GitHub sometimes returns `body: null` instead of `body: ""`

**Without contract tests:**
- You assume `body` is always a string
- `result.Body.Contains("...")` throws null reference

**With contract tests:**
- Schema validation shows `body` can be null
- You write defensive code: `result.Body?.Contains("...") ?? false`

## Test Organization

```
10xGitHubPolicies.Tests.Contracts/
├── GitHub/
│   ├── GitHubContractTestBase.cs           # Base class with WireMock setup
│   ├── GitHubApiSnapshotTests.cs           # Snapshot tests (Verify.NET)
│   ├── RepositoryResponseContractTests.cs  # Repository API contracts
│   ├── IssueResponseContractTests.cs       # Issue API contracts
│   ├── WorkflowPermissionsContractTests.cs # Workflow API contracts
│   └── Snapshots/                          # Verified snapshots
│       ├── GitHubApiSnapshotTests.*.verified.txt
├── Schemas/                                 # JSON Schema definitions
│   ├── github-repository-response.json
│   ├── github-issue-response.json
│   └── github-workflow-permissions-response.json
└── 10xGitHubPolicies.Tests.Contracts.csproj
```

## Writing New Contract Tests

### Step 1: Create Test Class

```csharp
[Trait("Category", "Contract")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "MyFeatureContract")]
public class MyFeatureContractTests : GitHubContractTestBase
{
    [Fact]
    public async Task MyMethod_ResponseMatchesSchema()
    {
        // Arrange
        SetupGitHubAppAuthentication();
        MockServer.Given(...).RespondWith(...);
        
        // Act
        var result = await Sut.MyMethodAsync();
        
        // Assert
        result.Should().NotBeNull();
        result.Field.Should().NotBeNullOrEmpty();
        
        // Optional: Snapshot test
        await Verify(result).ScrubMembers("DynamicField");
    }
}
```

**See `Tests.Contracts/GitHub/RepositoryResponseContractTests.cs` for complete examples.**

### Step 2: Add Schema (Optional but Recommended)

Create a JSON schema file in `Schemas/` to formally define the contract structure.

## Best Practices

### 1. Use Meaningful Test Names
```csharp
// ✅ Good
[Fact]
public async Task CreateIssueAsync_ResponseMatchesSchema()

// ❌ Bad
[Fact]
public async Task Test1()
```

### 2. Scrub Dynamic Values in Snapshots
```csharp
await Verify(result)
    .ScrubMembers("Id", "CreatedAt", "UpdatedAt", "Token");
```

This prevents false positives from randomly generated values.

### 3. Test Both Success and Error Cases
Test both successful responses and error scenarios (404, 403, etc.).

### 4. Use Faker for Realistic Test Data
```csharp
var repositoryId = Faker.Random.Long(1, 999999);
var repoName = Faker.Internet.DomainWord();
```

### 5. Document Test Purpose
Use XML comments to explain what the test verifies.

### 6. Keep Tests Independent
Each test should:
- Set up its own mocks
- Not depend on other tests
- Clean up via `IAsyncLifetime` (implemented in base class)

## Running the Tests

### Run All Contract Tests
```bash
dotnet test --filter "Category=Contract"
```

### Run Specific Feature Tests
```bash
# Repository contracts only
dotnet test --filter "Feature=RepositoryContract"

# Issue contracts only
dotnet test --filter "Feature=IssueContract"

# Snapshot tests only
dotnet test --filter "Feature=ApiSnapshots"
```

### Update Snapshots
If you intentionally changed something and need to update the snapshots:
```bash
dotnet test -- Verify.UseProjectRelativeDirectory=true
```

After running this, review the changes in the `.verified.txt` files and commit them if they're correct.

For more commands, see the [Quick Reference](./quick-reference.md).

## Debugging Tips

### View WireMock Requests
```csharp
// In test method
LogWireMockRequests(); // Prints all requests/responses
```

### Check WireMock Logs
```csharp
var logEntries = MockServer.LogEntries.ToList();
foreach (var entry in logEntries)
{
    Console.WriteLine($"{entry.RequestMessage.Method} {entry.RequestMessage.Path}");
}
```

### Inspect Octokit Behavior
Remember that Octokit adds `/api/v3/` prefix when using a custom `BaseUrl` (GitHub Enterprise mode).

## Test Coverage

Current contract test coverage (11 tests):

| GitHub API Domain | Tests | Status |
|------------------|-------|--------|
| Repositories | 3 | ✅ Complete |
| Issues | 2 | ✅ Complete |
| Workflow Permissions | 1 | ✅ Complete |
| Team Membership | 1 | ✅ Complete |
| File Contents | 1 | ✅ Complete |
| API Snapshots | 5 | ✅ Complete |
| **Total** | **11** | **✅ Complete** |

### Coverage Matrix

| Method | Assertion Test | Snapshot Test | Schema |
|--------|---------------|---------------|--------|
| `GetRepositorySettingsAsync` | ✅ | ✅ | ✅ |
| `GetOrganizationRepositoriesAsync` | ✅ | - | ✅ |
| `ArchiveRepositoryAsync` | ✅ | - | ✅ |
| `CreateIssueAsync` | ✅ | ✅ | ✅ |
| `GetOpenIssuesAsync` | ✅ | - | ✅ |
| `GetFileContentAsync` | - | ✅ | - |
| `GetWorkflowPermissionsAsync` | - | ✅ | ✅ |
| `IsUserMemberOfTeamAsync` | - | ✅ | - |

## Summary

**Contract tests ensure:**
1. ✅ Your app correctly parses GitHub API responses
2. ✅ Breaking changes in GitHub's API are caught immediately
3. ✅ Octokit.NET updates don't silently break your code
4. ✅ You have documentation of the API structure

**How they work:**
1. **WireMock** creates a fake GitHub API server
2. **Real GitHubService** calls this fake server
3. **Real Octokit** parses the JSON responses
4. **Verify.NET** snapshots the parsed objects
5. **FluentAssertions** validates critical fields
6. **NJsonSchema** validates JSON structure

This is **Level 3** of the multi-level testing strategy - positioned between integration tests (Level 2) and component tests (Level 4).

## Related Documentation

- [Testing Strategy](./testing-strategy.md) - Multi-level testing approach overview
- [Integration Tests](./integration-tests.md) - Level 2: HTTP communication testing
- [Quick Reference](./quick-reference.md) - Common commands and patterns

