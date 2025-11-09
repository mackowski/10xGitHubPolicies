# AuthorizationService Unit Tests - Implementation Plan

## Overview

**Target Class**: `AuthorizationService` (`10xGitHubPolicies.App/Services/Authorization/AuthorizationService.cs`)

**Test Project**: `10xGitHubPolicies.Tests`

**Test File**: `10xGitHubPolicies.Tests/Services/Authorization/AuthorizationServiceTests.cs`

**Testing Framework**: xUnit + NSubstitute + FluentAssertions + Bogus

## Related Test Cases

This implementation covers the following test cases from `.ai/test-plan.md`:

- **TC-AUTH-001**: Successful GitHub Login
- **TC-AUTH-002**: Access Denied for Unauthorized Users
- **TC-AUTH-003**: Team Membership Validation

## Dependencies to Mock

```csharp
private readonly IGitHubService _gitHubService;                    // Mock with NSubstitute
private readonly IConfigurationService _configService;              // Mock with NSubstitute
private readonly ILogger<AuthorizationService> _logger;            // Mock with NSubstitute
private readonly IHttpContextAccessor _httpContextAccessor;        // Mock with NSubstitute
private readonly AuthorizationService _sut;                        // System Under Test
private readonly Faker _faker;                                     // Test data generation
```

## Test Class Structure

```csharp
using System.Security.Claims;
using _10xGitHubPolicies.App.Exceptions;
using _10xGitHubPolicies.App.Services.Authorization;
using _10xGitHubPolicies.App.Services.Configuration;
using _10xGitHubPolicies.App.Services.Configuration.Models;
using _10xGitHubPolicies.App.Services.GitHub;
using Bogus;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace _10xGitHubPolicies.Tests.Services.Authorization;

[Trait("Category", "Unit")]
[Trait("Service", "AuthorizationService")]
public class AuthorizationServiceTests
{
    private readonly IGitHubService _gitHubService;
    private readonly IConfigurationService _configService;
    private readonly ILogger<AuthorizationService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthorizationService _sut;
    private readonly Faker _faker;

    public AuthorizationServiceTests()
    {
        // Arrange - Create mocks
        _gitHubService = Substitute.For<IGitHubService>();
        _configService = Substitute.For<IConfigurationService>();
        _logger = Substitute.For<ILogger<AuthorizationService>>();
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _faker = new Faker();

        // Create system under test
        _sut = new AuthorizationService(
            _gitHubService,
            _configService,
            _logger,
            _httpContextAccessor);
    }

    // Test methods here...
}
```

## Test Scenarios

### 1. IsUserAuthorizedAsync - Successful Authorization

**Test Case**: `IsUserAuthorizedAsync_WhenUserIsMemberOfAuthorizedTeam_ReturnsTrue`

**Objective**: Verify authorized team members can access the application

**Related Test Case**: TC-AUTH-001

```csharp
[Fact]
public async Task IsUserAuthorizedAsync_WhenUserIsMemberOfAuthorizedTeam_ReturnsTrue()
{
    // Arrange
    var user = CreateClaimsPrincipal("testuser");
    var accessToken = _faker.Random.AlphaNumeric(40);
    var org = "test-org";
    var teamSlug = "test-team";
    var authorizedTeam = $"{org}/{teamSlug}";
    
    SetupHttpContextWithToken(accessToken);
    SetupConfigurationWithTeam(authorizedTeam);
    
    _gitHubService.IsUserMemberOfTeamAsync(accessToken, org, teamSlug)
        .Returns(true);

    // Act
    var result = await _sut.IsUserAuthorizedAsync(user);

    // Assert
    result.Should().BeTrue(because: "user is a member of the authorized team");
    
    await _gitHubService.Received(1).IsUserMemberOfTeamAsync(accessToken, org, teamSlug);
    await _configService.Received(1).GetConfigAsync();
}
```

### 2. IsUserAuthorizedAsync - User Not Team Member

**Test Case**: `IsUserAuthorizedAsync_WhenUserNotTeamMember_ReturnsFalse`

