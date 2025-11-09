<!-- e657f72a-7247-4ea1-9c89-d93e6fc069b7 c65ee418-b24f-4ac0-a8cc-e4661f3a412f -->
# Implementation Plan: First Unit Test Setup

## Overview

Create the unit testing infrastructure from scratch and implement a simple "Hello World" style unit test for the ConfigurationService. This will validate that xUnit, NSubstitute, and FluentAssertions are properly configured.

## Phase 1: Create Test Project

### 1.1 Create xUnit Test Project

Create a new xUnit test project named `10xGitHubPolicies.Tests` in the solution root:

- Use `dotnet new xunit -n 10xGitHubPolicies.Tests`
- Target framework: .NET 8.0 (matching the main application)

### 1.2 Add NuGet Packages

Install required testing packages:

- `xUnit` (v2.6+) - Already included in xUnit template
- `NSubstitute` (v5.1+) - For mocking
- `FluentAssertions` (v6.12+) - For readable assertions
- `Bogus` (v35.4+) - For test data generation
- `Microsoft.Extensions.Caching.Memory` - For IMemoryCache (used in ConfigurationService)
- `Microsoft.Extensions.Options` - For IOptions<T> (used in ConfigurationService)

### 1.3 Add Project Reference

Add reference to `10xGitHubPolicies.App` project

### 1.4 Add to Solution

Add the test project to the solution file

## Phase 2: Create Test Infrastructure

### 2.1 Create Directory Structure

```
10xGitHubPolicies.Tests/
├── Services/
│   └── Configuration/
│       └── ConfigurationServiceTests.cs
└── 10xGitHubPolicies.Tests.csproj
```

### 2.2 Create Simple "Hello World" Test

Create `ConfigurationServiceTests.cs` with a basic test that:

- Tests the simplest scenario: successful configuration retrieval
- Mocks `IGitHubService` to return valid YAML content
- Mocks `IMemoryCache` to simulate cache miss
- Verifies the returned `AppConfig` is not null
- Uses FluentAssertions for readable assertions

## Phase 3: Implement First Test

### Test: `GetConfigAsync_WhenValidConfigExists_ReturnsAppConfig`

**Arrange:**

- Mock `IGitHubService.GetFileContentAsync()` to return base64-encoded valid YAML
- Mock `IMemoryCache` with `TryGetValue()` returning false (cache miss)
- Mock `IOptions<GitHubAppOptions>` with default options
- Mock `ILogger<ConfigurationService>`
- Create `ConfigurationService` instance (System Under Test)

**Valid YAML for test:**

```yaml
access_control:
  authorized_team: "test-org/test-team"
policies:
 - type: "has_agents_md"
    action: "create-issue"
```

**Act:**

- Call `await sut.GetConfigAsync()`

**Assert:**

- Result should not be null
- Result should be of type `AppConfig`
- `AccessControl.AuthorizedTeam` should be "test-org/test-team"
- `Policies` collection should have 1 item

**Traits:**

- `[Trait("Category", "Unit")]`
- `[Trait("Feature", "Configuration")]`

## Phase 4: Verify Test Execution

### 4.1 Run the Test

Execute: `dotnet test --filter Category=Unit`

### 4.2 Verify Output

Ensure:

- Test project builds successfully
- Test executes and passes
- All dependencies are resolved correctly

## Testing Approach

This "Hello World" test validates:

- ✅ xUnit test framework is working
- ✅ NSubstitute mocking is configured correctly
- ✅ FluentAssertions assertions work
- ✅ Project references are correct
- ✅ ConfigurationService can be instantiated with mocked dependencies
- ✅ Basic async test execution works

## Success Criteria

1. Test project builds without errors
2. Single test passes successfully
3. Test follows all conventions from unit-testing.mdc
4. All mocking and assertions work as expected
5. Test can be run with `dotnet test --filter Category=Unit`

### To-dos

- [ ] Create xUnit test project and install required NuGet packages (xUnit, NSubstitute, FluentAssertions, Bogus)
- [ ] Add project reference to 10xGitHubPolicies.App and add test project to solution
- [ ] Create Services/Configuration directory structure in test project
- [ ] Implement ConfigurationServiceTests.cs with first unit test following unit-testing.mdc guidelines
- [ ] Run test with dotnet test and verify it passes successfully