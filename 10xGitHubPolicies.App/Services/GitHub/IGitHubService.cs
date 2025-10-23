using System.Collections.Generic;
using System.Threading.Tasks;

using Octokit;

namespace _10xGitHubPolicies.App.Services.GitHub;

public interface IGitHubService
{
    Task<IReadOnlyList<Repository>> GetOrganizationRepositoriesAsync();
    Task<bool> FileExistsAsync(long repositoryId, string filePath);
    Task<Repository> GetRepositorySettingsAsync(long repositoryId);
    Task<Issue> CreateIssueAsync(long repositoryId, string title, string body, IEnumerable<string> labels);
    Task ArchiveRepositoryAsync(long repositoryId);
    Task<bool> IsUserMemberOfTeamAsync(string userAccessToken, string org, string teamSlug);
    Task<string?> GetFileContentAsync(string repoName, string path);
    Task<string?> GetWorkflowPermissionsAsync(long repositoryId);
    Task<IReadOnlyList<Issue>> GetOpenIssuesAsync(long repositoryId, string label);
}