<!-- 43a57cce-dba6-4d82-adb9-0e50aa327a01 4cbd79df-8e32-47bc-9d02-844f2b4e79a7 -->
# Unit Test Implementation Plan: ConfigurationService

## Current State

- **File**: `10xGitHubPolicies.Tests/Services/Configuration/ConfigurationServiceTests.cs`
- **Tests Implemented**: 1 (happy path)
- **Tests Needed**: 11 additional tests
- **Coverage Target**: 85-90%

## Test Cases to Implement

### 1. Cache Hit Scenario

**Test**: `GetConfigAsync_WhenConfigCached_ReturnsCachedConfig`

**Priority**: HIGH (Core caching behavior)

Mock `IMemoryCache.TryGetValue()` to return a cached `AppConfig`. Verify:

- GitHub service is NOT called
- Same cached instance is returned
- Logger logs cache hit message

### 2. Missing Configuration File

**Test**: `GetConfigAsync_WhenConfigFileMissing_ThrowsConfigurationNotFoundException`

**Priority**: HIGH (Error handling - TC-CONFIG-006)

Mock `IGitHubService.GetFileContentAsync()` to return `null` or empty string. Verify:

- Throws `ConfigurationNotFoundException`
- Exception message contains `.github/config.yaml`
- Logger logs warning message

### 3. Invalid YAML Syntax

**Test**: `GetConfigAsync_WhenYamlInvalid_ThrowsInvalidConfigurationException`

**Priority**: HIGH (Error handling - TC-CONFIG-006)

Use malformed YAML:

```yaml
invalid: yaml: syntax:
  - missing quotes
  unclosed: [bracket
```

Verify:

- Throws `InvalidConfigurationException`
- Inner exception is `YamlException`
- Logger logs error

### 4. Missing Authorized Team Field

**Test**: `GetConfigAsync_WhenAuthorizedTeamMissing_ThrowsInvalidConfigurationException`

**Priority**: HIGH (Validation - TC-CONFIG-006)

Use YAML without `access_control.authorized_team`:

```yaml
policies:
  - type: "has_agents_md"
```

Verify exception message contains "authorized_team must be set"

### 5. Empty Authorized Team

**Test**: `GetConfigAsync_WhenAuthorizedTeamEmpty_ThrowsInvalidConfigurationException`

**Priority**: HIGH (Validation - TC-CONFIG-006)

Use YAML with empty `authorized_team`:

```yaml
access_control:
  authorized_team: ""
```

Verify validation fails with clear message

### 6. Null Access Control Object

**Test**: `GetConfigAsync_WhenAccessControlNull_ThrowsInvalidConfigurationException`

**Priority**: MEDIUM

Use YAML without `access_control` section. Verify validation catches null check.

### 7. Force Refresh Bypasses Cache

**Test**: `GetConfigAsync_WhenForceRefreshTrue_BypassesCache`

**Priority**: HIGH (Core feature - TC-CONFIG-007)

Steps:

1. First call populates cache
2. Update mock to return different config
3. Call with `forceRefresh: true`
4. Verify GitHub service called again
5. Verify new config returned (not cached)

### 8. Concurrent Requests (Semaphore Test)

**Test**: `GetConfigAsync_WhenCalledConcurrently_FetchesOnlyOnce`

**Priority**: CRITICAL (Thread-safety - TC-CONFIG-004)

Implementation:

```csharp
var callCount = 0;
_githubService.GetFileContentAsync(...)
    .Returns(_ => {
        Interlocked.Increment(ref callCount);
        return Task.Delay(100).ContinueWith(_ => base64Content);
    });

// Act - 10 concurrent calls
var tasks = Enumerable.Range(0, 10)
    .Select(_ => _sut.GetConfigAsync())
    .ToList();
var results = await Task.WhenAll(tasks);

// Assert
callCount.Should().Be(1, "semaphore should prevent duplicate fetches");
```