**Objective**: Verify non-team members are denied access

**Related Test Case**: TC-AUTH-002

```csharp
[Fact]
public async Task IsUserAuthorizedAsync_WhenUserNotTeamMember_ReturnsFalse()
{
    // Arrange
    var user = CreateClaimsPrincipal("unauthorized-user");
    var accessToken = _faker.Random.AlphaNumeric(40);
    var org = "test-org";
    var teamSlug = "test-team";
    var authorizedTeam = $"{org}/{teamSlug}";
    
    SetupHttpContextWithToken(accessToken);
    SetupConfigurationWithTeam(authorizedTeam);
    
    _gitHubService.IsUserMemberOfTeamAsync(accessToken, org, teamSlug)
        .Returns(false);

    // Act
    var result = await _sut.IsUserAuthorizedAsync(user);

    // Assert
    result.Should().BeFalse(because: "user is not a member of the authorized team");
    
    await _gitHubService.Received(1).IsUserMemberOfTeamAsync(accessToken, org, teamSlug);
    
    // Verify result is logged
    _logger.ReceivedWithAnyArgs().LogInformation(default, default, default);
}
```

### 3. IsUserAuthorizedAsync - No HTTP Context

**Test Case**: `IsUserAuthorizedAsync_WhenNoHttpContext_ReturnsFalse`

**Objective**: Verify graceful handling when HTTP context is unavailable

**Related Test Case**: TC-AUTH-002 (edge case)

```csharp
[Fact]
public async Task IsUserAuthorizedAsync_WhenNoHttpContext_ReturnsFalse()
{
    // Arrange
    var user = CreateClaimsPrincipal("testuser");
    _httpContextAccessor.HttpContext.Returns((HttpContext?)null);

    // Act
    var result = await _sut.IsUserAuthorizedAsync(user);

    // Assert
    result.Should().BeFalse(because: "no HTTP context is available");
    
    // Verify warning was logged
    _logger.ReceivedWithAnyArgs().LogWarning(default(string), default);
    
    // Verify no GitHub API calls made
    await _gitHubService.DidNotReceive().IsUserMemberOfTeamAsync(
        Arg.Any<string>(),
        Arg.Any<string>(),
        Arg.Any<string>());
}
```

### 4. IsUserAuthorizedAsync - Authentication Failed

**Test Case**: `IsUserAuthorizedAsync_WhenAuthenticationFails_ReturnsFalse`

**Objective**: Verify handling when user authentication failed

**Related Test Case**: TC-AUTH-002 (edge case)

```csharp
[Fact]
public async Task IsUserAuthorizedAsync_WhenAuthenticationFails_ReturnsFalse()
{
    // Arrange
    var user = CreateClaimsPrincipal("testuser");
    var httpContext = Substitute.For<HttpContext>();
    
    // Mock failed authentication
    var failedResult = AuthenticateResult.Fail("Authentication failed");
    httpContext.AuthenticateAsync(Arg.Any<string>()).Returns(failedResult);
    
    _httpContextAccessor.HttpContext.Returns(httpContext);

    // Act
    var result = await _sut.IsUserAuthorizedAsync(user);

    // Assert
    result.Should().BeFalse(because: "user authentication failed");
    
    _logger.ReceivedWithAnyArgs().LogWarning(default(string));
}
```

### 5. IsUserAuthorizedAsync - No Access Token

**Test Case**: `IsUserAuthorizedAsync_WhenNoAccessToken_ReturnsFalse`

**Objective**: Verify handling when access token is missing

**Related Test Case**: TC-AUTH-002 (edge case)

```csharp
[Fact]
public async Task IsUserAuthorizedAsync_WhenNoAccessToken_ReturnsFalse()
{
    // Arrange
    var user = CreateClaimsPrincipal("testuser");
    SetupHttpContextWithToken(null); // No token

    // Act
    var result = await _sut.IsUserAuthorizedAsync(user);

    // Assert
    result.Should().BeFalse(because: "no access token is available");
    
    _logger.ReceivedWithAnyArgs().LogWarning(default(string));
    
    await _gitHubService.DidNotReceive().IsUserMemberOfTeamAsync(
        Arg.Any<string>(),
        Arg.Any<string>(),
        Arg.Any<string>());
}
```

