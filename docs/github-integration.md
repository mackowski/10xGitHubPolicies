# GitHub Integration with Octokit.net

This document outlines the approach to integrating with the GitHub API using the `Octokit.net` library.

## Services

### `IGitHubService`
- **Purpose**: Defines the contract for interacting with the GitHub API.
- **Methods**:
    - `Task<IReadOnlyList<Repository>> GetOrganizationRepositoriesAsync()`: Retrieves all repositories in the configured organization.
    - `Task<bool> FileExistsAsync(long repositoryId, string filePath)`: Checks if a file exists in a repository.
    - `Task<Repository> GetRepositorySettingsAsync(long repositoryId)`: Gets repository settings and metadata.
    - `Task<Issue> CreateIssueAsync(long repositoryId, string title, string body, IEnumerable<string> labels)`: Creates an issue in a repository with the specified title, body, and labels.
    - `Task ArchiveRepositoryAsync(long repositoryId)`: Archives a repository.
    - `Task<bool> IsUserMemberOfTeamAsync(string userAccessToken, string org, string teamSlug)`: Verifies if a user is a member of a specific GitHub team (used for access control).
    - `Task<string?> GetFileContentAsync(string repoName, string path)`: Retrieves file content from a repository. Returns Base64-encoded content or `null` if the file doesn't exist.
    - `Task<string?> GetWorkflowPermissionsAsync(long repositoryId)`: Gets the default workflow permissions for a repository. Returns "read" or "write", or null if Actions are disabled.
    - `Task<IReadOnlyList<Issue>> GetOpenIssuesAsync(long repositoryId, string label)`: Retrieves all open issues for a repository filtered by a specific label. Returns an empty list if the repository is not found. Used for duplicate issue prevention.
    - `Task<IReadOnlyList<Organization>> GetUserOrganizationsAsync(string userAccessToken)`: Retrieves all organizations the authenticated user is a member of.
    - `Task<IReadOnlyList<Team>> GetOrganizationTeamsAsync(string userAccessToken, string org)`: Retrieves all teams in an organization.
    
    **E2E Testing Methods** (for automated testing and repository management):
    - `Task<Repository> CreateRepositoryAsync(string name, string description = "", bool isPrivate = false)`: Creates a new repository in the organization.
    - `Task CreateFileAsync(long repositoryId, string path, string content, string commitMessage = "")`: Creates a new file in a repository.
    - `Task UpdateFileAsync(long repositoryId, string path, string content, string commitMessage = "")`: Updates an existing file in a repository.
    - `Task DeleteFileAsync(long repositoryId, string path, string commitMessage = "")`: Deletes a file from a repository by repository ID.
    - `Task DeleteFileAsync(string repositoryName, string path, string commitMessage = "")`: Deletes a file from a repository by repository name.
    - `Task UpdateWorkflowPermissionsAsync(long repositoryId, string permissions)`: Updates workflow permissions for a repository ("read" or "write").
    - `Task UnarchiveRepositoryAsync(long repositoryId)`: Unarchives a previously archived repository.
    - `Task CloseIssueAsync(long repositoryId, int issueNumber)`: Closes an issue by number.
    - `Task DeleteRepositoryAsync(string repositoryName)`: Deletes a repository by name.
    - `Task<IReadOnlyList<Issue>> GetRepositoryIssuesAsync(string repositoryName)`: Gets all issues for a repository by name.

### `GitHubService`
- **Purpose**: Implements `IGitHubService` and handles the authentication and caching of the GitHub App installation token.
- **Authentication**:
    - Generates a JSON Web Token (JWT) using the GitHub App's private key.
    - Uses the JWT to get an installation token for the GitHub App.
    - Uses `IGitHubClientFactory` to create GitHubClient instances with proper configuration.
    - The `GetAuthenticatedClient()` method is private and used internally by the public methods.
- **Caching**:
    - Caches the installation token in memory to avoid requesting a new token for every API call.
    - The token is cached for 55 minutes (5 minutes before it expires).
- **Error Handling**:
    - Methods handle `NotFoundException` gracefully by returning `false` or `null` as appropriate.
    - Team membership verification logs warnings when team lookup fails.
- **Testability**:
    - Accepts an optional `IGitHubClientFactory` parameter for dependency injection.
    - Supports custom base URLs via `GitHubAppOptions.BaseUrl` for testing with WireMock.

### `IGitHubClientFactory`
- **Purpose**: Factory interface for creating `GitHubClient` instances with optional custom base URLs and HTTP configuration.
- **Methods**:
    - `GitHubClient CreateClient(string token)`: Creates a client authenticated with a user or installation access token.
    - `GitHubClient CreateAppClient(string jwt)`: Creates a client authenticated with a GitHub App JWT token.
- **Implementation**: `GitHubClientFactory` supports:
    - Custom base URLs for testing scenarios with WireMock.Net
    - Custom `HttpClientHandler` for advanced HTTP configuration (e.g., SSL certificate handling)
    - Automatic Enterprise mode detection when using custom base URLs
    - Proper connection setup for both production and test environments

