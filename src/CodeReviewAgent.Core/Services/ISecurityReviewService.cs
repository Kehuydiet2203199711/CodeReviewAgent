using CodeReviewAgent.Core.Models;

namespace CodeReviewAgent.Core.Services;

/// <summary>
/// Reviews a C# file for security vulnerabilities using the security_review.md prompt.
/// Returns null on API failure, empty list if no issues found.
/// </summary>
public interface ISecurityReviewService
{
    Task<List<SpecializedCodeIssue>?> ReviewAsync(
        FileReviewContext context,
        CancellationToken cancellationToken = default);
}
