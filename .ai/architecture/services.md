# 10x GitHub Policy Enforcer - Services and Architecture Plan

This document outlines the planned service-oriented architecture for the application. Each service has a distinct responsibility, communicates through interfaces, and is designed for testability and scalability.

---

## 1. Core Services

### `IConfigurationService`

*   **Responsibility**: Manages the application's configuration sourced from the `config.yaml` file in the organization's `.github` repository.
*   **Key Tasks**:
    *   Fetches the `config.yaml` file from GitHub.
    *   Deserializes the YAML content into strongly-typed C# objects.
    *   Caches the configuration to minimize API calls, with a mechanism to force a refresh.
*   **Proposed Interface**:
    ```csharp
    public interface IConfigurationService
    {
        /// <summary>
        /// Gets the application configuration. Uses a cached version unless forceRefresh is true.
        /// </summary>
        /// <param name="forceRefresh">If true, bypasses the cache and fetches the latest config from GitHub.</param>
        /// <returns>The strongly-typed application configuration.</returns>
        Task<AppConfig> GetConfigAsync(bool forceRefresh = false);
    }
    ```

### `IGitHubService`

*   **Responsibility**: Acts as a centralized and authenticated client for all interactions with the GitHub API, abstracting away the `Octokit.net` client.
*   **Key Tasks**:
    *   Manages authentication for the GitHub App (JWT and installation tokens).
    *   Provides methods for fetching repository data, file contents, and settings.
    *   Executes administrative actions (creating issues, archiving repositories).
    *   Provides methods for checking user permissions.
*   **Proposed Interface**:
    ```csharp
    public interface IGitHubService
    {
        Task<IReadOnlyList<Octokit.Repository>> GetOrganizationRepositoriesAsync();
        Task<bool> FileExistsAsync(long repositoryId, string filePath);
        Task<Octokit.Repository> GetRepositorySettingsAsync(long repositoryId);
        Task<Octokit.Issue> CreateIssueAsync(long repositoryId, string title, string body, IEnumerable<string> labels);
        Task ArchiveRepositoryAsync(long repositoryId);
        Task<bool> IsUserMemberOfTeamAsync(string userAccessToken, string org, string teamSlug);
    }
    ```

### `IAuthService`

*   **Responsibility**: Manages user authorization after they have authenticated via the GitHub OAuth flow.
*   **Key Tasks**:
    *   Uses the logged-in user's context to determine if they are a member of the required team.
    *   Encapsulates the authorization logic, keeping it out of the UI layer.
*   **Proposed Interface**:
    ```csharp
    public interface IAuthService
    {
        /// <summary>
        /// Checks if the currently authenticated user is a member of the authorized team specified in the application config.
        /// </summary>
        /// <param name="userPrincipal">The ClaimsPrincipal for the current user.</param>
        /// <returns>True if the user is authorized, otherwise false.</returns>
        Task<bool> IsCurrentUserAuthorizedAsync(System.Security.Claims.ClaimsPrincipal userPrincipal);
    }
    ```

---

## 2. Scanning & Policy Enforcement

### `IScanningService`

*   **Responsibility**: Orchestrates the end-to-end repository scanning process.
*   **Key Tasks**:
    *   Provides a trigger for starting a new scan.
    *   Enqueues the main scan execution as a background job.
    *   Orchestrates the workflow: get repos -> evaluate policies -> process actions.
*   **Proposed Interface**:
    ```csharp
    public interface IScanningService
    {
        /// <summary>
        /// Initiates a new scan by creating a Scan record and enqueuing a background job.
        /// </summary>
        /// <returns>The ID of the newly created scan.</returns>
        Task<int> StartScanAsync();

        /// <summary>
        /// [Background Job] Executes the full scan logic for a given scan ID.
        /// This method should only be invoked by a background job processor like Hangfire.
        /// </summary>
        /// <param name="scanId">The ID of the scan to execute.</param>
        Task ExecuteScanAsync(int scanId);
    }
    ```

### `IPolicyEvaluationService`

*   **Responsibility**: Evaluates a single repository against all configured policies using the Strategy pattern.
*   **Key Tasks**:
    *   Dynamically selects the correct `IPolicyEvaluator` for each policy.
    *   Aggregates all detected violations for a single repository.
*   **Proposed Interface**:
    ```csharp
    public interface IPolicyEvaluationService
    {
        /// <summary>
        /// Evaluates a single repository against all active policies.
        /// </summary>
        /// <param name="repository">The repository to evaluate.</param>
        /// <param name="policies">The list of policies to check against.</param>
        /// <returns>A list of policy violations found for the repository.</returns>
        Task<IEnumerable<PolicyViolation>> EvaluateRepositoryAsync(Octokit.Repository repository, IEnumerable<PolicyConfig> policies);
    }
    ```

### `IPolicyEvaluator` (Strategy Interface)

*   **Responsibility**: Represents a single, concrete policy check.
*   **Key Tasks**:
    *   Contains the specific logic for one type of policy check (e.g., file presence).
*   **Proposed Interface**:
    ```csharp
    public interface IPolicyEvaluator
    {
        /// <summary>
        /// A key that matches the policy 'type' in config.yaml (e.g., "has_agents_md").
        /// </summary>
        string PolicyType { get; }

        /// <summary>
        /// Evaluates the repository against this specific policy.
        /// </summary>
        /// <param name="repository">The repository to check.</param>
        /// <returns>A PolicyViolation object if the policy is violated, otherwise null.</returns>
        Task<PolicyViolation?> EvaluateAsync(Octokit.Repository repository);
    }
    ```

### `IActionService`

*   **Responsibility**: Executes the automated actions based on violations found during a scan.
*   **Key Tasks**:
    *   Processes all violations from a completed scan.
    *   Checks for pre-existing issues to prevent duplicates.
    *   Uses `IGitHubService` to perform the action.
    *   Logs all actions to the `ActionsLog` table for auditing.
*   **Proposed Interface**:
    ```csharp
    public interface IActionService
    {
        /// <summary>
        /// [Background Job] Processes all violations for a completed scan and executes the configured actions.
        /// </summary>
        /// <param name="scanId">The ID of the scan whose violations should be processed.</param>
        Task ProcessActionsForScanAsync(int scanId);
    }
    ```

---

## 3. Frontend & Data Presentation

### `IDashboardService`

*   **Responsibility**: Provides all necessary data for the Blazor frontend dashboard. Acts as a "Backend for Frontend" (BFF).
*   **Key Tasks**:
    *   Queries the database to get non-compliant repositories.
    *   Calculates summary metrics.
    *   Supports filtering.
*   **Proposed Interface**:
    ```csharp
    public interface IDashboardService
    {
        /// <summary>
        /// Gets the view model containing all data required to render the main dashboard.
        /// </summary>
        /// <param name="nameFilter">An optional string to filter repositories by name.</param>
        /// <returns>A view model for the dashboard.</returns>
        Task<DashboardViewModel> GetDashboardViewModelAsync(string? nameFilter = null);
    }
    ```
