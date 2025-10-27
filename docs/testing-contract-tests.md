# Contract Testing Guide

## Overview

Contract tests verify that your application correctly handles responses from external APIs (in this case, the **GitHub API**). They protect you from breaking changes when:

1. GitHub changes their API response format
2. The Octokit.NET library changes how it parses responses
3. Your code makes assumptions about the API that might not be true

Contract tests sit at **Level 3** of the [multi-level testing strategy](./testing-strategy.md) - positioned between integration tests and component tests.

---

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

---

## Key Components

### Package Dependencies

```xml
<!-- Contract Testing Stack -->
<PackageReference Include="NJsonSchema" Version="11.0.2" />      <!-- JSON Schema validation -->
<PackageReference Include="Verify.Xunit" Version="28.5.1" />     <!-- Snapshot testing -->
<PackageReference Include="WireMock.Net" Version="1.5.59" />     <!-- HTTP mocking -->

<!-- Testing Utilities -->
<PackageReference Include="FluentAssertions" Version="6.12.0" />  <!-- Readable assertions -->
<PackageReference Include="Bogus" Version="35.4.0" />            <!-- Fake data generation -->
<PackageReference Include="NSubstitute" Version="5.1.0" />       <!-- Mocking -->
```

---

## How It Works: Step-by-Step

### Step 1: Test Base Setup

Every contract test inherits from `GitHubContractTestBase`, which provides:

```csharp
protected readonly WireMockServer MockServer;  // Fake HTTP server
protected readonly GitHubService Sut;          // System Under Test
protected readonly IMemoryCache Cache;
protected readonly Faker Faker;                // Random test data
```

**What happens in the constructor:**

1. **Starts WireMock server** - A real HTTP server on `localhost:random_port`
2. **Generates fake GitHub credentials** - Valid RSA keys, fake app/installation IDs
3. **Configures GitHubService** - Points to WireMock instead of real GitHub
4. **Creates a GitHubClient** - Real Octokit client, but talking to mock server

```csharp
MockServer = WireMockServer.Start();

Options = CreateTestOptions();
Options.BaseUrl = MockServer.Url; // Point to WireMock!

ClientFactory = new GitHubClientFactory(MockServer.Url);
Sut = new GitHubService(optionsWrapper, Logger, Cache, ClientFactory);
```

### Step 2: Mock GitHub Authentication

Before each test, call `SetupGitHubAppAuthentication()` which configures WireMock to respond to GitHub App token requests:

```csharp
// Mock the installation token endpoint
// POST /api/v3/app/installations/{installationId}/access_tokens
MockServer
    .Given(Request.Create()
        .WithPath("/api/v3/app/installations/*/access_tokens")
        .UsingPost())
    .RespondWith(Response.Create()
        .WithStatusCode(201)
        .WithHeader("Content-Type", "application/json")
        .WithBody($@"{{
            ""token"": ""{installationToken}"",
            ""expires_at"": ""{tokenExpiry:yyyy-MM-ddTHH:mm:ssZ}"",
            ""permissions"": {{ ... }},
            ""repository_selection"": ""all""
        }}"));
```

This tells WireMock: *"When the GitHubClient requests an access token, give it this fake response"*

### Step 3: Mock API Endpoint

For each test, mock the specific GitHub API endpoint you're testing:

```csharp
MockServer
    .Given(Request.Create()
        .WithPath($"/api/v3/repositories/{repositoryId}")
        .UsingGet())
    .RespondWith(Response.Create()
        .WithStatusCode(200)
        .WithHeader("Content-Type", "application/json")
        .WithBodyAsJson(repositoryResponse));
```

This tells WireMock: *"When GET `/api/v3/repositories/123456` is called, return this JSON"*

### Step 4: Execute Real Code

Now call your actual production code:

```csharp
var result = await Sut.GetRepositorySettingsAsync(repositoryId);
```

