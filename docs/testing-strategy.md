# Testing Strategy

## Overview

The 10x GitHub Policy Enforcer employs a comprehensive multi-level testing strategy to ensure code quality, reliability, and maintainability. This document outlines our testing approach, tooling, and best practices.

## Testing Philosophy

Our testing strategy follows the testing pyramid, with a strong emphasis on fast, reliable unit tests and progressively fewer integration, contract, component, and end-to-end tests as we move up the pyramid.

```
         /\
        /  \    E2E (5 tests)
       /----\   - Playwright
      /      \  - Critical workflows only
     /--------\  
    /          \ Component (20-30 tests)
   /------------\ - bUnit
  /              \ - UI component testing
 /----------------\
/                  \ Integration (40-60 tests)
/--------------------\ - Testcontainers + Respawn
|                    | - WireMock.Net
|--------------------| - WebApplicationFactory
|                    |
| Unit (200+ tests)  | Contract (10-15 tests)
|                    | - NJsonSchema + Verify.NET
----------------------  - GitHub API contracts
```

## Testing Levels

### Level 1: Unit Tests
**Purpose**: Test individual components and business logic in isolation  
**Technology**: xUnit + NSubstitute + FluentAssertions + Bogus  
**Coverage Target**: 85-90% code coverage  
**Speed**: Very fast (< 100ms per test)

**When to Use**:
- Service business logic
- Algorithm testing
- Validation logic
- Helper methods
- Edge cases

**Example**:
```csharp
[Fact]
public async Task GetConfigAsync_WhenConfigExists_ReturnsValidConfig()
{
    // Arrange
    var mockGitHubService = Substitute.For<IGitHubService>();
    mockGitHubService.GetFileContentAsync(Arg.Any<string>(), Arg.Any<string>())
        .Returns("authorized_team: 'org/team'");
    
    var sut = new ConfigurationService(mockGitHubService, _logger, _cache);
    
    // Act
    var result = await sut.GetConfigAsync();
    
    // Assert
    result.Should().NotBeNull();
    result.AccessControl.AuthorizedTeam.Should().Be("org/team");
}
```

### Level 2: Integration Tests 
**Purpose**: Test interaction between components, database operations, and external API integrations  
**Technology**: Testcontainers + Respawn + WireMock.Net + WebApplicationFactory  
**Speed**: Slow (1-5 seconds per test)  
**When to Use**:
- Database operations
- GitHub API interactions (HTTP mocking)
- End-to-end workflows within services
- Cross-service communication

**Key Features**:
- **Testcontainers**: Manages ephemeral SQL Server instances in Docker for isolated database testing
- **Respawn**: Fast database cleanup between tests to ensure test isolation
- **WireMock.Net**: HTTP-level mocking for GitHub API to simulate rate limits, errors, and edge cases
- **GitHubClientFactory Pattern**: Enables redirecting Octokit API calls to WireMock for testing

**Implemented Test Coverage** (33 tests):
- File Operations: 5 tests
- Repository Operations: 7 tests
- Issue Operations: 5 tests
- Workflow Permissions: 3 tests
- Rate Limit Handling: 5 tests
- Token Caching: 3 tests
- Team Membership: 5 tests

**Example**:
```csharp
public class GitHubServiceIntegrationTests : GitHubServiceIntegrationTestBase
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetOrganizationRepositoriesAsync_ReturnsRepositories()
    {
        // Arrange
        SetupGitHubAppAuthentication();
        SetupRepositoryListResponse();
        
        // Act
        var repositories = await Sut.GetOrganizationRepositoriesAsync();
        
        // Assert
        repositories.Should().NotBeEmpty();
        repositories.Should().HaveCount(2);
    }
}
```

### Level 3: Contract Tests 
**Purpose**: Detect breaking changes in the GitHub API contract  
**Technology**: NJsonSchema + Verify.NET + WireMock.Net  
**Speed**: Slow (1-3 seconds per test)  

