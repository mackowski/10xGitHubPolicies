# GitHub Client Factory Pattern

## Overview

The `IGitHubClientFactory` interface and `GitHubClientFactory` implementation provide a testable abstraction for creating `GitHubClient` instances. This pattern enables redirecting GitHub API calls to mock servers (like WireMock.Net) during testing while maintaining clean production code.

## Architecture

### Components

1. **`IGitHubClientFactory`**: Interface for creating GitHubClient instances
2. **`GitHubClientFactory`**: Default implementation supporting custom base URLs
3. **`GitHubService`**: Consumes the factory via dependency injection
4. **`GitHubAppOptions`**: Configuration including optional `BaseUrl` property

## Interface Definition

```csharp
public interface IGitHubClientFactory
{
    /// <summary>
    /// Creates a GitHubClient authenticated with a user or installation access token.
    /// </summary>
    GitHubClient CreateClient(string token);
    
    /// <summary>
    /// Creates a GitHubClient authenticated with a GitHub App JWT token.
    /// </summary>
    GitHubClient CreateAppClient(string jwt);
}
```

## Implementation

```csharp
public class GitHubClientFactory : IGitHubClientFactory
{
    private readonly string? _baseUrl;
    
    public GitHubClientFactory(string? baseUrl = null)
    {
        _baseUrl = baseUrl;
    }
    
    public GitHubClient CreateClient(string token)
    {
        var productHeader = new ProductHeaderValue("10xGitHubPolicies");
        var credentials = new Credentials(token);
        var credentialStore = new InMemoryCredentialStore(credentials);
        
        if (_baseUrl != null)
        {
            return new GitHubClient(productHeader, credentialStore, new Uri(_baseUrl));
        }
        
        return new GitHubClient(productHeader, credentialStore);
    }
    
    public GitHubClient CreateAppClient(string jwt)
    {
        var productHeader = new ProductHeaderValue("10xGitHubPolicies");
        var credentials = new Credentials(jwt, AuthenticationType.Bearer);
        var credentialStore = new InMemoryCredentialStore(credentials);
        
        if (_baseUrl != null)
        {
            return new GitHubClient(productHeader, credentialStore, new Uri(_baseUrl));
        }
        
        return new GitHubClient(productHeader, credentialStore);
    }
}
```

## Dependency Injection Setup

### Production Configuration

In `Program.cs`:

```csharp
// Register GitHub client factory with optional base URL from configuration
builder.Services.AddSingleton<IGitHubClientFactory>(sp =>
{
    var options = sp.GetRequiredService<IOptions<GitHubAppOptions>>();
    return new GitHubClientFactory(options.Value.BaseUrl);
});

builder.Services.AddSingleton<IGitHubService, GitHubService>();
```

### GitHubService Integration

The `GitHubService` accepts an optional factory parameter:

```csharp
public class GitHubService : IGitHubService
{
    private readonly IGitHubClientFactory _clientFactory;
    
    public GitHubService(
        IOptions<GitHubAppOptions> options, 
        ILogger<GitHubService> logger, 
        IMemoryCache cache,
        IGitHubClientFactory? clientFactory = null)
    {
        _options = options.Value;
        _logger = logger;
        _cache = cache;
        _clientFactory = clientFactory ?? new GitHubClientFactory(_options.BaseUrl);
    }
    
    // Use factory throughout the service
    private async Task<GitHubClient> GetAuthenticatedClient()
    {
        var token = await GetOrCreateInstallationTokenAsync();
        return _clientFactory.CreateClient(token);
    }
}
```

## Testing Usage

### Integration Tests

```csharp
public abstract class GitHubServiceIntegrationTestBase : IAsyncLifetime
{
    protected readonly WireMockServer MockServer;
    protected readonly GitHubService Sut;
    protected readonly GitHubAppOptions Options;
    protected readonly IGitHubClientFactory ClientFactory;
    
    protected GitHubServiceIntegrationTestBase()
    {
        // Start WireMock server
        MockServer = WireMockServer.Start();
        
        // Configure options to point to WireMock
        Options = new GitHubAppOptions
        {
            AppId = 123456,
            InstallationId = 7891011,
            OrganizationName = "test-org",
            PrivateKey = GenerateTestPrivateKey(),
            BaseUrl = MockServer.Url  // Redirect to WireMock!
        };
        
        // Create factory with WireMock URL
        ClientFactory = new GitHubClientFactory(MockServer.Url);
        
        // Inject factory into service
        Sut = new GitHubService(
            Options.Create(Options), 
            logger, 
            cache, 
            ClientFactory
        );
    }
}
```

### Contract Tests

