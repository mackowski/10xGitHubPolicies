# Policy Evaluation Unit Tests - Implementation Plan

## Overview

**Target Components**: Policy Evaluation System

**Test Project**: `10xGitHubPolicies.Tests`

**Test Files**:

- `10xGitHubPolicies.Tests/Services/Policies/PolicyEvaluationServiceTests.cs`
- `10xGitHubPolicies.Tests/Services/Policies/Evaluators/HasAgentsMdEvaluatorTests.cs`
- `10xGitHubPolicies.Tests/Services/Policies/Evaluators/HasCatalogInfoYamlEvaluatorTests.cs`
- `10xGitHubPolicies.Tests/Services/Policies/Evaluators/CorrectWorkflowPermissionsEvaluatorTests.cs`

**Testing Framework**: xUnit + NSubstitute + FluentAssertions + Bogus

## Related Test Cases

This implementation covers the following test cases from `.ai/test-plan.md`:

- **TC-POLICY-001**: AGENTS.md File Presence
- **TC-POLICY-002**: catalog-info.yaml File Presence
- **TC-POLICY-003**: Workflow Permissions Validation
- **TC-POLICY-004**: Strategy Pattern Resolution
- **TC-POLICY-005**: Evaluator Error Handling

## Architecture Overview

```
PolicyEvaluationService (Orchestrator)
  ├─ IEnumerable<IPolicyEvaluator> evaluators
  └─ EvaluateRepositoryAsync(repository, policies)
      ├─ Matches policies to evaluators by PolicyType
      └─ Returns List<PolicyViolation>

IPolicyEvaluator Interface
  ├─ string PolicyType { get; }
  └─ Task<PolicyViolation?> EvaluateAsync(repository)

Implementations:
  ├─ HasAgentsMdEvaluator (PolicyType: "has_agents_md")
  │   └─ Checks for AGENTS.md file
  ├─ HasCatalogInfoYamlEvaluator (PolicyType: "has_catalog_info_yaml")
  │   └─ Checks for catalog-info.yaml file
  └─ CorrectWorkflowPermissionsEvaluator (PolicyType: "correct_workflow_permissions")
      └─ Checks workflow permissions == "read"
```

---

## Part 1: PolicyEvaluationService Tests

### Test Class Structure

```csharp
using _10xGitHubPolicies.App.Data.Entities;
using _10xGitHubPolicies.App.Services.Configuration.Models;
using _10xGitHubPolicies.App.Services.Policies;
using Bogus;
using FluentAssertions;
using NSubstitute;
using Octokit;
using Xunit;

namespace _10xGitHubPolicies.Tests.Services.Policies;

[Trait("Category", "Unit")]
[Trait("Service", "PolicyEvaluationService")]
public class PolicyEvaluationServiceTests
{
    private readonly Faker _faker;

    public PolicyEvaluationServiceTests()
    {
        _faker = new Faker();
    }

    // Test methods here...
}
```

### Test Scenarios - PolicyEvaluationService

#### 1.1 EvaluateRepositoryAsync - No Policies Configured

**Test Case**: `EvaluateRepositoryAsync_WhenNoPolicies_ReturnsEmptyList`

**Objective**: Verify empty policy list returns no violations

**Related Test Case**: TC-POLICY-004

```csharp
[Fact]
public async Task EvaluateRepositoryAsync_WhenNoPolicies_ReturnsEmptyList()
{
    // Arrange
    var evaluators = new List<IPolicyEvaluator>
    {
        Substitute.For<IPolicyEvaluator>()
    };
    var sut = new PolicyEvaluationService(evaluators);
    var repository = CreateMockRepository();
    var policies = new List<PolicyConfig>(); // Empty

    // Act
    var result = await sut.EvaluateRepositoryAsync(repository, policies);

    // Assert
    result.Should().BeEmpty(because: "no policies to evaluate");
    
    // Verify no evaluators were called
    await evaluators[0].DidNotReceive().EvaluateAsync(Arg.Any<Repository>());
}
```

#### 1.2 EvaluateRepositoryAsync - Single Policy Match

**Test Case**: `EvaluateRepositoryAsync_WhenSinglePolicyMatches_InvokesCorrectEvaluator`

**Objective**: Verify correct evaluator is selected based on PolicyType

**Related Test Case**: TC-POLICY-004

```csharp
[Fact]
public async Task EvaluateRepositoryAsync_WhenSinglePolicyMatches_InvokesCorrectEvaluator()
{
    // Arrange
    var mockEvaluator = Substitute.For<IPolicyEvaluator>();
    mockEvaluator.PolicyType.Returns("has_agents_md");
    mockEvaluator.EvaluateAsync(Arg.Any<Repository>())
        .Returns(new PolicyViolation { PolicyType = "has_agents_md" });
    
    var evaluators = new List<IPolicyEvaluator> { mockEvaluator };
    var sut = new PolicyEvaluationService(evaluators);
    
    var repository = CreateMockRepository();
    var policies = new List<PolicyConfig>
    {
        new PolicyConfig { Type = "has_agents_md", Name = "AGENTS.md Required" }
    };

    // Act
    var result = await sut.EvaluateRepositoryAsync(repository, policies);

    // Assert
    result.Should().HaveCount(1);
    result.First().PolicyType.Should().Be("has_agents_md");
    
    await mockEvaluator.Received(1).EvaluateAsync(repository);
}
```

#### 1.3 EvaluateRepositoryAsync - Multiple Policies

