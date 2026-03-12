using CodeReviewAgent.Core.Models;

namespace CodeReviewAgent.Core.Services;

/// <summary>
/// Reviews a C# file for coding convention violations using the convention_review.md prompt.
/// Returns null on API failure, empty list if no issues found.
/// </summary>
public interface IConventionReviewService
{
    Task<List<SpecializedCodeIssue>?> ReviewAsync(
        FileReviewContext context,
        CancellationToken cancellationToken = default);
}
