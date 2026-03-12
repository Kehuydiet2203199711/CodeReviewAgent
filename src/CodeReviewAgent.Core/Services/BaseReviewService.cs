using CodeReviewAgent.Core.Models;
using CodeReviewAgent.Core.Prompts;
using Microsoft.Extensions.Logging;

namespace CodeReviewAgent.Core.Services;

/// <summary>
/// Synthesis agent that receives the full file context and the three specialized agents' issue lists,
/// then produces a final curated, deduplicated issue list via base_review.md.
/// </summary>
public sealed class BaseReviewService : SpecializedReviewServiceBase, IBaseReviewService
{
    private const string AgentName = "Base/Synthesis";

    public BaseReviewService(
        IGeminiApiClient geminiClient,
        ILogger<BaseReviewService> logger)
        : base(geminiClient, logger) { }

    public Task<List<SpecializedCodeIssue>?> SynthesizeAsync(
        FileReviewContext context,
        List<SpecializedCodeIssue> conventionIssues,
        List<SpecializedCodeIssue> performanceIssues,
        List<SpecializedCodeIssue> securityIssues,
        CancellationToken cancellationToken = default) =>
        CallAndParseAsync(
            AgentName,
            context.FilePath,
            SpecializedReviewPrompts.BaseSystemPrompt,
            SpecializedReviewPrompts.BuildBaseUserMessage(
                context, conventionIssues, performanceIssues, securityIssues),
            cancellationToken);
}