### 6. IsUserAuthorizedAsync - Empty Access Token

**Test Case**: `IsUserAuthorizedAsync_WhenEmptyAccessToken_ReturnsFalse`

**Objective**: Verify handling when access token is empty string

**Related Test Case**: TC-AUTH-002 (edge case)

```csharp
[Fact]
public async Task IsUserAuthorizedAsync_WhenEmptyAccessToken_ReturnsFalse()
{
    // Arrange
    var user = CreateClaimsPrincipal("testuser");
    SetupHttpContextWithToken(string.Empty); // Empty token

    // Act
    var result = await _sut.IsUserAuthorizedAsync(user);

    // Assert
    result.Should().BeFalse(because: "access token is empty");
    
    _logger.ReceivedWithAnyArgs().LogWarning(default(string));
}
```

### 7. IsUserAuthorizedAsync - No Authorized Team Configured

**Test Case**: `IsUserAuthorizedAsync_WhenNoAuthorizedTeam_ReturnsFalse`

**Objective**: Verify handling when authorized team is not configured

**Related Test Case**: TC-AUTH-002, TC-CONFIG-001

```csharp
[Fact]
public async Task IsUserAuthorizedAsync_WhenNoAuthorizedTeam_ReturnsFalse()
{
    // Arrange
    var user = CreateClaimsPrincipal("testuser");
    var accessToken = _faker.Random.AlphaNumeric(40);
    
    SetupHttpContextWithToken(accessToken);
    SetupConfigurationWithTeam(null); // No team configured

    // Act
    var result = await _sut.IsUserAuthorizedAsync(user);

    // Assert
    result.Should().BeFalse(because: "no authorized team is configured");
    
    _logger.ReceivedWithAnyArgs().LogWarning(default(string));
    
    await _gitHubService.DidNotReceive().IsUserMemberOfTeamAsync(
        Arg.Any<string>(),
        Arg.Any<string>(),
        Arg.Any<string>());
}
```

### 8. IsUserAuthorizedAsync - Empty Authorized Team

**Test Case**: `IsUserAuthorizedAsync_WhenEmptyAuthorizedTeam_ReturnsFalse`

**Objective**: Verify handling when authorized team is empty string

**Related Test Case**: TC-AUTH-002, TC-CONFIG-002

```csharp
[Fact]
public async Task IsUserAuthorizedAsync_WhenEmptyAuthorizedTeam_ReturnsFalse()
{
    // Arrange
    var user = CreateClaimsPrincipal("testuser");
    var accessToken = _faker.Random.AlphaNumeric(40);
    
    SetupHttpContextWithToken(accessToken);
    SetupConfigurationWithTeam(string.Empty);

    // Act
    var result = await _sut.IsUserAuthorizedAsync(user);

    // Assert
    result.Should().BeFalse(because: "authorized team is empty");
    
    _logger.ReceivedWithAnyArgs().LogWarning(default(string));
}
```

### 9. IsUserAuthorizedAsync - Invalid Team Format (Missing Slash)

**Test Case**: `IsUserAuthorizedAsync_WhenInvalidTeamFormat_ReturnsFalse`

**Objective**: Verify validation of team format (requires "org/team")

**Related Test Case**: TC-AUTH-002, TC-CONFIG-002

```csharp
[Theory]
[InlineData("invalid-team-format")]
[InlineData("org")]
[InlineData("org/team/extra")]
[InlineData("/team")]
[InlineData("org/")]
public async Task IsUserAuthorizedAsync_WhenInvalidTeamFormat_ReturnsFalse(string invalidTeam)
{
    // Arrange
    var user = CreateClaimsPrincipal("testuser");
    var accessToken = _faker.Random.AlphaNumeric(40);
    
    SetupHttpContextWithToken(accessToken);
    SetupConfigurationWithTeam(invalidTeam);

    // Act
    var result = await _sut.IsUserAuthorizedAsync(user);

    // Assert
    result.Should().BeFalse(because: $"team format '{invalidTeam}' is invalid");
    
    // Verify error was logged
    _logger.ReceivedWithAnyArgs().LogError(default(string), default);
    
    await _gitHubService.DidNotReceive().IsUserMemberOfTeamAsync(
        Arg.Any<string>(),
        Arg.Any<string>(),
        Arg.Any<string>());
}
```