### 9. Double-Check Locking

**Test**: `GetConfigAsync_WhenMultipleThreadsWait_SecondCheckPreventsRefetch`

**Priority**: MEDIUM (Thread-safety edge case)

Verify that after acquiring semaphore, cache is checked again before fetching.

### 10. Cache Entry Options Validation

**Test**: `GetConfigAsync_WhenCaching_SetsSlidingExpiration15Minutes`

**Priority**: MEDIUM (TC-CONFIG-005)

Verify `IMemoryCache.Set()` is called with correct expiration:

```csharp
_cache.Received(1).Set(
    Arg.Is<string>(k => k == "AppConfigCacheKey"),
    Arg.Any<AppConfig>(),
    Arg.Is<MemoryCacheEntryOptions>(opts => 
        // Verify sliding expiration = 15 minutes
    )
);
```

### 11. Multiple Policy Parsing

**Test**: `GetConfigAsync_WhenMultiplePolicies_ParsesAllCorrectly`

**Priority**: MEDIUM (Data parsing validation)

Use YAML with 3 different policy types. Verify all are parsed with correct properties.

### 12. GitHub Service Error Propagation

**Test**: `GetConfigAsync_WhenGitHubServiceFails_PropagatesException`

**Priority**: MEDIUM (Error handling)

Mock GitHub service to throw exception. Verify:

- Exception propagates to caller
- Logger logs error
- Cache is not updated with invalid data

## Implementation Notes

### Mock Cache Correctly

The existing test shows the pattern for mocking `IMemoryCache.TryGetValue()`:

```csharp
_cache.TryGetValue(Arg.Any<object>(), out Arg.Any<AppConfig?>())
    .Returns(x => {
        x[1] = cachedValue; // or null for cache miss
        return cachedValue != null;
    });
```

### Base64 Encoding Helper

Consider extracting to helper method:

```csharp
private string ToBase64(string yaml) 
    => Convert.ToBase64String(Encoding.UTF8.GetBytes(yaml));
```

### Test Organization

Group tests using nested classes if file gets too large:

```csharp
public class ConfigurationServiceTests
{
    public class GetConfigAsyncTests : BaseTests { }
    public class ValidationTests : BaseTests { }
    public class CachingTests : BaseTests { }
}
```

### Traits for Filtering

All tests should have:

```csharp
[Trait("Category", "Unit")]
[Trait("Feature", "Configuration")]
```

## Expected Outcomes

- **Total Tests**: 13 (1 existing + 12 new)
- **Coverage**: 85-90% of ConfigurationService
- **Test Execution Time**: < 2 seconds total
- **All tests follow naming convention**: `MethodName_WhenCondition_ExpectedBehavior`

## Test Coverage Mapping

| ConfigurationService Feature | Test Case | Priority |

|------------------------------|-----------|----------|

| Happy path | ✅ Implemented | HIGH |

| Cache hit | #1 | HIGH |

| Cache miss → fetch | ✅ Implemented | HIGH |

| Force refresh | #7 | HIGH |

| Missing file | #2 | HIGH |

| Invalid YAML | #3 | HIGH |

| Validation (authorized_team) | #4, #5, #6 | HIGH |

| Thread-safety (semaphore) | #8, #9 | CRITICAL |

| Cache expiration config | #10 | MEDIUM |

| Multiple policies | #11 | MEDIUM |

| Error propagation | #12 | MEDIUM |

### To-dos

- [ ] Implement GetConfigAsync_WhenConfigCached_ReturnsCachedConfig test
- [ ] Implement error handling tests: missing file, invalid YAML, validation failures (tests #2-#6)
- [ ] Implement GetConfigAsync_WhenForceRefreshTrue_BypassesCache test
- [ ] Implement concurrent requests and double-check locking tests (#8-#9)
- [ ] Implement cache expiration, multiple policies, and error propagation tests (#10-#12)