# Policy Evaluation Implementation Plan
  
### Phase 1: Implement Core Scanning and Evaluation Logic

First, I will implement the services responsible for evaluating repository compliance against the configured policies and integrate this logic into the main scanning orchestrator.

1.  **Implement Policy Evaluation Strategy**

    -   **Goal:** Create the policy evaluation engine using the Strategy Pattern.
    -   **File Changes:**
        -   `10xGitHubPolicies.App/Services/IPolicyEvaluator.cs` (New): Define the strategy interface.
        -   `10xGitHubPolicies.App/Services/Implementations/PolicyEvaluators/` (New Files): Create concrete evaluators for the three MVP policies (`HasAgentsMdEvaluator`, `HasCatalogInfoYamlEvaluator`, `CorrectWorkflowPermissionsEvaluator`).
        -   `10xGitHubPolicies.App/Services/IPolicyEvaluationService.cs` (New): Define the main evaluation service interface.
        -   `10xGitHubPolicies.App/Services/Implementations/PolicyEvaluationService.cs` (New): Implement the service to consume the `IPolicyEvaluator` strategies.
        -   `10xGitHubPolicies.App/Program.cs`: Register the new services for dependency injection.

2.  **Integrate Evaluation into `ScanningService`**

    -   **Goal:** Update `ScanningService` to use the new evaluation service and save results to the database.
    -   **File Changes:**
        -   `10xGitHubPolicies.App/Services/ScanningService.cs`: Inject `ApplicationDbContext` and `IPolicyEvaluationService`. The `PerformScanAsync` method will be updated to orchestrate the scan: create a `Scan` record, fetch repositories, evaluate each one, and save `PolicyViolation` records.

3.  **Create Mock Action Service**

    -   **Goal:** Create a service that simulates taking action by logging to the console.
    -   **File Changes:**
        -   `10xGitHubPolicies.App/Services/IActionService.cs` (New): Define the action service interface.
        -   `10xGitHubPolicies.App/Services/Implementations/LoggingActionService.cs` (New): Implement the service to query for violations from a scan and log the intended actions.
        -   `10xGitHubPolicies.App/Program.cs`: Register the service.
        -   `10xGitHubPolicies.App/Services/ScanningService.cs`: Inject and call `IActionService` at the end of a scan, likely via a Hangfire background job.

### Phase 2: Display Data in the UI

Next, I will replace the mock dashboard data with real data from the database and enable the "Scan Now" functionality.

4.  **Implement Real `DashboardService`**

    -   **Goal:** Create a service to provide the frontend with real compliance data from the database.
    -   **File Changes:**
        -   `10xGitHubPolicies.App/Services/Implementations/DashboardService.cs` (New): Implement `IDashboardService` to query the database for the latest scan results and build the `DashboardViewModel`.
        -   `10xGitHubPolicies.App/Program.cs`: Replace the registration of `MockDashboardService` with the new `DashboardService`.

5.  **Enable UI Functionality**

    -   **Goal:** Allow users to trigger scans and see the results on the dashboard.
    -   **File Changes:**
        -   `10xGitHubPolicies.App/Pages/Index.razor`: Inject `IScanningService` and connect the "Scan Now" button to a method that calls it. I'll add UI feedback to indicate a scan is running and ensure the dashboard refreshes upon completion.