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
    Task<IReadOnlyList<Organization>> GetUserOrganizationsAsync(string userAccessToken);
    Task<IReadOnlyList<Team>> GetOrganizationTeamsAsync(string userAccessToken, string org);

    // E2E Testing Methods
    Task<Repository> CreateRepositoryAsync(string name, string description = "", bool isPrivate = false);
    Task CreateFileAsync(long repositoryId, string path, string content, string commitMessage = "");
    Task UpdateFileAsync(long repositoryId, string path, string content, string commitMessage = "");
    Task DeleteFileAsync(long repositoryId, string path, string commitMessage = "");
    Task DeleteFileAsync(string repositoryName, string path, string commitMessage = "");
    Task UpdateWorkflowPermissionsAsync(long repositoryId, string permissions);
    Task UnarchiveRepositoryAsync(long repositoryId);
    Task CloseIssueAsync(long repositoryId, int issueNumber);
    Task DeleteRepositoryAsync(string repositoryName);
    Task<IReadOnlyList<Issue>> GetRepositoryIssuesAsync(string repositoryName);

    // Pull Request Methods
    Task<IReadOnlyList<PullRequest>> GetOpenPullRequestsAsync(long repositoryId);
    Task<IssueComment> CreatePullRequestCommentAsync(long repositoryId, int pullRequestNumber, string comment);
    Task<IReadOnlyList<IssueComment>> GetPullRequestCommentsAsync(long repositoryId, int pullRequestNumber);
    Task<CheckRun> CreateStatusCheckAsync(long repositoryId, string headSha, string name, string status, string conclusion, string? detailsUrl = null);
    Task<CheckRun> UpdateStatusCheckAsync(long repositoryId, long checkRunId, string status, string conclusion, string? detailsUrl = null);
    Task<IReadOnlyList<CheckRun>> GetCheckRunsForRefAsync(long repositoryId, string @ref);
}