**Test Case**: `EvaluateRepositoryAsync_WhenMultiplePolicies_EvaluatesAll`

**Objective**: Verify all configured policies are evaluated

**Related Test Case**: TC-POLICY-004

```csharp
[Fact]
public async Task EvaluateRepositoryAsync_WhenMultiplePolicies_EvaluatesAll()
{
    // Arrange
    var evaluator1 = CreateMockEvaluator("has_agents_md", returnsViolation: true);
    var evaluator2 = CreateMockEvaluator("has_catalog_info_yaml", returnsViolation: true);
    var evaluator3 = CreateMockEvaluator("correct_workflow_permissions", returnsViolation: false);
    
    var evaluators = new List<IPolicyEvaluator> { evaluator1, evaluator2, evaluator3 };
    var sut = new PolicyEvaluationService(evaluators);
    
    var repository = CreateMockRepository();
    var policies = new List<PolicyConfig>
    {
        new PolicyConfig { Type = "has_agents_md" },
        new PolicyConfig { Type = "has_catalog_info_yaml" },
        new PolicyConfig { Type = "correct_workflow_permissions" }
    };

    // Act
    var result = await sut.EvaluateRepositoryAsync(repository, policies);

    // Assert
    result.Should().HaveCount(2, because: "2 out of 3 policies returned violations");
    result.Should().Contain(v => v.PolicyType == "has_agents_md");
    result.Should().Contain(v => v.PolicyType == "has_catalog_info_yaml");
    result.Should().NotContain(v => v.PolicyType == "correct_workflow_permissions");
    
    await evaluator1.Received(1).EvaluateAsync(repository);
    await evaluator2.Received(1).EvaluateAsync(repository);
    await evaluator3.Received(1).EvaluateAsync(repository);
}
```

#### 1.4 EvaluateRepositoryAsync - No Matching Evaluator

**Test Case**: `EvaluateRepositoryAsync_WhenNoMatchingEvaluator_SkipsPolicy`

**Objective**: Verify unknown policy types are skipped gracefully

**Related Test Case**: TC-POLICY-004

```csharp
[Fact]
public async Task EvaluateRepositoryAsync_WhenNoMatchingEvaluator_SkipsPolicy()
{
    // Arrange
    var evaluator = CreateMockEvaluator("has_agents_md", returnsViolation: false);
    var evaluators = new List<IPolicyEvaluator> { evaluator };
    var sut = new PolicyEvaluationService(evaluators);
    
    var repository = CreateMockRepository();
    var policies = new List<PolicyConfig>
    {
        new PolicyConfig { Type = "unknown_policy_type" } // No matching evaluator
    };

    // Act
    var result = await sut.EvaluateRepositoryAsync(repository, policies);

    // Assert
    result.Should().BeEmpty(because: "no evaluator matches the policy type");
    
    // Verify evaluator was not called
    await evaluator.DidNotReceive().EvaluateAsync(Arg.Any<Repository>());
}
```

#### 1.5 EvaluateRepositoryAsync - Case Insensitive Matching

**Test Case**: `EvaluateRepositoryAsync_WhenPolicyTypeDifferentCase_MatchesCorrectly`

**Objective**: Verify case-insensitive policy type matching

**Related Test Case**: TC-POLICY-004

```csharp
[Theory]
[InlineData("has_agents_md")]
[InlineData("HAS_AGENTS_MD")]
[InlineData("Has_Agents_Md")]
[InlineData("HAS_agents_MD")]
public async Task EvaluateRepositoryAsync_WhenPolicyTypeDifferentCase_MatchesCorrectly(string policyType)
{
    // Arrange
    var evaluator = Substitute.For<IPolicyEvaluator>();
    evaluator.PolicyType.Returns("has_agents_md"); // Lowercase in evaluator
    evaluator.EvaluateAsync(Arg.Any<Repository>())
        .Returns(new PolicyViolation { PolicyType = "has_agents_md" });
    
    var evaluators = new List<IPolicyEvaluator> { evaluator };
    var sut = new PolicyEvaluationService(evaluators);
    
    var repository = CreateMockRepository();
    var policies = new List<PolicyConfig>
    {
        new PolicyConfig { Type = policyType } // Different case
    };

    // Act
    var result = await sut.EvaluateRepositoryAsync(repository, policies);

    // Assert
    result.Should().HaveCount(1, because: "matching should be case-insensitive");
    await evaluator.Received(1).EvaluateAsync(repository);
}
```

#### 1.6 EvaluateRepositoryAsync - Evaluator Returns Null (No Violation)

**Test Case**: `EvaluateRepositoryAsync_WhenEvaluatorReturnsNull_ExcludesFromResult`

**Objective**: Verify null violations are not included in results

**Related Test Case**: TC-POLICY-001, TC-POLICY-002, TC-POLICY-003

```csharp
[Fact]
public async Task EvaluateRepositoryAsync_WhenEvaluatorReturnsNull_ExcludesFromResult()
{
    // Arrange
    var evaluator = Substitute.For<IPolicyEvaluator>();
    evaluator.PolicyType.Returns("has_agents_md");
    evaluator.EvaluateAsync(Arg.Any<Repository>())
        .Returns((PolicyViolation?)null); // Compliant - no violation
    
    var evaluators = new List<IPolicyEvaluator> { evaluator };
    var sut = new PolicyEvaluationService(evaluators);
    
    var repository = CreateMockRepository();
    var policies = new List<PolicyConfig>
    {
        new PolicyConfig { Type = "has_agents_md" }
    };

    // Act
    var result = await sut.EvaluateRepositoryAsync(repository, policies);

    // Assert
    result.Should().BeEmpty(because: "evaluator returned null (compliant)");
    await evaluator.Received(1).EvaluateAsync(repository);
}
```