**What happens internally:**
1. `GitHubService` asks for an access token → WireMock provides it
2. `GitHubService` calls GitHub API → WireMock intercepts and returns mock data
3. **Octokit parses the JSON** → This is what we're testing!
4. Your service returns the parsed Octokit object

### Step 5: Validate the Contract

Three validation approaches are used:

#### A) Assertion-Based Validation

Explicitly verify each required field:

```csharp
// Assert - Verify key properties match schema requirements
result.Should().NotBeNull();
result.Id.Should().Be(repositoryId, "id is a required integer field");
result.Name.Should().Be(repoName, "name is a required string field");
result.FullName.Should().Be($"{orgName}/{repoName}", "full_name is a required string field");
result.Owner.Should().NotBeNull("owner is a required object field");
result.Owner.Login.Should().Be(orgName, "owner.login is a required string field");
result.Owner.Id.Should().BeGreaterThan(0, "owner.id is a required integer field");
result.Owner.Type.Should().NotBeNull("owner.type is a required field");
result.Private.Should().BeFalse("private is a required boolean field");
result.Archived.Should().BeFalse("archived is a required boolean field");
```

This verifies each required field from the JSON schema exists and has the correct type.

#### B) Snapshot Testing

Capture the entire response structure:

```csharp
// Assert - Snapshot test
await Verify(result)
    .UseDirectory("Snapshots")
    .UseMethodName("RepositoryResponse")
    .ScrubMembers("CreatedAt", "UpdatedAt", "PushedAt"); // Scrub dynamic date values
```

**First run:**
- Verify.NET serializes the `result` object to a `.verified.txt` file
- You manually review this file and commit it

**Subsequent runs:**
- Verify.NET compares current result to the saved snapshot
- If anything changed, the test fails and shows you a diff

**Snapshot example:**

```
{
  HtmlUrl: https://github.com/test-org/test-repo,
  Id: 123456,
  Owner: {
    SiteAdmin: false,
    Suspended: false,
    UpdatedAt: {Scrubbed},
    CreatedAt: {Scrubbed},
    Id: 789,
    Login: test-org,
    Type: Organization
  },
  Name: test-repo,
  FullName: test-org/test-repo,
  IsTemplate: false,
  Description: Test repository,
  Private: false,
  Fork: false,
  PushedAt: {Scrubbed},
  CreatedAt: {Scrubbed},
  UpdatedAt: {Scrubbed},
  HasDiscussions: false,
  HasIssues: false,
  HasWiki: false,
  HasDownloads: false,
  HasPages: false,
  Archived: false
}
```

Notice `{Scrubbed}` - those fields are ignored because they change every test run.

#### C) JSON Schema Validation

Define the contract formally using JSON Schema:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "required": ["id", "name", "full_name", "owner", "private", "archived"],
  "properties": {
    "id": { "type": "integer" },
    "name": { "type": "string" },
    "full_name": { "type": "string" },
    "owner": {
      "type": "object",
      "required": ["login", "id", "type"],
      "properties": {
        "login": { "type": "string" },
        "id": { "type": "integer" },
        "type": { "type": "string", "enum": ["User", "Organization"] }
      }
    },
    "private": { "type": "boolean" },
    "archived": { "type": "boolean" }
  }
}
```

You can use `NJsonSchema` to validate the JSON against this schema:

```csharp
var schema = await JsonSchema.FromFileAsync("Schemas/github-repository-response.json");
var validationErrors = schema.Validate(responseJson);
validationErrors.Should().BeEmpty();
```

---

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

---

## Test Organization

The tests are organized by GitHub API domain:

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

---

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
# Run tests and accept new snapshots
dotnet test -- Verify.UseProjectRelativeDirectory=true
```

After running this, review the changes in the `.verified.txt` files and commit them if they're correct.

---

## Writing New Contract Tests

### Step 1: Add Schema (Optional but Recommended)

