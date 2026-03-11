using System.Text.Json;
using CodeReviewAgent.Core.Models;
using CodeReviewAgent.Core.Prompts;
using Microsoft.Extensions.Logging;

namespace CodeReviewAgent.Core.Services;

/// <summary>
/// Implements C# code review using the Google Gemini API.
/// </summary>
public sealed class GeminiReviewService : IGeminiReviewService
{
    private readonly IGeminiApiClient _geminiClient;
    private readonly ILogger<GeminiReviewService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of <see cref="GeminiReviewService"/>.
    /// </summary>
    public GeminiReviewService(
        IGeminiApiClient geminiClient,
        ILogger<GeminiReviewService> logger)
    {
        _geminiClient = geminiClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ReviewResult?> ReviewFileAsync(
        FileReviewContext context,
        CancellationToken cancellationToken = default)
    {
        var userMessage = CSharpReviewPrompt.BuildUserMessage(context);

        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                _logger.LogDebug(
                    "Calling Gemini API for file {File} (attempt {Attempt})",
                    context.FilePath, attempt);

                var rawJson = await _geminiClient.SendMessageAsync(
                    CSharpReviewPrompt.SystemPrompt,
                    userMessage,
                    cancellationToken);

                // Gemini sometimes wraps response in ```json ... ``` — strip it
                var cleaned = StripMarkdownCodeBlock(rawJson);

                var result = JsonSerializer.Deserialize<ReviewResult>(cleaned, JsonOptions);

                if (result is null)
                {
                    _logger.LogWarning("Gemini returned null result for {File}", context.FilePath);
                    return null;
                }

                _logger.LogInformation(
                    "Review for {File}: passed={Passed}, issues={Count}",
                    context.FilePath, result.Passed, result.Issues?.Count ?? 0);

                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "Failed to parse Gemini response for {File} (attempt {Attempt})",
                    context.FilePath, attempt);
                return null;
            }
            catch (Exception ex) when (attempt == 1)
            {
                _logger.LogWarning(ex,
                    "Gemini API call failed for {File} — retrying (attempt {Attempt})",
                    context.FilePath, attempt);

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Gemini API call failed for {File} after {Attempt} attempts — skipping",
                    context.FilePath, attempt);
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Strips markdown code fences (```json ... ```) that Gemini sometimes wraps around JSON.
    /// </summary>
    private static string StripMarkdownCodeBlock(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            var lastFence = trimmed.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                return trimmed.Substring(firstNewline + 1, lastFence - firstNewline - 1).Trim();
        }
        return trimmed;
    }
}
