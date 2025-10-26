# Test Plan - 10x GitHub Policy Enforcer

## 1. Test Objectives and Scope

### 1.1 Objectives
- Verify that all functional requirements from the PRD are correctly implemented
- Ensure the application meets security, performance, and reliability standards
- Validate integration with GitHub API and external services
- Confirm user experience meets acceptance criteria for all user stories
- Verify automated policy enforcement and action execution reliability

### 1.2 Scope
**In Scope:**
- All 11 user stories (US-001 through US-011)
- Authentication and authorization flows
- Policy evaluation engine (3 core policies)
- Automated actions (issue creation, repository archiving)
- Dashboard functionality and real-time updates
- Configuration management and validation
- Background job processing (Hangfire)
- GitHub API integration
- Database operations and data integrity

**Out of Scope:**
- Multi-organization support (single org per installation)
- Advanced policy types beyond MVP
- User-level permissions within the application
- Repository-level exceptions or overrides

## 2. Testing Types and Approach

### 2.1 Functional Testing
- **Unit Testing**: Individual components and services
- **Integration Testing**: Component interactions and API integrations
- **System Testing**: End-to-end user workflows
- **User Acceptance Testing**: Validation against user story acceptance criteria

### 2.2 Non-Functional Testing
- **Performance Testing**: Load testing for concurrent users and repository scanning
- **Security Testing**: Authentication, authorization, and data protection
- **Reliability Testing**: Background job processing and error handling
- **Usability Testing**: User interface and experience validation

### 2.3 Technology-Specific Testing
- **Blazor Server Testing**: Component rendering and circuit resilience (bUnit)
- **ASP.NET Core Testing**: Controller actions and middleware
- **Entity Framework Testing**: Database operations and migrations (Testcontainers)
- **Hangfire Testing**: Background job scheduling, execution, and retry logic
- **GitHub API Testing**: Token caching, rate limit handling, and HTTP mocking (WireMock.Net)
- **Azure Integration Testing**: App Service deployment and SQL Database connectivity

## 3. Test Environment Requirements

### 3.1 Development Environment
- Local development setup with Docker Compose
- Test GitHub organization with sample repositories
- Azure SQL Database (Development tier)
- GitHub OAuth App for testing

### 3.2 Staging Environment
- Azure App Service (Staging slot)
- Azure SQL Database (Standard tier)
- Production-like GitHub organization
- Full GitHub App installation

### 3.3 Test Data Requirements
- Sample repositories with various compliance states
- Test GitHub users with different team memberships
- Valid and invalid configuration files
- Mock GitHub API responses for edge cases

## 4. Detailed Test Scenarios

### 4.1 Authentication and Authorization (US-001, US-002, US-005)

#### Test Case TC-AUTH-001: Successful GitHub Login
**Objective**: Verify successful authentication for authorized team members
**Steps**:
1. Navigate to application login page
2. Click "Login with GitHub" button
3. Complete GitHub OAuth flow
4. Verify redirect to dashboard
5. Confirm user session is established

**Expected Result**: Authorized user successfully logs in and accesses dashboard

#### Test Case TC-AUTH-002: Access Denied for Unauthorized Users
**Objective**: Verify access restriction for non-team members
**Steps**:
1. Login with GitHub account not in authorized team
2. Verify redirect to "Access Denied" page
3. Confirm clear error message is displayed
4. Verify dashboard is not accessible

**Expected Result**: Unauthorized user is denied access with appropriate messaging

#### Test Case TC-AUTH-003: Team Membership Validation
**Objective**: Verify team membership checking against config.yaml
**Steps**:
1. Update config.yaml with different team slug
2. Login with user from new team
3. Verify access is granted
4. Login with user from old team
5. Verify access is denied

**Expected Result**: Access control updates based on configuration changes

### 4.2 Configuration Management (US-003, US-004)

