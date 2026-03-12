using CodeReviewAgent.Core.Models;
using CodeReviewAgent.Core.Prompts;
using Microsoft.Extensions.Logging;

namespace CodeReviewAgent.Core.Services;

/// <summary>
/// Specialized agent for performance review (EF N+1, async blocking, cache misuse, etc.).
/// Uses performance_review.md as its system prompt.
/// </summary>
public sealed class PerformanceReviewService : SpecializedReviewServiceBase, IPerformanceReviewService
{
    private const string AgentName = "Performance";

    public PerformanceReviewService(
        IGeminiApiClient geminiClient,
        ILogger<PerformanceReviewService> logger)
        : base(geminiClient, logger) { }

    public Task<List<SpecializedCodeIssue>?> ReviewAsync(
        FileReviewContext context,
        CancellationToken cancellationToken = default) =>
        CallAndParseAsync(
            AgentName,
            context.FilePath,
            SpecializedReviewPrompts.PerformanceSystemPrompt,
            SpecializedReviewPrompts.BuildSpecializedUserMessage(context),
            cancellationToken);
}
