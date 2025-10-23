# UI Implementation Plan

This plan details the steps to create the user interface for the 10x GitHub Policy Enforcer application. We will use MudBlazor to build a modern UI and start with mock data to facilitate parallel development of the frontend and backend services.

## 1. Setup and Configuration

First, I will configure the project to use the MudBlazor component library.

- **Update `Pages/_Host.cshtml`**: I will add the necessary CSS and JavaScript references for MudBlazor to function correctly.
- **Update `_Imports.razor`**: Ensure `@using MudBlazor` is present for component availability across the app.
- **Update `MainLayout.razor`**: The main layout will be refactored to use MudBlazor's layout components (`MudLayout`, `MudAppBar`, `MudMainContent`) to provide a consistent application shell.

## 2. Mock Data and Services

To develop the UI without a dependency on the live backend, I will create mock services and view models.

- **Create `IDashboardService` Interface**: First, I will create the `IDashboardService.cs` interface in `10xGitHubPolicies.App/Services/` as defined in the architecture plan. This ensures the mock and real services adhere to the same contract.
- **Create View Models**: I will define a `DashboardViewModel` and related classes in a new `10xGitHubPolicies.App/ViewModels` folder. These models will represent the data needed by the dashboard, such as the compliance percentage and a list of non-compliant repositories.
- **Implement Mock Service**: A `MockDashboardService` will be created in `10xGitHubPolicies.App/Services/Mock/`. This service will implement the `IDashboardService` interface and return hardcoded, realistic-looking data.
- **Register Mock Service**: I will register the `MockDashboardService` in `Program.cs` to be used for dependency injection during development.
```csharp:10xGitHubPolicies.App/Program.cs
// ... existing code ...
using _10xGitHubPolicies.App.Services;
using _10xGitHubPolicies.App.Services.Mock; // Add this using

// ... existing code ...
builder.Services.AddSingleton<IGitHubService, GitHubService>();
builder.Services.AddScoped<IScanningService, ScanningService>();
builder.Services.AddScoped<IDashboardService, MockDashboardService>(); // Add this line

builder.Services.Configure<GitHubAppOptions>(builder.Configuration.GetSection(GitHubAppOptions.GitHubApp));
// ... existing code ...
```


## 3. Dashboard Implementation

With the setup and mock data in place, I will build the main dashboard page as described in the PRD.

- **Location**: All changes will be applied to `10xGitHubPolicies.App/Pages/Index.razor`.
- **Layout**: I will use `MudGrid`, `MudItem`, and `MudPaper` to structure the dashboard.
- **Metrics**: A summary card will display the overall compliance percentage.
- **Controls**: A `MudTextField` will be added for filtering repositories by name, and a `MudButton` for the "Scan Now" action.
- **Repository List**: A `MudTable` will be used to display the list of non-compliant repositories, showing the repository name and the policies it violates. The table will be bound to the mock data and will update based on the filter input.

This approach will result in a functional and visually complete dashboard that is ready for integration with real data services once they become available.