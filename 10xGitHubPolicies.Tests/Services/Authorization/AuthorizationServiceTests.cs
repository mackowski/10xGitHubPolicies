using System.Security.Claims;
using _10xGitHubPolicies.App.Exceptions;
using _10xGitHubPolicies.App.Options;
using _10xGitHubPolicies.App.Services.Authorization;
using _10xGitHubPolicies.App.Services.Configuration;
using _10xGitHubPolicies.App.Services.Configuration.Models;
using _10xGitHubPolicies.App.Services.GitHub;
using Bogus;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

        // Create TestModeOptions mock
        var testModeOptions = Substitute.For<IOptions<TestModeOptions>>();
        testModeOptions.Value.Returns(new TestModeOptions { Enabled = false });

        // Create system under test
        _sut = new AuthorizationService(
            _gitHubService,
            _configService,
            _logger,
            _httpContextAccessor,
            testModeOptions);
    }

    #region IsUserAuthorizedAsync Tests

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
    }

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

        // Verify no GitHub API calls made
        await _gitHubService.DidNotReceive().IsUserMemberOfTeamAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task IsUserAuthorizedAsync_WhenAuthenticationFails_ReturnsFalse()
    {
        // Arrange
        var user = CreateClaimsPrincipal("testuser");
        var httpContext = Substitute.For<HttpContext>();

        // Mock failed authentication
        var failedResult = AuthenticateResult.Fail("Authentication failed");

        // Mock the authentication service
        var authService = Substitute.For<Microsoft.AspNetCore.Authentication.IAuthenticationService>();
        authService.AuthenticateAsync(Arg.Any<HttpContext>(), Arg.Any<string?>())
            .Returns(failedResult);

        // Mock the service provider to return the authentication service
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(Microsoft.AspNetCore.Authentication.IAuthenticationService))
            .Returns(authService);

        httpContext.RequestServices.Returns(serviceProvider);

        _httpContextAccessor.HttpContext.Returns(httpContext);

        // Act
        var result = await _sut.IsUserAuthorizedAsync(user);

        // Assert
        result.Should().BeFalse(because: "user authentication failed");
    }

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

        await _gitHubService.DidNotReceive().IsUserMemberOfTeamAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

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
    }

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

        await _gitHubService.DidNotReceive().IsUserMemberOfTeamAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

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
    }

    [Theory]
    [InlineData("invalid-team-format")]
    [InlineData("org")]
    [InlineData("org/team/extra")]
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

        await _gitHubService.DidNotReceive().IsUserMemberOfTeamAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

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

        await _gitHubService.DidNotReceive().IsUserMemberOfTeamAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

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

        await _gitHubService.DidNotReceive().IsUserMemberOfTeamAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

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
    }

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

    #endregion

    #region GetAuthorizedTeamAsync Tests

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
    }

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
    }

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
    }

    #endregion

    #region Helper Methods

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

        // Mock the authentication service
        var authService = Substitute.For<Microsoft.AspNetCore.Authentication.IAuthenticationService>();
        authService.AuthenticateAsync(Arg.Any<HttpContext>(), Arg.Any<string?>())
            .Returns(authResult);

        // Mock the service provider to return the authentication service
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(Microsoft.AspNetCore.Authentication.IAuthenticationService))
            .Returns(authService);

        httpContext.RequestServices.Returns(serviceProvider);

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

    #endregion
}