#### 1.7 EvaluateRepositoryAsync - Multiple Evaluators, Same Repository

**Test Case**: `EvaluateRepositoryAsync_WhenMultipleEvaluators_PassesSameRepository`

**Objective**: Verify same repository instance is passed to all evaluators

**Related Test Case**: TC-POLICY-004

```csharp
[Fact]
public async Task EvaluateRepositoryAsync_WhenMultipleEvaluators_PassesSameRepository()
{
    // Arrange
    var evaluator1 = Substitute.For<IPolicyEvaluator>();
    var evaluator2 = Substitute.For<IPolicyEvaluator>();
    
    evaluator1.PolicyType.Returns("policy1");
    evaluator2.PolicyType.Returns("policy2");
    
    evaluator1.EvaluateAsync(Arg.Any<Repository>()).Returns((PolicyViolation?)null);
    evaluator2.EvaluateAsync(Arg.Any<Repository>()).Returns((PolicyViolation?)null);
    
    var evaluators = new List<IPolicyEvaluator> { evaluator1, evaluator2 };
    var sut = new PolicyEvaluationService(evaluators);
    
    var repository = CreateMockRepository();
    var policies = new List<PolicyConfig>
    {
        new PolicyConfig { Type = "policy1" },
        new PolicyConfig { Type = "policy2" }
    };

    // Act
    await sut.EvaluateRepositoryAsync(repository, policies);

    // Assert
    await evaluator1.Received(1).EvaluateAsync(repository);
    await evaluator2.Received(1).EvaluateAsync(repository);
}
```

### Helper Methods - PolicyEvaluationService

```csharp
/// <summary>
/// Creates a mock Octokit.Repository for testing
/// </summary>
private Repository CreateMockRepository(long id = 12345, string name = "test-repo")
{
    var repository = Substitute.For<Repository>();
    repository.Id.Returns(id);
    repository.Name.Returns(name);
    repository.FullName.Returns($"owner/{name}");
    return repository;
}

/// <summary>
/// Creates a mock evaluator with specified policy type and violation behavior
/// </summary>
private IPolicyEvaluator CreateMockEvaluator(string policyType, bool returnsViolation)
{
    var evaluator = Substitute.For<IPolicyEvaluator>();
    evaluator.PolicyType.Returns(policyType);
    
    if (returnsViolation)
    {
        evaluator.EvaluateAsync(Arg.Any<Repository>())
            .Returns(new PolicyViolation { PolicyType = policyType });
    }
    else
    {
        evaluator.EvaluateAsync(Arg.Any<Repository>())
            .Returns((PolicyViolation?)null);
    }
    
    return evaluator;
}
```

---

## Part 2: HasAgentsMdEvaluator Tests

### Test Class Structure

```csharp
using _10xGitHubPolicies.App.Services.GitHub;
using _10xGitHubPolicies.App.Services.Policies.Evaluators;
using Bogus;
using FluentAssertions;
using NSubstitute;
using Octokit;
using Xunit;

namespace _10xGitHubPolicies.Tests.Services.Policies.Evaluators;

[Trait("Category", "Unit")]
[Trait("Service", "HasAgentsMdEvaluator")]
public class HasAgentsMdEvaluatorTests
{
    private readonly IGitHubService _gitHubService;
    private readonly HasAgentsMdEvaluator _sut;
    private readonly Faker _faker;

    public HasAgentsMdEvaluatorTests()
    {
        _gitHubService = Substitute.For<IGitHubService>();
        _sut = new HasAgentsMdEvaluator(_gitHubService);
        _faker = new Faker();
    }

    // Test methods here...
}
```

### Test Scenarios - HasAgentsMdEvaluator

#### 2.1 PolicyType Property

**Test Case**: `PolicyType_WhenAccessed_ReturnsCorrectValue`

**Objective**: Verify policy type constant is correct

**Related Test Case**: TC-POLICY-004

```csharp
[Fact]
public void PolicyType_WhenAccessed_ReturnsCorrectValue()
{
    // Act
    var policyType = _sut.PolicyType;

    // Assert
    policyType.Should().Be("has_agents_md");
}
```

#### 2.2 EvaluateAsync - File Exists (Compliant)

**Test Case**: `EvaluateAsync_WhenAgentsMdExists_ReturnsNull`

**Objective**: Verify no violation when AGENTS.md file exists

**Related Test Case**: TC-POLICY-001

```csharp
[Fact]
public async Task EvaluateAsync_WhenAgentsMdExists_ReturnsNull()
{
    // Arrange
    var repository = CreateMockRepository();
    _gitHubService.FileExistsAsync(repository.Id, "AGENTS.md")
        .Returns(true);

    // Act
    var result = await _sut.EvaluateAsync(repository);

    // Assert
    result.Should().BeNull(because: "AGENTS.md file exists, so repository is compliant");
    
    await _gitHubService.Received(1).FileExistsAsync(repository.Id, "AGENTS.md");
}
```

#### 2.3 EvaluateAsync - File Missing (Non-Compliant)

