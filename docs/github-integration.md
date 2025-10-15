# GitHub Integration with Octokit.net

This document outlines the approach to integrating with the GitHub API using the `Octokit.net` library.

## Services

### `IGitHubService`
- **Purpose**: Defines the contract for interacting with the GitHub API.
- **Methods**:
    - `Task<GitHubClient> GetAuthenticatedClient()`: Returns an authenticated `GitHubClient` instance.

### `GitHubService`
- **Purpose**: Implements `IGitHubService` and handles the authentication and caching of the `GitHubClient`.
- **Authentication**:
    - Generates a JSON Web Token (JWT) using the GitHub App's private key.
    - Uses the JWT to get an installation token for the GitHub App.
- **Caching**:
    - Caches the installation token in memory to avoid requesting a new token for every API call.
    - The token is cached for 55 minutes (5 minutes before it expires).

## Configuration

The GitHub App settings are configured using the .NET Secret Manager for local development to ensure that private keys and other secrets are not committed to source control. Refer to the main `README.md` for setup instructions.

## Usage

Inject `IGitHubService` into your services and use it to get an authenticated `GitHubClient`:

```csharp
public class MyService
{
    private readonly IGitHubService _gitHubService;

    public MyService(IGitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    public async Task DoSomethingWithGitHub()
    {
        var client = await _gitHubService.GetAuthenticatedClient();
        // Use the client to interact with the GitHub API
    }
}
```
