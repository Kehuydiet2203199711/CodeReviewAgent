using CodeReviewAgent.Core.Models;

namespace CodeReviewAgent.Core.Services;

/// <summary>
/// Synthesis agent that receives the file context and the three specialized agents' issue lists,
/// then produces a final curated, deduplicated issue list via base_review.md.
/// Returns null on API failure, empty list if no issues pass the score threshold.
/// </summary>
public interface IBaseReviewService
{
    Task<List<SpecializedCodeIssue>?> SynthesizeAsync(
        FileReviewContext context,
        List<SpecializedCodeIssue> conventionIssues,
        List<SpecializedCodeIssue> performanceIssues,
        List<SpecializedCodeIssue> securityIssues,
        CancellationToken cancellationToken = default);
}
