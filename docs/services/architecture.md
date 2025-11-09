# Services Architecture Overview

This document provides a high-level overview of the service-oriented architecture for the 10x GitHub Policy Enforcer application. Each service has a distinct responsibility, communicates through interfaces, and is designed for testability and scalability.

## Service Categories

### Core Services

These services handle fundamental application functionality:

- **[Configuration Service](./configuration-service.md)** (`IConfigurationService`)
  - Manages application configuration from `.github/config.yaml`
  - Handles YAML parsing, validation, and caching

- **[GitHub Integration](./github-integration.md)** (`IGitHubService`)
  - Centralized client for all GitHub API interactions
  - Manages GitHub App authentication (JWT and installation tokens)
  - Provides methods for repository, file, issue, and workflow operations

- **[Authentication](../authentication.md)** (`IAuthorizationService`)
  - Manages user authorization after GitHub OAuth authentication
  - Verifies team membership for dashboard access

### Scanning & Policy Enforcement

These services orchestrate the repository scanning and policy evaluation process:

- **[Scanning Service](./scanning-service.md)** (`IScanningService`)
  - Orchestrates the end-to-end repository scanning process
  - Synchronizes repositories and policies with the database
  - Coordinates policy evaluation and violation tracking

- **[Policy Evaluation Service](./policy-evaluation.md)** (`IPolicyEvaluationService`)
  - Evaluates repositories against configured policies using the Strategy pattern
  - Dynamically selects appropriate policy evaluators

- **[Policy Evaluators](./policy-evaluation.md#creating-a-new-policy-evaluator)** (`IPolicyEvaluator`)
  - Individual policy check implementations
  - Each evaluator handles one specific policy type

- **[Action Service](./action-service.md)** (`IActionService`)
  - Executes automated actions for policy violations
  - Creates issues, archives repositories, comments on PRs, blocks PRs, or logs violations
  - Prevents duplicate actions
  - Supports both webhook-based (real-time) and scan-based (periodic) processing

### Webhook Processing

These services handle real-time GitHub webhook events:

- **Webhook Service** (`IWebhookService`)
  - Routes incoming webhook events to appropriate handlers
  - Enqueues background jobs for async processing

- **Pull Request Webhook Handler** (`IPullRequestWebhookHandler`)
  - Processes all pull request webhook events (opened, synchronize, reopened, edited, ready_for_review, etc.)
  - Evaluates policies and executes PR actions (comments, status checks) in real-time

### Frontend & Data Presentation

- **[Dashboard Service](./dashboard-service.md)** (`IDashboardService`)
  - Provides data for the Blazor frontend dashboard
  - Calculates compliance metrics and filters repositories
  - Acts as a "Backend for Frontend" (BFF) pattern

## Service Relationships

```
┌─────────────────┐
│  Dashboard UI   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ DashboardService│
└────────┬────────┘
         │
         ▼
┌─────────────────┐     ┌──────────────────┐
│  Database       │◄────│ ScanningService  │
└─────────────────┘     └────────┬─────────┘
                                 │
                    ┌────────────┼────────────┐
                    ▼            ▼            ▼
         ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
         │Configuration │ │   GitHub     │ │   Policy     │
         │   Service    │ │   Service    │ │  Evaluation  │
         └──────────────┘ └──────────────┘ └──────┬───────┘
                                                   │
                                                   ▼
                                         ┌──────────────────┐
                                         │ Policy Evaluators│
                                         │  (Strategy)      │
                                         └──────────────────┘
                    ┌────────────────────────────┘
                    ▼
         ┌──────────────────┐
         │  Action Service  │
         └────────┬─────────┘
                  │
                  ▼
         ┌──────────────────┐
         │  GitHub Service  │
         └──────────────────┘
```

## Service Lifetime

- **Singleton**: `IConfigurationService`, `IGitHubService` (manage shared state/caching)
- **Scoped**: All other services (request/database-scoped operations)

## Testing Strategy

All services are designed for testability:
- **Unit Tests**: Mock dependencies using NSubstitute
- **Integration Tests**: Use WireMock.Net for GitHub API mocking and Testcontainers for database
- **Component Tests**: Use bUnit for Blazor component testing

See [Testing Strategy](../testing/testing-strategy.md) for more details.

## Related Documentation

- [Database Schema](../database.md) - Database structure and relationships
- [Hangfire Integration](../hangfire-integration.md) - Background job processing
- [Authentication](../authentication.md) - User authentication and authorization

