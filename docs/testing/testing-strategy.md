# Testing Strategy

## Overview

The 10x GitHub Policy Enforcer employs a comprehensive multi-level testing strategy to ensure code quality, reliability, and maintainability. This document provides a detailed overview of our testing approach.

## Testing Philosophy

Our testing strategy follows the testing pyramid, with a strong emphasis on fast, reliable unit tests and progressively fewer integration, contract, component, and end-to-end tests as we move up the pyramid.

```
         /\
        /  \    E2E (5 tests)
       /----\   - Playwright
      /      \  - Critical workflows only
     /--------\  
    /          \ Component (22 tests)
   /------------\ - bUnit
  /              \ - UI component testing
 /----------------\
/                  \ Integration (33 tests)
/--------------------\ - SQLite in-memory + WireMock.Net
||                    | - Fast, no Docker required
||--------------------| - WebApplicationFactory
||                    |
|| Unit (200+ tests)  | Contract (11 tests)
||                    | - NJsonSchema + Verify.NET
----------------------  - GitHub API contracts
```

## Testing Levels

### Level 1: Unit Tests
**Purpose**: Test individual components and business logic in isolation  
**Technology**: xUnit + NSubstitute + FluentAssertions + Bogus  
**Coverage Target**: 85-90% code coverage  
**Speed**: Very fast (< 100ms per test)  
**Location**: `10xGitHubPolicies.Tests/`

**When to Use**:
- Service business logic
- Algorithm testing
- Validation logic
- Helper methods
- Edge cases

**Example**: See `Tests/Services/GitHub/GitHubServiceTests.cs` for complete examples.

### Level 2: Integration Tests
**Purpose**: Test interaction between components, database operations, and external API integrations  
**Technology**: Testcontainers + Respawn + WireMock.Net + WebApplicationFactory  
**Speed**: Slow (1-5 seconds per test)  
**Location**: `10xGitHubPolicies.Tests.Integration/`

**When to Use**:
- Database operations
- GitHub API interactions (HTTP mocking)
- End-to-end workflows within services
- Cross-service communication

**Key Features**:
- **SQLite In-Memory**: Fast, lightweight in-memory database for isolated testing (no Docker required)
- **Manual Database Cleanup**: Fast database cleanup between tests to ensure test isolation
- **WireMock.Net**: HTTP-level mocking for GitHub API to simulate rate limits, errors, and edge cases
- **GitHubClientFactory Pattern**: Enables redirecting Octokit API calls to WireMock for testing

**Test Coverage** (33 tests):
- File Operations: 5 tests
- Repository Operations: 7 tests
- Issue Operations: 5 tests
- Workflow Permissions: 3 tests
- Rate Limit Handling: 5 tests
- Token Caching: 3 tests
- Team Membership: 5 tests
- Action Service (Archive): 4 tests

**Examples**:
- `Tests.Integration/GitHub/RepositoryOperationsTests.cs` - GitHubService integration tests
- `Tests.Integration/Action/ActionServiceArchiveTests.cs` - ActionService with database integration

**Detailed Guide**: [Integration Tests Guide](./integration-tests.md)

### Level 3: Contract Tests
**Purpose**: Detect breaking changes in the GitHub API contract  
**Technology**: NJsonSchema + Verify.NET + WireMock.Net  
**Speed**: Slow (1-3 seconds per test)  
**Location**: `10xGitHubPolicies.Tests.Contracts/`

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

**Test Coverage** (11 tests):
- Repository Response Schema: 3 tests
- Issue Response Schema: 2 tests
- Workflow Permissions Schema: 1 test
- Archive Repository Response: 1 test
- API Snapshots: 5 tests

**Example**: See `Tests.Contracts/GitHub/RepositoryResponseContractTests.cs` for complete examples.

**Detailed Guide**: [Contract Tests Guide](./contract-tests.md)

### Level 4: Blazor Component Tests
**Purpose**: Test UI component rendering and user interactions  
**Technology**: bUnit + NSubstitute + FluentAssertions  
**Speed**: Fast (< 500ms per test)  
**Location**: `10xGitHubPolicies.Tests/Components/`

**When to Use**:
- Blazor Razor component rendering
- User interaction testing
- Component state management
- Fluent UI component integration

**Test Coverage** (22 tests):
- `Components/Pages/IndexTests.cs` (5 tests) - Dashboard component
- `Components/Pages/OnboardingTests.cs` (5 tests) - Configuration setup
- `Components/Pages/AccessDeniedTests.cs` (3 tests) - Authorization
- `Components/Pages/LoginTests.cs` (3 tests) - Authentication
- `Components/Shared/MainLayoutTests.cs` (2 tests) - Layout
- `Components/Shared/RedirectToLoginTests.cs` (1 test) - Navigation
- `Components/Integration/AuthorizationFlowTests.cs` (3 tests) - Auth flows

**Example**: See `Tests/Components/Pages/IndexTests.cs` for complete examples.