**Test Case**: `EvaluateAsync_WhenAgentsMdMissing_ReturnsViolation`

**Objective**: Verify violation is returned when AGENTS.md is missing

**Related Test Case**: TC-POLICY-001

```csharp
[Fact]
public async Task EvaluateAsync_WhenAgentsMdMissing_ReturnsViolation()
{
    // Arrange
    var repository = CreateMockRepository();
    _gitHubService.FileExistsAsync(repository.Id, "AGENTS.md")
        .Returns(false);

    // Act
    var result = await _sut.EvaluateAsync(repository);

    // Assert
    result.Should().NotBeNull(because: "AGENTS.md file is missing");
    result!.PolicyType.Should().Be("has_agents_md");
    
    await _gitHubService.Received(1).FileExistsAsync(repository.Id, "AGENTS.md");
}
```

#### 2.4 EvaluateAsync - Correct Repository ID Passed

**Test Case**: `EvaluateAsync_WhenCalled_UsesCorrectRepositoryId`

**Objective**: Verify correct repository ID is passed to GitHub service

**Related Test Case**: TC-POLICY-001

```csharp
[Fact]
public async Task EvaluateAsync_WhenCalled_UsesCorrectRepositoryId()
{
    // Arrange
    var expectedRepoId = _faker.Random.Long(1000, 999999);
    var repository = CreateMockRepository(id: expectedRepoId);
    _gitHubService.FileExistsAsync(expectedRepoId, "AGENTS.md")
        .Returns(true);

    // Act
    await _sut.EvaluateAsync(repository);

    // Assert
    await _gitHubService.Received(1).FileExistsAsync(expectedRepoId, "AGENTS.md");
}
```

#### 2.5 EvaluateAsync - File Name Case Sensitivity

**Test Case**: `EvaluateAsync_WhenCalled_ChecksExactFileName`

**Objective**: Verify evaluator checks for exact file name (case-sensitive)

**Related Test Case**: TC-POLICY-001

```csharp
[Fact]
public async Task EvaluateAsync_WhenCalled_ChecksExactFileName()
{
    // Arrange
    var repository = CreateMockRepository();
    _gitHubService.FileExistsAsync(repository.Id, "AGENTS.md")
        .Returns(false);

    // Act
    await _sut.EvaluateAsync(repository);

    // Assert - Verify exact file name is checked
    await _gitHubService.Received(1).FileExistsAsync(
        repository.Id,
        Arg.Is<string>(s => s == "AGENTS.md"));
    
    // Should NOT check for lowercase variants
    await _gitHubService.DidNotReceive().FileExistsAsync(
        repository.Id,
        Arg.Is<string>(s => s == "agents.md"));
}
```

---

## Part 3: HasCatalogInfoYamlEvaluator Tests

### Test Class Structure

```csharp
using _10xGitHubPolicies.App.Services.GitHub;
using _10xGitHubPolicies.App.Services.Policies.Evaluators;
using Bogus;
using FluentAssertions;
using NSubstitute;
using Octokit;
using Xunit;

namespace _10xGitHubPolicies.Tests.Services.Policies.Evaluators;

[Trait("Category", "Unit")]
[Trait("Service", "HasCatalogInfoYamlEvaluator")]
public class HasCatalogInfoYamlEvaluatorTests
{
    private readonly IGitHubService _gitHubService;
    private readonly HasCatalogInfoYamlEvaluator _sut;
    private readonly Faker _faker;

    public HasCatalogInfoYamlEvaluatorTests()
    {
        _gitHubService = Substitute.For<IGitHubService>();
        _sut = new HasCatalogInfoYamlEvaluator(_gitHubService);
        _faker = new Faker();
    }

    // Test methods here...
}
```

### Test Scenarios - HasCatalogInfoYamlEvaluator

#### 3.1 PolicyType Property

**Test Case**: `PolicyType_WhenAccessed_ReturnsCorrectValue`

**Objective**: Verify policy type constant is correct

**Related Test Case**: TC-POLICY-004

```csharp
[Fact]
public void PolicyType_WhenAccessed_ReturnsCorrectValue()
{
    // Act
    var policyType = _sut.PolicyType;

    // Assert
    policyType.Should().Be("has_catalog_info_yaml");
}
```

#### 3.2 EvaluateAsync - File Exists (Compliant)

**Test Case**: `EvaluateAsync_WhenCatalogInfoYamlExists_ReturnsNull`

**Objective**: Verify no violation when catalog-info.yaml exists

**Related Test Case**: TC-POLICY-002

```csharp
[Fact]
public async Task EvaluateAsync_WhenCatalogInfoYamlExists_ReturnsNull()
{
    // Arrange
    var repository = CreateMockRepository();
    _gitHubService.FileExistsAsync(repository.Id, "catalog-info.yaml")
        .Returns(true);

    // Act
    var result = await _sut.EvaluateAsync(repository);

    // Assert
    result.Should().BeNull(because: "catalog-info.yaml file exists, so repository is compliant");
    
    await _gitHubService.Received(1).FileExistsAsync(repository.Id, "catalog-info.yaml");
}
```

#### 3.3 EvaluateAsync - File Missing (Non-Compliant)

**Test Case**: `EvaluateAsync_WhenCatalogInfoYamlMissing_ReturnsViolation`

**Objective**: Verify violation is returned when catalog-info.yaml is missing

**Related Test Case**: TC-POLICY-002