### 10. IsUserAuthorizedAsync - Configuration Not Found Exception

**Test Case**: `IsUserAuthorizedAsync_WhenConfigurationNotFound_ReturnsFalse`

**Objective**: Verify handling of missing configuration file

**Related Test Case**: TC-AUTH-002, TC-CONFIG-001

```csharp
[Fact]
public async Task IsUserAuthorizedAsync_WhenConfigurationNotFound_ReturnsFalse()
{
    // Arrange
    var user = CreateClaimsPrincipal("testuser");
    var accessToken = _faker.Random.AlphaNumeric(40);
    
    SetupHttpContextWithToken(accessToken);
    
    _configService.GetConfigAsync()
        .Returns(Task.FromException<AppConfig>(
            new ConfigurationNotFoundException("config.yaml not found")));

    // Act
    var result = await _sut.IsUserAuthorizedAsync(user);

    // Assert
    result.Should().BeFalse(because: "configuration file is missing");
    
    // Verify error was logged with correct exception type
    _logger.ReceivedWithAnyArgs().LogError(
        Arg.Any<ConfigurationNotFoundException>(),
        Arg.Any<string>());
    
    await _gitHubService.DidNotReceive().IsUserMemberOfTeamAsync(
        Arg.Any<string>(),
        Arg.Any<string>(),
        Arg.Any<string>());
}
```

### 11. IsUserAuthorizedAsync - Invalid Configuration Exception

**Test Case**: `IsUserAuthorizedAsync_WhenInvalidConfiguration_ReturnsFalse`

**Objective**: Verify handling of malformed configuration

**Related Test Case**: TC-AUTH-002, TC-CONFIG-002

```csharp
[Fact]
public async Task IsUserAuthorizedAsync_WhenInvalidConfiguration_ReturnsFalse()
{
    // Arrange
    var user = CreateClaimsPrincipal("testuser");
    var accessToken = _faker.Random.AlphaNumeric(40);
    
    SetupHttpContextWithToken(accessToken);
    
    _configService.GetConfigAsync()
        .Returns(Task.FromException<AppConfig>(
            new InvalidConfigurationException("Invalid YAML syntax")));

    // Act
    var result = await _sut.IsUserAuthorizedAsync(user);

    // Assert
    result.Should().BeFalse(because: "configuration is invalid");
    
    _logger.ReceivedWithAnyArgs().LogError(
        Arg.Any<InvalidConfigurationException>(),
        Arg.Any<string>());
    
    await _gitHubService.DidNotReceive().IsUserMemberOfTeamAsync(
        Arg.Any<string>(),
        Arg.Any<string>(),
        Arg.Any<string>());
}
```

### 12. IsUserAuthorizedAsync - GitHub Service Exception

**Test Case**: `IsUserAuthorizedAsync_WhenGitHubServiceThrows_ReturnsFalse`

**Objective**: Verify handling of GitHub API failures

**Related Test Case**: TC-AUTH-002 (edge case)

```csharp
[Fact]
public async Task IsUserAuthorizedAsync_WhenGitHubServiceThrows_ReturnsFalse()
{
    // Arrange
    var user = CreateClaimsPrincipal("testuser");
    var accessToken = _faker.Random.AlphaNumeric(40);
    var org = "test-org";
    var teamSlug = "test-team";
    var authorizedTeam = $"{org}/{teamSlug}";
    
    SetupHttpContextWithToken(accessToken);
    SetupConfigurationWithTeam(authorizedTeam);
    
    _gitHubService.IsUserMemberOfTeamAsync(accessToken, org, teamSlug)
        .Returns(Task.FromException<bool>(
            new HttpRequestException("GitHub API unavailable")));

    // Act
    var result = await _sut.IsUserAuthorizedAsync(user);

    // Assert
    result.Should().BeFalse(because: "GitHub API call failed");
    
    // Verify unexpected error was logged
    _logger.ReceivedWithAnyArgs().LogError(
        Arg.Any<Exception>(),
        Arg.Is<string>(s => s.Contains("Unexpected error")));
}
```

