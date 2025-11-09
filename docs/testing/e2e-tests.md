# End-to-End Testing with Playwright

This document provides guidance for writing and running End-to-End (E2E) tests using Playwright in the 10x GitHub Policy Enforcer application.

## Overview

E2E tests validate complete user workflows from browser to database, testing the application in a realistic environment. The E2E test suite uses Playwright.NET for browser automation and follows a dual-host architecture pattern.

## Architecture

### Dual-Host Architecture

The E2E tests use a **dual-host architecture** that separates concerns:

1. **Test Host**: A minimal .NET host that provides:
   - GitHub API services via `IGitHubService` for test data creation
   - Database access for verification and cleanup
   - Test utilities and helper services
   - Screenshot capture utilities

2. **Web Application**: The manually running application at `https://localhost:7040/` that provides:
   - The actual user interface (Blazor Server)
   - Complete application logic and business rules
   - Real policy evaluation and scanning
   - Test Mode authentication bypass

### Why This Architecture?

- **Separation of Concerns**: Test data creation is separate from UI testing
- **Real Environment Testing**: Tests run against the actual web application
- **Debugging**: You can debug the web application while tests run
- **Reliability**: Tests don't depend on the test host's web server
- **Flexibility**: Easy to test different application configurations

## Prerequisites

### 1. Web Application Running

The web application **must be running manually** before executing E2E tests:

```bash
cd 10xGitHubPolicies
docker-compose up -d  # Start SQL Server
cd 10xGitHubPolicies.App
dotnet run --launch-profile https
```

Wait for: `Now listening on: https://localhost:7040`

### 2. Database Available

SQL Server must be running via Docker Compose (see setup commands above).

### 3. GitHub Configuration

Valid GitHub App credentials must be configured (see main README.md for setup instructions).

### 4. Test Mode Enabled

The web application **must have Test Mode enabled** for authentication bypass:

```json
// appsettings.Development.json
{
  "TestMode": {
    "Enabled": true
  }
}
```

**Why Test Mode?**
- Eliminates need for real GitHub OAuth tokens
- Bypasses authentication and authorization checks
- Makes tests deterministic and fast
- Enables CI/CD automation

⚠️ **CRITICAL**: Test Mode should **NEVER** be enabled in production environments.

### 5. Playwright Browsers Installed

After building the test project, install Playwright browsers:

```bash
cd 10xGitHubPolicies.Tests.E2E
dotnet build
pwsh bin/Debug/net8.0/playwright.ps1 install chromium
```

### 6. E2E Test Configuration

The E2E test project requires a connection string configuration. The test infrastructure supports multiple configuration sources (in order of precedence):

1. **appsettings.json** (recommended for local development)
2. **Environment Variables**
3. **User Secrets**

#### Using appsettings.json

Create `appsettings.json` in the `10xGitHubPolicies.Tests.E2E` project directory:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=10xGitHubPolicies;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=True"
  }
}
```

**Note**: The `appsettings.json` file is automatically copied to the output directory during build. The test infrastructure automatically locates it relative to the test assembly location.

## Quick Start

### Step 1: Start the Web Application

```bash
# From project root
cd 10xGitHubPolicies
docker-compose up -d
cd 10xGitHubPolicies.App
dotnet run --launch-profile https
```

### Step 2: Run E2E Tests

```bash
# From project root
dotnet test 10xGitHubPolicies.Tests.E2E --logger "console;verbosity=detailed"
```

## Test Structure

### Base Test Class: `E2ETestBase`

All E2E tests inherit from `E2ETestBase` which provides:

- **Test Host Creation**: Minimal .NET host with GitHub services
- **Configuration Loading**: Automatic discovery and loading of `appsettings.json`
- **Connection String Validation**: Validates that database connection string is configured
- **Browser Management**: Playwright browser instance
- **Web Application Connectivity**: Verification that app is running
- **Screenshot Capture**: Automatic screenshots for debugging
- **Cleanup**: Automatic resource disposal

**Example**:
```csharp
public class WorkflowTests : E2ETestBase
{
    [Fact]
    [Trait("Category", "E2E-Workflow")]
    public async Task CompletePolicyEnforcementWorkflow_ShouldWorkEndToEnd()
    {
        // Test implementation
    }
}
```

### Page Object Model: `DashboardPage`

The Page Object Model encapsulates UI interactions:

```csharp
public class DashboardPage
{
    private readonly IPage _page;
    
    public async Task GotoAsync()
    {
        await _page.GotoAsync("https://localhost:7040/");
    }
    
