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

---

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

---

## Key Components

### Package Dependencies

```xml
<!-- Core Testing -->
<PackageReference Include="xunit" Version="2.5.3" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
<PackageReference Include="Bogus" Version="35.4.0" />

<!-- Integration Testing -->
<PackageReference Include="WireMock.Net" Version="1.5.59" />          <!-- HTTP mocking -->
<PackageReference Include="Testcontainers.MsSql" Version="3.7.0" />   <!-- Database containers -->
<PackageReference Include="Respawn" Version="6.2.1" />                <!-- Database reset -->

<!-- ASP.NET Core Integration -->
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.0" />

<!-- Dependencies -->
<PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="9.0.10" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.10" />
<PackageReference Include="Microsoft.Extensions.Options" Version="9.0.10" />
<PackageReference Include="NSubstitute" Version="5.1.0" />
```

---

## How It Works: Step-by-Step

### Step 1: Test Infrastructure Setup

The `GitHubApiFixture` provides shared test infrastructure using xUnit's `IClassFixture`:

```csharp
public class GitHubApiFixture : IAsyncLifetime
{
    public WireMockServer MockServer { get; private set; } = null!;
    public string BaseUrl => MockServer.Url!;
    
    public async Task InitializeAsync()
    {
        MockServer = WireMockServer.Start(new WireMockServerSettings
        {
            UseSSL = true,
            Port = 0 // Random port
        });
        await Task.CompletedTask;
    }
    
    public async Task DisposeAsync()
    {
        MockServer?.Stop();
        MockServer?.Dispose();
        await Task.CompletedTask;
    }
}
```

**What happens:**
1. **One WireMock server per test class** - Shared across all tests in the class
2. **HTTPS enabled** - Tests realistic SSL communication
3. **Random port** - Avoids port conflicts when running tests in parallel
4. **Automatic cleanup** - Server stops after all tests complete

### Step 2: Test Base Class

Every integration test inherits from `GitHubServiceIntegrationTestBase`:

```csharp
public abstract class GitHubServiceIntegrationTestBase : IClassFixture<GitHubApiFixture>, IAsyncLifetime
{
    protected readonly WireMockServer MockServer;
    protected readonly GitHubService Sut;              // System Under Test
    protected readonly IMemoryCache Cache;
    protected readonly ILogger<GitHubService> Logger;
    protected readonly Faker Faker;
    protected readonly GitHubAppOptions Options;
    protected readonly IGitHubClientFactory ClientFactory;
    
    protected GitHubServiceIntegrationTestBase(GitHubApiFixture fixture)
    {
        MockServer = fixture.MockServer;
        Faker = new Faker();
        Logger = Substitute.For<ILogger<GitHubService>>();
        Cache = new MemoryCache(new MemoryCacheOptions());
        
        Options = CreateTestOptions();
        Options.BaseUrl = MockServer.Url; // Point to WireMock!
        
        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(Options);
        ClientFactory = new GitHubClientFactory(MockServer.Url);
        
        Sut = new GitHubService(optionsWrapper, Logger, Cache, ClientFactory);
    }
}
```

**Key setup:**
- **SUT (System Under Test)**: Real `GitHubService` instance, not mocked
- **BaseUrl Override**: `Options.BaseUrl` points to WireMock instead of `api.github.com`
- **Real Dependencies**: Uses real `IMemoryCache` for token caching behavior
- **Substitute Logger**: Logs are captured but don't pollute test output
- **Test Credentials**: Generates valid RSA keys and fake GitHub App credentials

### Step 3: Mock GitHub App Authentication

Before each test that makes authenticated API calls, you must set up the authentication flow:

```csharp
protected void SetupGitHubAppAuthentication(DateTimeOffset? expiresAt = null)
{
    var tokenExpiry = expiresAt ?? DateTimeOffset.UtcNow.AddHours(1);
    var installationToken = Faker.Random.Hexadecimal(40, prefix: "ghs_");
    
    // Mock the installation token endpoint
    // POST /api/v3/app/installations/{installationId}/access_tokens
    // Note: /api/v3/ prefix is added by Octokit for Enterprise GitHub
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
                ""permissions"": {{
                    ""contents"": ""read"",
                    ""metadata"": ""read"",
                    ""issues"": ""write""
                }},
                ""repository_selection"": ""all""
            }}"));
}
```

