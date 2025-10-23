# Product Overview
The 10x GitHub Policy Enforcer is a GitHub App with an accompanying web UI designed to automate the enforcement of organizational policies and security best practices across all repositories within a GitHub organization.

---

## Setup commands
To run this project locally:
1. `cd 10xGitHubPolicies`
2. `docker-compose up -d`
3. `cd 10xGitHubPolicies.App`
4. `dotnet restore`
5. `dotnet ef database update`
6. `dotnet run`

Alternatively, you can run from the root directory using `dotnet run --project 10xGitHubPolicies.App/10xGitHubPolicies.App.csproj`.

---

## CODING_PRACTICES

You are a senior Blazor and .NET developer and an expert in `C#`, `ASP.NET Core`, and `Entity Framework Core`.

### Code Style and Structure
- Write idiomatic and efficient `Blazor` and `C#` code.
- Follow .NET and Blazor conventions.
- Use Razor Components appropriately for component-based UI development.
- Prefer inline functions for smaller components but separate complex logic into code-behind or service classes.
- Async/await should be used where applicable to ensure non-blocking UI operations.
- Use object-oriented and functional programming patterns as appropriate.
- Prefer LINQ and lambda expressions for collection operations.
- Use descriptive variable and method names (e.g., `IsUserSignedIn`, `CalculateTotal`).
- Structure files according to `.NET` conventions (Controllers, Models, Services, etc.).
- use `dotnet format` for formatting code.

### Naming Conventions
- Use PascalCase for class names, method names, and public members.
- Use camelCase for local variables and private fields.
- Use UPPERCASE for constants.
- Prefix interface names with "I" (e.g., `IUserService`).

### C# and .NET Usage
- Use `C# 12` and `.NET 8` features where appropriate (e.g., record types, primary constructors, pattern matching).
- Leverage built-in `ASP.NET Core` features and middleware.
- Use `Entity Framework Core` effectively for database operations.
- Use async methods (`SaveChangesAsync`, `ToListAsync`, etc.) for all database I/O.
- Use EF Core migrations to manage database schema changes.
- Configure EF Core logging in development to inspect generated SQL queries.

### Syntax and Formatting
- Follow the C# Coding Conventions (https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use `C#`'s expressive syntax (e.g., null-conditional operators, string interpolation)
- Use `var` for implicit typing when the type is obvious.

### Frontend Development (Blazor & MudBlazor)
- Use `MudBlazor` components to build the UI for a consistent look and feel.
- Adhere to a consistent styling and theming strategy using `MudBlazor`'s theme provider.
- Manage application state carefully. For complex state, consider using a cascading parameter-based state container.

### Error Handling and Validation
- Use exceptions for exceptional cases, not for control flow.
- Implement proper error logging using built-in `.NET` logging or a third-party logger.
- Use Data Annotations or Fluent Validation for model validation.
- Implement global exception handling middleware.
- Return appropriate HTTP status codes and consistent error responses.

### API Design
- Follow RESTful API design principles.
- Use attribute routing in controllers.
- Implement versioning for your API.
- Use action filters for cross-cutting concerns.

### Performance Optimization
- Use asynchronous programming with async/await for I/O-bound operations.
- Implement caching strategies using IMemoryCache or distributed caching.
- Use efficient LINQ queries and avoid N+1 query problems.
- Implement pagination for large data sets.

### Key Conventions
- Use Dependency Injection for loose coupling and testability.
- Choose the correct service lifetime (`Singleton`, `Scoped`, `Transient`) to avoid unintentional data sharing. Avoid using `Singleton` for services that handle user-specific or request-specific data to prevent security risks.
- Avoid handling mutable, request-specific state in `Singleton` services. If state must be shared, ensure it is thread-safe to prevent race conditions.
- Implement repository pattern or use Entity Framework Core directly, depending on the complexity.
- Use AutoMapper for object-to-object mapping if needed.
- Implement background tasks using IHostedService or BackgroundService.

### Testing
- Write unit tests using xUnit, NUnit, or MSTest.
- Use Moq or NSubstitute for mocking dependencies.
- Implement integration tests for API endpoints.
- Write unit tests for Blazor components using `bUnit`.

### Security
- Use Authentication and Authorization middleware.
- Implement JWT authentication for stateless API authentication.
- Use HTTPS and enforce TLS.
- Implement proper CORS policies.