### 13. IsUserAuthorizedAsync - Team Format Parsing

**Test Case**: `IsUserAuthorizedAsync_WhenValidTeamFormat_ParsesCorrectly`

**Objective**: Verify correct parsing of org/team format

**Related Test Case**: TC-AUTH-003

```csharp
[Theory]
[InlineData("myorg/myteam", "myorg", "myteam")]
[InlineData("org-name/team-slug", "org-name", "team-slug")]
[InlineData("Company123/Engineering", "Company123", "Engineering")]
public async Task IsUserAuthorizedAsync_WhenValidTeamFormat_ParsesCorrectly(
    string authorizedTeam,
    string expectedOrg,
    string expectedTeam)
{
    // Arrange
    var user = CreateClaimsPrincipal("testuser");
    var accessToken = _faker.Random.AlphaNumeric(40);
    
    SetupHttpContextWithToken(accessToken);
    SetupConfigurationWithTeam(authorizedTeam);
    
    _gitHubService.IsUserMemberOfTeamAsync(accessToken, expectedOrg, expectedTeam)
        .Returns(true);

    // Act
    var result = await _sut.IsUserAuthorizedAsync(user);

    // Assert
    result.Should().BeTrue();
    
    // Verify correct org and team were passed to GitHub service
    await _gitHubService.Received(1).IsUserMemberOfTeamAsync(
        accessToken,
        expectedOrg,
        expectedTeam);
}
```

### 14. GetAuthorizedTeamAsync - Success

**Test Case**: `GetAuthorizedTeamAsync_WhenConfigExists_ReturnsTeam`

**Objective**: Verify successful retrieval of authorized team

**Related Test Case**: TC-CONFIG-003

```csharp
[Fact]
public async Task GetAuthorizedTeamAsync_WhenConfigExists_ReturnsTeam()
{
    // Arrange
    var expectedTeam = "test-org/test-team";
    SetupConfigurationWithTeam(expectedTeam);

    // Act
    var result = await _sut.GetAuthorizedTeamAsync();

    // Assert
    result.Should().Be(expectedTeam, because: "configuration contains authorized team");
    
    await _configService.Received(1).GetConfigAsync();
}
```

### 15. GetAuthorizedTeamAsync - Configuration Not Found

**Test Case**: `GetAuthorizedTeamAsync_WhenConfigNotFound_ReturnsNull`

**Objective**: Verify handling when configuration file is missing

**Related Test Case**: TC-CONFIG-001

```csharp
[Fact]
public async Task GetAuthorizedTeamAsync_WhenConfigNotFound_ReturnsNull()
{
    // Arrange
    _configService.GetConfigAsync()
        .Returns(Task.FromException<AppConfig>(
            new ConfigurationNotFoundException("config.yaml not found")));

    // Act
    var result = await _sut.GetAuthorizedTeamAsync();

    // Assert
    result.Should().BeNull(because: "configuration file is missing");
    
    _logger.ReceivedWithAnyArgs().LogError(
        Arg.Any<ConfigurationNotFoundException>(),
        Arg.Any<string>());
}
```

### 16. GetAuthorizedTeamAsync - Invalid Configuration

**Test Case**: `GetAuthorizedTeamAsync_WhenInvalidConfig_ReturnsNull`

**Objective**: Verify handling when configuration is malformed

**Related Test Case**: TC-CONFIG-002

