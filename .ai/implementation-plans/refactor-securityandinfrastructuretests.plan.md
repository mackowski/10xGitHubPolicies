<!-- 983af4a0-2459-4bee-8378-597258ca5fce 41ed2dbc-5166-4d78-b91d-d6d12a816f35 -->
# Refactor SecurityAndInfrastructureTests

## Overview

Refactor `SecurityAndInfrastructureTests.cs` to:

1. Remove browser initialization for the smoke test (no Playwright dependency)
2. Remove test data creation and verification from the auth test (keep browser for URL testing only)

## Changes Required

### 1. Create Non-Browser Test Base Class

- Create a new base class `NonBrowserTestBase` in `Infrastructure/ Within NonBrowserTestBase.cs` that:
- Does not extend `PageTest` (avoids Playwright browser initialization)
- Provides the same test host setup as `E2ETestBase` (ServiceProvider, DbContext, GitHubService explored by CreateTestHost)
- Does not call `VerifyWebAppIsRunning()` (since it requires Page/browser)
- Shares the `CreateTestHost()` logic from `E2ETestBase`

### 2. Refactor Smoke Test

- Change `SmokeTests_EnvironmentValidation_Success` to inherit from `NonBrowserTestBase` instead of `E2ETestBase`
- Remove all browser-related setup/teardown dependencies
- Keep the test logic unchanged (configuration validation, database connection, GitHub API connectivity, service resolution)

### 3. Refactor Auth Test

- Keep `SecurityBoundaryValidation_Success` inheriting from `E2ETestBase` (needs browser for URL testing)
- Remove test repository creation code (lines 93-108: repository creation, screenshot, repository list tracking)
- Remove test data verification sections (lines 210-244: database verification, repository querying, repository content testing, cleanup verification)
- Keep only the URL testing logic (unauthenticated URLs, authenticated URLs, login flow, dashboard access)
- Remove `_testDataManager` and `_cleanupService` fields and their initialization if they become unused
- Remove cleanup logic from TearDown that depends on test repositories
- Simplify SetUp/TearDown to only handle what's needed for the auth test

### 4. Update Test Class Structure

- Remove unused `_testDataManager`, `_cleanupService`, and `_createdRepositories` fields from the class if they're no longer needed
- Simplify SetUp/TearDown methods to only initialize what's required for each specific test

## Files to Modify

- `10xGitHubPolicies.Tests.E2E/Infrastructure/E2ETestBase.cs` - Extract `CreateTestHost()` logic to be reusable
- `10xGitHubPolicies.Tests.E2E/Infrastructure/NonBrowserTestBase.cs` - Create new base class
- `10xGitHubPolicies.Tests.E2E/Tests/Security/SecurityAndInfrastructureTests.cs` - Refactor test class

### To-dos

- [ ] Extract CreateTestHost() method from E2ETestBase to be reusable by both base classes
- [ ] Create NonBrowserTestBase class that provides test host without browser/Playwright
- [ ] Change smoke test to inherit from NonBrowserTestBase and remove browser dependencies
- [ ] Remove test data creation, verification, and cleanup from auth test, keep only URL testing logic
- [ ] Remove unused fields and simplify SetUp/TearDown methods in SecurityAndInfrastructureTests