# CI/CD Workflows

## Overview

The project uses GitHub Actions for continuous integration and continuous deployment. The main workflow runs on pull requests to ensure code quality, test coverage, and compliance before code is merged.

## Pull Request Workflow

The **Pull Request** workflow (`.github/workflows/pull-request.yml`) is triggered automatically on pull requests to `main` or `develop` branches. It performs checks including code linting, multi-level testing, code coverage reporting, and automatic PR status comments.

### Workflow Structure

The workflow is organized into several interdependent jobs that run in parallel where possible to minimize execution time:

```
lint
 ├── unit-tests ──┐
 ├── component-tests ──┐
 │                    ├── integration-tests ──┐
 │                    ├── contract-tests ──┐  │
 │                    │                    │  ├── publish-coverage ──┐
 │                    │                    │  │                       │
 │                    │                    │  └── status-comment ─────┘
```

### Jobs

#### 1. Lint Job
**Purpose**: Enforces code formatting standards  
**Runs on**: Every PR  

Performs:
- Restores .NET dependencies
- Runs `dotnet format --verify-no-changes` to ensure all code adheres to project formatting rules
- Fails if any formatting changes are detected (prevents merge of unformatted code)

#### 2. Unit Tests Job
**Purpose**: Fast feedback on business logic correctness  
**Depends on**: `lint`  
**Runs on**: After linting passes  

Performs:
- Runs all tests tagged with `Category=Unit`
- Collects code coverage using Coverlet
- Generates TRX (Test Results XML) file for test reporting
- Uploads coverage and test result artifacts

**Coverage**: Business logic, services, helpers, utility functions

#### 3. Component Tests Job
**Purpose**: Validates Blazor component rendering and interactivity  
**Depends on**: `lint` (runs in parallel with unit-tests)  
**Runs on**: After linting passes  

Performs:
- Runs all tests tagged with `Category=Component`
- Tests Blazor components using bUnit
- Collects code coverage
- Generates TRX file for test reporting
- Uploads coverage and test result artifacts

**Coverage**: Blazor components, UI logic, authentication flows

#### 4. Integration Tests Job
**Purpose**: Validates service interactions with external dependencies  
**Depends on**: `unit-tests`, `component-tests`  
**Runs on**: After unit and component tests pass  

Performs:
- Runs all tests tagged with `Category=Integration`
- Uses WireMock.Net to mock GitHub API responses
- Uses Testcontainers for ephemeral database instances
- Collects code coverage
- Generates TRX file for test reporting
- Uploads coverage and test result artifacts

**Coverage**: GitHubService methods, database operations, service integrations

#### 5. Contract Tests Job
**Purpose**: Validates GitHub API contract compliance  
**Depends on**: `unit-tests`, `component-tests` (runs in parallel with integration-tests)  
**Runs on**: After unit and component tests pass  

Performs:
- Runs all tests tagged with `Category=Contract`
- Uses NJsonSchema for JSON schema validation
- Uses Verify.NET for snapshot testing
- Validates GitHub API response contracts
- Collects code coverage
- Generates TRX file for test reporting
- Uploads coverage and test result artifacts

**Coverage**: GitHub API response contracts, schema validation

#### 6. Publish Coverage Job
**Purpose**: Generates and publishes comprehensive code coverage reports  
**Depends on**: All test jobs  
**Runs on**: After all tests complete (regardless of pass/fail with `if: always()`)  

Performs:
- Downloads all test coverage artifacts from previous jobs
- Installs ReportGenerator .NET tool
- Aggregates coverage data from all test levels
- Generates HTML, JSON, Badges, and Cobertura format reports
- Filters out test projects from coverage calculations
- Uploads comprehensive coverage report artifact
- Extracts coverage percentage for PR comments
- Publishes coverage to Codecov with multiple flags (unit, component, integration, contract)

**Output Artifacts**:
- `coverage-report/index.html` - Interactive HTML coverage report
- `coverage-report/Summary.json` - Coverage summary in JSON format
- `coverage-report/Cobertura.xml` - Cobertura format for external tools
- `coverage-report/badges/` - Coverage badge images