**Why this is needed:**
1. `GitHubService` uses GitHub App authentication
2. First, it generates a JWT token signed with the private key
3. Then, it exchanges the JWT for an installation access token
4. This mock provides the installation token response

**Important Note:** Octokit treats custom `BaseUrl` as GitHub Enterprise mode and prepends `/api/v3/` to all paths.

### Step 4: Response Builder for Realistic Test Data

The `GitHubApiResponseBuilder` creates GitHub-compatible JSON responses:

```csharp
public class GitHubApiResponseBuilder
{
    private readonly Faker _faker;
    
    public string BuildRepositoryResponse(long id, string name, bool archived = false)
    {
        return $$"""
        {
          "id": {{id}},
          "name": "{{name}}",
          "full_name": "test-org/{{name}}",
          "private": false,
          "archived": {{archived.ToString().ToLower()}},
          "owner": {
            "login": "test-org",
            "id": 12345,
            "type": "Organization"
          }
        }
        """;
    }
    
    public string BuildIssueResponse(int number, string title, string label)
    {
        return $$"""
        {
          "id": {{_faker.Random.Int(1000000, 9999999)}},
          "number": {{number}},
          "title": "{{title}}",
          "body": "Test issue body",
          "state": "open",
          "labels": [{
            "id": {{_faker.Random.Int(1000, 9999)}},
            "name": "{{label}}"
          }],
          "html_url": "https://github.com/test-org/test-repo/issues/{{number}}"
        }
        """;
    }
    
    // ... more builders for files, workflows, teams, etc.
}
```

**Benefits:**
- **Realistic structure** - Matches actual GitHub API responses
- **Flexible** - Easy to customize for specific test scenarios
- **Bogus integration** - Generates realistic random data
- **Reusable** - Shared across all integration tests

### Step 5: Writing a Complete Integration Test

Here's a complete example with all the pieces together:

```csharp
[Trait("Category", "Integration")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "RepositoryOperations")]
public class RepositoryOperationsTests : GitHubServiceIntegrationTestBase
{
    private readonly GitHubApiResponseBuilder _responseBuilder;
    
    public RepositoryOperationsTests(GitHubApiFixture fixture) : base(fixture)
    {
        _responseBuilder = new GitHubApiResponseBuilder();
    }
    
    [Fact]
    public async Task GetOrganizationRepositoriesAsync_WhenCalled_ReturnsRepositories()
    {
        // Arrange
        SetupGitHubAppAuthentication();
        
        var repo1 = _responseBuilder.BuildRepositoryResponse(12345, "repo1", false);
        var repo2 = _responseBuilder.BuildRepositoryResponse(12346, "repo2", false);
        var repo3 = _responseBuilder.BuildRepositoryResponse(12347, "repo3", true);
        
        var repositoriesJson = $"[{repo1},{repo2},{repo3}]";
        
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/orgs/{Options.OrganizationName}/repos")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(repositoriesJson)
                .WithHeader("Content-Type", "application/json"));
        
        // Act
        var result = await Sut.GetOrganizationRepositoriesAsync();
        
        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result[0].Id.Should().Be(12345);
        result[0].Name.Should().Be("repo1");
        result[0].Archived.Should().BeFalse();
        result[2].Archived.Should().BeTrue();
    }
}
```

**Execution Flow:**

1. **Test Class Initialization** (once):
   - xUnit creates `GitHubApiFixture`
   - WireMock starts on `https://localhost:52341` (random port)

2. **Test Constructor** (per test):
   - Creates real `GitHubService` instance
   - Points service to WireMock URL
   - Injects real cache, logger, options

3. **Test InitializeAsync** (before each test):
   - WireMock resets (clears all stubs and logs)
   - Cache is fresh

4. **Arrange Phase**:
   - `SetupGitHubAppAuthentication()` - Mocks auth endpoint
   - `MockServer.Given()` - Mocks the repositories endpoint

5. **Act Phase**:
   ```
   GitHubService.GetOrganizationRepositoriesAsync()
     ↓
   Creates JWT token with RSA signature
     ↓
   POST /api/v3/app/installations/123/access_tokens (WireMock intercepts)
     ↓
   Receives fake token "ghs_abc123..."
     ↓
   Caches token in IMemoryCache
     ↓
   GET /api/v3/orgs/test-org/repos (WireMock intercepts)
     ↓
   Receives JSON array with 3 repositories
     ↓
   Octokit parses JSON into Repository objects
     ↓
   Returns List<Repository>
   ```

