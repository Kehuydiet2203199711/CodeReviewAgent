namespace CodeReviewAgent.Core.Models;

/// <summary>
/// Represents the aggregated result of a code review.
/// </summary>
/// <param name="Passed">Indicates whether the review passed (no critical or high issues).</param>
/// <param name="Summary">A short description of the review outcome.</param>
/// <param name="Issues">The list of issues found during review.</param>
public record ReviewResult(
    bool Passed,
    string Summary,
    List<CodeIssue> Issues
);