```csharp
public abstract class GitHubContractTestBase : IAsyncLifetime
{
    protected readonly WireMockServer MockServer;
    protected readonly GitHubService Sut;
    protected readonly IGitHubClientFactory ClientFactory;
    
    protected GitHubContractTestBase()
    {
        MockServer = WireMockServer.Start();
        
        var options = CreateTestOptions();
        options.BaseUrl = MockServer.Url;
        
        ClientFactory = new GitHubClientFactory(MockServer.Url);
        Sut = new GitHubService(
            Options.Create(options), 
            logger, 
            cache, 
            ClientFactory
        );
    }
}
```

## Important: Octokit Path Prefix

When using a custom `BaseUrl`, Octokit automatically prepends `/api/v3/` to all API paths. This is GitHub Enterprise mode behavior.

### WireMock Configuration

All WireMock stubs must include the `/api/v3/` prefix:

```csharp
// ✅ CORRECT - includes /api/v3/ prefix
MockServer
    .Given(Request.Create()
        .WithPath("/api/v3/repos/test-org/test-repo")
        .UsingGet())
    .RespondWith(Response.Create()
        .WithStatusCode(200)
        .WithBody(repositoryJson));

// ❌ INCORRECT - missing /api/v3/ prefix
MockServer
    .Given(Request.Create()
        .WithPath("/repos/test-org/test-repo")  // Will not match!
        .UsingGet())
    .RespondWith(Response.Create()
        .WithStatusCode(200)
        .WithBody(repositoryJson));
```

### Authentication Endpoint

The installation token endpoint also requires the prefix:

```csharp
MockServer
    .Given(Request.Create()
        .WithPath("/api/v3/app/installations/*/access_tokens")
        .UsingPost())
    .RespondWith(Response.Create()
        .WithStatusCode(201)
        .WithBody(tokenJson));
```

## Configuration Options

### GitHubAppOptions

```csharp
public class GitHubAppOptions
{
    public long AppId { get; set; }
    public string PrivateKey { get; set; } = string.Empty;
    public long InstallationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional base URL for GitHub API. If null, uses default GitHub API (https://api.github.com).
    /// Primarily used for testing with WireMock.
    /// </summary>
    public string? BaseUrl { get; set; }
}
```

### appsettings.json (Production)

```json
{
  "GitHubApp": {
    "OrganizationName": "your-org"
    // BaseUrl is null by default - uses standard GitHub API
  }
}
```

### Test Configuration

```csharp
var options = new GitHubAppOptions
{
    AppId = 123456,
    InstallationId = 7891011,
    OrganizationName = "test-org",
    PrivateKey = GenerateTestPrivateKey(),
    BaseUrl = "http://localhost:8080"  // WireMock server
};
```

## Benefits

### Testability
- Enables HTTP-level mocking without modifying production code
- Supports integration testing without real API calls
- Allows contract testing to validate API response structures

### Maintainability
- Centralized GitHubClient creation logic
- Consistent authentication handling
- Easy to update client configuration

### Flexibility
- Supports both production and test scenarios
- Optional dependency injection (backward compatible)
- Configurable base URL for different environments

## Best Practices

1. **Production Code**: Never set `BaseUrl` in production configuration
2. **Test Code**: Always set `BaseUrl` to point to WireMock server
3. **Path Prefixes**: Remember to include `/api/v3/` in all WireMock stubs
4. **Factory Registration**: Register factory as singleton in DI container
5. **Backward Compatibility**: GitHubService creates default factory if none provided

## Related Documentation

- **[GitHub Integration](./github-integration.md)**: Complete guide to GitHub API integration
- **[Testing Strategy](./testing-strategy.md)**: Multi-level testing approach
- **Integration Tests**: `10xGitHubPolicies.Tests.Integration` project
- **Contract Tests**: `10xGitHubPolicies.Tests.Contracts` project

## Troubleshooting

### Issue: WireMock stubs not matching

**Symptom**: Tests fail with 404 errors even though stubs are configured

**Solution**: Verify that WireMock stubs include the `/api/v3/` path prefix

```csharp
// Check WireMock logs
protected void LogWireMockRequests()
{
    var logEntries = MockServer.LogEntries.ToList();
    Console.WriteLine($"=== WireMock Log ({logEntries.Count} requests) ===");
    foreach (var entry in logEntries)
    {
        Console.WriteLine($"{entry.RequestMessage.Method} {entry.RequestMessage.Path}");
        Console.WriteLine($"Response: {entry.ResponseMessage.StatusCode}");
    }
}
```

### Issue: Factory not being used in tests

**Symptom**: Tests still hitting real GitHub API

**Solution**: Ensure factory is passed to GitHubService constructor

```csharp
// ✅ CORRECT
var sut = new GitHubService(options, logger, cache, clientFactory);

// ❌ INCORRECT - uses default factory
var sut = new GitHubService(options, logger, cache);
```

### Issue: BaseUrl not being respected

**Symptom**: Requests still go to api.github.com

**Solution**: Verify BaseUrl is set in both GitHubAppOptions and GitHubClientFactory

```csharp
// Both must match
options.BaseUrl = mockServer.Url;
var factory = new GitHubClientFactory(mockServer.Url);
```

