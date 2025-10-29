# E2E Tests for 10x GitHub Policies

This directory contains End-to-End (E2E) tests for the 10x GitHub Policies application using Playwright.

## Overview

The E2E tests are designed to test the complete user workflow against a **manually running web application**. This approach allows for:

- Testing against the actual production-like environment
- Debugging issues in real-time
- Verifying the complete user experience
- Testing with real GitHub API interactions

## Prerequisites

1. **Web Application Running**: The web application must be running manually before running tests
2. **Database**: SQL Server database must be running (via Docker Compose)
3. **GitHub Configuration**: Valid GitHub App credentials must be configured
4. **Test Mode**: The application should be running with Test Mode enabled

## Quick Start

### 1. Start the Web Application

```bash
# From the project root
cd 10xGitHubPolicies
docker-compose up -d  # Start SQL Server
cd 10xGitHubPolicies.App
dotnet run --launch-profile https
```

Wait for the application to start and show:
```
Now listening on: https://localhost:7040
```

### 2. Run E2E Tests

**Option A: Using the test runner script (Recommended)**

```bash
# From the project root
cd 10xGitHubPolicies.Tests.E2E
./run-e2e-tests.sh  # Linux/macOS
# OR
pwsh run-e2e-tests.ps1  # Windows PowerShell
```

**Option B: Using dotnet test directly**

```bash
# From the project root
dotnet test --project 10xGitHubPolicies.Tests.E2E --logger "console;verbosity=detailed"
```

## Test Architecture

### Test Host vs Web Application

The E2E tests use a **dual-host architecture**:

1. **Test Host**: A minimal .NET host that provides:
   - GitHub API services for test data creation
   - Database access for verification
   - Test utilities and cleanup services

2. **Web Application**: The manually running application at `https://localhost:7040/` that provides:
   - The actual user interface
   - The complete application logic
   - Real policy evaluation and scanning

### Why This Architecture?

- **Separation of Concerns**: Test data creation is separate from UI testing
- **Real Environment Testing**: Tests run against the actual web application
- **Debugging**: You can debug the web application while tests run
- **Reliability**: Tests don't depend on the test host's web server

## Test Configuration

### Test Mode

The web application should be running with Test Mode enabled:

```json
// appsettings.Development.json
{
  "TestMode": {
    "Enabled": true
  }
}
```

Test Mode provides:
- Authentication bypass (automatic login as `mackowski` user)
- Authorization bypass (always returns `true` for team membership)
- Full GitHub API functionality

### Database Connection

Both the test host and web application use the same database connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=10xGitHubPolicies;User Id=sa;Password=yourStrong(!)Password;Encrypt=False"
  }
}
```

## Test Structure

### E2ETestBase

The base class provides:
- Test host creation and management
- Web application connectivity verification
- Common test utilities
- Screenshot capture for debugging

### WorkflowTests

The main test class contains:
- `CompletePolicyEnforcementWorkflow_ShouldWorkEndToEnd()`: Comprehensive workflow test
- Repository creation and cleanup
- UI interaction testing
- Policy compliance verification

## Test Data Management

### TestDataManager

Creates test repositories with specific compliance states:
- **Compliant repositories**: Include required files (e.g., `AGENTS.md`)
- **Non-compliant repositories**: Missing required files

### TestCleanupService

Cleans up test data after tests:
- Removes test repositories from GitHub
- Cleans up database records
- Handles cleanup failures gracefully

## Screenshots and Debugging

Tests automatically capture screenshots at key points:
- Initial dashboard state
- After scan completion
- Error states
- Final results

Screenshots are saved to `test-results/screenshots/` for debugging.

## Common Issues and Solutions

### 1. "Connection refused to https://localhost:7040/"

**Problem**: Web application is not running
**Solution**: Start the web application with `dotnet run --launch-profile https`

### 2. "Target page, context or browser has been closed"

**Problem**: Browser context was closed during test execution
**Solution**: This usually indicates the test host was disposed prematurely. Check that the web application is still running.

### 3. "Repository not visible in dashboard"

**Problem**: Test data not visible to web application
**Solution**: Ensure both test host and web application use the same database connection.

### 4. Authentication issues

**Problem**: Tests fail with authentication errors
**Solution**: Ensure Test Mode is enabled in `appsettings.Development.json`

## Running Individual Tests

To run a specific test:

```bash
dotnet test --project 10xGitHubPolicies.Tests.E2E --filter "TestCategory=E2E-Workflow" --logger "console;verbosity=detailed"
```

## Continuous Integration

For CI/CD pipelines, ensure:
1. Web application is started before tests
2. Database is available
3. GitHub credentials are configured
4. Test Mode is enabled

## Best Practices

1. **Always start the web application first**
2. **Use the test runner scripts** for convenience
3. **Check screenshots** when tests fail
4. **Clean up test data** after each test run
5. **Verify Test Mode is enabled** for consistent behavior
6. **Use descriptive test names** and console output
7. **Handle cleanup failures gracefully** in teardown methods

## Troubleshooting

### Check Web Application Status

```bash
curl -k https://localhost:7040/
```

### Check Database Connection

```bash
# From the web application directory
dotnet ef database update
```

### View Test Logs

Tests output detailed logs to the console. Look for:
- ✅ Success indicators
- ❌ Error messages
- 🔍 Debug information
- 🚨 Issue identification

### Manual Verification

You can manually verify the web application by:
1. Opening `https://localhost:7040/` in a browser
2. Checking that Test Mode authentication works
3. Verifying the dashboard loads correctly
4. Testing the scan functionality manually
