<!-- 4aeb93ac-ec1e-4ba0-b4b7-b3547888c975 71e9222a-27bf-4009-ac6d-ce3ba3b1cf47 -->
# Refactor E2E Test Code and Page Model

## Current Status Analysis

### ✅ Already Done

- DashboardPage class exists with basic structure
- RepositoryHelper exists for cleanup operations
- TestConstants defined with timeout values

### ❌ Issues Found

#### 1. DashboardPage Selector Mismatches

**Current DashboardPage uses WRONG selectors:**

- `#scan-button` → Should be `[data-testid='scan-button']`
- `fluent-data-grid` → Should be `[data-testid='non-compliant-repositories-grid']`
- Uses `QuerySelectorAllAsync` → Should use `Locator` API (like WorkflowTests)

**WorkflowTests uses CORRECT selectors (matching Index.razor):**

- `[data-testid='scan-button']` ✓
- `[data-testid='non-compliant-repositories-grid']` ✓
- `[data-testid='repository-name']` ✓
- `[data-testid='repository-violations']` ✓
- `[data-testid='violation-badge']` ✓

#### 2. DashboardPage Logic Mismatches

**GetNonCompliantRepositories()** uses wrong approach:

- Uses `fluent-data-grid fluent-data-grid-row` selectors
- Uses `QuerySelectorAllAsync` instead of `Locator` API
- Structure doesn't match actual DOM (table > tbody > tr)

**WorkflowTests.GetRepositoryViolationsAsync()** uses correct approach:

- Uses `[data-testid='non-compliant-repositories-grid'] tbody tr` ✓
- Uses `Locator` API consistently ✓
- Properly waits for elements ✓
- Handles errors with detailed logging ✓

#### 3. Scan Button Logic Mismatches

**DashboardPage.StartScan()** is too simple:

- Doesn't wait for button to be enabled
- Doesn't handle errors properly
- Doesn't match exact waiting logic from WorkflowTests

**WorkflowTests scan button logic** is more robust:

- Waits for button element handle
- Waits for button to be enabled via JavaScript function
- Has try-catch with screenshot on error
- Waits for "Scanning..." text
- Waits for "Scan Now" text with proper timeout

## Refactoring Plan (Small Steps)

### Step 1: Fix DashboardPage Selectors ✅ FIRST STEP

**Goal**: Update DashboardPage to use exact same selectors as WorkflowTests
**Changes**:

- Replace `#scan-button` with `[data-testid='scan-button']`
- Replace `fluent-data-grid` with `[data-testid='non-compliant-repositories-grid']`
- Update all methods to use `Locator` API instead of `QuerySelectorAllAsync`

**Files to modify**:

- `10xGitHubPolicies.Tests.E2E/Pages/DashboardPage.cs`

**Risk**: Low - Only changing selectors, not logic

---

### Step 2: Add GetRepositoryViolations to DashboardPage

**Goal**: Extract the exact `GetRepositoryViolationsAsync` logic from WorkflowTests into DashboardPage
**Changes**:

- Copy exact implementation from WorkflowTests line 49-128
- Rename to `GetRepositoryViolations(string fullRepoName)`
- Keep all Console.WriteLine statements for debugging
- Keep exact same error handling and logging

**Files to modify**:

- `10xGitHubPolicies.Tests.E2E/Pages/DashboardPage.cs`

**Risk**: Low - Exact copy, no changes

---

### Step 3: Add ClickScanButton to DashboardPage

**Goal**: Extract scan button click logic from WorkflowTests
**Changes**:

- Copy exact implementation from WorkflowTests line 195-229 (try-catch block)
- Create `ClickScanButton()` method
- Keep all error handling and screenshot logic
- Keep exact same waits and checks

**Files to modify**:

- `10xGitHubPolicies.Tests.E2E/Pages/DashboardPage.cs`

**Risk**: Low - Exact copy, no changes

---

