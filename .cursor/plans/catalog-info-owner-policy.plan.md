<!-- fc07db95-e575-467f-af78-015ccdbd21e0 e1d4765a-5543-4705-835c-6c8ecd748762 -->
# Catalog Info Owner Policy Implementation Plan

## Current Status

The application already has a `HasCatalogInfoYamlEvaluator` that checks for the existence of `catalog-info.yaml` file. This new policy will extend the validation to ensure the file contains a valid owner assignment in the `spec.owner` field.

## Implementation Tasks

### 1. Create CatalogInfoHasOwnerEvaluator

**Location**: `10xGitHubPolicies.App/Services/Policies/Evaluators/CatalogInfoHasOwnerEvaluator.cs`

**Implementation Details**:

- Implement `IPolicyEvaluator` interface
- Policy type: `catalog_info_has_owner`
- Use `IGitHubService.GetFileContentAsync()` to retrieve file content (returns base64-encoded string)
- Decode base64 content to UTF-8 string
- Parse YAML using `YamlDotNet` (already in project dependencies)
- Navigate to `spec.owner` field in the parsed YAML structure
- Validate that owner field exists and is not null/empty/whitespace
- Return `PolicyViolation` if owner is missing or empty, null if compliant
- Handle edge cases:
                                                                                                                                - File doesn't exist: Return null (covered by `has_catalog_info_yaml` policy)
                                                                                                                                - Invalid YAML: Log error and return violation (malformed file)
                                                                                                                                - Missing `spec` section: Return violation
                                                                                                                                - Missing `owner` field: Return violation
                                                                                                                                - Empty/whitespace owner value: Return violation

**Dependencies**:

- `IGitHubService.GetFileContentAsync()` (already exists)
- `YamlDotNet` package (already in project)
- Repository name format: Use `repository.FullName` or construct from `repository.Owner.Login` and `repository.Name`

**Code Structure**:

```csharp
public class CatalogInfoHasOwnerEvaluator : IPolicyEvaluator
{
    private readonly IGitHubService _githubService;
    private readonly ILogger<CatalogInfoHasOwnerEvaluator> _logger;
    
    public string PolicyType => "catalog_info_has_owner";
    
    public async Task<PolicyViolation?> EvaluateAsync(Octokit.Repository repository)
    {
        // 1. Get file content (returns base64 or null)
        // 2. If null, return null (file doesn't exist - covered by other policy)
        // 3. Decode base64 to UTF-8
        // 4. Parse YAML
        // 5. Check spec.owner exists and is not empty
        // 6. Return violation or null
    }
}
```

### 2. Register Evaluator in Dependency Injection

**Location**: `10xGitHubPolicies.App/Program.cs`

**Changes**:

- Add evaluator registration after line 125:
```csharp
builder.Services.AddScoped<IPolicyEvaluator, CatalogInfoHasOwnerEvaluator>();
```


### 3. Update PRD Documentation

**Location**: `docs/prd.md`

**Changes**:

- Add new policy to section 3.4 "Core Policies (MVP)":
                                                                                                                                - Add: "4. Verify that the `catalog-info.yaml` file contains an assigned owner in the `spec.owner` field."

### 4. Update Configuration Template

**Location**: `10xGitHubPolicies.App/Pages/Onboarding.razor`

**Changes**:

- Add example policy configuration to the config template (around line 167):
```yaml
  - name: 'Verify catalog-info.yaml has owner'
    type: 'catalog_info_has_owner'
    action: 'create-issue'
    issue_details:
      title: 'Compliance: catalog-info.yaml missing owner'
      body: 'The catalog-info.yaml file exists but does not have an owner assigned in the spec.owner field. Please add an owner to comply with organization standards.'
      labels: ['policy-violation', 'backstage']
```


**Location**: `.github/config.yaml` (example configuration)

**Changes**:

- Add example policy entry for reference

## Testing Implementation

### Level 1: Unit Tests

**Location**: `10xGitHubPolicies.Tests/Services/Policies/Evaluators/CatalogInfoHasOwnerEvaluatorTests.cs`

**Test Cases**:

1. `PolicyType_WhenAccessed_ReturnsCorrectValue` - Verify policy type is `catalog_info_has_owner`
2. `EvaluateAsync_WhenFileDoesNotExist_ReturnsNull` - File missing should return null (covered by other policy)
3. `EvaluateAsync_WhenOwnerExistsAndNotEmpty_ReturnsNull` - Valid owner should pass
4. `EvaluateAsync_WhenOwnerMissing_ReturnsViolation` - Missing spec.owner field
5. `EvaluateAsync_WhenOwnerEmpty_ReturnsViolation` - Empty string owner
6. `EvaluateAsync_WhenOwnerWhitespace_ReturnsViolation` - Whitespace-only owner
7. `EvaluateAsync_WhenSpecSectionMissing_ReturnsViolation` - Missing spec section
8. `EvaluateAsync_WhenInvalidYaml_ReturnsViolation` - Malformed YAML file
9. `EvaluateAsync_WhenCalled_UsesCorrectRepositoryName` - Verify correct repo name passed to service
10. `EvaluateAsync_WhenOwnerIsNull_ReturnsViolation` - Null owner value