#### Test Case TC-CONFIG-001: Missing Configuration File
**Objective**: Verify onboarding flow when config.yaml is missing
**Steps**:
1. Login to application without config.yaml in .github repository
2. Verify onboarding message is displayed
3. Confirm configuration template is provided
4. Verify dashboard functionality is disabled

**Expected Result**: Clear onboarding guidance with template provided

#### Test Case TC-CONFIG-002: Invalid Configuration File
**Objective**: Verify handling of malformed configuration
**Steps**:
1. Create config.yaml with invalid YAML syntax
2. Login to application
3. Verify error handling and user feedback
4. Test with missing required fields

**Expected Result**: Graceful error handling with clear user guidance

#### Test Case TC-CONFIG-003: Configuration Updates
**Objective**: Verify configuration changes are loaded automatically
**Steps**:
1. Update config.yaml with new policies
2. Trigger manual scan
3. Verify new policies are evaluated
4. Test action configuration changes

**Expected Result**: Configuration changes are applied without application restart

### 4.3 Policy Evaluation (US-004)

#### Test Case TC-POLICY-001: AGENTS.md File Presence
**Objective**: Verify detection of missing AGENTS.md files
**Steps**:
1. Create repository without AGENTS.md
2. Run policy scan
3. Verify violation is detected
4. Add AGENTS.md file
5. Re-scan and verify compliance

**Expected Result**: Policy correctly identifies presence/absence of AGENTS.md

#### Test Case TC-POLICY-002: catalog-info.yaml File Presence
**Objective**: Verify detection of missing catalog-info.yaml files
**Steps**:
1. Create repository without catalog-info.yaml
2. Run policy scan
3. Verify violation is detected
4. Add catalog-info.yaml file
5. Re-scan and verify compliance

**Expected Result**: Policy correctly identifies presence/absence of catalog-info.yaml

#### Test Case TC-POLICY-003: Workflow Permissions Validation
**Objective**: Verify workflow permissions policy evaluation
**Steps**:
1. Create repository with incorrect workflow permissions
2. Run policy scan
3. Verify violation is detected
4. Update permissions to correct setting
5. Re-scan and verify compliance

**Expected Result**: Policy correctly validates workflow permission settings

### 4.4 Dashboard Functionality (US-006, US-007, US-008)

#### Test Case TC-DASH-001: Compliance Dashboard Display
**Objective**: Verify dashboard shows non-compliant repositories
**Steps**:
1. Create repositories with various policy violations
2. Run scan
3. Verify dashboard displays non-compliant repositories
4. Confirm violation details are shown
5. Verify compliance percentage calculation

**Expected Result**: Dashboard accurately displays compliance status and metrics

#### Test Case TC-DASH-002: Repository Filtering
**Objective**: Verify real-time repository filtering functionality
**Steps**:
1. Display dashboard with multiple non-compliant repositories
2. Enter filter text in search field
3. Verify list updates in real-time
4. Test partial matches and case sensitivity
5. Clear filter and verify full list restoration

**Expected Result**: Filtering works in real-time with accurate results

#### Test Case TC-DASH-003: On-Demand Scan
**Objective**: Verify manual scan triggering and feedback
**Steps**:
1. Click "Scan Now" button
2. Verify visual feedback (loading indicator)
3. Confirm scan completes successfully
4. Verify dashboard updates with new results
5. Test concurrent scan requests

**Expected Result**: Manual scans execute successfully with proper UI feedback

### 4.5 Automated Actions (US-010, US-011)

#### Test Case TC-ACTION-001: Issue Creation
**Objective**: Verify automated GitHub issue creation
**Steps**:
1. Configure policy with 'create-issue' action
2. Create repository violating the policy
3. Run scan
4. Verify issue is created in repository
5. Confirm issue title, body, and label are correct
6. Test duplicate issue prevention

**Expected Result**: Issues are created with proper content and no duplicates