#### 7. Status Comment Job
**Purpose**: Provides PR authors with clear feedback on CI status  
**Depends on**: All jobs (lint, tests, coverage)  
**Runs on**: After all jobs complete (regardless of pass/fail with `if: always()`)  
**Only on**: Pull requests (not on direct pushes)  

Performs:
- Downloads all test result artifacts
- Parses TRX files to extract test statistics (total, passed, failed)
- Reads coverage percentage from coverage report
- Determines overall status based on job results
- Creates or updates PR comment with:
  - Overall status (✅ All checks passed / ❌ Tests failed / ❌ Linting failed)
  - Test results summary (total, passed, failed)
  - Job status table with emoji indicators
  - Code coverage percentage
- Updates existing bot comment if one exists (prevents comment spam)

**Comment Format**:
```markdown
## 🔍 Pull Request CI Status

✅ All checks passed

### Test Results Summary
- **Total Tests:** 120
- **Passed:** 118
- **Failed:** 2

### Job Status
| Job | Status |
|-----|--------|
| Linting | ✅ |
| Unit Tests | ✅ |
| Component Tests | ✅ |
| Integration Tests | ✅ |
| Contract Tests | ✅ |

### Code Coverage
**Line Coverage:** 87.45%

---
*This comment is automatically updated on each workflow run.*
```

## Security Considerations

### Pinned Action Versions

All GitHub Actions used in workflows are **pinned to full-length commit SHAs** rather than version tags for security compliance. This prevents supply chain attacks from malicious updates to action versions.

**Pinned Actions**:
- `actions/checkout@b4ffde65f46336ab88eb53be808477a3936bae11` (v4)
- `actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9` (v4)
- `actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02` (v4)
- `actions/download-artifact@d3f86a106a0bac45b974a628896c90dbdf5c8093` (v4)
- `codecov/codecov-action@b9fd7d16f6d7d1b5d2bec1a2887e65ceed900238` (v4)
- `actions/github-script@f28e40c7f34bde8b3046d885e986cb6290c5673b` (v7)

**Updating Action SHAs**:
1. Visit the action's GitHub repository Releases page (e.g., https://github.com/actions/checkout/releases/tag/v4)
2. Find the commit SHA for the desired release
3. Update the SHA in the workflow file
4. Test the workflow to ensure compatibility

### Workflow Permissions

The workflow uses minimal required permissions:
- `contents: read` - Read repository contents
- `packages: read` - Read NuGet packages
- `pull-requests: write` - Create/update PR comments

Following the principle of least privilege reduces the attack surface if a workflow is compromised.

## Code Coverage

### Coverage Collection

Coverage is collected at multiple levels using Coverlet:
- **Unit Tests**: Coverage of business logic and services
- **Component Tests**: Coverage of Blazor components
- **Integration Tests**: Coverage of integration points
- **Contract Tests**: Coverage of contract validation logic

### Coverage Reporting

The aggregated coverage report:
- **Excludes**: Test projects (`*Tests*`, `*TestHelpers*`)
- **Formats**: HTML, JSON Summary, Cobertura XML, Badges
- **Publishes**: To Codecov with separate flags for each test level
- **Filters**: Test helpers and test infrastructure from coverage calculations

### Coverage Targets

While not enforced by CI, the project aims for:
- **Overall Coverage**: 80%+ line coverage
- **Business Logic**: 90%+ line coverage
- **Services**: 85%+ line coverage

## Troubleshooting

### Workflow Failures

**Lint Failure**:
```bash
# Fix formatting issues locally
dotnet format

# Verify no changes
dotnet format --verify-no-changes
```

**Test Failures**:
- Check test output in GitHub Actions logs
- Run failing tests locally: `dotnet test --filter "Category=Unit"`
- Review test artifacts uploaded to workflow artifacts

**Coverage Report Missing**:
- Verify coverage files are generated in test jobs
- Check artifact upload/download steps succeeded
- Review ReportGenerator installation step

**PR Comment Not Appearing**:
- Ensure workflow has `pull-requests: write` permission
- Check that `github.event_name == 'pull_request'` condition is met
- Review GitHub Script action logs for API errors