> **📖 For a detailed guide**, see **[Contract Testing Documentation](./testing-contract-tests.md)**

**When to Use**:
- GitHub API response validation
- Detecting breaking changes
- API versioning protection
- Response structure documentation

**Key Features**:
- **NJsonSchema**: JSON Schema validation for critical API responses
- **Verify.NET**: Snapshot testing to detect structural changes
- **WireMock.Net**: Mock GitHub API responses for consistent testing
- **JSON Schemas**: Defined schemas for repository, issue, and workflow permissions responses

**Implemented Test Coverage** (11 tests):
- Repository Response Schema: 3 tests (structure, required fields, field types)
- Issue Response Schema: 2 tests (structure, required fields)
- Workflow Permissions Schema: 1 test (structure validation)
- API Snapshots: 5 tests (repository list, single repository, issue creation, workflow permissions, file content)

**Example**:
```csharp
[Fact]
[Trait("Category", "Contract")]
public async Task GetRepository_ResponseMatchesSchema()
{
    // Arrange
    SetupGitHubAppAuthentication();
    var schema = await JsonSchema.FromFileAsync("Schemas/github-repository-response.json");
    
    // Act
    var repository = await Sut.GetRepositorySettingsAsync(12345);
    var json = JsonSerializer.Serialize(repository);
    
    // Assert
    var errors = schema.Validate(json);
    errors.Should().BeEmpty("GitHub API response should match the expected schema");
}

[Fact]
[Trait("Category", "Contract")]
public async Task GetRepositoryList_SnapshotStable()
{
    // Arrange
    SetupGitHubAppAuthentication();
    SetupRepositoryListResponse();
    
    // Act
    var repositories = await Sut.GetOrganizationRepositoriesAsync();
    
    // Assert - Snapshot testing with scrubbing of volatile fields
    await Verify(repositories)
        .ScrubMembers("Id", "CreatedAt", "UpdatedAt", "PushedAt");
}
```

### Level 4: Blazor Component Tests 
**Purpose**: Test UI component rendering and user interactions  
**Technology**: bUnit + NSubstitute + FluentAssertions  
**Speed**: Fast (< 500ms per test)  

**When to Use**:
- Blazor Razor component rendering
- User interaction testing
- Component state management
- Fluent UI component integration

**Implemented Test Files**:
- `Components/Pages/IndexTests.cs` (5 tests) - Dashboard component
- `Components/Pages/OnboardingTests.cs` (5 tests) - Configuration setup
- `Components/Pages/AccessDeniedTests.cs` (3 tests) - Authorization
- `Components/Pages/LoginTests.cs` (3 tests) - Authentication
- `Components/Shared/MainLayoutTests.cs` (2 tests) - Layout
- `Components/Shared/RedirectToLoginTests.cs` (1 test) - Navigation
- `Components/Integration/AuthorizationFlowTests.cs` (3 tests) - Auth flows

**Example**:
```csharp
public class DashboardTests : AppTestContext
{
    [Fact]
    public async Task Dashboard_Renders_ComplianceMetrics()
    {
        // Arrange
        var viewModel = TestDataBuilder.CreateDashboardViewModel();
        DashboardService.GetDashboardViewModelAsync().Returns(viewModel);
        
        // Act
        var cut = RenderComponent<_10xGitHubPolicies.App.Pages.Index>();
        
        // Assert
        cut.Find(".kpi-value").TextContent.Should().Contain("85.50%");
    }
}
```

**Known Limitations**:
- Complex Fluent UI interactions (FluentDataGrid, scan button state) have timing issues and complex JSInterop requirements
- Logout functionality test skipped (requires full authentication services setup - better suited for E2E tests)
- Tests focus on core rendering and navigation flows rather than detailed UI interactions

