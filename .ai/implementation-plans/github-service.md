# IGitHubService Implementation Plan

## 1. Service Overview

The `IGitHubService` is a singleton service that acts as a centralized and authenticated gateway for all interactions with the GitHub API. It abstracts the underlying `Octokit.net` client, providing a clean and testable interface for other application services.

Its primary responsibility is to manage the application-level authentication using the project's GitHub App credentials. This involves generating a JSON Web Token (JWT) from a private key, exchanging it for a short-lived installation access token, and caching that token for reuse. It also provides a method to perform actions on behalf of an authenticated user using their OAuth token.

## 2. Input Details

### 2.1. Configuration Inputs

The service will be configured via `appsettings.json` and mapped to a strongly-typed `GitHubAppOptions` class.

| Setting               | Type   | Description                                                                 | Example                 |
| --------------------- | ------ | --------------------------------------------------------------------------- | ----------------------- |
| `AppId`               | `long` | The unique ID of the GitHub App.                                            | `123456`                |
| `InstallationId`      | `long` | The ID of the installation of the App into the target organization.         | `789012`                |
| `PrivateKey`          | `string` | The PEM-formatted private key for the GitHub App. **Must use a secure source like .NET Secret Manager or Azure Key Vault.** | `"-----BEGIN RSA PRIVATE KEY-----\n..."` |
| `OrganizationName`    | `string` | The name of the GitHub organization the app is installed in.                 | `"my-org"`              |

### 2.2. Method Inputs

| Method                         | Parameter           | Type                      | Description                                                  |
| ------------------------------ | ------------------- | ------------------------- | ------------------------------------------------------------ |
| `GetOrganizationRepositoriesAsync` | (none)              | -                         | -                                                            |
| `FileExistsAsync`              | `repositoryId`      | `long`                    | The unique numeric ID of the repository from the GitHub API. |
|                                | `filePath`          | `string`                  | The full path to the file within the repository (e.g., `README.md`). |
| `GetRepositorySettingsAsync`   | `repositoryId`      | `long`                    | The unique numeric ID of the repository.                     |
| `CreateIssueAsync`             | `repositoryId`      | `long`                    | The unique numeric ID of the repository.                     |
|                                | `title`             | `string`                  | The title of the GitHub issue.                               |
|                                | `body`              | `string`                  | The markdown content of the issue body.                      |
|                                | `labels`            | `IEnumerable<string>`     | A collection of labels to apply to the issue.                |
| `ArchiveRepositoryAsync`       | `repositoryId`      | `long`                    | The unique numeric ID of the repository.                     |
| `IsUserMemberOfTeamAsync`      | `userAccessToken` | `string`                  | The OAuth access token for the currently logged-in user.     |
|                                | `org`               | `string`                  | The GitHub organization slug.                                |
|                                | `teamSlug`          | `string`                  | The slug for the GitHub team (e.g., `security-team`).        |

## 3. Output Details

| Method                         | Return Type                        | Description                                                                                              |
| ------------------------------ | ---------------------------------- | -------------------------------------------------------------------------------------------------------- |
| `GetOrganizationRepositoriesAsync` | `Task<IReadOnlyList<Repository>>`  | A read-only list of all non-archived repositories in the configured organization.                        |
| `FileExistsAsync`              | `Task<bool>`                       | `true` if the file exists at the specified path, otherwise `false`.                                      |
| `GetRepositorySettingsAsync`   | `Task<Repository>`                 | An `Octokit.Repository` object containing settings and metadata.                                         |
| `CreateIssueAsync`             | `Task<Issue>`                      | An `Octokit.Issue` object representing the newly created issue.                                          |
| `ArchiveRepositoryAsync`       | `Task`                             | A task that completes when the repository has been successfully archived.                                |
| `IsUserMemberOfTeamAsync`      | `Task<bool>`                       | `true` if the user is a member of the specified team, otherwise `false`.                                 |

## 4. Data Flow

### 4.1. Application-Authenticated Flow (Default)

This flow applies to all methods except `IsUserMemberOfTeamAsync`. The service uses its GitHub App credentials to act on behalf of the application.

```mermaid
sequenceDiagram
    participant Calling Service
    participant GitHubService
    participant InMemoryCache
    participant GitHub API

    Calling Service->>GitHubService: Call method (e.g., GetOrganizationRepositoriesAsync)
    activate GitHubService
    GitHubService->>InMemoryCache: Has valid Installation Token?
    alt Token is valid
        InMemoryCache-->>GitHubService: Return cached token
    else Token is missing or expired
        GitHubService->>GitHubService: Generate JWT from Private Key
        GitHubService->>GitHub API: Request Installation Token using JWT
        activate GitHub API
        GitHub API-->>GitHubService: Return new Installation Token
        deactivate GitHub API
        GitHubService->>InMemoryCache: Store new token with 55-min expiry
    end
    GitHubService->>GitHubService: Create Octokit client with token
    GitHubService->>GitHub API: Make API request (e.g., get repositories)
    activate GitHub API
    GitHub API-->>GitHubService: Return API response
    deactivate GitHub API
    GitHubService-->>Calling Service: Return result
    deactivate GitHubService
```

### 4.2. User-Authenticated Flow (`IsUserMemberOfTeamAsync`)

This flow uses the user's OAuth token to check their specific permissions.