#### Test Case TC-ACTION-002: Repository Archiving
**Objective**: Verify automated repository archiving
**Steps**:
1. Configure policy with 'archive-repo' action
2. Create repository violating the policy
3. Run scan
4. Verify repository is archived
5. Confirm repository becomes read-only
6. Verify action is logged

**Expected Result**: Repositories are successfully archived with proper logging

#### Test Case TC-ACTION-003: Action Failure Handling
**Objective**: Verify graceful handling of action failures
**Steps**:
1. Configure action that will fail (e.g., insufficient permissions)
2. Trigger policy violation
3. Verify error handling and logging
4. Confirm other actions continue processing
5. Test retry mechanisms

**Expected Result**: Action failures are handled gracefully without blocking other operations

#### Test Case TC-ACTION-004: Duplicate Issue Prevention Logic
**Objective**: Verify duplicate check algorithm (US-010 requirement)
**Steps**:
1. Mock existing open issue with same title and primary label
2. Attempt to create duplicate issue via ActionService
3. Verify no new issue is created
4. Verify action logged as "Skipped" with existing issue URL
5. Test with different title - should create new issue
6. Test with closed issue with same title - should create new issue

**Expected Result**: Duplicate issues are prevented with proper logging

#### Test Case TC-ACTION-005: Partial Failure Isolation
**Objective**: Verify one action failure doesn't block others
**Steps**:
1. Create 3 violations for same scan
2. Mock GitHub API to fail on second violation only
3. Verify first violation action completes successfully
4. Verify third violation action completes successfully
5. Verify all 3 actions logged with correct status
6. Verify scan is not marked as failed

**Expected Result**: Individual action failures are isolated and logged

### 4.6 Background Job Processing (US-009)

#### Test Case TC-JOB-001: Scheduled Daily Scans
**Objective**: Verify automatic daily scan execution
**Steps**:
1. Verify RecurringJob is registered with ID "daily-scan"
2. Verify CRON expression is "0 0 * * *" (midnight UTC)
3. Verify TimeZone is set to UTC
4. Test job can be triggered manually via Hangfire
5. Verify job invokes IScanningService.PerformScanAsync()

**Expected Result**: Daily scans are correctly configured and execute reliably

**Testing Approach**: Test job CONFIGURATION, not execution timing

#### Test Case TC-JOB-002: Job Failure and Retry Logic
**Objective**: Verify failed jobs retry with exponential backoff
**Steps**:
1. Mock IScanningService to throw exception
2. Enqueue scan job via BackgroundJobClient
3. Verify Hangfire retries the job
4. Verify exponential backoff between retries
5. Verify job marked as failed after max retries (default: 10)
6. Check Hangfire dashboard shows failure details

**Expected Result**: Job failures are handled with appropriate retry and logging

**Testing Approach**: Integration test with Hangfire.InMemory

#### Test Case TC-JOB-003: Action Job Chaining
**Objective**: Verify scan completion triggers action processing
**Steps**:
1. Complete a scan with policy violations
2. Verify scan status set to "Completed"
3. Verify IActionService.ProcessActionsForScanAsync is enqueued
4. Verify action job has correct scanId parameter
5. Test chain completes even if action processing fails
6. Verify both jobs appear in Hangfire dashboard

**Expected Result**: Scan and action jobs are properly chained

### 4.7 Integration Testing

#### Test Case TC-INT-001: GitHub API Integration
**Objective**: Verify GitHub API interactions work correctly
**Steps**:
1. Test repository listing and filtering
2. Verify file content retrieval
3. Test repository settings access
4. Confirm issue creation API calls
5. Test repository archiving API calls
6. Verify rate limiting handling

**Expected Result**: All GitHub API interactions function correctly

#### Test Case TC-INT-002: Database Operations
**Objective**: Verify database operations and data integrity
**Steps**:
1. Test scan result storage
2. Verify policy violation tracking
3. Test action log recording
4. Confirm data consistency across operations
5. Test database migration scenarios

**Expected Result**: Database operations maintain data integrity and consistency

### 4.8 GitHub API Contract Testing

