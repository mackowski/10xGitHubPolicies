# Integration Testing Guide

## Overview

Integration tests verify that your application components work correctly together with external dependencies at the **HTTP communication level**. For the 10x GitHub Policy Enforcer, this means testing the **GitHubService** as it communicates with the GitHub API.

Integration tests protect you from:

1. HTTP communication failures (wrong headers, paths, methods)
2. Request/response serialization issues
3. Authentication flow problems (JWT generation, token exchange)
4. GitHub API rate limiting behavior
5. Error handling for various HTTP status codes

Integration tests sit at **Level 2** of the [multi-level testing strategy](./testing-strategy.md) - positioned between unit tests and contract tests.

## Architecture

The integration tests use **HTTP-level mocking** rather than code-level mocking:

### Key Principle: Test Real HTTP Communication

Unlike traditional unit tests that mock the `IGitHubService` interface, integration tests:

- ✅ Use the **real GitHubService implementation**
- ✅ Use the **real Octokit.GitHubClient**
- ✅ Make **real HTTP requests** (to a mock server)
- ✅ Test **actual serialization/deserialization**
- ✅ Verify **complete request/response cycle**

### Mock Server: WireMock.Net

**WireMock.Net** creates a real HTTP server that:
- Listens on `localhost` with a random port
- Accepts actual HTTP requests
- Returns pre-configured responses
- Logs all requests for debugging

This approach catches issues that code-level mocking would miss:
- Wrong HTTP paths or methods
- Missing or incorrect headers
- JSON serialization problems
- Authentication header formatting

## Key Components

### Test Infrastructure

- **GitHubApiFixture**: Shared WireMock server setup using xUnit's `IClassFixture`
- **GitHubServiceIntegrationTestBase**: Base class providing common setup for all integration tests
- **GitHubApiResponseBuilder**: Creates GitHub-compatible JSON responses for testing
- **DatabaseFixture**: SQLite in-memory database for fast, isolated testing (for ActionService tests)

### Package Dependencies

```xml
<!-- Core Testing -->
<PackageReference Include="xunit" Version="2.5.3" />
<PackageReference Include="FluentAssertions" Version="8.7.1" />
<PackageReference Include="Bogus" Version="35.6.4" />

<!-- Integration Testing -->
<PackageReference Include="WireMock.Net" Version="1.5.59" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.11" />
```

## How It Works

### Step 1: Test Infrastructure Setup

The `GitHubApiFixture` provides shared test infrastructure:
- One WireMock server per test class (shared across all tests)
- HTTPS enabled for realistic SSL communication
- SSL certificate handling for self-signed certificates
- Random port assignment to avoid conflicts
- Automatic cleanup after all tests complete

### Step 2: Test Base Class

Every integration test inherits from `GitHubServiceIntegrationTestBase`:
- **SUT (System Under Test)**: Real `GitHubService` instance, not mocked
- **BaseUrl Override**: Points to WireMock instead of `api.github.com`
- **Real Dependencies**: Uses real `IMemoryCache` for token caching behavior
- **Test Credentials**: Generates valid RSA keys and fake GitHub App credentials

### Step 3: Mock GitHub App Authentication

Before each test that makes authenticated API calls, call `SetupGitHubAppAuthentication()`:
- Mocks the installation token endpoint
- Provides fake installation access tokens
- Handles JWT token exchange flow

**Important Note:** Octokit treats custom `BaseUrl` as GitHub Enterprise mode and prepends `/api/v3/` to all paths.

### Step 4: Mock API Endpoints

For each test, mock the specific GitHub API endpoint:
- Use `MockServer.Given()` to define request matching
- Use `Response.Create()` to define response structure
- Use `GitHubApiResponseBuilder` for realistic JSON responses

### Step 5: Execute and Assert

Call the real service method and verify results using FluentAssertions.

**Example**: See `Tests.Integration/GitHub/RepositoryOperationsTests.cs` for a complete example.

## ActionService Tests with Database

ActionService integration tests use SQLite in-memory database for fast, isolated testing:

### Database Setup

- **SQLite In-Memory**: Fast, lightweight database (no Docker required)
- **CollectionFixture**: Shared database across all ActionService tests
- **Manual Cleanup**: Fast database cleanup between tests
- **EnsureCreated**: Uses `EnsureCreatedAsync()` instead of migrations (migrations contain SQL Server-specific syntax)

### Example: ActionService Test

```csharp
[Collection("ActionService Integration Tests")]
[Trait("Category", "Integration")]
public class ActionServiceArchiveTests : ActionServiceIntegrationTestBase
{
    [Fact]
    public async Task ProcessActionsForScanAsync_WhenArchiveRepoAction_ArchivesRepositoryAndLogsAction()
    {
        // Arrange - Create test data in database
        var scan = await CreateScanAsync();
        var policy = await CreatePolicyAsync("test-policy", "archive-repo");
        var repository = await CreateRepositoryAsync("test-repo", 12345);
        var violation = await CreateViolationAsync(scan.ScanId, policy.PolicyId, repository.RepositoryId);
        
        // Mock GitHub API responses
        SetupGitHubAppAuthentication();
        MockServer.Given(...).RespondWith(...);
        
        // Act
        await Sut.ProcessActionsForScanAsync(scan.ScanId);
        
        // Assert - Verify database persistence
        var actionLog = await DbContext.ActionsLogs.FirstOrDefaultAsync();
        actionLog.Should().NotBeNull();
        actionLog.Status.Should().Be("Success");
    }
}
```

**Key Features:**
- Real database operations (CRUD, queries, transactions)
- Database state verification
- Fast execution (seconds instead of minutes)
- No Docker required

## What Problems Does This Solve?

### Problem 1: Wrong HTTP Path
**Scenario:** You change a method to use a different GitHub API endpoint

**Without integration tests:**
- Unit tests pass (they mock the interface)
- Production fails with 404 Not Found

**With integration tests:**
- WireMock expects specific path
- Test fails immediately
- You see: "No matching stub found for GET /repos/test-org/test-repo"

### Problem 2: Missing Authentication Header
**Scenario:** You refactor authentication logic and forget to set the header

**Without integration tests:**
- Mocked interfaces return success
- Production gets 401 Unauthorized

**With integration tests:**
- WireMock can verify headers were sent
- Test fails with authentication error
- You catch it before deployment

### Problem 3: Serialization Issues
**Scenario:** You add a new property but misspell the JSON field name

**Without integration tests:**
- Unit tests use in-memory objects
- Serialization never tested

**With integration tests:**
- Real Octokit parses real JSON
- Property remains `false` when it should be `true`
- Assertion fails

### Problem 4: Token Caching Bugs
**Scenario:** Token caching logic doesn't work correctly

**Without integration tests:**
- Hard to test caching behavior with mocks
- Production makes unnecessary token requests
- Rate limits exceeded

**With integration tests:**
- WireMock logs show multiple token requests
- Test verifies token was cached
- You can test expiration behavior

## Test Organization

```
10xGitHubPolicies.Tests.Integration/
├── Fixtures/
│   ├── GitHubApiFixture.cs              # Shared WireMock server setup
│   └── DatabaseFixture.cs               # SQLite in-memory database setup
├── Builders/
│   └── GitHubApiResponseBuilder.cs      # JSON response builders
├── GitHub/
│   ├── GitHubServiceIntegrationTestBase.cs  # Base class
│   ├── FileOperationsTests.cs           # File content operations
│   ├── IssueOperationsTests.cs          # Issue CRUD operations
│   ├── RateLimitHandlingTests.cs        # Rate limit behavior
│   ├── RepositoryOperationsTests.cs     # Repository operations
│   ├── TeamMembershipTests.cs           # Team/member checks
│   ├── TokenCachingTests.cs             # Authentication caching
│   └── WorkflowPermissionsTests.cs      # Workflow permissions
└── Action/
    ├── ActionServiceIntegrationTestBase.cs  # Base class with database
    └── ActionServiceArchiveTests.cs     # Archive repository actions
```