```csharp
[Fact]
public async Task EvaluateAsync_WhenCatalogInfoYamlMissing_ReturnsViolation()
{
    // Arrange
    var repository = CreateMockRepository();
    _gitHubService.FileExistsAsync(repository.Id, "catalog-info.yaml")
        .Returns(false);

    // Act
    var result = await _sut.EvaluateAsync(repository);

    // Assert
    result.Should().NotBeNull(because: "catalog-info.yaml file is missing");
    result!.PolicyType.Should().Be("has_catalog_info_yaml");
    
    await _gitHubService.Received(1).FileExistsAsync(repository.Id, "catalog-info.yaml");
}
```

#### 3.4 EvaluateAsync - Correct Repository ID Passed

**Test Case**: `EvaluateAsync_WhenCalled_UsesCorrectRepositoryId`

**Objective**: Verify correct repository ID is passed to GitHub service

**Related Test Case**: TC-POLICY-002

```csharp
[Fact]
public async Task EvaluateAsync_WhenCalled_UsesCorrectRepositoryId()
{
    // Arrange
    var expectedRepoId = _faker.Random.Long(1000, 999999);
    var repository = CreateMockRepository(id: expectedRepoId);
    _gitHubService.FileExistsAsync(expectedRepoId, "catalog-info.yaml")
        .Returns(true);

    // Act
    await _sut.EvaluateAsync(repository);

    // Assert
    await _gitHubService.Received(1).FileExistsAsync(expectedRepoId, "catalog-info.yaml");
}
```

#### 3.5 EvaluateAsync - File Name Exactness

**Test Case**: `EvaluateAsync_WhenCalled_ChecksExactFileName`

**Objective**: Verify evaluator checks for exact file name with hyphen and lowercase

**Related Test Case**: TC-POLICY-002

```csharp
[Fact]
public async Task EvaluateAsync_WhenCalled_ChecksExactFileName()
{
    // Arrange
    var repository = CreateMockRepository();
    _gitHubService.FileExistsAsync(repository.Id, "catalog-info.yaml")
        .Returns(false);

    // Act
    await _sut.EvaluateAsync(repository);

    // Assert - Verify exact file name is checked
    await _gitHubService.Received(1).FileExistsAsync(
        repository.Id,
        Arg.Is<string>(s => s == "catalog-info.yaml"));
}
```

---

## Part 4: CorrectWorkflowPermissionsEvaluator Tests

### Test Class Structure

```csharp
using _10xGitHubPolicies.App.Services.GitHub;
using _10xGitHubPolicies.App.Services.Policies.Evaluators;
using Bogus;
using FluentAssertions;
using NSubstitute;
using Octokit;
using Xunit;

namespace _10xGitHubPolicies.Tests.Services.Policies.Evaluators;

[Trait("Category", "Unit")]
[Trait("Service", "CorrectWorkflowPermissionsEvaluator")]
public class CorrectWorkflowPermissionsEvaluatorTests
{
    private readonly IGitHubService _gitHubService;
    private readonly CorrectWorkflowPermissionsEvaluator _sut;
    private readonly Faker _faker;

    public CorrectWorkflowPermissionsEvaluatorTests()
    {
        _gitHubService = Substitute.For<IGitHubService>();
        _sut = new CorrectWorkflowPermissionsEvaluator(_gitHubService);
        _faker = new Faker();
    }

    // Test methods here...
}
```

### Test Scenarios - CorrectWorkflowPermissionsEvaluator

#### 4.1 PolicyType Property

**Test Case**: `PolicyType_WhenAccessed_ReturnsCorrectValue`

**Objective**: Verify policy type constant is correct

**Related Test Case**: TC-POLICY-004

```csharp
[Fact]
public void PolicyType_WhenAccessed_ReturnsCorrectValue()
{
    // Act
    var policyType = _sut.PolicyType;

    // Assert
    policyType.Should().Be("correct_workflow_permissions");
}
```

#### 4.2 EvaluateAsync - Permissions are "read" (Compliant)

**Test Case**: `EvaluateAsync_WhenPermissionsAreRead_ReturnsNull`

**Objective**: Verify no violation when permissions are set to "read"

**Related Test Case**: TC-POLICY-003

```csharp
[Fact]
public async Task EvaluateAsync_WhenPermissionsAreRead_ReturnsNull()
{
    // Arrange
    var repository = CreateMockRepository();
    _gitHubService.GetWorkflowPermissionsAsync(repository.Id)
        .Returns("read");

    // Act
    var result = await _sut.EvaluateAsync(repository);

    // Assert
    result.Should().BeNull(because: "permissions are set to 'read' (secure setting)");
    
    await _gitHubService.Received(1).GetWorkflowPermissionsAsync(repository.Id);
}
```

#### 4.3 EvaluateAsync - Permissions are "write" (Non-Compliant)

**Test Case**: `EvaluateAsync_WhenPermissionsAreWrite_ReturnsViolation`

**Objective**: Verify violation is returned when permissions are "write"

**Related Test Case**: TC-POLICY-003

```csharp
[Fact]
public async Task EvaluateAsync_WhenPermissionsAreWrite_ReturnsViolation()
{
    // Arrange
    var repository = CreateMockRepository();
    _gitHubService.GetWorkflowPermissionsAsync(repository.Id)
        .Returns("write");

    // Act
    var result = await _sut.EvaluateAsync(repository);

    // Assert
    result.Should().NotBeNull(because: "permissions are 'write' (insecure setting)");
    result!.PolicyType.Should().Be("correct_workflow_permissions");
    
    await _gitHubService.Received(1).GetWorkflowPermissionsAsync(repository.Id);
}
```

