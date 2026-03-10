using CodeReviewAgent.Core.Models;

namespace CodeReviewAgent.Core.Services;

/// <summary>
/// Provides AI-powered code review using the Anthropic Claude API.
/// </summary>
public interface IClaudeReviewService
{
    /// <summary>
    /// Reviews the provided C# diff for a specific file and returns a structured review result.
    /// </summary>
    /// <param name="filePath">The path of the file being reviewed (used for context and issue reporting).</param>
    /// <param name="diff">The unified diff content to review.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="ReviewResult"/> containing pass/fail status and any identified issues,
    /// or <c>null</c> if the review could not be completed due to an API error.
    /// </returns>
    Task<ReviewResult?> ReviewFileAsync(
        string filePath,
        string diff,
        CancellationToken cancellationToken = default);
}
