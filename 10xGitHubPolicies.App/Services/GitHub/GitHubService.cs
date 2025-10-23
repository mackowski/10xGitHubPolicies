using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;

using _10xGitHubPolicies.App.Options;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using Octokit;
using Octokit.Internal;

namespace _10xGitHubPolicies.App.Services.GitHub;

public class GitHubService : IGitHubService
{
    private readonly GitHubAppOptions _options;
    private readonly ILogger<GitHubService> _logger;
    private readonly IMemoryCache _cache;
    private const string InstallationTokenCacheKey = "GitHubInstallationToken";

    public GitHubService(IOptions<GitHubAppOptions> options, ILogger<GitHubService> logger, IMemoryCache cache)
    {
        _options = options.Value;
        _logger = logger;
        _cache = cache;
    }

    public async Task ArchiveRepositoryAsync(long repositoryId)
    {
        var client = await GetAuthenticatedClient();
        await client.Repository.Edit(repositoryId, new RepositoryUpdate { Archived = true });
    }

    public async Task<Issue> CreateIssueAsync(long repositoryId, string title, string body, IEnumerable<string> labels)
    {
        var client = await GetAuthenticatedClient();
        var newIssue = new NewIssue(title) { Body = body };
        foreach (var label in labels)
        {
            newIssue.Labels.Add(label);
        }
        return await client.Issue.Create(repositoryId, newIssue);
    }

    public async Task<bool> FileExistsAsync(long repositoryId, string filePath)
    {
        var client = await GetAuthenticatedClient();
        try
        {
            var contents = await client.Repository.Content.GetAllContents(repositoryId, filePath);
            return contents.Any();
        }
        catch (Octokit.NotFoundException)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<Repository>> GetOrganizationRepositoriesAsync()
    {
        var client = await GetAuthenticatedClient();
        return await client.Repository.GetAllForOrg(_options.OrganizationName);
    }

    public async Task<Repository> GetRepositorySettingsAsync(long repositoryId)
    {
        var client = await GetAuthenticatedClient();
        return await client.Repository.Get(repositoryId);
    }

    public async Task<bool> IsUserMemberOfTeamAsync(string userAccessToken, string org, string teamSlug)
    {
        var userClient = new GitHubClient(new ProductHeaderValue("10xGitHubPolicies"))
        {
            Credentials = new Credentials(userAccessToken)
        };

        try
        {
            var team = await userClient.Organization.Team.GetByName(org, teamSlug);
            var user = await userClient.User.Current();
            var membership = await userClient.Organization.Team.GetMembershipDetails(team.Id, user.Login);
            return membership.State.ToString().Equals("active", StringComparison.OrdinalIgnoreCase);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Could not verify team membership for {Org}/{TeamSlug}. The team may not exist or the user may not have permission to view it.", org, teamSlug);
            return false;
        }
    }

    public async Task<string?> GetFileContentAsync(string repoName, string path)
    {
        var client = await GetAuthenticatedClient();
        try
        {
            var contents = await client.Repository.Content.GetAllContents(_options.OrganizationName, repoName, path);
            var file = contents.FirstOrDefault();
            if (file == null)
            {
                // This case should ideally not be hit if the path is correct and it's a file.
                // NotFoundException is the primary way to know it doesn't exist.
                return null;
            }
            return file.EncodedContent; // This is Base64 encoded content. The caller will decode it.
        }
        catch (Octokit.NotFoundException)
        {
            return null;
        }
    }

    public async Task<string?> GetWorkflowPermissionsAsync(long repositoryId)
    {
        var client = await GetAuthenticatedClient();
        try
        {
            // Use the GitHub API endpoint: GET /repos/{owner}/{repo}/actions/permissions/workflow
            var connection = client.Connection;
            var endpoint = new Uri($"repositories/{repositoryId}/actions/permissions/workflow", UriKind.Relative);
            var response = await connection.Get<WorkflowPermissionsResponse>(endpoint, null);
            return response.Body.DefaultWorkflowPermissions;
        }
        catch (NotFoundException)
        {
            _logger.LogWarning("Workflow permissions not found for repository {RepositoryId}. Actions may be disabled.", repositoryId);
            return null;
        }
    }

    public async Task<IReadOnlyList<Issue>> GetOpenIssuesAsync(long repositoryId, string label)
    {
        var client = await GetAuthenticatedClient();
        try
        {
            var issueRequest = new RepositoryIssueRequest
            {
                State = ItemStateFilter.Open,
                Filter = IssueFilter.All
            };
            issueRequest.Labels.Add(label);

            return await client.Issue.GetAllForRepository(repositoryId, issueRequest);
        }
        catch (NotFoundException)
        {
            _logger.LogWarning("Could not retrieve issues for repository {RepositoryId}.", repositoryId);
            return new List<Issue>();
        }
    }

    private async Task<GitHubClient> GetAuthenticatedClient()
    {
        var token = await _cache.GetOrCreateAsync(InstallationTokenCacheKey, async entry =>
        {
            _logger.LogInformation("Installation token not found in cache. Generating a new one.");

            var jwt = GetJwt();
            var appClient = new GitHubClient(new ProductHeaderValue("10xGitHubPolicies"), new InMemoryCredentialStore(new Credentials(jwt, AuthenticationType.Bearer)));

            var tokenResponse = await appClient.GitHubApps.CreateInstallationToken(_options.InstallationId);

            entry.AbsoluteExpiration = tokenResponse.ExpiresAt.AddMinutes(-5); // Cache for 55 minutes

            _logger.LogInformation("Successfully generated a new installation token, expiring at {Expiry}", tokenResponse.ExpiresAt);

            return tokenResponse.Token;
        });

        return new GitHubClient(new ProductHeaderValue("10xGitHubPolicies"), new InMemoryCredentialStore(new Credentials(token)));
    }

    private string GetJwt()
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(_options.PrivateKey);
        var key = new RsaSecurityKey(rsa);

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.AppId.ToString(),
            IssuedAt = DateTime.UtcNow.AddMinutes(-1), // Add a 1-minute buffer
            Expires = DateTime.UtcNow.AddMinutes(9), // Expires in 9 minutes
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}

// Add a private class for deserialization
public class WorkflowPermissionsResponse
{
    public string DefaultWorkflowPermissions { get; set; } = string.Empty;
    public bool CanApprovePullRequestReviews { get; set; }
}