6. **Assert Phase**:
   - Verifies correct number of repositories
   - Validates property values match mock data
   - Uses FluentAssertions for readable assertions

7. **Test Cleanup** (after each test):
   - Cache disposed
   - WireMock logs available for debugging

---

## What Problems Does This Solve?

### Problem 1: Wrong HTTP Path

**Scenario:** You change a method to use a different GitHub API endpoint

```csharp
// Before: GET /repositories/{id}
// After:  GET /repos/{owner}/{repo}  (wrong!)
```

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

```csharp
[JsonProperty("archieved")] // Typo! Should be "archived"
public bool Archived { get; set; }
```

**Without integration tests:**
- Unit tests use in-memory objects
- Serialization never tested

**With integration tests:**
- Real Octokit parses real JSON
- Property remains `false` when it should be `true`
- Assertion fails: `result.Archived.Should().BeTrue()`

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

### Problem 5: Error Handling

**Scenario:** GitHub returns 403 Forbidden or 404 Not Found

**Without integration tests:**
- You don't test HTTP error paths
- App crashes on unexpected responses

**With integration tests:**
- Mock different HTTP status codes
- Verify correct exceptions thrown
- Test retry logic

---

## Test Organization

```
10xGitHubPolicies.Tests.Integration/
├── Fixtures/
│   └── GitHubApiFixture.cs              # Shared WireMock server setup
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
└── 10xGitHubPolicies.Tests.Integration.csproj
```

**Organization principles:**
- **Fixtures/** - Shared test infrastructure
- **Builders/** - Test data generation
- **GitHub/** - Tests grouped by feature area
- One test class per feature area
- Base class provides common setup

---

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

# Token caching only
dotnet test --filter "Feature=TokenCaching"
```

### Run Specific Service Tests

```bash
# All GitHubService tests
dotnet test --filter "Service=GitHubService"
```

### Run with Verbose Logging

```bash
dotnet test --filter "Category=Integration" --logger "console;verbosity=detailed"
```

---

## Writing New Integration Tests

### Step 1: Identify the Feature to Test

Determine which `GitHubService` method you want to test and what scenarios to cover:
- Happy path (successful response)
- Error cases (404, 403, etc.)
- Edge cases (empty results, null values)

### Step 2: Create or Extend Test Class

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

### Step 3: Write Test Using AAA Pattern

```csharp
/// <summary>
/// TC-MYFEATURE-001: MyMethod - Success Case
/// Verifies that MyMethod returns expected data when API responds successfully
/// </summary>
[Fact]
public async Task MyMethod_WhenApiRespondsSuccessfully_ReturnsExpectedData()
{
    // Arrange
    SetupGitHubAppAuthentication();
    
    var mockResponse = new { /* GitHub API response structure */ };
    
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
    result.SomeProperty.Should().Be("expected-value");
}
```

### Step 4: Add Response Builder Method

If you need complex JSON responses, add a builder method:

```csharp
public string BuildMyFeatureResponse(int id, string name)
{
    return $$"""
    {
      "id": {{id}},
      "name": "{{name}}",
      "nested": {
        "field": "{{_faker.Lorem.Word()}}"
      }
    }
    """;
}
```

### Step 5: Test Error Cases

```csharp
[Fact]
public async Task MyMethod_WhenResourceNotFound_ThrowsNotFoundException()
{
    // Arrange
    SetupGitHubAppAuthentication();
    
    MockServer
        .Given(Request.Create()
            .WithPath("/api/v3/my/endpoint/999"))
        .RespondWith(Response.Create()
            .WithStatusCode(404)
            .WithBody("{\"message\": \"Not Found\"}"));
    
    // Act
    var act = async () => await Sut.MyMethodAsync(999);
    
    // Assert
    await act.Should().ThrowAsync<NotFoundException>();
}
```

---

## Best Practices

### 1. Always Call SetupGitHubAppAuthentication()

Every test that makes GitHub API calls must mock authentication:

```csharp
// ✅ Good
[Fact]
public async Task MyTest()
{
    SetupGitHubAppAuthentication();
    // ... rest of test
}