```csharp
[Fact]
public async Task GetAuthorizedTeamAsync_WhenInvalidConfig_ReturnsNull()
{
    // Arrange
    _configService.GetConfigAsync()
        .Returns(Task.FromException<AppConfig>(
            new InvalidConfigurationException("Invalid YAML syntax")));

    // Act
    var result = await _sut.GetAuthorizedTeamAsync();

    // Assert
    result.Should().BeNull(because: "configuration is invalid");
    
    _logger.ReceivedWithAnyArgs().LogError(
        Arg.Any<InvalidConfigurationException>(),
        Arg.Any<string>());
}
```

### 17. GetAuthorizedTeamAsync - Unexpected Exception

**Test Case**: `GetAuthorizedTeamAsync_WhenUnexpectedException_ReturnsNull`

**Objective**: Verify handling of unexpected errors

**Related Test Case**: Error handling

```csharp
[Fact]
public async Task GetAuthorizedTeamAsync_WhenUnexpectedException_ReturnsNull()
{
    // Arrange
    _configService.GetConfigAsync()
        .Returns(Task.FromException<AppConfig>(
            new InvalidOperationException("Unexpected error")));

    // Act
    var result = await _sut.GetAuthorizedTeamAsync();

    // Assert
    result.Should().BeNull(because: "unexpected exception occurred");
    
    _logger.ReceivedWithAnyArgs().LogError(
        Arg.Any<Exception>(),
        Arg.Is<string>(s => s.Contains("Unexpected error")));
}
```

### 18. IsUserAuthorizedAsync - Logging Verification

**Test Case**: `IsUserAuthorizedAsync_WhenCalled_LogsAppropriateInformation`

**Objective**: Verify logging behavior throughout authorization flow

**Related Test Case**: Implicit in all test cases

```csharp
[Fact]
public async Task IsUserAuthorizedAsync_WhenCalled_LogsAppropriateInformation()
{
    // Arrange
    var user = CreateClaimsPrincipal("testuser");
    var accessToken = _faker.Random.AlphaNumeric(40);
    var org = "test-org";
    var teamSlug = "test-team";
    var authorizedTeam = $"{org}/{teamSlug}";
    
    SetupHttpContextWithToken(accessToken);
    SetupConfigurationWithTeam(authorizedTeam);
    
    _gitHubService.IsUserMemberOfTeamAsync(accessToken, org, teamSlug)
        .Returns(true);

    // Act
    await _sut.IsUserAuthorizedAsync(user);

    // Assert - Verify all expected log messages
    _logger.ReceivedWithAnyArgs(3).LogInformation(
        default(string),
        Arg.Any<object[]>());
}
```

## Helper Methods

```csharp
/// <summary>
/// Creates a ClaimsPrincipal with a GitHub username claim
/// </summary>
private ClaimsPrincipal CreateClaimsPrincipal(string username)
{
    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, username),
        new Claim(ClaimTypes.NameIdentifier, _faker.Random.Int(1, 99999).ToString())
    };
    var identity = new ClaimsIdentity(claims, "TestAuthType");
    return new ClaimsPrincipal(identity);
}

/// <summary>
/// Sets up HttpContext mock with authentication result containing access token
/// </summary>
private void SetupHttpContextWithToken(string? accessToken)
{
    var httpContext = Substitute.For<HttpContext>();
    
    var authProperties = new AuthenticationProperties();
    if (!string.IsNullOrEmpty(accessToken))
    {
        authProperties.StoreTokens(new[]
        {
            new AuthenticationToken { Name = "access_token", Value = accessToken }
        });
    }
    
    var authResult = AuthenticateResult.Success(
        new AuthenticationTicket(
            new ClaimsPrincipal(),
            authProperties,
            "GitHub"));
    
    httpContext.AuthenticateAsync(Arg.Any<string>()).Returns(authResult);
    
    _httpContextAccessor.HttpContext.Returns(httpContext);
}

/// <summary>
/// Sets up configuration service mock with specified authorized team
/// </summary>
private void SetupConfigurationWithTeam(string? authorizedTeam)
{
    var config = new AppConfig
    {
        AccessControl = new AccessControlConfig
        {
            AuthorizedTeam = authorizedTeam ?? string.Empty
        },
        Policies = new List<PolicyConfig>()
    };
    
    _configService.GetConfigAsync().Returns(config);
}
```

