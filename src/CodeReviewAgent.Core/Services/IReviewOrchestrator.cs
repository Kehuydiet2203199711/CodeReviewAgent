using CodeReviewAgent.Core.Models;

namespace CodeReviewAgent.Core.Services;

/// <summary>
/// Orchestrates the full code review workflow for a GitLab merge request.
/// </summary>
public interface IReviewOrchestrator
{
    /// <summary>
    /// Executes the end-to-end review pipeline for the specified merge request:
    /// fetches diffs, reviews C# files, posts comments, and approves/merges on pass.
    /// </summary>
    /// <param name="mergeRequestEvent">The GitLab webhook event triggering the review.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task OrchestrateReviewAsync(
        MergeRequestEvent mergeRequestEvent,
        CancellationToken cancellationToken = default);
}