### Step 4: Add WaitForScanComplete to DashboardPage

**Goal**: Extract scan completion wait logic from WorkflowTests
**Changes**:

- Copy exact implementation from WorkflowTests line 232-237
- Create `WaitForScanComplete()` method
- Keep exact same timeout values

**Files to modify**:

- `10xGitHubPolicies.Tests.E2E/Pages/DashboardPage.cs`

**Risk**: Low - Exact copy, no changes

---

### Step 5: Add WaitForNonCompliantRepositoriesGrid to DashboardPage

**Goal**: Extract grid wait logic from WorkflowTests
**Changes**:

- Copy exact implementation from WorkflowTests line 307-311
- Create `WaitForNonCompliantRepositoriesGrid()` method
- Keep exact same waits and delays

**Files to modify**:

- `10xGitHubPolicies.Tests.E2E/Pages/DashboardPage.cs`

**Risk**: Low - Exact copy, no changes

---

### Step 6: Add ReloadAndWaitForGrid to DashboardPage

**Goal**: Extract page reload logic from WorkflowTests
**Changes**:

- Copy exact implementation from WorkflowTests line 293-313
- Create `ReloadAndWaitForGrid()` method
- Keep exact same navigation and waits

**Files to modify**:

- `10xGitHubPolicies.Tests.E2E/Pages/DashboardPage.cs`

**Risk**: Low - Exact copy, no changes

---

### Step 7: Add IsRepositoryVisible to DashboardPage (Update Existing)

**Goal**: Update existing method to match WorkflowTests approach
**Changes**:

- Replace current implementation with WorkflowTests line 336-340 logic
- Use `Page.Locator("[data-testid='repository-name']").Filter()` approach

**Files to modify**:

- `10xGitHubPolicies.Tests.E2E/Pages/DashboardPage.cs`

**Risk**: Low - Using proven approach from WorkflowTests

---

### Step 8: Create DatabaseHelper Class

**Goal**: Extract database query patterns from WorkflowTests
**Changes**:

- Extract scan waiting logic (lines 254-290)
- Extract action log waiting logic (lines 392-427)
- Extract re-scan waiting logic (lines 496-533)
- Create helper methods with exact same logic

**Files to create**:

- `10xGitHubPolicies.Tests.E2E/Helpers/DatabaseHelper.cs`

**Files to modify**:

- None (new file)

**Risk**: Medium - Need to ensure DbContext access

---

### Step 9: Create GitHubHelper Class

**Goal**: Extract GitHub API verification patterns from WorkflowTests
**Changes**:

- Extract repository visibility check (lines 151-172)
- Extract policy violation issues waiting (lines 429-462)
- Extract re-scan issues waiting (lines 630-663)

**Files to create**:

- `10xGitHubPolicies.Tests.E2E/Helpers/GitHubHelper.cs`

**Files to modify**:

- None (new file)

**Risk**: Medium - Need to ensure GitHubService access

---

### Step 10: Create ScreenshotHelper Class

**Goal**: Centralize screenshot taking with consistent naming
**Changes**:

- Extract screenshot pattern from WorkflowTests (lines 187-190, 315-318, etc.)
- Use TestContext.CurrentContext.Test.Name for auto-naming
- Support custom filenames

**Files to create**:

- `10xGitHubPolicies.Tests.E2E/Helpers/ScreenshotHelper.cs`

**Files to modify**:

- None (new file)

**Risk**: Low - Simple wrapper

---

### Step 11: Refactor WorkflowTests to Use DashboardPage Methods

**Goal**: Replace inline code with DashboardPage methods
**Changes**:

- Replace `GetRepositoryViolationsAsync` call with `_dashboardPage.GetRepositoryViolations()`
- Replace scan button logic with `_dashboardPage.ClickScanButton()` and `_dashboardPage.WaitForScanComplete()`
- Replace grid waiting with `_dashboardPage.WaitForNonCompliantRepositoriesGrid()`
- Replace page reload with `_dashboardPage.ReloadAndWaitForGrid()`
- Replace repository visibility checks with `_dashboardPage.IsRepositoryVisible()`