## Test Execution Order

1. **Successful authorization** - Happy path
2. **User not team member** - Core denial case
3. **Valid team format parsing** - Verify org/team parsing
4. **GetAuthorizedTeamAsync success** - Helper method happy path
5. **No HTTP context** - Edge case handling
6. **Authentication failed** - OAuth failure
7. **No access token** - Missing token
8. **Empty access token** - Empty token
9. **No authorized team** - Configuration missing team
10. **Empty authorized team** - Empty team configuration
11. **Invalid team format** - Validation testing
12. **Configuration not found** - Missing config.yaml
13. **Invalid configuration** - Malformed YAML
14. **GitHub service exception** - API failure handling
15. **GetAuthorizedTeamAsync exceptions** - Helper method error cases
16. **Logging verification** - Audit trail validation

## Code Coverage Expectations

**Target Coverage**: 85-90%

**What to Cover**:

- ✅ Successful authorization flow
- ✅ All denial scenarios (no token, not team member, etc.)
- ✅ Team format validation (org/team parsing)
- ✅ Exception handling (ConfigurationNotFoundException, InvalidConfigurationException, general exceptions)
- ✅ HTTP context and authentication handling
- ✅ Both IsUserAuthorizedAsync and GetAuthorizedTeamAsync methods
- ✅ Logging at all decision points

**What NOT to Cover**:

- ❌ HttpContext internal implementation
- ❌ ClaimsPrincipal internal methods
- ❌ AuthenticateResult internal implementation
- ❌ Logger internal implementation

## Implementation Notes

### Mocking HttpContext and Authentication

HttpContext mocking is complex due to its many dependencies. Key points:

```csharp
// Use NSubstitute for HttpContext
var httpContext = Substitute.For<HttpContext>();

// Mock authentication result with token
var authProperties = new AuthenticationProperties();
authProperties.StoreTokens(new[]
{
    new AuthenticationToken { Name = "access_token", Value = "token_value" }
});

var authResult = AuthenticateResult.Success(
    new AuthenticationTicket(principal, authProperties, "GitHub"));

httpContext.AuthenticateAsync(Arg.Any<string>()).Returns(authResult);
```

### Testing Authentication Failure

```csharp
var failedResult = AuthenticateResult.Fail("Authentication failed");
httpContext.AuthenticateAsync(Arg.Any<string>()).Returns(failedResult);
```

### Team Format Validation Testing

The service expects "org/team" format. Test both valid and invalid formats:

- Valid: "myorg/myteam", "Company/Engineering"
- Invalid: "no-slash", "/team", "org/", "org/team/extra"

### Exception Testing Strategy

Test three types of exceptions:

1. **ConfigurationNotFoundException** - Missing config.yaml
2. **InvalidConfigurationException** - Malformed YAML
3. **General Exception** - Unexpected errors (GitHub API, network, etc.)

### Logging Verification

Use flexible logging verification to avoid brittle tests:

```csharp
// Verify log level called
_logger.ReceivedWithAnyArgs().LogInformation(default, default);
_logger.ReceivedWithAnyArgs().LogWarning(default(string));
_logger.ReceivedWithAnyArgs().LogError(default(Exception), default(string));

// Or verify specific message pattern
_logger.Received().LogError(
    Arg.Any<ConfigurationNotFoundException>(),
    Arg.Is<string>(s => s.Contains("Configuration not found")));
```

## Edge Cases to Consider

### 1. Null vs Empty String Handling

- `authorizedTeam == null` vs `authorizedTeam == string.Empty`
- `accessToken == null` vs `accessToken == string.Empty`

Both should return `false`, but may log differently.

### 2. Whitespace in Team Format

```csharp
[InlineData(" org/team ", "org", "team")] // Should trim work?
[InlineData("org / team", "org ", " team")] // Spaces in org/team names
```

Consider if the service should trim whitespace or reject it.

### 3. Case Sensitivity

