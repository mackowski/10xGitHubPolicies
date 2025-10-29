using Bogus;

namespace _10xGitHubPolicies.Tests.Integration.Builders;

public class GitHubApiResponseBuilder
{
    private readonly Faker _faker;
    
    public GitHubApiResponseBuilder()
    {
        _faker = new Faker();
    }
    
    public string BuildRepositoryResponse(long id, string name, bool archived = false)
    {
        return $$"""
        {
          "id": {{id}},
          "name": "{{name}}",
          "full_name": "test-org/{{name}}",
          "private": false,
          "archived": {{archived.ToString().ToLower()}},
          "owner": {
            "login": "test-org",
            "id": 12345,
            "type": "Organization"
          }
        }
        """;
    }
    
    public string BuildFileContentResponse(string content, string path)
    {
        var base64Content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(content));
        return $$"""
        [{
          "name": "{{System.IO.Path.GetFileName(path)}}",
          "path": "{{path}}",
          "sha": "{{_faker.Random.Hexadecimal(40, prefix: "")}}",
          "size": {{content.Length}},
          "type": "file",
          "content": "{{base64Content}}",
          "encoding": "base64"
        }]
        """;
    }
    
    public string BuildIssueResponse(int number, string title, string label)
    {
        return $$"""
        {
          "id": {{_faker.Random.Int(1000000, 9999999)}},
          "number": {{number}},
          "title": "{{title}}",
          "body": "Test issue body",
          "state": "open",
          "labels": [{
            "id": {{_faker.Random.Int(1000, 9999)}},
            "name": "{{label}}"
          }],
          "html_url": "https://github.com/test-org/test-repo/issues/{{number}}"
        }
        """;
    }
    
    public string BuildWorkflowPermissionsResponse(string permissions)
    {
        return $$"""
        {
          "default_workflow_permissions": "{{permissions}}",
          "can_approve_pull_request_reviews": false
        }
        """;
    }
    
    public string BuildTeamResponse(int id, string slug)
    {
        return $$"""
        {
          "id": {{id}},
          "name": "{{slug}}",
          "slug": "{{slug}}",
          "description": "Test team"
        }
        """;
    }
    
    public string BuildTeamMembershipResponse(string state = "active")
    {
        return $$"""
        {
          "state": "{{state}}",
          "role": "member"
        }
        """;
    }
    
    public string BuildRepositoryCreationResponse(long id, string name, bool isPrivate = false, string defaultBranch = "main")
    {
        return $$"""
        {
          "id": {{id}},
          "name": "{{name}}",
          "full_name": "test-org/{{name}}",
          "private": {{isPrivate.ToString().ToLower()}},
          "archived": false,
          "default_branch": "{{defaultBranch}}",
          "owner": {
            "login": "test-org",
            "id": 12345,
            "type": "Organization"
          }
        }
        """;
    }
    
    public string BuildFileCreationResponse(string content, string path, string sha, string commitMessage)
    {
        var base64Content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(content));
        return $$"""
        {
          "content": {
            "name": "{{System.IO.Path.GetFileName(path)}}",
            "path": "{{path}}",
            "sha": "{{sha}}",
            "size": {{content.Length}},
            "type": "file",
            "content": "{{base64Content}}",
            "encoding": "base64"
          },
          "commit": {
            "sha": "{{_faker.Random.Hexadecimal(40, prefix: "")}}",
            "message": "{{commitMessage}}",
            "author": {
              "name": "test-user",
              "email": "test@example.com",
              "date": "{{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ssZ}}"
            }
          }
        }
        """;
    }
    
    public string BuildFileUpdateResponse(string content, string path, string sha, string commitMessage)
    {
        // Same structure as CreateFileResponse
        return BuildFileCreationResponse(content, path, sha, commitMessage);
    }
    
    public string BuildFileDeleteResponse(string path, string sha, string commitMessage)
    {
        return $$"""
        {
          "content": null,
          "commit": {
            "sha": "{{_faker.Random.Hexadecimal(40, prefix: "")}}",
            "message": "{{commitMessage}}",
            "author": {
              "name": "test-user",
              "email": "test@example.com",
              "date": "{{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ssZ}}"
            }
          }
        }
        """;
    }
    
    public string BuildClosedIssueResponse(int number, string title)
    {
        return $$"""
        {
          "id": {{_faker.Random.Int(1000000, 9999999)}},
          "number": {{number}},
          "title": "{{title}}",
          "body": "Test issue body",
          "state": "closed",
          "labels": [],
          "html_url": "https://github.com/test-org/test-repo/issues/{{number}}"
        }
        """;
    }
    
    public string BuildRepositoryIssuesResponse(int[] issueNumbers, string[] issueTitles)
    {
        var issues = issueNumbers.Zip(issueTitles, (num, title) => $$"""
          {
            "id": {{_faker.Random.Int(1000000, 9999999)}},
            "number": {{num}},
            "title": "{{title}}",
            "body": "Test issue body",
            "state": "open",
            "labels": [],
            "html_url": "https://github.com/test-org/test-repo/issues/{{num}}"
          }
        """);
        
        return $"[{string.Join(",", issues)}]";
    }
    
    public string BuildWorkflowPermissionsUpdateResponse(string permissions)
    {
        // Same structure as GetWorkflowPermissionsResponse
        return BuildWorkflowPermissionsResponse(permissions);
    }
}

