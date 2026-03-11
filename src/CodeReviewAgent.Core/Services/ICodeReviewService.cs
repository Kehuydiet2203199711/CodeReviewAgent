using CodeReviewAgent.Core.Models;

namespace CodeReviewAgent.Core.Services;

/// <summary>
/// Base contract for any AI-powered C# code review service.
/// Both <see cref="IClaudeReviewService"/> and <see cref="IChatGptReviewService"/> extend this.
/// </summary>
public interface ICodeReviewService
{
    /// <summary>
    /// Reviews the provided C# file using a rich context object and returns a structured review result.
    /// </summary>
    /// <param name="context">
    /// All context required for a meaningful review: diff, full file content,
    /// MR metadata, file status flags, and list of other changed files.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="ReviewResult"/> on success, or <c>null</c> if the review could not be
    /// completed due to an API error (caller must treat null as a hard failure, not a pass).
    /// </returns>
    Task<ReviewResult?> ReviewFileAsync(
        FileReviewContext context,
        CancellationToken cancellationToken = default);
}
