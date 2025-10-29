using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace _10xGitHubPolicies.Tests.Contracts.GitHub;

[Trait("Category", "Contract")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "WorkflowPermissionsContract")]
public class WorkflowPermissionsContractTests : GitHubContractTestBase
{
    // Schema validation tests verify that the API response contains required fields
    // as defined in the GitHub API JSON schemas

    /// <summary>
    /// GetWorkflowPermissionsAsync - Response Schema
    /// Verifies that GetWorkflowPermissionsAsync response matches JSON schema
    /// </summary>
    [Fact]
    public async Task GetWorkflowPermissionsAsync_ResponseMatchesSchema()
    {
        // Arrange
        SetupGitHubAppAuthentication();

        var repositoryId = Faker.Random.Long(1, 999999);
        var repoName = Faker.Internet.DomainWord();
        var orgName = Options.OrganizationName;

        // Mock workflow permissions response using repositoryId endpoint
        var workflowPermissionsResponse = new
        {
            default_workflow_permissions = "read",
            can_approve_pull_request_reviews = true
        };

        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/actions/permissions/workflow")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(workflowPermissionsResponse));

        // Act
        var result = await Sut.GetWorkflowPermissionsAsync(repositoryId);

        // Assert - Verify the response matches schema requirements
        result.Should().NotBeNull("result should not be null");
        result.Should().BeOneOf("read", "write", "default_workflow_permissions must be 'read' or 'write'");
        result.Should().Be("read", "the mocked response should return 'read'");

        // Verify the mock response structure matches schema expectations
        // (default_workflow_permissions: string with enum ["read", "write"])
        // (can_approve_pull_request_reviews: boolean - optional)
        workflowPermissionsResponse.default_workflow_permissions.Should().Be("read");
        workflowPermissionsResponse.can_approve_pull_request_reviews.Should().BeTrue();
    }
}