**Known Limitations**:
- Complex Fluent UI interactions (FluentDataGrid, scan button state) have timing issues and complex JSInterop requirements
- Logout functionality test skipped (requires full authentication services setup - better suited for E2E tests)
- Tests focus on core rendering and navigation flows rather than detailed UI interactions

### Level 5: End-to-End Tests
**Purpose**: Validate critical user workflows in a real browser environment  
**Technology**: Playwright (.NET)  
**Speed**: Very slow (10-60 seconds per test)  
**Location**: `10xGitHubPolicies.Tests.E2E/`

**When to Use** (sparingly):
- Critical user workflows only (< 10 tests)
- Complete policy enforcement workflow validation
- UI interaction testing with real browser
- Pre-production smoke tests

**Key Features**:
- **Dual-Host Architecture**: Test host for data creation + manually running web application
- **Test Mode Integration**: Uses Test Mode for authentication bypass
- **Page Object Model**: `DashboardPage` encapsulates UI interactions
- **Test Data Management**: `RepositoryHelper` and `DatabaseHelper` for setup/cleanup
- **Screenshot Capture**: Automatic screenshots for debugging failures

**Architecture**:
- **Test Host**: Minimal .NET host providing GitHub API services and database access
- **Web Application**: Manually running application at `https://localhost:7040/` for UI testing
- **Separation**: Test data creation separate from UI testing for better debugging

**Test Mode Requirement**:
E2E tests require Test Mode to be enabled in the web application configuration. See [E2E Tests Guide](./e2e-tests.md) for details.

**Example**: See `Tests.E2E/Tests/Workflow/WorkflowTests.cs` for complete examples.

**Detailed Guide**: [E2E Tests Guide](./e2e-tests.md)

## GitHub API Testing Strategy

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

## Test Coverage Goals

| Test Type | Target Coverage | Reality Check | Status |
|-----------|----------------|---------------|--------|
| Unit | 85-90% | Focus on business logic, not getters/setters | ✅ Implemented |
| Integration | All critical paths | Database operations, API calls, workflows | ✅ Implemented (33 tests) |
| Contract | Critical APIs only | 5-10 endpoints maximum | ✅ Implemented (11 tests) |
| Component | Key UI components | Dashboard, forms, navigation | ✅ Implemented (22 tests) |
| E2E | 5-10 critical workflows | Authentication, scan, view results | ✅ Implemented (5 tests) |

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
// Unit/Integration: MethodName_WhenCondition_ExpectedBehavior
GetRepository_WhenNotFound_ThrowsException()

// Component: Component_Action_ExpectedResult
Dashboard_Renders_ComplianceMetrics()

// E2E: Feature_Scenario_ExpectedResult
CompletePolicyEnforcementWorkflow_ShouldWorkEndToEnd()
```

### Test Isolation
- Each test should be independent
- Use test fixtures for shared setup
- Clean up resources in `IAsyncLifetime.DisposeAsync()`
- Use `Respawn` for database cleanup in integration tests

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

## Testing Tools

### Core Testing Framework
- **xUnit** (v2.6+): Unit and integration testing framework
- **FluentAssertions** (v6.12+): Readable, expressive assertions
- **NSubstitute** (v5.1+): Clean, simple mocking framework

### Specialized Testing
- **bUnit** (v1.28+): Blazor component testing
- **Playwright** (latest): End-to-end browser testing
- **Microsoft.EntityFrameworkCore.Sqlite** (v8.0+): SQLite in-memory database for fast integration tests
- **Bogus** (v35.4+): Realistic fake data generation

### Integration & Mocking
- **WireMock.Net** (v1.5+): HTTP-level mocking for GitHub API
- **WebApplicationFactory**: ASP.NET Core integration testing (built-in)

### Contract Testing
- **NJsonSchema** (v11.0+): JSON Schema validation for API responses
- **Verify.NET** (v25.0+): Snapshot testing for detecting structural changes

### Code Quality
- **Coverlet** (v6.0+): Code coverage analysis (built into .NET SDK)

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

## Resources

### Documentation
- [Main Testing Guide](./README.md) - Entry point for all testing documentation
- [Quick Reference](./quick-reference.md) - Common commands and patterns
- [Integration Tests Guide](./integration-tests.md) - Detailed integration testing guide
- [Contract Tests Guide](./contract-tests.md) - Detailed contract testing guide
- [E2E Tests Guide](./e2e-tests.md) - Detailed E2E testing guide

### External Resources
- [xUnit Documentation](https://xunit.net/)
- [bUnit Documentation](https://bunit.dev/)
- [Testcontainers Documentation](https://dotnet.testcontainers.org/)
- [WireMock.Net Documentation](https://github.com/WireMock-Net/WireMock.Net)
- [Playwright Documentation](https://playwright.dev/)
- [Verify.NET Documentation](https://github.com/VerifyTests/Verify)
- [NJsonSchema Documentation](https://github.com/RicoSuter/NJsonSchema)