### Level 5: End-to-End Tests
**Purpose**: Validate critical user workflows in a real browser environment  
**Technology**: Playwright (.NET)  
**Speed**: Very slow (10-60 seconds per test)

> **📖 For a detailed guide**, see **[E2E Testing Documentation](./testing-e2e-tests.md)**

**When to Use** (sparingly):
- Critical user workflows only (< 10 tests)
- Complete policy enforcement workflow validation
- UI interaction testing with real browser
- Pre-production smoke tests

**Key Features**:
- **Dual-Host Architecture**: Test host for data creation + manually running web application
- **Test Mode Integration**: Uses Test Mode for authentication bypass (see [Test Mode](#test-mode) section)
- **Page Object Model**: `DashboardPage` encapsulates UI interactions
- **Test Data Management**: `RepositoryHelper` and `DatabaseHelper` for setup/cleanup
- **Screenshot Capture**: Automatic screenshots for debugging failures

**Architecture**:
- **Test Host**: Minimal .NET host providing GitHub API services and database access
- **Web Application**: Manually running application at `https://localhost:7040/` for UI testing
- **Separation**: Test data creation separate from UI testing for better debugging

**Test Mode Requirement**:
E2E tests require Test Mode to be enabled in the web application configuration:
```json
{
  "TestMode": {
    "Enabled": true
  }
}
```

**Example**:
```csharp
[Fact]
[Trait("Category", "E2E-Workflow")]
public async Task CompletePolicyEnforcementWorkflow_ShouldWorkEndToEnd()
{
    // Arrange - Create test repository via test host
    var repository = await RepositoryHelper.CreateTestRepositoryAsync("e2e-test-repo");
    
    // Act - Test UI via manually running web application
    var page = await Browser.NewPageAsync();
    var dashboardPage = new DashboardPage(page);
    
    await dashboardPage.GotoAsync();
    await dashboardPage.TriggerScanAsync();
    await dashboardPage.WaitForScanCompletionAsync();
    
    // Assert - Verify results via database
    var violations = await DatabaseHelper.GetPolicyViolationsAsync(repository.Id);
    violations.Should().NotBeEmpty();
    
    // Cleanup
    await RepositoryHelper.DeleteTestRepositoryAsync(repository.Name);
}
```

**Setup Requirements**:
1. Web application must be running manually: `dotnet run --launch-profile https`
2. Database must be available (Docker Compose)
3. Test Mode must be enabled in `appsettings.Development.json`
4. Playwright browsers must be installed: `pwsh bin/Debug/net8.0/playwright.ps1 install chromium`

## Testing Tools

### Core Testing Framework
- **xUnit** (v2.6+): Unit and integration testing framework
- **FluentAssertions** (v6.12+): Readable, expressive assertions
- **NSubstitute** (v5.1+): Clean, simple mocking framework

### Specialized Testing
- **bUnit** (v1.28+): Blazor component testing
- **Playwright** (latest): End-to-end browser testing
- **Testcontainers.MsSql** (v3.7+): SQL Server containerization
- **Respawn** (v6.2+): Fast database cleanup between tests
- **Bogus** (v35.4+): Realistic fake data generation

### Integration & Mocking
- **WireMock.Net** (v1.5+): HTTP-level mocking for GitHub API
- **Hangfire.InMemory** (v0.10+): In-memory storage for Hangfire testing
- **WebApplicationFactory**: ASP.NET Core integration testing (built-in)

### Contract Testing
- **NJsonSchema** (v11.0+): JSON Schema validation for API responses
- **Verify.NET** (v25.0+): Snapshot testing for detecting structural changes

### Code Quality
- **Coverlet** (v6.0+): Code coverage analysis (built into .NET SDK)

## Project Structure

```
10xGitHubPolicies/
├── 10xGitHubPolicies.App/              # Application code
├── 10xGitHubPolicies.Tests/            # Unit tests
│   ├── Services/
│   │   ├── ConfigurationServiceTests.cs
│   │   ├── GitHubServiceTests.cs
│   │   └── PolicyEvaluationServiceTests.cs
│   └── ...
├── 10xGitHubPolicies.Tests.Integration/ # Integration tests
│   ├── Database/
│   ├── GitHub/
│   └── Workflows/
├── 10xGitHubPolicies.Tests.Contracts/   # Contract tests
│   ├── Schemas/
│   └── Snapshots/
├── 10xGitHubPolicies.Tests.E2E/        # E2E tests (Playwright)
│   ├── Tests/
│   │   └── Workflow/
│   │       └── WorkflowTests.cs
│   ├── Pages/
│   │   └── DashboardPage.cs
│   ├── Helpers/
│   │   ├── RepositoryHelper.cs
│   │   └── DatabaseHelper.cs
│   └── README.md
```

## Running Tests

### All Tests
```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
```

### By Category
```bash
# Unit tests only (fast feedback)
dotnet test --filter Category=Unit

# Integration tests
dotnet test --filter Category=Integration

# Contract tests
dotnet test --filter Category=Contract

# Component tests (22 tests, 100% passing)
dotnet test --filter Category=Component

# E2E tests (requires web application running)
dotnet test 10xGitHubPolicies.Tests.E2E
dotnet test --filter Category=E2E-Workflow
```

### By Project
```bash
# Unit tests
dotnet test 10xGitHubPolicies.Tests

# Integration tests
dotnet test 10xGitHubPolicies.Tests.Integration

# Component tests
dotnet test 10xGitHubPolicies.Tests.Components

# E2E tests
dotnet test 10xGitHubPolicies.Tests.E2E
```

## CI/CD Pipeline Order

```yaml
1. Unit Tests        (always, fast feedback)
   ↓
2. Lint/Format      (code quality gates)
   ↓
3. Integration Tests (on main branch)
   ↓
4. Component Tests   (UI validation)
   ↓
5. Contract Tests    (API stability check)
   ↓
6. E2E Tests        (smoke tests before deploy)
   ↓
7. Deploy           (if all pass)
```

## Test Coverage Goals

| Test Type | Target Coverage | Reality Check | Status |
|-----------|----------------|---------------|--------|
| Unit | 85-90% | Focus on business logic, not getters/setters | ✅ Implemented |
| Integration | All critical paths | Database operations, API calls, workflows | ✅ Implemented (33 tests) |
| Contract | Critical APIs only | 5-10 endpoints maximum | ✅ Implemented (11 tests) |
| Component | Key UI components | Dashboard, forms, navigation | ✅ Implemented (22 tests) |
| E2E | 5-10 critical workflows | Authentication, scan, view results | ✅ Implemented (Playwright tests) |

## Testing Best Practices

### General Guidelines
- ✅ Write unit tests first (fastest feedback)
- ✅ Integration tests for cross-component behavior
- ✅ Contract tests to catch API breaking changes
- ✅ Component tests for UI validation
- ✅ E2E tests sparingly (slow and expensive)
- ✅ Run tests in CI/CD before merging
- ✅ Keep tests independent and deterministic
- ❌ Don't test framework code
- ❌ Don't over-engineer test infrastructure
- ❌ Don't skip tests "just this once"

### Naming Conventions
```csharp
// Unit tests
[Fact]
public async Task MethodName_WhenCondition_ExpectedBehavior()

// Integration tests
[Fact]
public async Task Feature_Scenario_ExpectedResult()

// Contract tests
[Fact]
public async Task Endpoint_ContractStability()

// Component tests
[Fact]
public void Component_Action_ExpectedResult()
```

### Test Isolation
- Each test should be independent
- Use test fixtures for shared setup
- Clean up resources in `IAsyncLifetime.DisposeAsync()`
- Use `Respawn` for database cleanup in integration tests

### GitHub API Testing Strategy

The application relies heavily on GitHub API integration. Testing is performed at multiple levels:

**Level 1: Unit Tests (Fast, Isolated)**
- Full mocking with NSubstitute
- Test service logic without network calls
- Fast feedback for business logic
- Example: `GitHubService` token caching logic, error handling

**Level 2: Integration Tests (HTTP Mocking)**
- WireMock.Net for HTTP-level mocking
- Test actual HTTP interactions without real API calls
- Simulate rate limits, errors, edge cases
- Example: Rate limit handling, retry logic, timeout scenarios

**Level 3: Contract Tests (Schema Validation)**
- JSON Schema validation for critical responses
- WireMock.Net recording mode for capturing real responses
- Verify.NET snapshot testing for response structure stability
- Catch breaking API changes early
- Example: Repository metadata structure, issue creation responses

**Level 5: E2E Tests (Real API + Browser)**
- Test complete user workflows in real browser environment
- Uses Test Mode for authentication bypass
- Real GitHub API calls via test host for data creation
- UI testing against manually running web application
- Example: Complete policy enforcement workflow from UI to database
- **Priority**: MEDIUM - Use for critical workflows only due to slow execution

## Quick Decision Tree

```
Need to test something?
│
├─ Is it business logic?
│  └─ → Unit Test
│
├─ Does it interact with database/API?
│  └─ → Integration Test
│
├─ Is it an external API response?
│  └─ → Contract Test
│
├─ Is it a Blazor UI component?
│  └─ → Component Test
│
└─ Is it a critical user workflow?
   └─ → E2E Test
```

## Resources

### Documentation
- **Test Plan**: See `.ai/test-plan.md` for comprehensive test scenarios
- **Tech Stack**: See `.ai/tech-stack.md` for testing tool rationale
- **Cursor Rules**: See `.cursor/rules/testing-*.mdc` for detailed testing guidelines

### External Resources
- [xUnit Documentation](https://xunit.net/)
- [bUnit Documentation](https://bunit.dev/)
- [Testcontainers Documentation](https://dotnet.testcontainers.org/)
- [WireMock.Net Documentation](https://github.com/WireMock-Net/WireMock.Net)
- [Playwright Documentation](https://playwright.dev/)
- [Verify.NET Documentation](https://github.com/VerifyTests/Verify)
- [NJsonSchema Documentation](https://github.com/RicoSuter/NJsonSchema)

## Common Commands Cheat Sheet

```bash
# Development
dotnet test --filter FullyQualifiedName~MyTest   # Run specific test

# Coverage
dotnet test /p:CollectCoverage=true              # Generate coverage
reportgenerator -reports:coverage.cobertura.xml  # View report

# CI/CD
dotnet test --filter Category!=E2E               # All except E2E
dotnet test --logger "trx;LogFileName=results.trx" # CI-friendly output

# Pre-Push Validation
./pre-push-test.sh                                # Run complete test suite including E2E
./test-workflow-local.sh                          # Run workflow tests (lint, unit, component, integration, contract)

# E2E (requires web application running first)
dotnet test 10xGitHubPolicies.Tests.E2E         # Run all E2E tests
dotnet test --filter Category=E2E-Workflow      # Run specific category

# Cleanup
docker ps -a | grep testcontainers | awk '{print $1}' | xargs docker rm -f
```

## Performance Context

**Application Profile:**
- **Expected Users**: Maximum 50 concurrent users (low load)
- **Repository Volume**: Up to 10,000 repositories per organization (high volume)
- **Critical Bottleneck**: GitHub API rate limits (5,000 requests/hour)
- **Primary Concern**: Repository scan performance, not user load

**Testing Focus:**
Given the low user count but high repository volume, performance testing should focus on:
1. ✅ Repository scan throughput and rate limit management
2. ✅ Database query performance with large datasets
3. ✅ Background job processing capacity
4. ❌ NOT traditional load testing (50 users is negligible for Blazor Server)