#### 4.4 EvaluateAsync - Permissions are null (Actions Disabled - Compliant)

**Test Case**: `EvaluateAsync_WhenPermissionsAreNull_ReturnsNull`

**Objective**: Verify null permissions (Actions disabled) is considered compliant

**Related Test Case**: TC-POLICY-003, TC-GITHUB-004

```csharp
[Fact]
public async Task EvaluateAsync_WhenPermissionsAreNull_ReturnsNull()
{
    // Arrange
    var repository = CreateMockRepository();
    _gitHubService.GetWorkflowPermissionsAsync(repository.Id)
        .Returns((string?)null);

    // Act
    var result = await _sut.EvaluateAsync(repository);

    // Assert
    result.Should().BeNull(because: "null permissions means GitHub Actions is disabled (compliant)");
    
    await _gitHubService.Received(1).GetWorkflowPermissionsAsync(repository.Id);
}
```

#### 4.5 EvaluateAsync - Different Permission Values

**Test Case**: `EvaluateAsync_WhenVariousPermissions_EvaluatesCorrectly`

**Objective**: Verify evaluation for different permission values

**Related Test Case**: TC-POLICY-003

```csharp
[Theory]
[InlineData("read", false)]      // Compliant
[InlineData("write", true)]      // Violation
[InlineData("admin", true)]      // Violation
[InlineData("none", true)]       // Violation (not "read")
[InlineData("unknown", true)]    // Violation (not "read")
public async Task EvaluateAsync_WhenVariousPermissions_EvaluatesCorrectly(
    string permissions,
    bool expectViolation)
{
    // Arrange
    var repository = CreateMockRepository();
    _gitHubService.GetWorkflowPermissionsAsync(repository.Id)
        .Returns(permissions);

    // Act
    var result = await _sut.EvaluateAsync(repository);

    // Assert
    if (expectViolation)
    {
        result.Should().NotBeNull(because: $"permissions '{permissions}' should be a violation");
        result!.PolicyType.Should().Be("correct_workflow_permissions");
    }
    else
    {
        result.Should().BeNull(because: $"permissions '{permissions}' should be compliant");
    }
}
```

#### 4.6 EvaluateAsync - Case Sensitivity Check

**Test Case**: `EvaluateAsync_WhenPermissionsCaseDiffers_IsCaseSensitive`

**Objective**: Verify permission comparison is case-sensitive

**Related Test Case**: TC-POLICY-003

```csharp
[Theory]
[InlineData("read")]   // Lowercase - compliant
[InlineData("Read")]   // Capital R - violation
[InlineData("READ")]   // Uppercase - violation
[InlineData("ReAd")]   // Mixed case - violation
public async Task EvaluateAsync_WhenPermissionsCaseDiffers_IsCaseSensitive(string permissions)
{
    // Arrange
    var repository = CreateMockRepository();
    _gitHubService.GetWorkflowPermissionsAsync(repository.Id)
        .Returns(permissions);

    // Act
    var result = await _sut.EvaluateAsync(repository);

    // Assert
    if (permissions == "read")
    {
        result.Should().BeNull(because: "lowercase 'read' is the correct value");
    }
    else
    {
        result.Should().NotBeNull(because: "comparison is case-sensitive");
    }
}
```

#### 4.7 EvaluateAsync - Correct Repository ID Passed

**Test Case**: `EvaluateAsync_WhenCalled_UsesCorrectRepositoryId`

**Objective**: Verify correct repository ID is passed to GitHub service

**Related Test Case**: TC-POLICY-003

```csharp
[Fact]
public async Task EvaluateAsync_WhenCalled_UsesCorrectRepositoryId()
{
    // Arrange
    var expectedRepoId = _faker.Random.Long(1000, 999999);
    var repository = CreateMockRepository(id: expectedRepoId);
    _gitHubService.GetWorkflowPermissionsAsync(expectedRepoId)
        .Returns("read");

    // Act
    await _sut.EvaluateAsync(repository);

    // Assert
    await _gitHubService.Received(1).GetWorkflowPermissionsAsync(expectedRepoId);
}
```

---

## Shared Helper Methods

These helper methods are used across all evaluator test classes:

```csharp
/// <summary>
/// Creates a mock Octokit.Repository for testing
/// </summary>
private Repository CreateMockRepository(long id = 12345, string name = "test-repo")
{
    var repository = Substitute.For<Repository>();
    repository.Id.Returns(id);
    repository.Name.Returns(name);
    repository.FullName.Returns($"owner/{name}");
    return repository;
}
```

---

## Test Execution Order

### PolicyEvaluationService (7 tests)

1. No policies configured
2. Single policy match
3. Multiple policies
4. No matching evaluator
5. Case insensitive matching (Theory with 4 cases)
6. Evaluator returns null
7. Same repository passed to all evaluators

### HasAgentsMdEvaluator (5 tests)

1. PolicyType property value
2. File exists (compliant)
3. File missing (violation)
4. Correct repository ID
5. Exact file name check

### HasCatalogInfoYamlEvaluator (5 tests)

1. PolicyType property value
2. File exists (compliant)
3. File missing (violation)
4. Correct repository ID
5. Exact file name check

### CorrectWorkflowPermissionsEvaluator (7 tests)