#### Test Case TC-CONTRACT-001: GitHub API Response Schema Validation
**Objective**: Verify GitHub API responses match expected structure
**Priority**: MEDIUM - Important for catching breaking changes
**Steps**:
1. Record actual GitHub API responses using WireMock.Net recording mode
2. Create JSON Schema definitions for critical endpoints:
   - Repository list response
   - File content response
   - Issue creation response
   - Workflow permissions response
3. Store schemas in test fixtures directory
4. Validate live responses against schemas in integration tests
5. Update schemas when GitHub API versions change

**Expected Result**: API changes are detected before they break production

**Testing Approach**: WireMock.Net recording + NJsonSchema validation

#### Test Case TC-CONTRACT-002: GitHub API Response Snapshot Testing
**Objective**: Detect unexpected changes in GitHub API response structure
**Steps**:
1. Capture baseline responses for key API calls
2. Store as JSON snapshots in version control
3. Compare new responses against snapshots in CI/CD
4. Flag any structural differences
5. Manually review and approve changes when GitHub updates API

**Expected Result**: Structural API changes are caught in tests

**Testing Approach**: Verify.NET for snapshot testing

### 4.9 GitHub Service Testing

#### Test Case TC-GITHUB-001: Installation Token Caching
**Objective**: Verify token caching prevents unnecessary GitHub API calls
**Steps**:
1. First API call triggers JWT generation and token request
2. Verify token is cached with 55-minute expiration
3. Second API call reuses cached token
4. Verify only ONE token request made to GitHub
5. Mock token expiration after 55 minutes
6. Verify new token is requested on next call

**Expected Result**: Token caching reduces GitHub API calls and prevents rate limiting

**Testing Approach**: Unit test with NSubstitute for GitHubClient

#### Test Case TC-GITHUB-002: Rate Limit Handling
**Objective**: Verify graceful handling of GitHub API rate limits
**Steps**:
1. Mock GitHub API returning 429 (rate limit exceeded)
2. Verify service logs appropriate warning
3. Test with secondary rate limit (403 with retry-after header)
4. Verify service respects retry-after header
5. Test rate limit recovery scenario

**Expected Result**: Rate limits are handled gracefully with proper logging

**Testing Approach**: Integration test with WireMock.Net

#### Test Case TC-GITHUB-003: File Existence Check Edge Cases
**Objective**: Verify FileExistsAsync handles all scenarios
**Steps**:
1. Test existing file returns true
2. Test missing file returns false (not exception)
3. Test invalid repository ID returns false
4. Test network failure throws appropriate exception
5. Test private repository access

**Expected Result**: File existence checks are reliable across all scenarios

**Testing Approach**: Unit test with mocked GitHubClient

#### Test Case TC-GITHUB-004: Workflow Permissions API
**Objective**: Verify GetWorkflowPermissionsAsync handles all cases
**Steps**:
1. Test repository with "read" permissions
2. Test repository with "write" permissions
3. Test repository with GitHub Actions disabled (returns null)
4. Test repository not found (returns null)
5. Verify correct API endpoint usage

**Expected Result**: Workflow permissions are correctly retrieved

### 4.10 Configuration Service Testing

#### Test Case TC-CONFIG-004: Concurrent Configuration Requests
**Objective**: Verify semaphore prevents duplicate GitHub API fetches
**Steps**:
1. Clear cache to force fetch
2. Simulate 10 concurrent GetConfigAsync() calls
3. Verify only ONE GitHub API call is made
4. Verify all callers receive the same config object
5. Verify semaphore is properly released

**Expected Result**: Thread-safe configuration loading prevents duplicate fetches

**Testing Approach**: Integration test with mocked IGitHubService

#### Test Case TC-CONFIG-005: Sliding Cache Expiration
**Objective**: Verify 15-minute sliding expiration works correctly
**Steps**:
1. Load configuration (cache miss)
2. Access config at 10 minutes (cache hit, extends expiration)
3. Access config at 20 minutes (cache hit - extended from step 2)
4. Wait 15 minutes with no access
5. Next access triggers cache miss and re-fetch

