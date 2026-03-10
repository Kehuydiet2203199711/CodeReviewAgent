using CodeReviewAgent.Core.Models;

namespace CodeReviewAgent.Core.Services;

/// <summary>
/// Provides operations for interacting with the GitLab REST API v4.
/// </summary>
public interface IGitLabService
{
    /// <summary>
    /// Fetches all file changes (diffs) for a given merge request.
    /// </summary>
    /// <param name="projectId">The GitLab project ID.</param>
    /// <param name="mrIid">The internal ID (iid) of the merge request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The merge request changes including all diffs.</returns>
    Task<MergeRequestChanges?> GetMergeRequestChangesAsync(
        int projectId,
        int mrIid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts a Markdown comment to the specified merge request.
    /// </summary>
    /// <param name="projectId">The GitLab project ID.</param>
    /// <param name="mrIid">The internal ID (iid) of the merge request.</param>
    /// <param name="markdown">The Markdown-formatted comment body.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task PostCommentAsync(
        int projectId,
        int mrIid,
        string markdown,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves the specified merge request.
    /// </summary>
    /// <param name="projectId">The GitLab project ID.</param>
    /// <param name="mrIid">The internal ID (iid) of the merge request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task ApproveMergeRequestAsync(
        int projectId,
        int mrIid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Merges the specified merge request (should only be called after approval).
    /// </summary>
    /// <param name="projectId">The GitLab project ID.</param>
    /// <param name="mrIid">The internal ID (iid) of the merge request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task MergeMergeRequestAsync(
        int projectId,
        int mrIid,
        CancellationToken cancellationToken = default);
}
