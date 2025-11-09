# Testing Guide

Welcome to the testing documentation for the 10x GitHub Policy Enforcer. This guide will help you understand our testing strategy and how to write effective tests.

## Quick Start

- **[Quick Reference](./quick-reference.md)** - Common commands and patterns
- **[Testing Strategy](./testing-strategy.md)** - Detailed testing approach and philosophy
- **[Test Decision Tree](#test-decision-tree)** - Which test type should I write?

## Test Decision Tree

```
Need to test something?
│
├─ Is it business logic?
│  └─ → Unit Test
│
├─ Does it interact with database/API?
│  └─ → Integration Test
│
├─ Is it an external API response?
│  └─ → Contract Test
│
├─ Is it a Blazor UI component?
│  └─ → Component Test
│
└─ Is it a critical user workflow?
   └─ → E2E Test
```

## Testing Levels Overview

We follow a **multi-level testing strategy** based on the testing pyramid:

- **Level 1: Unit Tests** - Business logic in isolation (200+ tests)
- **Level 2: Integration Tests** - Database and API interactions (33 tests)
- **Level 3: Contract Tests** - GitHub API contract validation (11 tests)
- **Level 4: Component Tests** - Blazor UI components (22 tests)
- **Level 5: E2E Tests** - Critical user workflows (5 tests)

For detailed information about each level, see the [Testing Strategy](./testing-strategy.md) guide.

## Project Structure

```
10xGitHubPolicies/
├── 10xGitHubPolicies.App/              # Application code
├── 10xGitHubPolicies.Tests/            # Unit + Component tests
│   ├── Services/                       # Unit tests for services
│   └── Components/                     # Component tests
├── 10xGitHubPolicies.Tests.Integration/ # Integration tests
│   ├── Fixtures/                       # Test fixtures (Database, GitHub API)
│   ├── Builders/                       # Test data builders
│   ├── GitHub/                         # GitHubService integration tests
│   └── Action/                         # ActionService integration tests
├── 10xGitHubPolicies.Tests.Contracts/   # Contract tests
│   ├── GitHub/                         # GitHub API contract tests
│   ├── Schemas/                        # JSON Schema definitions
│   └── Snapshots/                      # Verify.NET snapshots
└── 10xGitHubPolicies.Tests.E2E/        # E2E tests (Playwright)
    ├── Tests/                          # Test classes
    ├── Pages/                          # Page Object Model
    ├── Helpers/                        # Test helpers
    └── Infrastructure/                 # Test infrastructure
```

## Documentation

### Guides
- **[Testing Strategy](./testing-strategy.md)** - Philosophy, testing levels, best practices, tools
- **[Integration Tests](./integration-tests.md)** - HTTP-level testing with WireMock
- **[Contract Tests](./contract-tests.md)** - API contract validation
- **[E2E Tests](./e2e-tests.md)** - End-to-end testing with Playwright

### Reference
- **[Quick Reference](./quick-reference.md)** - Commands, patterns, troubleshooting

## Getting Help

If you're unsure which test type to use, refer to the [Test Decision Tree](#test-decision-tree) above. For specific implementation details, check the example test files in each test project or refer to the detailed guides linked above.