**Organization principles:**
- **Fixtures/** - Shared test infrastructure
- **Builders/** - Test data generation
- **GitHub/** - Tests grouped by feature area
- **Action/** - Tests for ActionService with database integration
- One test class per feature area
- Base class provides common setup

## Writing New Integration Tests

### Step 1: Create Test Class

```csharp
[Trait("Category", "Integration")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "MyFeature")]
public class MyFeatureTests : GitHubServiceIntegrationTestBase
{
    private readonly GitHubApiResponseBuilder _responseBuilder;
    
    public MyFeatureTests(GitHubApiFixture fixture) : base(fixture)
    {
        _responseBuilder = new GitHubApiResponseBuilder();
    }
}
```

### Step 2: Write Test Using AAA Pattern

```csharp
[Fact]
public async Task MyMethod_WhenCalled_ReturnsExpected()
{
    // Arrange
    SetupGitHubAppAuthentication();
    
    MockServer
        .Given(Request.Create()
            .WithPath("/api/v3/my/endpoint")
            .UsingGet())
        .RespondWith(Response.Create()
            .WithStatusCode(200)
            .WithHeader("Content-Type", "application/json")
            .WithBodyAsJson(mockResponse));
    
    // Act
    var result = await Sut.MyMethodAsync();
    
    // Assert
    result.Should().NotBeNull();
    result.Property.Should().Be("expected-value");
}
```

**See `Tests.Integration/GitHub/RepositoryOperationsTests.cs` for complete examples.**

## Best Practices

### 1. Always Call SetupGitHubAppAuthentication()
Every test that makes GitHub API calls must mock authentication.

### 2. Use Descriptive Test Names
Follow the pattern: `MethodName_Scenario_ExpectedResult`

### 3. Document Test Purpose
Use XML comments to explain what the test verifies.

### 4. Test Both Success and Failure Paths
For every method, test:
- ✅ Happy path (200 OK)
- ✅ Not found (404)
- ✅ Unauthorized (401/403) if applicable
- ✅ Bad request (400) if applicable
- ✅ Rate limit (429) if applicable

### 5. Use FluentAssertions for Readable Assertions
```csharp
result.Should().NotBeNull();
result.Count.Should().BeGreaterThan(0);
result.First().Name.Should().Be("expected-name");
```

### 6. Keep Tests Independent
Each test should:
- Set up its own mocks
- Not depend on execution order
- Not share state with other tests

### 7. Use Response Builder for Complex JSON
```csharp
var repoJson = _responseBuilder.BuildRepositoryResponse(123, "my-repo");
MockServer.Given(...).RespondWith(Response.Create().WithBody(repoJson));
```

### 8. Remember the /api/v3/ Path Prefix
Octokit adds `/api/v3/` prefix when using custom `BaseUrl`:
```csharp
// ✅ Correct - Include /api/v3/ prefix
MockServer.Given(Request.Create().WithPath("/api/v3/repositories/123"))
```

### 9. Use Traits for Test Organization
```csharp
[Trait("Category", "Integration")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "RepositoryOperations")]
```

## Debugging Tips

### 1. Log WireMock Requests
```csharp
// In test method
LogWireMockRequests(); // Prints all HTTP requests/responses
```

### 2. Inspect Specific Request
```csharp
var requests = MockServer.LogEntries.ToList();
var repoRequest = requests.FirstOrDefault(r => r.RequestMessage.Path.Contains("repos"));
```

### 3. Verify Request Was Made
```csharp
var requests = MockServer.LogEntries;
requests.Should().ContainSingle(r => 
    r.RequestMessage.Path.Contains("repositories/123") && 
    r.RequestMessage.Method == "PATCH");
```

## Test Coverage

Current integration test coverage (33 tests):

| GitHub Service Method | Tests | Status |
|----------------------|-------|--------|
| `GetOrganizationRepositoriesAsync` | 1 | ✅ Complete |
| `GetRepositorySettingsAsync` | 2 | ✅ Complete |
| `ArchiveRepositoryAsync` | 2 | ✅ Complete |
| `GetFileContentAsync` | 3 | ✅ Complete |
| `CreateIssueAsync` | 2 | ✅ Complete |
| `GetOpenIssuesAsync` | 1 | ✅ Complete |
| `GetWorkflowPermissionsAsync` | 2 | ✅ Complete |
| `GetTeamBySlugAsync` | 2 | ✅ Complete |
| `IsUserMemberOfTeamAsync` | 2 | ✅ Complete |
| **ActionService** | | |
| `ProcessActionsForScanAsync` (Archive) | 4 | ✅ Complete |
| **Total** | **33** | **✅ Complete** |

### Coverage Matrix

| Test Class | Tests | Features Tested |
|-----------|-------|-----------------|
| `FileOperationsTests` | 3 | File content retrieval, base64 decoding, error handling |
| `IssueOperationsTests` | 3 | Issue creation, searching, label handling |
| `RateLimitHandlingTests` | 2 | Rate limit detection, retry logic |
| `RepositoryOperationsTests` | 4 | Repository listing, settings, archiving |
| `TeamMembershipTests` | 4 | Team lookup, membership checks |
| `TokenCachingTests` | 4 | Token generation, caching, expiration |
| `WorkflowPermissionsTests` | 2 | Permission retrieval, parsing |
| `ActionServiceArchiveTests` | 4 | Archive repository with database persistence |

### Test Scenarios Covered

✅ **Success Paths**
- All methods tested with valid responses
- Proper data parsing and transformation
- Correct return types and values

✅ **Error Handling**
- 404 Not Found scenarios
- 403 Forbidden responses
- Invalid data handling

✅ **Authentication**
- JWT generation and signing
- Installation token exchange
- Token caching and reuse
- Token expiration handling

✅ **HTTP Communication**
- Correct paths and methods
- Proper headers (Authorization, Accept)
- Request body serialization
- Response deserialization

## Running the Tests

### Run All Integration Tests
```bash
dotnet test --filter "Category=Integration"
```

### Run Specific Feature Tests
```bash
# Repository operations only
dotnet test --filter "Feature=RepositoryOperations"

# Issue operations only
dotnet test --filter "Feature=IssueOperations"
```

### Run with Verbose Logging
```bash
dotnet test --filter "Category=Integration" --logger "console;verbosity=detailed"
```

For more commands, see the [Quick Reference](./quick-reference.md).

## Related Documentation

- [Testing Strategy](./testing-strategy.md) - Multi-level testing approach overview
- [Contract Tests](./contract-tests.md) - Level 3: API contract validation
- [GitHub Integration](../services/github-integration.md) - GitHub integration architecture and factory pattern
- [Quick Reference](./quick-reference.md) - Common commands and patterns

## Summary

**Integration tests ensure:**
1. ✅ Real HTTP communication works correctly
2. ✅ Authentication flow (JWT → Installation Token) functions properly
3. ✅ Request/response serialization is accurate
4. ✅ Error handling works for various HTTP status codes
5. ✅ Token caching reduces unnecessary API calls

**How they work:**
1. **WireMock.Net** creates a fake GitHub API server on localhost
2. **Real GitHubService** instance makes actual HTTP requests
3. **Real Octokit.GitHubClient** handles serialization/deserialization
4. **Real IMemoryCache** manages token caching
5. **FluentAssertions** validates results with readable syntax

**Key differences from other test levels:**

| Aspect | Unit Tests | Integration Tests | Contract Tests |
|--------|-----------|-------------------|----------------|
| **What's Real** | Logic only | HTTP + Service | HTTP + Service + Parsing |
| **What's Mocked** | All dependencies | GitHub API | GitHub API |
| **Tests** | Business logic | Communication | API structure |
| **Speed** | Fastest | Fast | Fast |
| **Catches** | Logic bugs | HTTP bugs | Contract changes |

This is **Level 2** of the multi-level testing strategy - positioned between unit tests (Level 1) and contract tests (Level 3).