```mermaid
sequenceDiagram
    participant Calling Service
    participant GitHubService
    participant GitHub API

    Calling Service->>GitHubService: Call IsUserMemberOfTeamAsync(userToken, org, team)
    activate GitHubService
    GitHubService->>GitHubService: Create temporary Octokit client with userToken
    GitHubService->>GitHub API: Check team membership for authenticated user
    activate GitHub API
    GitHub API-->>GitHubService: Return membership status
    deactivate GitHub API
    GitHubService-->>Calling Service: Return true or false
    deactivate GitHubService
```

## 5. Security Considerations

-   **Secret Management**: The `PrivateKey` for the GitHub App is a high-value secret. It **must not** be stored in `appsettings.json` in source control. Use the .NET Secret Manager for local development and a secure vault like Azure Key Vault (with Managed Identity) for production environments.
-   **Least Privilege**: The GitHub App permissions must be strictly limited to those required for its function (as defined in `auth-spec.md`: Administration, Contents, Issues, Metadata). Do not grant organization-level admin permissions.
-   **Token Handling**: The user's OAuth token (`userAccessToken`) should be treated as sensitive data. It should only be held in memory for the duration of the API call and never logged or stored.
-   **Input Validation**: Although inputs like `repositoryId` are internal, the service should still operate defensively and log any unexpected or invalid inputs.

## 6. Error Handling

-   The service must implement robust error handling for all `Octokit.net` API calls.
-   It will inject `ILogger<GitHubService>` for structured logging. All exceptions should be logged with relevant context (e.g., method name, repository ID).
-   Specific `Octokit` exceptions should be caught and handled gracefully:
    -   `Octokit.NotFoundException`: Should be handled gracefully, often by returning `null` or an empty collection. For `FileExistsAsync`, this indicates the file does not exist and should return `false`.
    -   `Octokit.RateLimitExceededException`: Should be logged with details about the rate limit. The calling service may need to implement retry logic or back-off strategies based on this.
    -   `Octokit.ApiValidationException`: Indicates a problem with the request (e.g., invalid input). This should be logged as a critical error.
-   The service should re-throw exceptions when it cannot handle them, allowing the calling service (e.g., a Hangfire job) to manage its state (e.g., fail the job for a retry).

## 7. Performance

-   **Singleton Lifetime**: The service must be registered with a singleton lifetime in the DI container to manage the installation token cache effectively.
-   **Token Caching**: Caching the installation token is critical to avoid making authentication requests to the GitHub API on every call. The token is valid for 1 hour; a cache expiry of 55 minutes is recommended to ensure a new token is fetched proactively. A simple in-memory cache (e.g., using a private field with a `DateTimeOffset` for expiry) is sufficient.
-   **Asynchronous Operations**: All methods must be fully asynchronous (`async`/`await`) to ensure the service is non-blocking and scalable.

## 8. Implementation Steps

1.  **Project Setup**:
    *   Add the `Octokit` NuGet package to the `10xGitHubPolicies.App.csproj` project.
    *   Add `Microsoft.Extensions.Http.Polly` for potential retry policies.

2.  **Configuration**:
    *   In `Options/`, create a new class `GitHubAppOptions.cs` with properties for `AppId`, `InstallationId`, `PrivateKey`, and `OrganizationName`.
    *   In `appsettings.json`, add a `GitHubApp` section for these settings. For local development, configure the `PrivateKey` using the .NET Secret Manager: `dotnet user-secrets set "GitHubApp:PrivateKey" "..."`.
    *   In `Program.cs`, register the options: `builder.Services.Configure<GitHubAppOptions>(builder.Configuration.GetSection("GitHubApp"));`.

3.  **Interface and Class Definition**:
    *   In `Services/`, create the `IGitHubService.cs` interface with the methods defined in this plan.
    *   In `Services/`, create the `GitHubService.cs` class implementing `IGitHubService`.

4.  **Constructor and Dependencies**:
    *   Inject `IOptions<GitHubAppOptions>` and `ILogger<GitHubService>` into the `GitHubService` constructor.
    *   Initialize private fields for the options, logger, and a field to cache the installation token and its expiry.

5.  **Implement Core Authentication Logic**:
    *   Create a private `async Task<GitHubClient> GetInstallationClientAsync()` method.
    *   Inside this method, check if the cached token is still valid. If yes, create and return a `new GitHubClient` authenticated with the cached token.
    *   If the token is invalid/expired, implement the JWT generation logic using the `PrivateKey`.
    *   Use the JWT to create a temporary `GitHubClient`.
    *   Call `client.GitHubApps.CreateInstallationToken(installationId)` to get a new installation token.
    *   Cache the new token and its `ExpiresAt` value.
    *   Return a `new GitHubClient` authenticated with the new token.

6.  **Implement Public Methods**:
    *   For each application-authenticated method (e.g., `GetOrganizationRepositoriesAsync`), call `await GetInstallationClientAsync()` first, then use the resulting client to make the appropriate `Octokit.net` API call.
    *   For `IsUserMemberOfTeamAsync`, create a *new, temporary* `GitHubClient` authenticated with the passed-in `userAccessToken` and use it to check team membership. Do not use the installation client for this.

7.  **Dependency Injection Registration**:
    *   In `Program.cs`, register the service as a singleton: `builder.Services.AddSingleton<IGitHubService, GitHubService>();`.
