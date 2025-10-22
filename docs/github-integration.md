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
    - `Task<string> GetFileContentAsync(string repoName, string path)`: Retrieves file content from a repository. Returns Base64-encoded content or `null` if the file doesn't exist.

### `GitHubService`
- **Purpose**: Implements `IGitHubService` and handles the authentication and caching of the GitHub App installation token.
- **Authentication**:
    - Generates a JSON Web Token (JWT) using the GitHub App's private key.
    - Uses the JWT to get an installation token for the GitHub App.
    - The `GetAuthenticatedClient()` method is private and used internally by the public methods.
- **Caching**:
    - Caches the installation token in memory to avoid requesting a new token for every API call.
    - The token is cached for 55 minutes (5 minutes before it expires).
- **Error Handling**:
    - Methods handle `NotFoundException` gracefully by returning `false` or `null` as appropriate.
    - Team membership verification logs warnings when team lookup fails.

## Configuration

The GitHub App settings are configured using the .NET Secret Manager for local development to ensure that private keys and other secrets are not committed to source control. 

### Required Settings

1. **App Credentials** (via Secret Manager):
   - `GitHubApp:AppId`: The GitHub App ID
   - `GitHubApp:InstallationId`: The Installation ID for your organization
   - `GitHubApp:PrivateKey`: The private key (`.pem` file contents)

2. **Organization Name** (in `appsettings.json`):
   - `GitHubApp:OrganizationName`: The GitHub organization name (slug)

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

## Best Practices

1. **Use Dependency Injection**: Always inject `IGitHubService` rather than creating instances directly.
2. **Handle Exceptions**: While the service handles `NotFoundException` internally, be prepared for other exceptions like rate limiting or network issues.
3. **Cache Responses**: Consider caching frequently accessed data (e.g., repository lists) to reduce API calls and avoid rate limits.
4. **Use Async/Await**: All methods are asynchronous; always use `await` to avoid blocking threads.
5. **Respect Rate Limits**: GitHub API has rate limits. Monitor your usage and implement backoff strategies if needed.
6. **Log Appropriately**: Use structured logging to track API calls and diagnose issues.
