using CodeReviewAgent.Core.Models;

namespace CodeReviewAgent.Core.Services;

/// <summary>
/// Reviews a C# file for performance issues using the performance_review.md prompt.
/// Returns null on API failure, empty list if no issues found.
/// </summary>
public interface IPerformanceReviewService
{
    Task<List<SpecializedCodeIssue>?> ReviewAsync(
        FileReviewContext context,
        CancellationToken cancellationToken = default);
}