Create a JSON schema file in `Schemas/`:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "required": ["field1", "field2"],
  "properties": {
    "field1": { "type": "string" },
    "field2": { "type": "integer" }
  }
}
```

### Step 2: Create Test Class

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
        
        var mockResponse = new { /* ... */ };
        
        MockServer
            .Given(Request.Create()
                .WithPath("/api/v3/my/endpoint")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(mockResponse));
        
        // Act
        var result = await Sut.MyMethodAsync();
        
        // Assert - Assertion-based
        result.Should().NotBeNull();
        result.Field1.Should().NotBeNullOrEmpty();
        result.Field2.Should().BeGreaterThan(0);
        
        // Assert - Snapshot-based (alternative)
        await Verify(result)
            .UseDirectory("Snapshots")
            .UseMethodName("MyFeatureResponse")
            .ScrubMembers("DynamicField");
    }
}
```

### Step 3: Run and Review

1. Run the test: `dotnet test --filter "Feature=MyFeatureContract"`
2. If using snapshots, review the generated `.verified.txt` file
3. Commit the test and snapshot files

---

## Best Practices

### 1. Use Meaningful Test Names

```csharp
// Good
[Fact]
public async Task CreateIssueAsync_ResponseMatchesSchema()

// Bad
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

```csharp
[Fact]
public async Task GetRepository_NotFound_ReturnsNull()
{
    SetupGitHubAppAuthentication();
    
    MockServer
        .Given(Request.Create().WithPath("/api/v3/repositories/999999"))
        .RespondWith(Response.Create().WithStatusCode(404));
    
    var result = await Sut.GetRepositorySettingsAsync(999999);
    
    result.Should().BeNull();
}
```

### 4. Use Faker for Realistic Test Data

```csharp
var repositoryId = Faker.Random.Long(1, 999999);
var repoName = Faker.Internet.DomainWord();
var description = Faker.Lorem.Sentence();
```

### 5. Document Test Purpose

```csharp
/// <summary>
/// TC-CONTRACT-001: GetRepositorySettingsAsync - Response Schema
/// Verifies that GetRepositorySettingsAsync response matches JSON schema
/// Ensures Octokit correctly parses all required fields
/// </summary>
[Fact]
public async Task GetRepositorySettingsAsync_ResponseMatchesSchema()
```

### 6. Keep Tests Independent

Each test should:
- Set up its own mocks
- Not depend on other tests
- Clean up via `IAsyncLifetime` (implemented in base class)

---

## Debugging Tips

### View WireMock Requests

Use the helper method to see what requests were made:

```csharp
[Fact]
public async Task MyTest()
{
    // ... test code ...
    
    LogWireMockRequests(); // Prints all requests/responses
}
```

### Check WireMock Logs

```csharp
var logEntries = MockServer.LogEntries.ToList();
foreach (var entry in logEntries)
{
    Console.WriteLine($"{entry.RequestMessage.Method} {entry.RequestMessage.Path}");
    Console.WriteLine($"Response: {entry.ResponseMessage.StatusCode}");
}
```

### Inspect Octokit Behavior

Remember that Octokit adds `/api/v3/` prefix when using a custom `BaseUrl` (GitHub Enterprise mode).

So if you call: `client.Repository.Get(123)`

WireMock sees: `GET /api/v3/repositories/123`

---

## Test Coverage

Current contract test coverage (as of last update):

| GitHub API Domain | Tests | Status |
|------------------|-------|--------|
| Repositories | 3 | ✅ Complete |
| Issues | 2 | ✅ Complete |
| Workflow Permissions | 1 | ✅ Complete |
| Team Membership | 1 | ✅ Complete |
| File Contents | 1 | ✅ Complete |
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

---

## Related Documentation

- [Testing Strategy](./testing-strategy.md) - Multi-level testing approach
- [GitHub Integration](./github-integration.md) - GitHubService usage
- [GitHub Client Factory](./github-client-factory.md) - Client factory pattern for testing

---

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