**Expected Result**: Sliding cache reduces API calls while keeping config fresh

**Testing Approach**: Unit test with time manipulation or FakeSystemClock

#### Test Case TC-CONFIG-006: Invalid YAML Handling
**Objective**: Verify robust error handling for malformed configuration
**Steps**:
1. Test with invalid YAML syntax
2. Test with missing required field (authorized_team)
3. Test with empty authorized_team
4. Test with invalid policy type
5. Verify InvalidConfigurationException contains helpful message

**Expected Result**: Clear error messages for configuration problems

#### Test Case TC-CONFIG-007: Force Refresh Bypass
**Objective**: Verify forceRefresh parameter bypasses cache
**Steps**:
1. Load configuration (populates cache)
2. Update config.yaml in GitHub
3. Call GetConfigAsync() without forceRefresh (gets cached version)
4. Call GetConfigAsync(forceRefresh: true)
5. Verify fresh configuration is retrieved

**Expected Result**: Force refresh provides immediate configuration updates

### 4.11 Policy Evaluator Testing

#### Test Case TC-POLICY-004: Strategy Pattern Resolution
**Objective**: Verify correct evaluator is invoked based on policy type
**Steps**:
1. Register mock evaluators with different PolicyType values
2. Configure test policies in config.yaml
3. Run policy evaluation
4. Verify only matching evaluators are called
5. Test with unknown policy type (should log warning)

**Expected Result**: Policy evaluation engine correctly matches and invokes evaluators

**Testing Approach**: Integration test with dependency injection

#### Test Case TC-POLICY-005: Evaluator Error Handling
**Objective**: Verify policy evaluation continues despite individual evaluator failures
**Steps**:
1. Register evaluator that throws exception
2. Register normal evaluator
3. Run evaluation on repository
4. Verify exception is logged
5. Verify other evaluators still execute

**Expected Result**: Individual evaluator failures don't block other policy checks

### 4.12 Authentication Enhancement Testing

#### Test Case TC-AUTH-004: OAuth State Parameter Validation
**Objective**: Verify OAuth state parameter prevents CSRF attacks
**Steps**:
1. Initiate OAuth flow via /challenge endpoint
2. Capture state parameter from redirect
3. Simulate callback with mismatched state
4. Verify authentication fails with error
5. Test with correct state - authentication succeeds

**Expected Result**: State validation prevents CSRF attacks

**Testing Approach**: E2E test with Playwright

#### Test Case TC-AUTH-005: Session Expiration (24 hours)
**Objective**: Verify sessions expire after 24 hours (fixed expiration)
**Steps**:
1. Authenticate user and capture cookie
2. Mock system time to 23 hours later
3. Verify session still valid
4. Mock system time to 25 hours later
5. Verify user redirected to login page

**Expected Result**: Sessions expire after exactly 24 hours

**Testing Approach**: Integration test with time manipulation

#### Test Case TC-AUTH-006: Hangfire Dashboard Authorization
**Objective**: Verify Hangfire dashboard requires authentication
**Steps**:
1. Access /hangfire without authentication
2. Verify redirect to login
3. Authenticate with authorized user
4. Verify /hangfire is accessible
5. Test with unauthorized user (not in team)
6. Verify access denied

**Expected Result**: Hangfire dashboard is protected by HangfireAuthorizationFilter

## 5. Performance Testing

### 5.1 Performance Context
**Application Profile:**
- **Expected Users**: Maximum 50 concurrent users (low load)
- **Repository Volume**: Up to 10,000 repositories per organization (high volume)
- **Critical Bottleneck**: GitHub API rate limits (5,000 requests/hour)
- **Primary Concern**: Repository scan performance, not user load