**Files to modify**:

- `10xGitHubPolicies.Tests.E2E/Tests/Workflow/WorkflowTests.cs`

**Risk**: Medium - Need to ensure all logic matches

---

### Step 12: Refactor WorkflowTests to Use Helper Classes

**Goal**: Replace database and GitHub API code with helper methods
**Changes**:

- Replace database waiting loops with `DatabaseHelper.WaitForScanComplete()` etc.
- Replace GitHub API loops with `GitHubHelper.WaitForPolicyViolationIssues()` etc.
- Replace screenshot calls with `ScreenshotHelper.TakeScreenshot()`

**Files to modify**:

- `10xGitHubPolicies.Tests.E2E/Tests/Workflow/WorkflowTests.cs`

**Risk**: Medium - Need to ensure all logic matches

---

### Step 13: Remove Deprecated Methods from DashboardPage

**Goal**: Clean up DashboardPage after refactoring
**Changes**:

- Remove old `GetNonCompliantRepositories()` if no longer used
- Remove old `StartScan()` if replaced by `ClickScanButton()` + `WaitForScanComplete()`
- Keep methods that are still used by other tests

**Files to modify**:

- `10xGitHubPolicies.Tests.E2E/Pages/DashboardPage.cs`

**Risk**: Low - Only remove unused code

---

## Files to Modify (Summary)

### Existing Files

- `10xGitHubPolicies.Tests.E2E/Tests/Workflow/WorkflowTests.cs` - Refactor to use helpers and page model
- `10xGitHubPolicies.Tests.E2E/Pages/DashboardPage.cs` - Update selectors and add missing methods

### New Files to Create

- `10xGitHubPolicies.Tests.E2E/Helpers/DatabaseHelper.cs` - Database query helpers
- `10xGitHubPolicies.Tests.E2E/Helpers/GitHubHelper.cs` - GitHub API helpers  
- `10xGitHubPolicies.Tests.E2E/Helpers/ScreenshotHelper.cs` - Screenshot helpers

## Critical Rules

1. **DO NOT CHANGE** selectors - use exact same as WorkflowTests
2. **DO NOT CHANGE** wait logic - use exact same timeouts and delays
3. **DO NOT CHANGE** error handling - keep all Console.WriteLine statements
4. **DO NOT CHANGE** DOM traversal logic - use exact same Locator API patterns
5. **TEST EACH STEP** before proceeding to next step

## Expected Outcome

After all steps:

- WorkflowTests.cs should be significantly shorter and more readable
- All UI interactions should go through DashboardPage
- All database operations should go through DatabaseHelper
- All GitHub API verifications should go through GitHubHelper
- All screenshots should go through ScreenshotHelper
- Code duplication should be eliminated
- All selectors and logic should match exactly with current working implementation

### To-dos

- [x] Analysis complete - identified all mismatches
- [ ] Step 1: Fix DashboardPage selectors
- [ ] Step 2: Add GetRepositoryViolations to DashboardPage
- [ ] Step 3: Add ClickScanButton to DashboardPage
- [ ] Step 4: Add WaitForScanComplete to DashboardPage
- [ ] Step 5: Add WaitForNonCompliantRepositoriesGrid to DashboardPage
- [ ] Step 6: Add ReloadAndWaitForGrid to DashboardPage
- [ ] Step 7: Update IsRepositoryVisible in DashboardPage
- [ ] Step 8: Create DatabaseHelper class
- [ ] Step 9: Create GitHubHelper class
- [ ] Step 10: Create ScreenshotHelper class
- [ ] Step 11: Refactor WorkflowTests to use DashboardPage methods
- [ ] Step 12: Refactor WorkflowTests to use helper classes
- [ ] Step 13: Remove deprecated methods from DashboardPage