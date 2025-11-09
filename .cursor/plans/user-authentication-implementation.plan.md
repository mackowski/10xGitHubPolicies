<!-- 71b416db-4a21-4a16-88c9-4334eb7f72c1 e85163ce-c2f0-49b5-b924-bc011a8e060a -->
# User Authentication Implementation Plan

## Overview

Implement GitHub OAuth App authentication flow for the Blazor web dashboard, including team-based authorization, secure session management, logout functionality, and first-time user onboarding wizard.

## Phase 1: NuGet Packages & Configuration

### 1.1 Add Required NuGet Packages

Add to `10xGitHubPolicies.App.csproj`:

- `AspNet.Security.OAuth.GitHub` (latest stable version)
- `Microsoft.AspNetCore.Authentication.Cookies` (included in .NET 8, verify)

### 1.2 Configure User Secrets

Set up OAuth App credentials using .NET Secret Manager:

```bash
cd 10xGitHubPolicies.App
dotnet user-secrets set "GitHub:ClientId" "YOUR_OAUTH_APP_CLIENT_ID"
dotnet user-secrets set "GitHub:ClientSecret" "YOUR_OAUTH_APP_CLIENT_SECRET"
```

### 1.3 Update appsettings.json

Add placeholder for OAuth configuration (actual values in secrets):

```json
"GitHub": {
  "ClientId": "",
  "ClientSecret": ""
}
```

## Phase 2: Authentication Infrastructure

### 2.1 Configure Authentication in Program.cs

Add authentication middleware before `var app = builder.Build();`:

```csharp
// Add authentication services
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = "GitHub";
})
.AddCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
    options.SlidingExpiration = false;
})
.AddGitHub(options =>
{
    options.ClientId = builder.Configuration["GitHub:ClientId"];
    options.ClientSecret = builder.Configuration["GitHub:ClientSecret"];
    options.CallbackPath = "/signin-github";
    options.Scope.Add("read:org");
    options.SaveTokens = true; // Save access token for team verification
});

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
```

### 2.2 Add Authentication Middleware

Add after `app.UseRouting();` in Program.cs:

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

## Phase 3: Create Authentication Pages

### 3.1 Create Login Page (`Pages/Login.razor`)

- Display "Login with GitHub" button
- Trigger OAuth challenge on click
- Show application branding/description

### 3.2 Create Access Denied Page (`Pages/AccessDenied.razor`)

- Display clear message about insufficient permissions
- Show which team is required (from config)
- Provide instructions on requesting access
- Include logout button

### 3.3 Create Logout Page (`Pages/Logout.razor`)

- Handle sign-out logic
- Clear authentication cookies
- Redirect to login page

### 3.4 Create Onboarding Page (`Pages/Onboarding.razor`)

- Step-by-step wizard for first-time setup
- Display config.yaml template with copy button
- Instructions for creating `.github` repository
- Instructions for placing config.yaml file
- "Check Configuration" button to verify setup

## Phase 4: Authorization Service

### 4.1 Create Authorization Service Interface

Create `Services/Authorization/IAuthorizationService.cs`:

```csharp
public interface IAuthorizationService
{
    Task<bool> IsUserAuthorizedAsync(ClaimsPrincipal user);
    Task<string?> GetAuthorizedTeamAsync();
}
```

### 4.2 Implement Authorization Service

Create `Services/Authorization/AuthorizationService.cs`:

- Inject `IGitHubService` and `IConfigurationService`
- Extract user's access token from claims
- Retrieve authorized team from config.yaml
- Call `IGitHubService.IsUserMemberOfTeamAsync()` to verify membership
- Handle configuration errors gracefully

### 4.3 Register Service in Program.cs

```csharp
builder.Services.AddScoped<IAuthorizationService, AuthorizationService>();
```

## Phase 5: Protect Dashboard & Add Authorization Logic

### 5.1 Update Index.razor

- Add `@attribute [Authorize]` at the top
- Inject `IAuthorizationService` and `IConfigurationService`
- In `OnInitializedAsync()`:
  - Check if config.yaml exists (catch `ConfigurationNotFoundException`)
  - If missing, redirect to `/onboarding`
  - If present, verify team membership via `IAuthorizationService`
  - If not authorized, redirect to `/access-denied`
  - If authorized, load dashboard data as normal

### 5.2 Update App.razor

Replace with `CascadingAuthenticationState` wrapper:

```razor
<CascadingAuthenticationState>
    <Router AppAssembly="@typeof(App).Assembly">
        <Found Context="routeData">
            <AuthorizeRouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)">
                <NotAuthorized>
                    <RedirectToLogin />
                </NotAuthorized>
            </AuthorizeRouteView>
            <FocusOnNavigate RouteData="@routeData" Selector="h1" />
        </Found>
        <NotFound>
            <PageTitle>Not found</PageTitle>
            <LayoutView Layout="@typeof(MainLayout)">
                <p role="alert">Sorry, there's nothing at this address.</p>
            </LayoutView>
        </NotFound>
    </Router>
</CascadingAuthenticationState>
```

### 5.3 Create RedirectToLogin Component

Create `Shared/RedirectToLogin.razor`:

- Use `NavigationManager` to redirect to `/login`

## Phase 6: Update UI Components

### 6.1 Update MainLayout.razor

- Add `<AuthorizeView>` to show user info when authenticated
- Display username from claims
- Add logout button in header/navigation
- Show login prompt for unauthenticated users

### 6.2 Update _Imports.razor

Add required namespaces:

```razor
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.AspNetCore.Authorization
@using System.Security.Claims
```

## Phase 7: Testing & Validation

### 7.1 Manual Testing Checklist

- Unauthenticated user redirected to login
- OAuth flow completes successfully
- Authorized team member can access dashboard
- Non-team member sees access denied page
- Missing config.yaml shows onboarding wizard
- Logout clears session and redirects to login
- Session expires after 24 hours
- Callback URL `/signin-github` works correctly

### 7.2 Error Scenarios

- Handle GitHub OAuth errors gracefully
- Handle network failures during team verification
- Handle invalid/expired access tokens
- Handle malformed config.yaml

## Phase 8: Documentation Updates

### 8.1 Update README.md

- Add OAuth App setup instructions
- Document required GitHub OAuth App configuration
- Add callback URL setup instructions
- Update local development setup steps

### 8.2 Create Authentication Documentation

Create `docs/authentication.md`:

- Detailed OAuth flow explanation
- Team-based authorization process
- Session management details
- Troubleshooting guide

### 8.3 Update CHANGELOG.md

Add entry for authentication feature implementation

## Key Files to Create/Modify

**New Files:**

- `Pages/Login.razor`
- `Pages/Logout.razor`
- `Pages/AccessDenied.razor`
- `Pages/Onboarding.razor`
- `Services/Authorization/IAuthorizationService.cs`
- `Services/Authorization/AuthorizationService.cs`
- `Shared/RedirectToLogin.razor`
- `docs/authentication.md`

**Modified Files:**

- `Program.cs` (authentication configuration)
- `10xGitHubPolicies.App.csproj` (NuGet packages)
- `Pages/Index.razor` (authorization checks)
- `App.razor` (authentication state)
- `Shared/MainLayout.razor` (user info, logout)
- `_Imports.razor` (namespaces)
- `appsettings.json` (OAuth config placeholders)
- `README.md` (setup instructions)
- `CHANGELOG.md` (feature entry)

## Security Considerations

1. **Token Storage**: OAuth access tokens stored securely in authentication cookie (encrypted by ASP.NET Core)
2. **HTTPS Only**: Ensure cookies are marked as secure in production
3. **CSRF Protection**: Blazor Server provides built-in anti-forgery protection
4. **Session Timeout**: 24-hour fixed expiration prevents indefinite sessions
5. **Minimal Scopes**: Only request `read:org` scope (least privilege)
6. **Secret Management**: Use .NET Secret Manager for local dev, Azure Key Vault for production

## Dependencies

- Existing `IGitHubService.IsUserMemberOfTeamAsync()` method
- Existing `IConfigurationService.GetConfigAsync()` method
- GitHub OAuth App must be created and configured with correct callback URL

### To-dos

- [ ] Add authentication NuGet packages and configure user secrets
- [ ] Configure authentication middleware in Program.cs with Cookie and GitHub OAuth
- [ ] Create IAuthorizationService and implementation for team-based authorization
- [ ] Create Login.razor page with GitHub OAuth button
- [ ] Create Logout.razor page to handle sign-out
- [ ] Create AccessDenied.razor page with clear messaging and instructions
- [ ] Create Onboarding.razor wizard for first-time setup with config template
- [ ] Update App.razor with CascadingAuthenticationState and AuthorizeRouteView
- [ ] Add authorization logic to Index.razor with config check and team verification
- [ ] Update MainLayout.razor with user info display and logout button
- [ ] Add authentication namespaces to _Imports.razor
- [ ] Test complete authentication flow including OAuth, authorization, and error scenarios
- [ ] Update README.md, create docs/authentication.md, and update CHANGELOG.md