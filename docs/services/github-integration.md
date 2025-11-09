# GitHub Integration Architecture

> **Note**: This document is part of the [Services Architecture](./architecture.md) documentation.

## Overview

The GitHub integration provides a testable, maintainable abstraction over the GitHub API using Octokit.net. The architecture follows a factory pattern to enable HTTP-level mocking for testing while maintaining clean production code.

## Architecture Components

### Core Services

**`IGitHubService`** - High-level service interface defining all GitHub API operations:
- Repository management (list, get settings, archive, create, delete)
- File operations (check existence, read content, create, update, delete)
- Issue management (create, list, close)
- Pull request operations (list open PRs, create comments, get comments)
- Status checks (create, update, get check runs for ref)
- Workflow permissions (get, update)
- User and team operations (organizations, team membership)
- E2E testing utilities (repository lifecycle management)

**`GitHubService`** - Production implementation:
- Handles GitHub App authentication via JWT tokens
- Caches installation tokens (55-minute TTL, 5 minutes before expiration)
- Uses `IGitHubClientFactory` for client creation
- Gracefully handles `NotFoundException` and other API errors
- Supports both installation token and user token authentication

**`IGitHubClientFactory`** - Factory interface for creating `GitHubClient` instances:
- `CreateClient(string token)` - Creates client with user/installation access token
- `CreateAppClient(string jwt)` - Creates client with GitHub App JWT token

**`GitHubClientFactory`** - Default factory implementation:
- Supports custom base URLs for testing (redirects to WireMock.Net)
- Supports custom `HttpClientHandler` for advanced HTTP configuration (e.g., SSL certificate handling)
- Automatically detects Enterprise mode when using custom base URLs

### Authentication Flow

1. **JWT Generation**: `GitHubService` generates a JWT using the GitHub App's private key
2. **Installation Token**: JWT is used to obtain an installation access token via `CreateAppClient()`
3. **Token Caching**: Installation token is cached in memory for 55 minutes
4. **Client Creation**: Authenticated `GitHubClient` instances are created via `CreateClient()` using cached token
5. **User Token Support**: For user-specific operations (team membership, organizations), user access tokens are passed directly to `CreateClient()` via the factory, bypassing the JWT/installation token flow

### Configuration

**`GitHubAppOptions`** - Configuration model containing App ID, Private Key, Installation ID, Organization Name, and optional BaseUrl (for testing).

**Dependency Injection**: Both `IGitHubClientFactory` and `IGitHubService` are registered as singletons. Configuration is loaded from Secret Manager (local) or environment variables (production).

## Testing Strategy

The factory pattern enables comprehensive testing at multiple levels:

### Integration Testing
- Uses WireMock.Net to mock GitHub API at HTTP level
- Factory configured with WireMock server URL via `BaseUrl`
- Tests actual HTTP interactions without real API calls
- 33 tests covering all `GitHubService` methods

### Contract Testing
- Validates GitHub API response structures using NJsonSchema
- Snapshot testing with Verify.NET to detect breaking changes
- 11 tests validating critical API contracts

### Important: Octokit Path Prefix

When using a custom `BaseUrl` (e.g., for testing with WireMock), Octokit automatically prepends `/api/v3/` to all API paths (GitHub Enterprise mode behavior). All mock stubs must include this prefix.

## Design Principles

### Testability
- Factory pattern enables HTTP-level mocking without modifying production code
- Optional dependency injection maintains backward compatibility
- Custom base URLs support redirecting to mock servers

### Maintainability
- Centralized client creation logic
- Consistent authentication handling
- Clear separation of concerns (service vs. factory)

### Flexibility
- Supports both production and test scenarios
- Configurable base URL for different environments
- Custom HTTP handlers for advanced scenarios (SSL, proxies)

## Webhook Infrastructure

The application includes webhook support for real-time processing of GitHub events:

### Webhook Controller

**`WebhookController`** - Handles incoming GitHub webhook events:
- **Endpoint**: `POST /api/webhooks/github`
- **Signature Verification**: Validates webhook payloads using HMAC-SHA256
- **Event Processing**: Routes events to appropriate handlers
- **Supported Events**: `pull_request` (opened, synchronize, reopened), `ping`

### Webhook Services

**`IWebhookService`** - Service for processing webhook events:
- Routes webhook events to appropriate handlers
- Uses Hangfire to enqueue background jobs for async processing

**`IPullRequestWebhookHandler`** - Handler for pull request webhook events:
- Evaluates repository policies for PR repositories
- Executes PR actions (comments, status checks) based on violations
- Processes `opened`, `synchronize`, and `reopened` PR actions

### Webhook Configuration

- **Webhook Secret**: Configured via `GitHubApp:WebhookSecret` in application configuration
- **GitHub App Settings**: Enable webhook delivery and subscribe to `pull_request` events
- **Webhook URL**: `https://your-domain.com/api/webhooks/github`

## Pull Request Operations

The service provides methods for interacting with pull requests:

### List Open Pull Requests

```csharp
Task<IReadOnlyList<PullRequest>> GetOpenPullRequestsAsync(long repositoryId)
```

Retrieves all open pull requests for a repository.

### PR Comments

```csharp
Task<IssueComment> CreatePullRequestCommentAsync(long repositoryId, int pullRequestNumber, string comment)
Task<IReadOnlyList<IssueComment>> GetPullRequestCommentsAsync(long repositoryId, int pullRequestNumber)
```

Creates and retrieves comments on pull requests. PRs are treated as issues in the GitHub API.

### Status Checks

```csharp
Task<CheckRun> CreateStatusCheckAsync(long repositoryId, string headSha, string name, string status, string conclusion, string? detailsUrl = null)
Task<CheckRun> UpdateStatusCheckAsync(long repositoryId, long checkRunId, string status, string conclusion, string? detailsUrl = null)
Task<IReadOnlyList<CheckRun>> GetCheckRunsForRefAsync(long repositoryId, string @ref)
```

Creates, updates, and retrieves status checks (check runs) for commits. Used to block PR merges when policy violations are detected.

## Related Documentation

- **[Authentication](../authentication.md)**: User OAuth authentication - user access tokens obtained during OAuth flow are used by `AuthorizationService` which calls `GitHubService.IsUserMemberOfTeamAsync()` for authorization checks
- **[Testing Strategy](../testing/testing-strategy.md)**: Multi-level testing approach
- **[Configuration Service](./configuration-service.md)**: Policy configuration management
- **[Action Service](./action-service.md)**: PR actions (comment-on-prs, block-prs)
- **Integration Tests**: `10xGitHubPolicies.Tests.Integration` project
- **Contract Tests**: `10xGitHubPolicies.Tests.Contracts` project