// ❌ Bad - Will fail with authentication error
[Fact]
public async Task MyTest()
{
    // Missing authentication setup!
}
```

### 2. Use Descriptive Test Names

Follow the pattern: `MethodName_Scenario_ExpectedResult`

```csharp
// ✅ Good
[Fact]
public async Task GetRepository_WhenRepositoryExists_ReturnsRepositoryDetails()

[Fact]
public async Task GetRepository_WhenRepositoryNotFound_ThrowsNotFoundException()

// ❌ Bad
[Fact]
public async Task Test1()

[Fact]
public async Task GetRepositoryTest()
```

### 3. Document Test Purpose

Use XML comments to explain what the test verifies:

```csharp
/// <summary>
/// TC-REPO-001: GetRepositorySettingsAsync - Success
/// Verifies that GetRepositorySettingsAsync returns repository details
/// when the repository exists and user has access
/// </summary>
[Fact]
public async Task GetRepositorySettingsAsync_WhenRepositoryExists_ReturnsSettings()
```

### 4. Test Both Success and Failure Paths

For every method, test:
- ✅ Happy path (200 OK)
- ✅ Not found (404)
- ✅ Unauthorized (401/403) if applicable
- ✅ Bad request (400) if applicable
- ✅ Rate limit (429) if applicable

### 5. Use FluentAssertions for Readable Assertions

```csharp
// ✅ Good - Readable and clear
result.Should().NotBeNull();
result.Count.Should().BeGreaterThan(0);
result.First().Name.Should().Be("expected-name");

// ❌ Bad - Harder to read failure messages
Assert.NotNull(result);
Assert.True(result.Count > 0);
Assert.Equal("expected-name", result.First().Name);
```

### 6. Keep Tests Independent

Each test should:
- Set up its own mocks
- Not depend on execution order
- Not share state with other tests

```csharp
// ✅ Good - Self-contained
[Fact]
public async Task Test1()
{
    SetupGitHubAppAuthentication();
    MockServer.Given(...).RespondWith(...);
    // test code
}

// ❌ Bad - Depends on another test
private static string _sharedToken;

[Fact]
public async Task Test1() { _sharedToken = "abc"; }

[Fact]
public async Task Test2() { /* uses _sharedToken */ }
```

### 7. Use Response Builder for Complex JSON

```csharp
// ✅ Good - Reusable and maintainable
var repoJson = _responseBuilder.BuildRepositoryResponse(123, "my-repo");
MockServer.Given(...).RespondWith(Response.Create().WithBody(repoJson));

// ❌ Bad - Hardcoded and error-prone
var repoJson = @"{""id"": 123, ""name"": ""my-repo"", ... 50 more lines ...}";
```

### 8. Remember the /api/v3/ Path Prefix

Octokit adds `/api/v3/` prefix when using custom `BaseUrl`:

```csharp
// ✅ Correct - Include /api/v3/ prefix
MockServer
    .Given(Request.Create().WithPath("/api/v3/repositories/123"))
    .RespondWith(...);

// ❌ Wrong - Missing prefix, test will fail
MockServer
    .Given(Request.Create().WithPath("/repositories/123"))
    .RespondWith(...);
```

### 9. Use Traits for Test Organization

```csharp
[Trait("Category", "Integration")]      // Test level
[Trait("Service", "GitHubService")]     // Component being tested
[Trait("Feature", "RepositoryOperations")]  // Feature area
public class RepositoryOperationsTests : GitHubServiceIntegrationTestBase
```

### 10. Clean Up Test Data

The base class handles cleanup automatically via `IAsyncLifetime`:

```csharp
public virtual Task InitializeAsync()
{
    MockServer.Reset();  // Clear all stubs and logs
    return Task.CompletedTask;
}

public virtual async Task DisposeAsync()
{
    Cache?.Dispose();  // Clean up cache
    await Task.CompletedTask;
}
```

---

## Debugging Tips

### 1. Log WireMock Requests

Use the helper method to see all HTTP requests:

```csharp
[Fact]
public async Task MyTest()
{
    // Arrange & Act
    // ...
    
    // Debug - View all requests/responses
    LogWireMockRequests();
    
    // Assert
    // ...
}
```

Output example:
```
=== WireMock Request Log ===
[14:32:15.123] POST /api/v3/app/installations/123456/access_tokens
  URL: https://localhost:52341/api/v3/app/installations/123456/access_tokens
  Header: Authorization = Bearer eyJhbGc...
  Response: 201