    public async Task TriggerScanAsync()
    {
        await _page.Locator("button:has-text('Scan Now')").ClickAsync();
    }
}
```

**Benefits**:
- Reusable UI interactions
- Easy to maintain when UI changes
- Clear test intent
- Reduces code duplication

### Test Helpers

- **RepositoryHelper**: Manages test repository creation and cleanup
- **DatabaseHelper**: Manages database operations for verification

**Example**: See `Tests.E2E/Tests/Workflow/WorkflowTests.cs` for complete examples.

## Writing E2E Tests

### Test Structure

Follow the Arrange-Act-Assert pattern:

```csharp
[Fact]
[Trait("Category", "E2E-Workflow")]
public async Task CompletePolicyEnforcementWorkflow_ShouldWorkEndToEnd()
{
    // Arrange
    var repositoryName = $"e2e-test-{Guid.NewGuid():N}";
    var repository = await RepositoryHelper.CreateTestRepositoryAsync(repositoryName);
    
    try
    {
        // Act
        var page = await Browser.NewPageAsync();
        var dashboardPage = new DashboardPage(page);
        
        await dashboardPage.GotoAsync();
        await dashboardPage.TriggerScanAsync();
        await dashboardPage.WaitForScanCompletionAsync();
        
        // Assert
        var violations = await DatabaseHelper.GetPolicyViolationsAsync(repository.Id);
        violations.Should().NotBeEmpty();
    }
    finally
    {
        // Cleanup
        await RepositoryHelper.DeleteTestRepositoryAsync(repositoryName);
        await DatabaseHelper.CleanupTestDataAsync(repositoryName);
    }
}
```

### Best Practices

1. **Always Clean Up**: Use `try-finally` blocks to ensure cleanup
2. **Use Unique Names**: Generate unique repository names using GUIDs
3. **Wait for Operations**: Use explicit waits for async operations
4. **Capture Screenshots**: Take screenshots at key points for debugging
5. **Handle Failures Gracefully**: Cleanup should handle failures
6. **Test Mode Required**: Always ensure Test Mode is enabled
7. **Verify Web App Running**: Check connectivity before tests

## Common Issues and Solutions

### 1. "Connection refused to https://localhost:7040/"

**Problem**: Web application is not running

**Solution**: 
```bash
cd 10xGitHubPolicies.App
dotnet run --launch-profile https
```

### 2. "Target page, context or browser has been closed"

**Problem**: Browser context was closed during test execution

**Solution**: 
- Check that the web application is still running
- Verify Test Mode is enabled
- Check for unhandled exceptions in the web application

### 3. "Repository not visible in dashboard"

**Problem**: Test data not visible to web application

**Solution**: 
- Ensure both test host and web application use the same database connection string
- Verify repository was created successfully
- Check database connection in web application

### 4. Authentication Issues

**Problem**: Tests fail with authentication errors

**Solution**: 
1. Verify Test Mode is enabled in `appsettings.Development.json`
2. Restart the web application after enabling Test Mode
3. Check web application logs for authentication errors

### 5. Playwright Browser Not Found

**Problem**: "Browser executable not found"

**Solution**: 
```bash
cd 10xGitHubPolicies.Tests.E2E
dotnet build
pwsh bin/Debug/net8.0/playwright.ps1 install chromium
```

## Test Mode

### What Test Mode Does

Test Mode is a special application configuration that enables:

- **Authentication Bypass**: Automatically authenticates users as fake `mackowski` user
- **Authorization Bypass**: Skips team membership verification (always returns `true`)
- **GitHub App Services**: Remain fully functional for repository operations
- **No Real Tokens**: Eliminates need for actual GitHub Personal Access Tokens

### Enabling Test Mode

**Via Configuration File** (Recommended):
```json
// appsettings.Development.json
{
  "TestMode": {
    "Enabled": true
  }
}
```

**Via Environment Variable**:
```bash
export TestMode__Enabled=true
dotnet run --launch-profile https
```

**Via Command Line**:
```bash
dotnet run --launch-profile https --TestMode:Enabled=true
```

### Security Warning

⚠️ **CRITICAL**: Test Mode should **NEVER** be enabled in production environments. It completely bypasses security controls and should only be used in development, testing, or demo scenarios.

## Running Tests

### Run All E2E Tests
```bash
dotnet test 10xGitHubPolicies.Tests.E2E
```

### Run Specific Test Category
```bash
dotnet test --filter "Category=E2E-Workflow"
```

### Run with Verbose Output
```bash
dotnet test 10xGitHubPolicies.Tests.E2E --logger "console;verbosity=detailed"
```

### Run Single Test
```bash
dotnet test --filter "FullyQualifiedName~CompletePolicyEnforcementWorkflow"
```

For more commands, see the [Quick Reference](./quick-reference.md).

## Debugging

### Manual Verification

1. Open `https://localhost:7040/` in a browser
2. Verify Test Mode authentication works (should auto-login as `mackowski`)
3. Check dashboard loads correctly
4. Test scan functionality manually

### Check Web Application Status
```bash
curl -k https://localhost:7040/
```

### Check Database Connection
```bash
cd 10xGitHubPolicies.App
dotnet ef database update
```

### View Test Logs

Tests output detailed logs to the console. Look for:
- ✅ Success indicators
- ❌ Error messages
- 🔍 Debug information
- 🚨 Issue identification

### Screenshots

Screenshots are automatically saved to `test-results/screenshots/` for debugging failures.

## CI/CD Integration

For CI/CD pipelines:

1. **Start Web Application**:
   ```bash
   dotnet run --project 10xGitHubPolicies.App --launch-profile https &
   ```

2. **Wait for Application**:
   ```bash
   timeout 30 bash -c 'until curl -k https://localhost:7040/; do sleep 1; done'
   ```

3. **Run Tests**:
   ```bash
   dotnet test 10xGitHubPolicies.Tests.E2E
   ```

4. **Ensure Test Mode Enabled**: Configure via environment variables or configuration files

## Best Practices Summary

1. ✅ **Always start the web application first**
2. ✅ **Verify Test Mode is enabled** before running tests
3. ✅ **Use unique repository names** (GUIDs) to avoid conflicts
4. ✅ **Clean up test data** in `finally` blocks
5. ✅ **Handle cleanup failures gracefully**
6. ✅ **Capture screenshots** at key points
7. ✅ **Use Page Object Model** for maintainable tests
8. ✅ **Verify web application connectivity** before tests
9. ✅ **Check logs** when tests fail
10. ✅ **Never enable Test Mode in production**

## Related Documentation

- [Testing Strategy](./testing-strategy.md) - Overview of all testing levels
- [Quick Reference](./quick-reference.md) - Common commands and patterns
- [GitHub Integration](../services/github-integration.md) - GitHubService API documentation
- [Test Mode Documentation](../../README.md#test-mode) - Detailed Test Mode guide
- [Playwright Documentation](https://playwright.dev/dotnet/) - Official Playwright.NET docs

