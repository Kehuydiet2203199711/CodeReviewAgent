using CodeReviewAgent.Core.Models;
using CodeReviewAgent.Core.Prompts;
using Microsoft.Extensions.Logging;

namespace CodeReviewAgent.Core.Services;

/// <summary>
/// Specialized agent for coding convention review (naming, formatting, SOLID, async rules, etc.).
/// Uses convention_review.md as its system prompt.
/// </summary>
public sealed class ConventionReviewService : SpecializedReviewServiceBase, IConventionReviewService
{
    private const string AgentName = "Convention";

    public ConventionReviewService(
        IGeminiApiClient geminiClient,
        ILogger<ConventionReviewService> logger)
        : base(geminiClient, logger) { }

    public Task<List<SpecializedCodeIssue>?> ReviewAsync(
        FileReviewContext context,
        CancellationToken cancellationToken = default) =>
        CallAndParseAsync(
            AgentName,
            context.FilePath,
            SpecializedReviewPrompts.ConventionSystemPrompt,
            SpecializedReviewPrompts.BuildSpecializedUserMessage(context),
            cancellationToken);
}
