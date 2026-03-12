using CodeReviewAgent.Core.Models;
using CodeReviewAgent.Core.Prompts;
using Microsoft.Extensions.Logging;

namespace CodeReviewAgent.Core.Services;

/// <summary>
/// Specialized agent for security review (SQL injection, auth gaps, secrets, input validation, etc.).
/// Uses security_review.md as its system prompt.
/// </summary>
public sealed class SecurityReviewService : SpecializedReviewServiceBase, ISecurityReviewService
{
    private const string AgentName = "Security";

    public SecurityReviewService(
        IGeminiApiClient geminiClient,
        ILogger<SecurityReviewService> logger)
        : base(geminiClient, logger) { }

    public Task<List<SpecializedCodeIssue>?> ReviewAsync(
        FileReviewContext context,
        CancellationToken cancellationToken = default) =>
        CallAndParseAsync(
            AgentName,
            context.FilePath,
            SpecializedReviewPrompts.SecuritySystemPrompt,
            SpecializedReviewPrompts.BuildSpecializedUserMessage(context),
            cancellationToken);
}
