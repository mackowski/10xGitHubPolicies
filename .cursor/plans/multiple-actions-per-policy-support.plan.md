<!-- f9e29cb0-878b-4daa-aa8a-42abcfdc96ae c4a106a7-9e29-4e16-8d8d-24cf6d173e75 -->
# Multiple Actions Per Policy Support Implementation Plan

## Current Status

The application currently supports only a single action per policy:

- `PolicyConfig.Action` is a `string` property (single action)
- `ActionService.ProcessActionsForScanAsync()` uses if/else logic to execute one action per violation
- `Policies` table stores a single action as `NVARCHAR(MAX)` (not used for execution, only for data integrity)
- YAML configuration supports only `action: 'create-issue'` format (single value)

## Implementation Tasks

### 1. Update PolicyConfig Model to Support Multiple Actions

**Location**: `10xGitHubPolicies.App/Services/Configuration/Models/PolicyConfig.cs`

**Changes Needed**:

- Change `Action` property from `string` to `List<string>`
- Add custom YAML deserialization to support both formats:
                                - Single string: `action: 'create-issue'` (backward compatible)
                                - List: `action: ['create-issue', 'archive-repo']` (new format)
- Use `YamlMember` with custom converter or handle in deserialization logic
- Ensure backward compatibility with existing single-action configurations

**Implementation Approach**:

- Use a custom `IYamlTypeConverter` or handle in `ConfigurationService` after deserialization
- Alternative: Use a property setter that normalizes single strings to lists
- Consider using `System.Text.Json` for JSON serialization when storing in database

### 2. Update ActionService to Process Multiple Actions

**Location**: `10xGitHubPolicies.App/Services/Action/ActionService.cs`

**Changes Needed**:

- Modify `ProcessActionsForScanAsync()` to iterate over `policyConfig.Actions` (plural) instead of checking single `Action`
- Replace if/else chain with a loop that processes each action in the list
- Ensure each action is executed independently (one failure doesn't block others)
- Maintain existing error handling and logging per action
- Each action should be logged separately in `ActionLog` table

**Key Logic Changes**:

```csharp
// Current: if (policyConfig.Action == "create-issue")
// New: foreach (var action in policyConfig.Actions) { ... }
```

### 3. Update ScanningService to Store Multiple Actions

**Location**: `10xGitHubPolicies.App/Services/Scanning/ScanningService.cs`

**Changes Needed**:

- Update `SyncPoliciesAsync()` method to serialize `policyConfig.Actions` list to JSON string
- Store JSON in `Policy.Action` column (e.g., `["create-issue", "archive-repo"]`)
- Use `System.Text.Json.JsonSerializer.Serialize()` for consistent JSON formatting
- Handle empty action lists gracefully (should not occur, but defensive coding)

**Implementation**:

- Serialize `List<string>` to JSON string before storing in database
- No database migration needed (column already `NVARCHAR(MAX)`)

### 4. Update Configuration Service (if needed)

**Location**: `10xGitHubPolicies.App/Services/Configuration/ConfigurationService.cs`

**Changes Needed**:

- Verify YAML deserialization handles both single string and list formats
- May need custom deserialization logic if YamlDotNet doesn't handle this automatically
- Test that existing single-action configs still work (backward compatibility)

## Testing Implementation

### Level 1: Unit Tests

**Location**: `10xGitHubPolicies.Tests/Services/Action/ActionServiceTests.cs`

**Existing Tests to Update**:

- All tests that mock `PolicyConfig` need to use `Actions` list instead of `Action` string
- Update test data builders to support multiple actions

**New Tests Needed**:

- `ProcessActionsForScanAsync_WhenMultipleActions_ExecutesAllActions` - Verify all actions in list are executed
- `ProcessActionsForScanAsync_WhenMultipleActions_OneFails_OthersContinue` - Verify action isolation
- `ProcessActionsForScanAsync_WhenMultipleActions_LogsEachActionSeparately` - Verify separate action logs
- `ProcessActionsForScanAsync_WhenEmptyActionsList_HandlesGracefully` - Edge case handling
- `ProcessActionsForScanAsync_WhenSingleAction_BackwardCompatible` - Verify backward compatibility

**Location**: `10xGitHubPolicies.Tests/Services/Configuration/ConfigurationServiceTests.cs`

**New Tests Needed**:

- `GetConfigAsync_WhenActionIsString_ParsesAsSingleItemList` - Backward compatibility
- `GetConfigAsync_WhenActionIsList_ParsesCorrectly` - New format support
- `GetConfigAsync_WhenActionIsEmptyList_ThrowsException` - Validation

**Location**: `10xGitHubPolicies.Tests/Services/Scanning/ScanningServiceTests.cs`

**New Tests Needed**:

- `SyncPoliciesAsync_WhenMultipleActions_StoresAsJson` - Verify JSON serialization
- `SyncPoliciesAsync_WhenSingleAction_StoresAsJson` - Verify single action stored correctly

### Level 2: Integration Tests

**Location**: `10xGitHubPolicies.Tests.Integration/`

**New Tests Needed**:

- Test ActionService with real database and multiple actions per policy
- Test that multiple ActionLog entries are created for one violation
- Test action execution order and isolation
- Test with WireMock.Net mocking GitHub API for multiple action types

### Level 3: Contract Tests

**No changes needed** - Contract tests validate GitHub API responses, not internal configuration format.

### Level 4: Component Tests

**Location**: `10xGitHubPolicies.Tests/Components/`

**Tasks**:

- Verify dashboard displays correctly when multiple actions are configured
- Test onboarding page template shows new list format as example
- Verify configuration examples in UI components

### Level 5: E2E Tests

**DO NOT CREATE NEW E2E tests - it will be covered as a separate task**

## Documentation Updates

### 1. Configuration Service Documentation

**Location**: `docs/services/configuration-service.md`

**Updates Needed**:

- Update configuration format examples to show both single action and multiple actions
- Document backward compatibility
- Add example with multiple actions:
```yaml
policies:
  - name: 'Critical Security Policy'
    type: 'has_agents_md'
    action: ['create-issue', 'archive-repo']
```


### 2. Action Service Documentation

**Location**: `docs/services/action-service.md`

**Updates Needed**:

- Document that multiple actions can be configured per policy
- Explain that actions execute in order and independently
- Update configuration examples
- Document that each action creates a separate ActionLog entry

### 3. README.md

**Location**: `README.md`

**Updates Needed**:

- Update policy configuration examples to show multiple actions support
- Add note about backward compatibility

### 4. CHANGELOG

**Location**: `CHANGELOG.md`

**Updates Needed**:

- Add entry for multiple actions per policy feature
- Note backward compatibility with single-action format

## Implementation Order

1. **Update PolicyConfig Model** - Change Action to List<string> with backward compatibility
2. **Update ConfigurationService** - Ensure YAML deserialization handles both formats
3. **Update ActionService** - Modify to iterate over multiple actions
4. **Update ScanningService** - Store actions as JSON in database
5. **Unit Tests** - Update existing tests and add new test scenarios
6. **Integration Tests** - Add tests for multiple actions workflow
7. **Documentation** - Update all relevant documentation

## Success Criteria

- Policies can be configured with multiple actions in YAML (both list and single string formats)
- All actions in the list are executed for each violation
- Each action creates a separate ActionLog entry
- Actions execute independently (one failure doesn't block others)
- Backward compatibility maintained (single-action configs still work)
- Actions are stored as JSON in database Policies table
- All existing tests pass with updated code
- New tests cover multiple actions scenarios
- Documentation is updated and accurate

## Backward Compatibility

- Single string format `action: 'create-issue'` must continue to work
- Existing configurations should not break
- Database can store either format (JSON handles both)

## To-dos

- [ ] Update PolicyConfig.Action to List<string> with custom YAML deserialization for backward compatibility
- [ ] Update ActionService.ProcessActionsForScanAsync() to iterate over multiple actions
- [ ] Update ScanningService.SyncPoliciesAsync() to serialize actions list to JSON for database storage
- [ ] Update ConfigurationService if needed for YAML deserialization
- [ ] Update all unit tests to use Actions list instead of Action string
- [ ] Add unit tests for multiple actions execution, action isolation, and backward compatibility
- [ ] Add integration tests for multiple actions with real database
- [ ] Update configuration-service.md, action-service.md, README.md, and CHANGELOG.md
- [ ] Verify backward compatibility with existing single-action configurations

### To-dos

- [ ] Update PolicyConfig.Action to List<string> with custom YAML deserialization to support both single string (backward compatible) and list formats
- [ ] Update ActionService.ProcessActionsForScanAsync() to iterate over multiple actions instead of single action if/else logic
- [ ] Update ScanningService.SyncPoliciesAsync() to serialize actions list to JSON string for database storage
- [ ] Verify and update ConfigurationService YAML deserialization to handle both single string and list formats
- [ ] Update all existing unit tests to use Actions list and add new tests for multiple actions execution, isolation, and backward compatibility
- [ ] Add integration tests for multiple actions workflow with real database and mocked GitHub API
- [ ] Update configuration-service.md, action-service.md, README.md, and CHANGELOG.md with multiple actions support and examples
- [ ] Add E2E test for multiple actions workflow (create-issue + archive-repo) with complete validation