1. PolicyType property value
2. Permissions are "read" (compliant)
3. Permissions are "write" (violation)
4. Permissions are null (compliant)
5. Various permission values (Theory with 5 cases)
6. Case sensitivity (Theory with 4 cases)
7. Correct repository ID

**Total Tests**: ~24 individual test methods (with Theory expansions: ~38 test executions)

---

## Code Coverage Expectations

**Target Coverage**: 85-90%

### What to Cover

**PolicyEvaluationService**:

- ✅ Empty policy list handling
- ✅ Policy-to-evaluator matching logic
- ✅ Case-insensitive comparison
- ✅ No matching evaluator scenario
- ✅ Null violation filtering
- ✅ Multiple policy evaluation

**All Evaluators**:

- ✅ PolicyType property
- ✅ Compliant scenarios (returns null)
- ✅ Non-compliant scenarios (returns violation)
- ✅ Correct GitHub service method calls
- ✅ Correct parameters passed

**CorrectWorkflowPermissionsEvaluator Specific**:

- ✅ Null permissions handling
- ✅ "read" vs "write" comparison
- ✅ Case sensitivity

### What NOT to Cover

- ❌ Octokit.Repository internal implementation
- ❌ IGitHubService internal implementation (tested separately)
- ❌ LINQ FirstOrDefault internals

---

## Implementation Notes

### Mocking Octokit.Repository

The `Repository` class from Octokit should be mocked:

```csharp
var repository = Substitute.For<Repository>();
repository.Id.Returns(12345);
repository.Name.Returns("test-repo");
```

### Testing Strategy Pattern

The `PolicyEvaluationService` uses the Strategy pattern. Tests should verify:

1. Correct strategy selection based on `PolicyType`
2. All strategies are invoked when configured
3. Unknown strategies are skipped gracefully

### Case Sensitivity Considerations

**PolicyEvaluationService**: Uses `StringComparison.OrdinalIgnoreCase` for policy type matching

**CorrectWorkflowPermissionsEvaluator**: Uses case-sensitive comparison for "read" value

Document both behaviors in tests.

### Null Handling

All evaluators can return `null` for compliant repositories. The orchestrator filters these out. Test both:

- Individual evaluators returning null
- Orchestrator excluding null from results

### GitHub Service Verification

Each evaluator calls specific `IGitHubService` methods:

- `HasAgentsMdEvaluator`: `FileExistsAsync(id, "AGENTS.md")`
- `HasCatalogInfoYamlEvaluator`: `FileExistsAsync(id, "catalog-info.yaml")`
- `CorrectWorkflowPermissionsEvaluator`: `GetWorkflowPermissionsAsync(id)`

Verify exact method signatures and parameters in tests.

---

## Running Tests

```bash
# Run all policy evaluation tests
dotnet test --filter FullyQualifiedName~Policies

# Run orchestrator tests only
dotnet test --filter FullyQualifiedName~PolicyEvaluationServiceTests

# Run all evaluator tests
dotnet test --filter FullyQualifiedName~Evaluators

# Run specific evaluator tests
dotnet test --filter FullyQualifiedName~HasAgentsMdEvaluatorTests
dotnet test --filter FullyQualifiedName~HasCatalogInfoYamlEvaluatorTests
dotnet test --filter FullyQualifiedName~CorrectWorkflowPermissionsEvaluatorTests

# Run with coverage
dotnet test --filter FullyQualifiedName~Policies /p:CollectCoverage=true

# Watch mode for TDD
dotnet watch test --filter FullyQualifiedName~Policies
```

---

## Success Criteria

### Overall

- ✅ All ~24 test methods pass (~38 with Theory expansions)
- ✅ Code coverage > 85% for all policy components
- ✅ Test execution time < 5 seconds total
- ✅ No flaky tests (tests pass consistently)
- ✅ Proper test isolation

### PolicyEvaluationService

- ✅ All TC-POLICY-004 scenarios covered (strategy pattern)
- ✅ Case-insensitive matching verified
- ✅ Unknown policy types handled gracefully
- ✅ Multiple evaluators tested

### HasAgentsMdEvaluator

- ✅ TC-POLICY-001 fully covered
- ✅ Both compliant and non-compliant scenarios
- ✅ Exact file name verification

### HasCatalogInfoYamlEvaluator

- ✅ TC-POLICY-002 fully covered
- ✅ Both compliant and non-compliant scenarios
- ✅ Exact file name verification

### CorrectWorkflowPermissionsEvaluator

- ✅ TC-POLICY-003 fully covered
- ✅ All permission values tested (read, write, null)
- ✅ Case sensitivity documented
- ✅ Actions disabled scenario (null) handled

---

## Integration with Existing Tests

### Dependencies Tested Elsewhere

- **IGitHubService**: Tested in `GitHubServiceTests.cs` (to be created)
  - `FileExistsAsync` method
  - `GetWorkflowPermissionsAsync` method

### Related Test Coverage

This test suite focuses on:

- Policy evaluation business logic
- Strategy pattern implementation
- Individual policy checks

Integration tests should cover:

- End-to-end policy evaluation with real GitHub API (mocked with WireMock.Net)
- Full scanning workflow invoking PolicyEvaluationService

---

## Next Steps After Implementation