**Testing Focus:**
Given the low user count but high repository volume, performance testing should focus on:
1. ✅ **Repository scan throughput and rate limit management**
2. ✅ **Database query performance with large datasets**
3. ✅ **Background job processing capacity**
4. ❌ **NOT traditional load testing (50 users is negligible for Blazor Server)**

### 5.2 Repository Scan Performance Testing

#### Test Case TC-PERF-001: Large Organization Scan Performance
**Objective**: Measure scan performance for organizations with high repository counts
**Priority**: CRITICAL - Core application function
**Steps**:
1. Create test organization with varying repository counts: 100, 500, 1000, 5000, 10000
2. Trigger full organization scan
3. Measure:
   - Total scan duration
   - GitHub API calls made
   - Rate limit consumption
   - Database write performance
   - Memory usage during scan
4. Verify scan completes successfully without timeout
5. Check for GitHub API rate limit errors

**Expected Results:**
- **100 repos**: ~8 minutes (400 API calls)
- **500 repos**: ~40 minutes (2,000 API calls)
- **1,000 repos**: ~1.2 hours (4,000 API calls)
- **5,000 repos**: ~6 hours (20,000 API calls, multiple rate limit windows)
- **10,000 repos**: ~12 hours (40,000 API calls, 8 rate limit cycles)

**Performance Criteria:**
- No GitHub API rate limit errors (429 responses)
- Memory usage remains stable (< 1GB)
- Database writes complete successfully
- Application remains responsive during scan

#### Test Case TC-PERF-002: GitHub API Rate Limit Management
**Objective**: Verify application respects and manages GitHub API rate limits
**Priority**: CRITICAL - Prevents application failure
**Steps**:
1. Monitor GitHub API rate limit headers during scan
2. Track `X-RateLimit-Remaining` header
3. Verify application pauses when approaching limit
4. Test rate limit reset handling
5. Measure API call efficiency (minimize unnecessary calls)

**Expected Results:**
- Application monitors rate limit headers
- Automatically throttles when < 100 calls remaining
- Resumes after rate limit reset (1 hour)
- No 429 (rate limit exceeded) errors
- Optimal API call batching (where possible)

**Optimization Strategies:**
- Implement exponential backoff for rate limit errors
- Cache repository metadata to reduce API calls
- Process repositories in batches to manage throughput
- Add rate limit buffer (stop at 100 remaining, not 0)


## 6. Security Testing

### 6.1 Authentication Security
- OAuth flow security validation
- Session management and timeout testing
- Token handling and storage security
- Cross-site request forgery (CSRF) protection

### 6.2 Authorization Security
- Team membership validation accuracy
- Privilege escalation prevention
- Configuration file access control
- API endpoint authorization

### 6.3 Data Security
- Sensitive data encryption
- SQL injection prevention
- Input validation and sanitization
- Audit logging completeness

## 7. Risk Assessment and Mitigation

### 7.1 High-Risk Areas
| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| GitHub API rate limiting | High | Medium | Implement caching and request throttling |
| Background job failures | High | Medium | Implement retry mechanisms and monitoring |
| Configuration file corruption | Medium | Low | Validate configuration on load |
| Database connectivity issues | High | Low | Implement connection pooling and retry logic |

### 7.2 Medium-Risk Areas
| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| OAuth authentication failures | Medium | Medium | Implement fallback authentication flows |
| Policy evaluation errors | Medium | Low | Comprehensive policy testing |
| Dashboard performance degradation | Medium | Medium | Implement pagination and caching |

## 8. Test Data Management

### 8.1 Test Repository Setup
- **Compliant Repositories**: Repositories meeting all policy requirements
- **Partially Compliant**: Repositories violating specific policies
- **Non-Compliant**: Repositories violating multiple policies
- **Edge Cases**: Empty repositories, archived repositories, private repositories

### 8.2 Test User Accounts
- **Authorized Users**: Members of the configured GitHub team
- **Unauthorized Users**: GitHub users not in the team
- **Admin Users**: Users with repository management permissions
- **Regular Users**: Standard GitHub users with limited permissions