---
[14:32:15.456] GET /api/v3/orgs/test-org/repos
  URL: https://localhost:52341/api/v3/orgs/test-org/repos
  Header: Authorization = token ghs_abc123...
  Response: 200
---
=== End Request Log ===
```

### 2. Inspect Specific Request

```csharp
var requests = MockServer.LogEntries.ToList();
var repoRequest = requests.FirstOrDefault(r => r.RequestMessage.Path.Contains("repos"));

Console.WriteLine($"Path: {repoRequest.RequestMessage.Path}");
Console.WriteLine($"Method: {repoRequest.RequestMessage.Method}");
Console.WriteLine($"Body: {repoRequest.RequestMessage.Body}");
```

### 3. Verify Request Was Made

```csharp
// Assert that archive request was sent
var requests = MockServer.LogEntries;
requests.Should().ContainSingle(r => 
    r.RequestMessage.Path.Contains("repositories/123") && 
    r.RequestMessage.Method == "PATCH");
```

### 4. Check Authentication Token

```csharp
var authRequest = MockServer.LogEntries
    .FirstOrDefault(r => r.RequestMessage.Path.Contains("access_tokens"));

authRequest.Should().NotBeNull("authentication request should have been made");
authRequest.RequestMessage.Headers["Authorization"]
    .Should().Contain("Bearer", "should use JWT authentication");
```

### 5. Test Token Caching Behavior

```csharp
[Fact]
public async Task MultipleRequests_ReusesCachedToken()
{
    // Arrange
    SetupGitHubAppAuthentication();
    MockServer.Given(...).RespondWith(...);
    
    // Act - Make two requests
    await Sut.GetOrganizationRepositoriesAsync();
    await Sut.GetOrganizationRepositoriesAsync();
    
    // Assert - Should only request token once
    var tokenRequests = MockServer.LogEntries
        .Count(r => r.RequestMessage.Path.Contains("access_tokens"));
    
    tokenRequests.Should().Be(1, "token should be cached after first request");
}
```

### 6. Use VS Code Test Explorer

The traits allow filtering in Test Explorer:
- Filter by `Category=Integration`
- Filter by `Feature=RepositoryOperations`
- Run/debug individual tests

### 7. Run Single Test with Logging

```bash
dotnet test --filter "FullyQualifiedName~GetOrganizationRepositoriesAsync_WhenCalled_ReturnsRepositories" --logger "console;verbosity=detailed"
```

---

## Test Coverage

Current integration test coverage:

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

---

## Related Documentation

- [Testing Strategy](./testing-strategy.md) - Multi-level testing approach overview
- [Contract Tests](./testing-contract-tests.md) - Level 3: API contract validation
- [GitHub Integration](./github-integration.md) - GitHubService usage guide
- [GitHub Client Factory](./github-client-factory.md) - Factory pattern for testability

---

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

---

## Additional Notes

### When to Write Integration Tests

Write integration tests when you:
- ✅ Add new GitHubService methods
- ✅ Change authentication logic
- ✅ Modify HTTP request/response handling
- ✅ Add error handling for new scenarios
- ✅ Change how data is serialized/deserialized

### When NOT to Write Integration Tests

Skip integration tests for:
- ❌ Pure business logic (use unit tests)
- ❌ UI components (use component tests)
- ❌ Database operations (use database integration tests)
- ❌ End-to-end workflows (use E2E tests)

### Maintenance Tips

1. **Keep WireMock stubs realistic** - Use actual GitHub API response formats
2. **Update tests when GitHub API changes** - Monitor GitHub changelog
3. **Review test failures carefully** - Integration test failures often indicate real bugs
4. **Don't over-mock** - Test as much real code as possible
5. **Keep tests fast** - Integration tests should run in milliseconds, not seconds

### Performance Considerations

- **Parallel execution**: Tests can run in parallel (WireMock uses random ports)
- **Shared fixtures**: One WireMock server per test class reduces startup overhead
- **Fast assertions**: FluentAssertions compiles expressions efficiently
- **No I/O**: No real network calls or disk access

**Typical execution time:** 33 tests complete in < 5 seconds