1. **Review test coverage report** - Ensure 85%+ coverage
2. **Add integration tests** - Test with WireMock.Net for GitHub API
3. **Document policy addition process** - How to add new evaluators
4. **Create evaluator template** - Standardize new evaluator creation
5. **Update policy-evaluation.md** - Add testing examples
6. **Consider performance tests** - Evaluate 1000+ repositories
7. **Plan E2E tests** - Full scan with multiple policies

---

## Adding New Policy Evaluators

When adding a new policy evaluator, follow this test pattern:

```csharp
// 1. Create evaluator test class
public class NewPolicyEvaluatorTests
{
    private readonly IGitHubService _gitHubService;
    private readonly NewPolicyEvaluator _sut;
    
    // 2. Test PolicyType property
    [Fact]
    public void PolicyType_WhenAccessed_ReturnsCorrectValue()
    {
        _sut.PolicyType.Should().Be("new_policy_type");
    }
    
    // 3. Test compliant scenario (returns null)
    [Fact]
    public async Task EvaluateAsync_WhenCompliant_ReturnsNull()
    {
        // Setup compliant state
        var result = await _sut.EvaluateAsync(repository);
        result.Should().BeNull();
    }
    
    // 4. Test non-compliant scenario (returns violation)
    [Fact]
    public async Task EvaluateAsync_WhenNonCompliant_ReturnsViolation()
    {
        // Setup non-compliant state
        var result = await _sut.EvaluateAsync(repository);
        result.Should().NotBeNull();
        result!.PolicyType.Should().Be("new_policy_type");
    }
    
    // 5. Test GitHub service interactions
    [Fact]
    public async Task EvaluateAsync_WhenCalled_CallsCorrectGitHubMethod()
    {
        await _sut.EvaluateAsync(repository);
        await _gitHubService.Received(1).NewMethod(repository.Id, args);
    }
}
```

---

## Common Pitfalls to Avoid

❌ **Don't**: Mock `PolicyViolation` entity

✅ **Do**: Create real `PolicyViolation` instances in tests

❌ **Don't**: Test Octokit.Repository internal behavior

✅ **Do**: Mock repository and test evaluator logic

❌ **Don't**: Hard-code repository IDs

✅ **Do**: Use Faker to generate realistic test data

❌ **Don't**: Skip null handling tests

✅ **Do**: Explicitly test null returns (compliant cases)

❌ **Don't**: Assume case-insensitive comparison everywhere

✅ **Do**: Document and test case sensitivity for each evaluator

❌ **Don't**: Test LINQ method internals

✅ **Do**: Test that correct evaluator is selected and invoked

❌ **Don't**: Forget to verify GitHub service calls

✅ **Do**: Use `Received()` to verify correct methods and parameters

---

## Performance Considerations

### Evaluator Performance

- Each evaluator makes 1 GitHub API call
- Tests should complete in < 50ms per test
- No database operations in evaluators

### Orchestrator Performance

- O(n*m) complexity: n policies × m evaluators
- Use `FirstOrDefault` for efficiency (stops at first match)
- Tests with 10+ policies should complete quickly

### Test Performance Goals

- Single test: < 50ms
- Test class: < 500ms
- All policy tests: < 5 seconds

---

## Security Testing Notes

These evaluators enforce security policies:

- **AGENTS.md**: AI agent configuration documentation
- **catalog-info.yaml**: Service catalog metadata
- **Workflow permissions**: Prevent privilege escalation in CI/CD

### Security Test Considerations

1. **Bypass Attempts**: Test that evaluators can't be bypassed
2. **False Positives**: Ensure compliant repos aren't flagged
3. **False Negatives**: Ensure violations are caught
4. **API Manipulation**: Verify correct API responses are checked

---

## Example: Complete Test Implementation

```csharp
namespace _10xGitHubPolicies.Tests.Services.Policies.Evaluators;

[Trait("Category", "Unit")]
[Trait("Service", "HasAgentsMdEvaluator")]
public class HasAgentsMdEvaluatorTests
{
    private readonly IGitHubService _gitHubService;
    private readonly HasAgentsMdEvaluator _sut;
    private readonly Faker _faker;

    public HasAgentsMdEvaluatorTests()
    {
        _gitHubService = Substitute.For<IGitHubService>();
        _sut = new HasAgentsMdEvaluator(_gitHubService);
        _faker = new Faker();
    }

    [Fact]
    public void PolicyType_WhenAccessed_ReturnsCorrectValue()
    {
        _sut.PolicyType.Should().Be("has_agents_md");
    }

    [Fact]
    public async Task EvaluateAsync_WhenAgentsMdExists_ReturnsNull()
    {
        var repository = CreateMockRepository();
        _gitHubService.FileExistsAsync(repository.Id, "AGENTS.md").Returns(true);

        var result = await _sut.EvaluateAsync(repository);

        result.Should().BeNull();
        await _gitHubService.Received(1).FileExistsAsync(repository.Id, "AGENTS.md");
    }

    [Fact]
    public async Task EvaluateAsync_WhenAgentsMdMissing_ReturnsViolation()
    {
        var repository = CreateMockRepository();
        _gitHubService.FileExistsAsync(repository.Id, "AGENTS.md").Returns(false);

        var result = await _sut.EvaluateAsync(repository);

        result.Should().NotBeNull();
        result!.PolicyType.Should().Be("has_agents_md");
    }

    private Repository CreateMockRepository(long id = 12345)
    {
        var repository = Substitute.For<Repository>();
        repository.Id.Returns(id);
        return repository;
    }
}
```

This plan provides comprehensive coverage for the entire policy evaluation system!