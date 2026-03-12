using System.Text.Json;
using CodeReviewAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeReviewAgent.Core.Services;

/// <summary>
/// Abstract base class for all specialized review services.
/// Provides retry logic, JSON parsing with markdown-fence stripping, and structured logging.
/// All specialized agents use IGeminiApiClient for AI calls.
/// </summary>
public abstract class SpecializedReviewServiceBase
{
    protected readonly IGeminiApiClient GeminiClient;
    protected readonly ILogger Logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected SpecializedReviewServiceBase(
        IGeminiApiClient geminiClient,
        ILogger logger)
    {
        GeminiClient = geminiClient;
        Logger = logger;
    }

    /// <summary>
    /// Calls Gemini with the given system prompt and user message.
    /// Retries once on transient API failures.
    /// Returns null on unrecoverable failure, empty list if agent found no issues.
    /// </summary>
    protected async Task<List<SpecializedCodeIssue>?> CallAndParseAsync(
        string agentName,
        string filePath,
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                Logger.LogDebug(
                    "[{Agent}] Calling Gemini for {File} (attempt {Attempt})",
                    agentName, filePath, attempt);

                var raw = await GeminiClient.SendMessageAsync(
                    systemPrompt, userMessage, cancellationToken);

                var cleaned = StripMarkdownFence(raw);
                var issues = JsonSerializer.Deserialize<List<SpecializedCodeIssue>>(
                    cleaned, JsonOptions);

                Logger.LogInformation(
                    "[{Agent}] {File}: {Count} issue(s) found",
                    agentName, filePath, issues?.Count ?? 0);

                return issues ?? [];
            }
            catch (JsonException ex)
            {
                Logger.LogError(ex,
                    "[{Agent}] JSON parse failure for {File} (attempt {Attempt}) — not retrying",
                    agentName, filePath, attempt);
                return null;
            }
            catch (Exception ex) when (attempt == 1)
            {
                Logger.LogWarning(ex,
                    "[{Agent}] API call failed for {File} — retrying",
                    agentName, filePath);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,
                    "[{Agent}] API call failed for {File} after 2 attempts",
                    agentName, filePath);
                return null;
            }
        }

        return null;
    }

    private static string StripMarkdownFence(string raw)
    {
        var trimmed = raw.Trim();
        if (!trimmed.StartsWith("```")) return trimmed;
        var firstNewline = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```");
        if (firstNewline > 0 && lastFence > firstNewline)
            return trimmed.Substring(firstNewline + 1, lastFence - firstNewline - 1).Trim();
        return trimmed;
    }
}