## Configuration

The GitHub App settings are configured using the .NET Secret Manager for local development to ensure that private keys and other secrets are not committed to source control. 

### Required Settings

1. **App Credentials** (via Secret Manager):
   - `GitHubApp:AppId`: The GitHub App ID
   - `GitHubApp:InstallationId`: The Installation ID for your organization
   - `GitHubApp:PrivateKey`: The private key (`.pem` file contents)

2. **Organization Name** (in `appsettings.json`):
   - `GitHubApp:OrganizationName`: The GitHub organization name (slug)

3. **Optional Testing Configuration** (in `appsettings.json` or test configuration):
   - `GitHubApp:BaseUrl`: Custom base URL for GitHub API (primarily for testing with WireMock.Net)
   - When not specified, defaults to the standard GitHub API URL (`https://api.github.com`)

Refer to the main `README.md` for detailed setup instructions.

## Usage

Inject `IGitHubService` into your services and use its methods to interact with GitHub:

### Example: Scanning Repositories

```csharp
public class ScanningService
{
    private readonly IGitHubService _gitHubService;
    private readonly ILogger<ScanningService> _logger;

    public ScanningService(IGitHubService gitHubService, ILogger<ScanningService> logger)
    {
        _gitHubService = gitHubService;
        _logger = logger;
    }

    public async Task ScanRepositoriesAsync()
    {
        // Get all organization repositories
        var repositories = await _gitHubService.GetOrganizationRepositoriesAsync();
        
        foreach (var repo in repositories)
        {
            _logger.LogInformation("Processing repository: {RepoName}", repo.Name);
            
            // Check if required file exists
            bool hasAgentsFile = await _gitHubService.FileExistsAsync(repo.Id, "AGENTS.md");
            
            if (!hasAgentsFile)
            {
                // Create an issue for non-compliance
                await _gitHubService.CreateIssueAsync(
                    repo.Id,
                    "Missing AGENTS.md file",
                    "This repository is missing the required AGENTS.md file.",
                    new[] { "policy-violation", "documentation" }
                );
            }
        }
    }
}
```

### Example: Retrieving Configuration Files

```csharp
public class ConfigurationService
{
    private readonly IGitHubService _gitHubService;

    public ConfigurationService(IGitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    public async Task<string> GetConfigurationAsync()
    {
        // Get file content (Base64 encoded)
        var base64Content = await _gitHubService.GetFileContentAsync(".github", "config.yaml");
        
        if (base64Content == null)
        {
            throw new FileNotFoundException("Configuration file not found");
        }
        
        // Decode the content
        var yamlContent = Encoding.UTF8.GetString(Convert.FromBase64String(base64Content));
        return yamlContent;
    }
}
```

### Example: Enforcing Team-Based Access Control

```csharp
public class AccessControlService
{
    private readonly IGitHubService _gitHubService;

    public AccessControlService(IGitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    public async Task<bool> IsUserAuthorizedAsync(string userToken, string teamSlug)
    {
        return await _gitHubService.IsUserMemberOfTeamAsync(
            userToken, 
            "my-org", 
            teamSlug
        );
    }
}
```

### Example: Checking Workflow Permissions

```csharp
public async Task CheckRepositorySecurityAsync(long repositoryId)
{
    var permissions = await _gitHubService.GetWorkflowPermissionsAsync(repositoryId);
    
    if (permissions == "write")
    {
        _logger.LogWarning("Repository {RepoId} has write permissions for workflows", repositoryId);
    }
}
```

### Example: Checking for Duplicate Issues

```csharp
public async Task CreateIssueWithDuplicateCheckAsync(long repositoryId, string title, string body, string label)
{
    // Check for existing open issues with the same label
    var existingIssues = await _gitHubService.GetOpenIssuesAsync(repositoryId, label);
    
    // Check if an issue with the same title already exists
    var duplicateIssue = existingIssues.FirstOrDefault(issue => 
        issue.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
    
    if (duplicateIssue != null)
    {
        _logger.LogInformation("Issue already exists: {IssueUrl}", duplicateIssue.HtmlUrl);
        return;
    }
    
    // Create the issue if no duplicate found
    var newIssue = await _gitHubService.CreateIssueAsync(
        repositoryId,
        title,
        body,
        new[] { label }
    );
    
    _logger.LogInformation("Created issue: {IssueUrl}", newIssue.HtmlUrl);
}
```

### Example: E2E Testing - Creating Test Repositories

