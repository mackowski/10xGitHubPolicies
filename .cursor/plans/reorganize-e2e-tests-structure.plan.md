<!-- 5e412022-addf-40f4-85b7-c8e918f57d3b cc3c9139-5c96-494a-9d23-13d26b54d8ac -->
# Reorganize E2E Tests Directory Structure

## Current State

- Test classes, services, and models are mixed at the root level
- Models are embedded in page objects
- Common logic is duplicated across test classes
- Structure doesn't align with patterns used in `Tests.Integration` and `Tests` projects

## Objectives

1. Create clear separation of concerns (infrastructure, fixtures, pages, models, helpers, tests)
2. Align structure with existing test project patterns
3. Extract duplicated code into reusable helpers
4. Improve discoverability and maintainability

## Tasks

### 1. Create Infrastructure Directory

- Create `Infrastructure/` directory
- Move `E2ETestBase.cs` to `Infrastructure/E2ETestBase.cs`
- Create `Infrastructure/TestConstants.cs` for shared constants (BaseUrl, timeouts, etc.)
- Update namespace to `_10xGitHubPolicies.Tests.E2E.Infrastructure`

### 2. Create Fixtures Directory

- Create `Fixtures/` directory
- Move `ITestDataManager.cs` and `TestDataManager.cs` to `Fixtures/`
- Move `ITestCleanupService.cs` and `TestCleanupService.cs` to `Fixtures/`
- Update namespaces to `_10xGitHubPolicies.Tests.E2E.Fixtures`

### 3. Create Models Directory

- Create `Models/` directory
- Extract `DashboardMetrics` class from `Pages/DashboardPage.cs` to `Models/DashboardMetrics.cs`
- Extract `NonCompliantRepository` class from `Pages/DashboardPage.cs` to `Models/NonCompliantRepository.cs`
- Update namespaces to `_10xGitHubPolicies.Tests.E2E.Models`
- Update `DashboardPage.cs` to reference models from new location

### 4. Create Helpers Directory

- Create `Helpers/` directory
- Create `Helpers/RepositoryHelper.cs` with static methods for common repository cleanup operations
- Extract duplicated repository cleanup logic from `SecurityAndInfrastructureTests.cs` and `WorkflowTests.cs`
- Update namespace to `_10xGitHubPolicies.Tests.E2E.Helpers`

### 5. Organize Tests by Domain

- Create `Tests/` directory with subdirectories
- Create `Tests/Workflow/` directory
- Move `WorkflowTests.cs` to `Tests/Workflow/WorkflowTests.cs`
- Create `Tests/Security/` directory
- Move `SecurityAndInfrastructureTests.cs` to `Tests/Security/SecurityAndInfrastructureTests.cs`
- Update namespaces accordingly

### 6. Update All References

- Update all `using` statements across all files to reflect new namespaces
- Update references to moved classes and services
- Verify all test classes properly reference `E2ETestBase` from new location

### 7. Update Test Classes

- Refactor `SecurityAndInfrastructureTests.cs` to use `RepositoryHelper` for cleanup
- Refactor `WorkflowTests.cs` to use `RepositoryHelper` for cleanup
- Update both test classes to use `TestConstants` for shared values

### 8. Verify Project Structure

- Ensure `Pages/` directory remains with `DashboardPage.cs` (only page objects)
- Verify `TestResults/` directory structure is preserved
- Confirm `README.md` and `AssemblyInfo.cs` remain at root level
- Validate all namespaces are consistent and follow conventions

## Files to Modify

- `10xGitHubPolicies.Tests.E2E/E2ETestBase.cs` → `Infrastructure/E2ETestBase.cs`
- `10xGitHubPolicies.Tests.E2E/TestDataManager.cs` → `Fixtures/TestDataManager.cs`
- `10xGitHubPolicies.Tests.E2E/ITestDataManager.cs` → `Fixtures/ITestDataManager.cs`
- `10xGitHubPolicies.Tests.E2E/TestCleanupService.cs` → `Fixtures/TestCleanupService.cs`
- `10xGitHubPolicies.Tests.E2E/ITestCleanupService.cs` → `Fixtures/ITestCleanupService.cs`
- `10xGitHubPolicies.Tests.E2E/WorkflowTests.cs` → `Tests/Workflow/WorkflowTests.cs`
- `10xGitHubPolicies.Tests.E2E/SecurityAndInfrastructureTests.cs` → `Tests/Security/SecurityAndInfrastructureTests.cs`
- `10xGitHubPolicies.Tests.E2E/Pages/DashboardPage.cs` (modify to extract models)

## Files to Create

- `Infrastructure/TestConstants.cs`
- `Models/DashboardMetrics.cs`
- `Models/NonCompliantRepository.cs`
- `Helpers/RepositoryHelper.cs`