### 8.3 Configuration Test Data
- **Valid Configurations**: Complete, valid config.yaml files
- **Invalid Configurations**: Malformed YAML, missing fields
- **Edge Case Configurations**: Empty policies, invalid team slugs

### 8.4 GitHub API Testing Strategy

The application relies heavily on GitHub API integration. Testing is performed at four levels:

**Level 1: Unit Tests (Fast, Isolated)**
- Full mocking with NSubstitute
- Test service logic without network calls
- Fast feedback for business logic
- Example: `GitHubService` token caching logic, error handling

**Level 2: Integration Tests (HTTP Mocking)**
- WireMock.Net for HTTP-level mocking
- Test actual HTTP interactions without real API calls
- Simulate rate limits, errors, edge cases
- Example: Rate limit handling, retry logic, timeout scenarios

**Level 3: Contract Tests (Schema Validation)**
- JSON Schema validation for critical responses
- WireMock.Net recording mode for capturing real responses
- Verify.NET snapshot testing for response structure stability
- Catch breaking API changes early
- Example: Repository metadata structure, issue creation responses
- **Priority**: MEDIUM - Add post-MVP for production stability

**Level 4: E2E Tests (Real API)**
- Test organization with controlled test repositories
- Real GitHub API calls (limited, expensive)
- Pre-production smoke testing
- Example: Full scan workflow against test organization
- **Priority**: LOW - Use sparingly due to rate limits and cost

**Testing Tools by Level:**
| Level | Tool | Use Case | Speed | Cost |
|-------|------|----------|-------|------|
| Unit | NSubstitute | Business logic | Fast | Low |
| Integration | WireMock.Net | HTTP interactions | Fast | Low |
| Contract | NJsonSchema + Verify.NET | API stability | Medium | Low |
| E2E | Real GitHub API | Production validation | Slow | High (rate limits) |

**Best Practices:**
- Prefer lower levels (Unit/Integration) for most tests
- Use contract tests to catch GitHub API changes
- Reserve E2E tests for critical paths only
- Mock GitHub API in CI/CD to avoid rate limits
- Store WireMock recordings in version control

## 9. Entry and Exit Criteria

### 9.1 Entry Criteria
- All development features are complete and integrated
- Unit tests pass with >90% code coverage
- Development environment is stable and accessible
- Test data is prepared and available
- GitHub test organization is configured

### 9.2 Exit Criteria
- All critical and high-priority test cases pass
- Performance criteria are met
- Security vulnerabilities are identified and addressed
- User acceptance criteria are validated
- Production deployment readiness is confirmed

## 10. Test Automation Strategy

### 10.1 Automated Test Categories
- **Unit Tests**: Individual component testing (xUnit, NUnit)
- **Integration Tests**: API and database testing
- **UI Tests**: Blazor component testing (bUnit)
- **End-to-End Tests**: Complete user workflow testing

### 10.2 Automation Tools

**Core Testing Framework:**
- **xUnit** (v2.6+): Unit and integration testing framework
- **FluentAssertions** (v6.12+): Readable, expressive assertions
- **NSubstitute** (v5.1+): Clean, simple mocking framework

**Specialized Testing:**
- **bUnit** (v1.28+): Blazor component testing with strong community support
- **Playwright** (latest): End-to-end browser testing with excellent reliability
- **Testcontainers.MsSql** (v3.7+): SQL Server containerization for integration tests
- **Respawn** (v6.2+): Fast database cleanup between tests
- **Bogus** (v35.4+): Realistic fake data generation

**Integration & Mocking:**
- **WireMock.Net** (v1.5+): HTTP-level mocking for GitHub API
- **Hangfire.InMemory** (v0.10+): In-memory storage for Hangfire testing
- **WebApplicationFactory** (built-in): ASP.NET Core integration testing

**Contract Testing:**
- **NJsonSchema** (v11.0+): JSON Schema validation for API responses
- **Verify.NET** (v25.0+): Snapshot testing for detecting structural changes