### Security Best Practices
- **Secret Management**: Never hard-code secrets (passwords, API keys, connection strings). Use the .NET Secret Manager for local development and a secure vault (like Azure Key Vault or AWS Secrets Manager) for production.
- **Prevent SQL Injection (CWE-89)**: Use Entity Framework Core for all database access. It automatically parameterizes queries, preventing SQL injection vulnerabilities. Prefer LINQ queries or `FromSqlInterpolated`. Do not use `FromSqlRaw` with user-provided values.
- **Prevent Cross-Site Scripting (XSS, CWE-79)**: Rely on Blazor's automatic output encoding. Avoid manually rendering raw HTML. When you must handle raw HTML or interact with JavaScript, ensure data is properly sanitized and encoded.
- **Prevent Cross-Site Requestgery (CSRF, CWE-352)**: Blazor Server includes anti-forgery protection by default. Ensure this is not disabled. For APIs, use `[ValidateAntiForgeryToken]` on state-changing actions.
- **Enforce Authentication (CWE-306)**: Protect all sensitive endpoints and resources. Apply the `[Authorize]` attribute to controllers, API endpoints, and Blazor pages.
- **Secure Deserialization (CWE-502)**: Avoid using unsafe deserializers like `BinaryFormatter`. Prefer `System.Text.Json` and configure it securely. Do not deserialize data from untrusted sources.
- **Prevent Path Traversal (CWE-22)**: When working with file paths, validate and sanitize all user-provided input. Use `Path.GetFullPath()` to resolve the absolute path and verify that it is within the expected base directory before accessing the file system.
- **Dependency Security**: Be cautious when adding new NuGet packages. Verify the publisher and check for signs of typosquatting or "slopsquatting." Comment on any uncommon or low-reputation packages used.

### GitHub Integration (Octokit.net)
- Use the provided `IGitHubService` for all GitHub API interactions.
- The service provides high-level methods for common operations (repository management, file operations, issue creation, etc.).
- Do not directly access `GitHubClient` - use the specialized methods provided by `IGitHubService`.
- The `GitHubService` handles authentication, token caching, and error handling internally.
- Be mindful of API rate limits. Cache responses where appropriate.
- Use asynchronous methods to avoid blocking threads.
- Refer to `/docs/github-integration.md` for detailed usage examples and best practices.

### Policy Evaluation Engine
- Use `IPolicyEvaluationService` to evaluate repositories against configured policies.
- Individual policy checks are implemented as `IPolicyEvaluator` strategies.
- To add a new policy, create a new `IPolicyEvaluator` implementation and register it in `Program.cs`.
- Refer to `/docs/policy-evaluation.md` for a detailed guide on creating new evaluators.

### Configuration Service
- Use `IConfigurationService` to retrieve the centralized policy configuration from `.github/config.yaml`.
- The service handles YAML parsing, validation, and caching automatically.
- Configuration is cached for 15 minutes with sliding expiration.
- Handle `ConfigurationNotFoundException` and `InvalidConfigurationException` appropriately.
- Use `forceRefresh: true` parameter sparingly to avoid unnecessary GitHub API calls.
- Refer to `/docs/configuration-service.md` for detailed usage examples and configuration format.

### Background Jobs (Hangfire)
- Enqueue background jobs using `IBackgroundJobClient`.
- Keep background jobs small, idempotent, and focused on a single task.
- Use the Hangfire Dashboard (`/hangfire`) for monitoring jobs.
- Configure queues and workers appropriately for production environments.
- Refer to `/docs/hangfire-integration.md` for more details.

Follow the official Microsoft documentation and `ASP.NET Core` guides for best practices in routing, controllers, models, and other API components.

---

## Guidelines for DOCUMENTATION
#### DOC_UPDATES

- Update relevant documentation in /docs when modifying features
- Keep README.md in sync with new capabilities
- Maintain changelog entries in `CHANGELOG.md`
- Keep .cursor/rules and AGENTS.md up to date

#### API Documentation
- Use `Swagger`/`OpenAPI` for API documentation. The `Swashbuckle.AspNetCore` package should be used for this.
- Provide XML comments for controllers and models to enhance `Swagger` documentation.

---

## Guidelines for VERSION_CONTROL
#### CONVENTIONAL_COMMITS

- Follow the format: `type(scope): description` for all commit messages
- Use consistent types (feat, fix, docs, style, refactor, test, chore) across the project
- Include issue references in commit messages to link changes to requirements
- Use breaking change footer (`!` or `BREAKING CHANGE:`) to clearly mark incompatible changes
- Configure commitlint to automatically enforce conventional commit format