**Test Data Requirements**:

- Mock `IGitHubService.GetFileContentAsync()` to return various scenarios:
                                                                                                                                - Valid YAML with owner
                                                                                                                                - Valid YAML without owner
                                                                                                                                - Valid YAML with empty owner
                                                                                                                                - Invalid YAML
                                                                                                                                - Null (file doesn't exist)
- Use base64-encoded YAML content in test data
- Test with different owner values (team names, user names)

**Mock Setup Pattern**:

```csharp
var validYamlWithOwner = Convert.ToBase64String(Encoding.UTF8.GetBytes(@"
apiVersion: backstage.io/v1alpha1
kind: Component
spec:
  owner: appsec
"));
_gitHubService.GetFileContentAsync(Arg.Any<string>(), "catalog-info.yaml")
    .Returns(validYamlWithOwner);
```

### Level 2: Integration Tests

**Location**: `10xGitHubPolicies.Tests.Integration/` (if applicable)

**Test Cases**:

- Test full policy evaluation workflow with real database
- Test YAML parsing with various edge cases
- Test error handling for malformed YAML

**Note**: May not be necessary if unit tests cover YAML parsing thoroughly. Integration tests would focus on database persistence of violations.

### Level 3: Contract Tests

**Not Applicable**: This policy doesn't interact with external GitHub API contracts directly (it uses existing `GetFileContentAsync` which is already tested).

### Level 4: Component Tests

**Not Applicable**: This is a background policy evaluation, no UI components directly involved.

### Level 5: E2E Tests

**Not Applicable**: This will be covered as a separate task

## Documentation Updates

### 1. Policy Evaluation Documentation

**Location**: `docs/policy-evaluation.md`

**Updates**:

- Add example of `CatalogInfoHasOwnerEvaluator` in the "Creating a New Policy Evaluator" section
- Document YAML parsing approach
- Document edge case handling

### 2. Configuration Documentation

**Location**: `docs/configuration-service.md`

**Updates**:

- Add example policy configuration for `catalog_info_has_owner` in the configuration examples section

### 3. CHANGELOG

**Location**: `CHANGELOG.md`

**Updates**:

- Add entry under "Added" section:
                                                                                                                                - New policy evaluator: `CatalogInfoHasOwnerEvaluator` - Checks that `catalog-info.yaml` contains an assigned owner in `spec.owner` field

### 4. README

**Location**: `README.md`

**Updates**:

- Update policy list if there's a section listing available policies

## Implementation Order

1. **Create CatalogInfoHasOwnerEvaluator** - Implement the core evaluator logic with YAML parsing
2. **Register in DI Container** - Add to Program.cs
3. **Unit Tests** - Write comprehensive unit tests covering all scenarios
4. **Update Documentation** - PRD, policy-evaluation.md, configuration-service.md, CHANGELOG
5. **Update Configuration Templates** - Onboarding.razor and .github/config.yaml
6. **Code Review** - Verify all tests pass and implementation follows patterns

## Success Criteria

- `CatalogInfoHasOwnerEvaluator` correctly identifies repositories with missing/empty owners
- All unit tests pass (10+ test cases)
- Policy evaluator is registered and discoverable by `PolicyEvaluationService`
- YAML parsing handles edge cases gracefully (invalid YAML, missing sections, etc.)
- Documentation is updated and accurate
- Configuration examples are provided
- Code follows existing patterns and conventions
- PRD is updated with the new policy requirement

## Technical Considerations

### YAML Parsing Approach

Use `YamlDotNet` with dynamic deserialization to navigate the YAML structure:

- Option 1: Deserialize to `Dictionary<string, object>` and navigate manually
- Option 2: Create a simple model class for the structure we need
- Option 3: Use `YamlStream` and navigate the document tree

**Recommendation**: Use dynamic deserialization to `Dictionary<string, object>` for flexibility, as we only need to check `spec.owner` and don't need the full structure.

### Error Handling

- Invalid YAML: Log error and return violation (file is malformed)
- File not found: Return null (covered by `has_catalog_info_yaml` policy)
- API errors: Let exception propagate (will be handled by scanning service)

### Performance

- File content is retrieved once per repository per scan
- YAML parsing is lightweight for small files like `catalog-info.yaml`
- No additional caching needed beyond existing GitHub service caching

### To-dos

- [ ] Create CatalogInfoHasOwnerEvaluator class implementing IPolicyEvaluator with YAML parsing logic to check spec.owner field
- [ ] Register CatalogInfoHasOwnerEvaluator in Program.cs dependency injection container
- [ ] Write comprehensive unit tests (10+ cases) covering file missing, owner missing, owner empty, invalid YAML, and edge cases
- [ ] Update docs/prd.md section 3.4 to include the new policy requirement
- [ ] Update policy-evaluation.md, configuration-service.md, and CHANGELOG.md with new policy details and examples
- [ ] Update Onboarding.razor config template and .github/config.yaml with example policy configuration
- [ ] Add E2E test CatalogInfoOwnerPolicyWorkflow_ShouldDetectMissingOwner to validate complete workflow