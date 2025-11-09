# Testing Quick Reference

Quick reference for common testing commands and patterns.

## Running Tests

### All Tests
```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# Run all except E2E (for faster feedback)
dotnet test --filter Category!=E2E
```

### By Category
```bash
# Unit tests only
dotnet test --filter Category=Unit

# Integration tests
dotnet test --filter Category=Integration

# Contract tests
dotnet test --filter Category=Contract

# Component tests
dotnet test --filter Category=Component

# E2E tests (requires web application running)
dotnet test --filter Category=E2E-Workflow
```

### By Project
```bash
# Unit tests
dotnet test 10xGitHubPolicies.Tests

# Integration tests
dotnet test 10xGitHubPolicies.Tests.Integration

# Contract tests
dotnet test 10xGitHubPolicies.Tests.Contracts

# E2E tests
dotnet test 10xGitHubPolicies.Tests.E2E
```

### By Feature/Service
```bash
# Repository operations only
dotnet test --filter "Feature=RepositoryOperations"

# Issue operations only
dotnet test --filter "Feature=IssueOperations"

# GitHubService tests
dotnet test --filter "Service=GitHubService"
```

### Single Test
```bash
# Run specific test by name
dotnet test --filter FullyQualifiedName~TestName

# Example
dotnet test --filter FullyQualifiedName~GetOrganizationRepositoriesAsync_WhenCalled_ReturnsRepositories
```

### With Verbose Output
```bash
# Detailed logging
dotnet test --logger "console;verbosity=detailed"

# TRX output for CI/CD
dotnet test --logger "trx;LogFileName=results.trx"
```

## Test Naming Conventions

```csharp
// Unit/Integration: MethodName_WhenCondition_ExpectedBehavior
GetRepository_WhenNotFound_ThrowsException()
GetOrganizationRepositoriesAsync_WhenCalled_ReturnsRepositories()

// Component: Component_Action_ExpectedResult
Dashboard_Renders_ComplianceMetrics()
Index_WhenDataLoaded_DisplaysMetrics()

// E2E: Feature_Scenario_ExpectedResult
CompletePolicyEnforcementWorkflow_ShouldWorkEndToEnd()
```

## Common Test Patterns

### Unit Test Pattern
```csharp
[Fact]
public async Task MethodName_WhenCondition_ExpectedBehavior()
{
    // Arrange
    var mockService = Substitute.For<IService>();
    mockService.GetDataAsync().Returns(expectedData);
    var sut = new MyService(mockService);
    
    // Act
    var result = await sut.MethodAsync();
    
    // Assert
    result.Should().NotBeNull();
    result.Property.Should().Be(expectedValue);
}
```

### Integration Test Pattern
```csharp
[Trait("Category", "Integration")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "RepositoryOperations")]
public class MyTests : GitHubServiceIntegrationTestBase
{
    [Fact]
    public async Task MyMethod_WhenCalled_ReturnsExpected()
    {
        // Arrange
        SetupGitHubAppAuthentication();
        MockServer.Given(...).RespondWith(...);
        
        // Act
        var result = await Sut.MyMethodAsync();
        
        // Assert
        result.Should().NotBeNull();
    }
}
```

### Contract Test Pattern
```csharp
[Trait("Category", "Contract")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "RepositoryContract")]
public class MyContractTests : GitHubContractTestBase
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

### Component Test Pattern
```csharp
public class MyComponentTests : AppTestContext
{
    [Fact]
    public void Component_Action_ExpectedResult()
    {
        // Arrange
        var service = Services.GetRequiredService<IMyService>();
        service.GetDataAsync().Returns(expectedData);
        
        // Act
        var cut = RenderComponent<MyComponent>();
        
        // Assert
        cut.Find(".expected-class").TextContent.Should().Contain("expected");
    }
}
```

## E2E Test Prerequisites

### 1. Start Database
```bash
cd 10xGitHubPolicies
docker-compose up -d
```

### 2. Start Web Application
```bash
cd 10xGitHubPolicies.App
dotnet run --launch-profile https
```

Wait for: `Now listening on: https://localhost:7040`

### 3. Enable Test Mode
Edit `appsettings.Development.json`:
```json
{
  "TestMode": {
    "Enabled": true
  }
}
```

### 4. Install Playwright Browsers
```bash
cd 10xGitHubPolicies.Tests.E2E
dotnet build
pwsh bin/Debug/net8.0/playwright.ps1 install chromium
```

### 5. Run E2E Tests
```bash
dotnet test 10xGitHubPolicies.Tests.E2E
```

## Debugging Tips

### View WireMock Requests (Integration/Contract Tests)
```csharp
// In test method
LogWireMockRequests(); // Prints all HTTP requests/responses
```

### Check Test Logs
```bash
# Run with detailed logging
dotnet test --logger "console;verbosity=detailed" --filter FullyQualifiedName~TestName
```

### Run Single Test in Debug Mode
Use your IDE's test runner to set breakpoints and debug individual tests.

### View Coverage Report
```bash
# Generate coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# View report (requires reportgenerator tool)
reportgenerator -reports:coverage.cobertura.xml -targetdir:coverage-report
```

## Common Issues

### "No matching stub found" (WireMock)
- Check that `SetupGitHubAppAuthentication()` was called
- Verify the path includes `/api/v3/` prefix (Octokit adds this for Enterprise mode)
- Check WireMock logs: `LogWireMockRequests()`

### "Connection refused" (E2E Tests)
- Ensure web application is running: `dotnet run --launch-profile https`
- Verify Test Mode is enabled
- Check application is listening on `https://localhost:7040`

### "Browser executable not found" (E2E Tests)
```bash
cd 10xGitHubPolicies.Tests.E2E
dotnet build
pwsh bin/Debug/net8.0/playwright.ps1 install chromium
```

### Database Connection Issues (Integration Tests)
- Ensure Docker is running
- Check Testcontainers can start SQL Server container
- Verify connection string in test configuration

## Cleanup Commands

### Clean Test Results
```bash
# Remove test results
rm -rf TestResults/
rm -rf test-results/
```

### Clean Testcontainers
```bash
# Remove all testcontainers
docker ps -a | grep testcontainers | awk '{print $1}' | xargs docker rm -f
```

### Clean Build Artifacts
```bash
# Clean all projects
dotnet clean

# Remove bin/obj directories
find . -type d -name "bin" -o -name "obj" | xargs rm -rf
```

## CI/CD Commands

### Pre-Push Validation
```bash
# Run complete test suite (if script exists)
./pre-push-test.sh

# Run workflow tests (if script exists)
./test-workflow-local.sh
```

### Generate Test Results for CI
```bash
# Generate TRX files
dotnet test --logger "trx;LogFileName=unit-tests.trx" --filter Category=Unit
dotnet test --logger "trx;LogFileName=integration-tests.trx" --filter Category=Integration
dotnet test --logger "trx;LogFileName=contract-tests.trx" --filter Category=Contract
dotnet test --logger "trx;LogFileName=component-tests.trx" --filter Category=Component
```

## Quick Links

- [Main Testing Guide](./README.md)
- [Testing Strategy](./testing-strategy.md)
- [Integration Tests Guide](./integration-tests.md)
- [Contract Tests Guide](./contract-tests.md)
- [E2E Tests Guide](./e2e-tests.md)
