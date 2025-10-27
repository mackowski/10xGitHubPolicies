using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace _10xGitHubPolicies.Tests.Contracts.GitHub;

[Trait("Category", "Contract")]
[Trait("Service", "GitHubService")]
[Trait("Feature", "IssueContract")]
public class IssueResponseContractTests : GitHubContractTestBase
{
    // Schema validation tests verify that Octokit objects contain all required fields
    // as defined in the GitHub API JSON schemas
    
    /// <summary>
    /// CreateIssueAsync - Response Schema
    /// Verifies that CreateIssueAsync response matches JSON schema
    /// </summary>
    [Fact]
    public async Task CreateIssueAsync_ResponseMatchesSchema()
    {
        // Arrange
        SetupGitHubAppAuthentication();
        
        var repositoryId = Faker.Random.Long(1, 999999);
        var repoName = Faker.Internet.DomainWord();
        var orgName = Options.OrganizationName;
        var issueNumber = Faker.Random.Int(1, 9999);
        var issueId = Faker.Random.Long(1, 999999);
        
        // Mock issue creation using repositoryId endpoint (NOT owner/repo format)
        var issueResponse = new
        {
            id = issueId,
            number = issueNumber,
            title = "Test Issue",
            body = "Test body",
            state = "open",
            labels = new[]
            {
                new { id = Faker.Random.Long(1, 999999), name = "policy-violation" },
                new { id = Faker.Random.Long(1, 999999), name = "auto-generated" }
            },
            html_url = $"https://github.com/{orgName}/{repoName}/issues/{issueNumber}"
        };
        
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/issues")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(201)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(issueResponse));
        
        // Act
        var result = await Sut.CreateIssueAsync(
            repositoryId,
            "Test Issue",
            "Test body",
            new[] { "policy-violation", "auto-generated" });
        
        // Assert - Verify key properties match schema requirements
        result.Should().NotBeNull();
        result.Id.Should().Be(issueId, "id is a required integer field");
        result.Number.Should().Be(issueNumber, "number is a required integer field");
        result.Title.Should().Be("Test Issue", "title is a required string field");
        result.Body.Should().Be("Test body", "body is a string or null field");
        result.State.Value.ToString().ToLower().Should().Be("open", "state must be 'open' or 'closed'");
        result.HtmlUrl.Should().NotBeNull("html_url is a required URI field");
        result.Labels.Should().HaveCount(2, "labels should be an array");
        result.Labels.Should().OnlyContain(l => l.Id > 0 && !string.IsNullOrEmpty(l.Name), 
            "each label must have id and name");
    }
    
    /// <summary>
    /// GetOpenIssuesAsync - Response Schema
    /// Verifies that GetOpenIssuesAsync response array matches schema
    /// </summary>
    [Fact]
    public async Task GetOpenIssuesAsync_ResponseMatchesSchema()
    {
        // Arrange
        SetupGitHubAppAuthentication();
        
        var repositoryId = Faker.Random.Long(1, 999999);
        var repoName = Faker.Internet.DomainWord();
        var orgName = Options.OrganizationName;
        var labelName = "policy-violation";
        
        // Mock issues response using repositoryId endpoint
        var issues = Enumerable.Range(1, 3).Select(i => new
        {
            id = Faker.Random.Long(1, 999999),
            number = Faker.Random.Int(1, 9999),
            title = $"Issue {i}",
            body = $"Body {i}",
            state = "open",
            labels = new[]
            {
                new { id = Faker.Random.Long(1, 999999), name = labelName }
            },
            html_url = $"https://github.com/{orgName}/{repoName}/issues/{i}"
        }).ToList();
        
        MockServer
            .Given(Request.Create()
                .WithPath($"/api/v3/repositories/{repositoryId}/issues")
                .WithParam("state", "open")
                .WithParam("labels", labelName)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(issues));
        
        // Act
        var result = await Sut.GetOpenIssuesAsync(repositoryId, labelName);
        
        // Assert - Verify each issue has required schema fields
        result.Should().NotBeNull();
        result.Should().HaveCount(3, "should return all mocked issues");
        
        foreach (var issue in result)
        {
            issue.Id.Should().BeGreaterThan(0, "id is required");
            issue.Number.Should().BeGreaterThan(0, "number is required");
            issue.Title.Should().NotBeNullOrEmpty("title is required");
            issue.State.Value.ToString().ToLower().Should().Be("open", "should only return open issues");
            issue.HtmlUrl.Should().NotBeNull("html_url is required");
            issue.Labels.Should().Contain(l => l.Name == labelName, "should have the filtered label");
            issue.Labels.Should().OnlyContain(l => l.Id > 0 && !string.IsNullOrEmpty(l.Name),
                "each label must have id and name");
        }
    }
}