```csharp
public async Task SetupTestRepositoryAsync(string repoName)
{
    // Create a test repository
    var repository = await _gitHubService.CreateRepositoryAsync(
        repoName,
        "Test repository for E2E testing",
        isPrivate: false
    );
    
    // Create required files for compliance
    await _gitHubService.CreateFileAsync(
        repository.Id,
        "AGENTS.md",
        "# Test Agents File\n\nThis is a test file.",
        "Add AGENTS.md file"
    );
    
    // Update workflow permissions to read-only
    await _gitHubService.UpdateWorkflowPermissionsAsync(repository.Id, "read");
    
    return repository;
}

public async Task CleanupTestRepositoryAsync(string repoName)
{
    // Get all issues
    var issues = await _gitHubService.GetRepositoryIssuesAsync(repoName);
    
    // Close all open issues
    foreach (var issue in issues.Where(i => i.State.Value == ItemState.Open))
    {
        await _gitHubService.CloseIssueAsync(issue.Repository.Id, issue.Number);
    }
    
    // Delete the repository
    await _gitHubService.DeleteRepositoryAsync(repoName);
}
```

### Example: E2E Testing - Managing Repository Files

```csharp
public async Task SetupCompliantRepositoryAsync(long repositoryId)
{
    // Create AGENTS.md file
    await _gitHubService.CreateFileAsync(
        repositoryId,
        "AGENTS.md",
        "# AGENTS.md\n\nRepository agents documentation.",
        "Add AGENTS.md"
    );
    
    // Create catalog-info.yaml file
    await _gitHubService.CreateFileAsync(
        repositoryId,
        "catalog-info.yaml",
        "apiVersion: backstage.io/v1alpha1\nkind: Component",
        "Add catalog-info.yaml"
    );
}

public async Task MakeRepositoryNonCompliantAsync(long repositoryId)
{
    // Delete AGENTS.md to make repository non-compliant
    await _gitHubService.DeleteFileAsync(
        repositoryId,
        "AGENTS.md",
        "Remove AGENTS.md for testing"
    );
}
```

## Best Practices

1. **Use Dependency Injection**: Always inject `IGitHubService` rather than creating instances directly.
2. **Handle Exceptions**: While the service handles `NotFoundException` internally, be prepared for other exceptions like rate limiting or network issues.
3. **Cache Responses**: Consider caching frequently accessed data (e.g., repository lists) to reduce API calls and avoid rate limits.
4. **Use Async/Await**: All methods are asynchronous; always use `await` to avoid blocking threads.
5. **Respect Rate Limits**: GitHub API has rate limits. Monitor your usage and implement backoff strategies if needed.
6. **Log Appropriately**: Use structured logging to track API calls and diagnose issues.

## Testing

The application includes comprehensive testing for GitHub API integrations across multiple levels:

### Test Infrastructure

**GitHubClientFactory Pattern**:
- `IGitHubClientFactory` interface enables dependency injection of GitHubClient creation
- `GitHubClientFactory` implementation supports custom base URLs and HttpClientHandler for testing
- `GitHubService` accepts an optional factory parameter, defaulting to production configuration
- Enables redirecting API calls to WireMock.Net for integration and contract testing
- Supports custom SSL certificate handling for test environments with self-signed certificates

**Test Configuration**:
```csharp
// In test setup
var options = new GitHubAppOptions
{
    AppId = 123456,
    InstallationId = 7891011,
    OrganizationName = "test-org",
    PrivateKey = GenerateTestPrivateKey(),
    BaseUrl = mockServer.Url  // Point to WireMock
};

var httpClientHandler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true
};
var clientFactory = new GitHubClientFactory(mockServer.Url, httpClientHandler);
var sut = new GitHubService(Options.Create(options), logger, cache, clientFactory);
```

### Testing Levels

For comprehensive guidance on testing GitHub API integrations, see **[Testing Strategy](./testing-strategy.md)**:

1. **Unit Testing**: Mock `IGitHubService` with NSubstitute for fast, isolated tests
   - Test business logic without network calls
   - Fast feedback for service logic

2. **Integration Testing**: Use WireMock.Net for HTTP-level mocking
   - Test actual HTTP interactions without real API calls
   - Simulate rate limits, errors, and edge cases
   - 33 tests covering all GitHubService methods
   - Located in `10xGitHubPolicies.Tests.Integration`

3. **Contract Testing**: Use NJsonSchema and Verify.NET to detect GitHub API breaking changes
   - JSON Schema validation for critical responses
   - Snapshot testing for response structure stability
   - 11 tests validating API contracts
   - Located in `10xGitHubPolicies.Tests.Contracts`

### Running GitHub Integration Tests

```bash
# Run all integration tests
dotnet test 10xGitHubPolicies.Tests.Integration

# Run specific test categories
dotnet test --filter "Category=Integration"

# Run contract tests
dotnet test 10xGitHubPolicies.Tests.Contracts
dotnet test --filter "Category=Contract"
```

### Important Notes

**Octokit Path Prefix**: When using a custom `BaseUrl`, Octokit automatically prepends `/api/v3/` to all paths (GitHub Enterprise mode). WireMock stubs must include this prefix:

```csharp
// Correct - includes /api/v3/ prefix
MockServer
    .Given(Request.Create()
        .WithPath("/api/v3/repos/test-org/test-repo")
        .UsingGet())
    .RespondWith(Response.Create()
        .WithStatusCode(200)
        .WithBody(repositoryJson));
```