**Code Quality:**
- **Coverlet** (v6.0+): Code coverage analysis (built into .NET SDK)

### 10.3 CI/CD Integration
- Automated test execution on pull requests
- Test result reporting and notifications
- Test coverage reporting and tracking
- Performance regression detection

### 10.4 Example: Contract Testing Implementation

Below is a practical example of implementing GitHub API contract testing:

```csharp
using NJsonSchema;
using NJsonSchema.Validation;
using Verify = VerifyXunit;
using WireMock.Server;
using FluentAssertions;
using Xunit;

namespace _10xGitHubPolicies.Tests.Contracts;

public class GitHubApiContractTests : IAsyncLifetime
{
    private readonly JsonSchema _repositorySchema;
    private readonly IGitHubService _githubService;
    private WireMockServer? _wireMockServer;

    public GitHubApiContractTests()
    {
        // Load JSON Schema from file
        _repositorySchema = JsonSchema.FromFileAsync(
            "Schemas/github-repository-response.json"
        ).Result;
        
        _githubService = /* inject your service */;
    }

    [Fact]
    public async Task GetRepository_ResponseMatchesJsonSchema()
    {
        // Arrange - Use WireMock to capture/replay real response
        _wireMockServer = WireMockServer.Start();
        _wireMockServer
            .Given(Request.Create()
                .WithPath("/repos/*/*")
                .UsingGet())
            .RespondWithFile("Fixtures/github-repository-response.json");
        
        // Act - Call through your service
        var repository = await _githubService.GetRepositoryAsync(12345);
        var json = JsonSerializer.Serialize(repository);
        
        // Assert - Validate against JSON Schema
        var errors = _repositorySchema.Validate(json);
        errors.Should().BeEmpty(
            because: "GitHub API response must match expected schema"
        );
    }

    [Fact]
    public async Task GetFileContent_StructureRemainsStable()
    {
        // Arrange
        var repoName = "test-repo";
        var filePath = "AGENTS.md";
        
        // Act - Get actual response structure
        var response = await _githubService.GetFileContentAsync(repoName, filePath);
        
        // Assert - Snapshot test with Verify.NET
        // First run creates snapshot, subsequent runs compare against it
        await Verify(response)
            .UseDirectory("Snapshots")
            .UseMethodName("GitHubFileContentStructure");
        
        // If structure changes, test fails and shows diff
        // Manually review and approve with: dotnet verify accept
    }

    [Fact]
    public async Task CreateIssue_RequestAndResponseContract()
    {
        // Arrange - Record mode: captures real GitHub API response
        _wireMockServer = WireMockServer.Start();
        _wireMockServer.StartRecording(); // Records to mappings folder
        
        // Act - Make real API call through WireMock proxy
        var issue = await _githubService.CreateIssueAsync(
            repositoryId: 12345,
            title: "Test Issue",
            body: "Test body",
            labels: new[] { "test" }
        );
        
        // Assert - Verify response and save recording
        issue.Should().NotBeNull();
        _wireMockServer.SaveMappings();
        
        // Recording can be replayed in future tests without real API calls
    }

    public Task InitializeAsync() => Task.CompletedTask;
    
    public Task DisposeAsync()
    {
        _wireMockServer?.Stop();
        _wireMockServer?.Dispose();
        return Task.CompletedTask;
    }
}
```

**JSON Schema Example** (`Schemas/github-repository-response.json`):
```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "required": ["id", "name", "full_name", "owner"],
  "properties": {
    "id": { "type": "integer" },
    "name": { "type": "string" },
    "full_name": { "type": "string" },
    "owner": {
      "type": "object",
      "required": ["login", "id"],
      "properties": {
        "login": { "type": "string" },
        "id": { "type": "integer" }
      }
    },
    "private": { "type": "boolean" },
    "archived": { "type": "boolean" }
  }
}
```