GitHub team slugs are case-insensitive. Verify service handles this correctly.

### 4. Special Characters in Team Names

```csharp
[InlineData("my-org/my-team", "my-org", "my-team")]
[InlineData("org_name/team_slug", "org_name", "team_slug")]
```

## Running Tests

```bash
# Run all AuthorizationService tests
dotnet test --filter FullyQualifiedName~AuthorizationServiceTests

# Run specific test
dotnet test --filter FullyQualifiedName~AuthorizationServiceTests.IsUserAuthorizedAsync_WhenUserIsMemberOfAuthorizedTeam_ReturnsTrue

# Run with coverage
dotnet test --filter FullyQualifiedName~AuthorizationServiceTests /p:CollectCoverage=true

# Watch mode for TDD
dotnet watch test --filter FullyQualifiedName~AuthorizationServiceTests

# Run only IsUserAuthorizedAsync tests
dotnet test --filter "FullyQualifiedName~AuthorizationServiceTests&FullyQualifiedName~IsUserAuthorizedAsync"

# Run only GetAuthorizedTeamAsync tests
dotnet test --filter "FullyQualifiedName~AuthorizationServiceTests&FullyQualifiedName~GetAuthorizedTeamAsync"
```

## Success Criteria

- ✅ All 18 test scenarios pass
- ✅ Code coverage > 85%
- ✅ All test cases from test plan (TC-AUTH-001, TC-AUTH-002, TC-AUTH-003) covered
- ✅ Test execution time < 5 seconds total
- ✅ No flaky tests (tests pass consistently)
- ✅ Proper test isolation (tests can run in any order)
- ✅ Clear test names following `MethodName_WhenCondition_ExpectedBehavior` pattern
- ✅ Comprehensive assertion messages with `because` parameter
- ✅ All exception types tested (ConfigurationNotFoundException, InvalidConfigurationException, generic)
- ✅ Both public methods (IsUserAuthorizedAsync, GetAuthorizedTeamAsync) fully tested

## Integration with Existing Tests

### Dependencies Tested Elsewhere

- **ConfigurationService**: Tested in `ConfigurationServiceTests.cs`
- **GitHubService**: Tested in `GitHubServiceTests.cs` (to be created)

### Related Test Coverage

This test suite focuses on:

- Authorization logic and team membership validation
- OAuth token extraction and handling
- Configuration-based access control

Integration tests should cover:

- End-to-end OAuth flow with real authentication
- Team membership checks against test GitHub organization

## Next Steps After Implementation

1. Review test coverage report
2. Consider integration tests for OAuth flow (with Playwright)
3. Add stress tests for concurrent authorization checks
4. Document authorization patterns in project docs
5. Update authentication.md with test examples
6. Plan E2E tests for complete login workflow (TC-AUTH-001 full coverage)

## Security Testing Notes

This service is critical for application security. Additional considerations:

### Security-Specific Test Cases to Add

1. **Token Validation**: Ensure expired/revoked tokens are rejected
2. **Session Fixation**: Verify tokens are not reused across sessions
3. **Rate Limiting**: Test behavior under rapid authorization requests
4. **Timing Attacks**: Ensure consistent response times for authorized/unauthorized

### Future Enhancement Ideas

- Test OAuth state parameter validation (CSRF protection)
- Test token refresh scenarios
- Test concurrent authorization requests for same user
- Test authorization caching (if implemented)

## Common Pitfalls to Avoid

❌ **Don't**: Mock `ClaimsPrincipal` - use real instance with test claims

✅ **Do**: Create helper method `CreateClaimsPrincipal(username)`

❌ **Don't**: Test ASP.NET Core authentication internals

✅ **Do**: Test service behavior given authentication states

❌ **Don't**: Hard-code tokens or team names

✅ **Do**: Use Bogus/Faker for realistic test data

❌ **Don't**: Test logging message exact text (brittle)

✅ **Do**: Verify log level and key information presence

❌ **Don't**: Skip exception testing (security implications)

✅ **Do**: Test all exception paths thoroughly