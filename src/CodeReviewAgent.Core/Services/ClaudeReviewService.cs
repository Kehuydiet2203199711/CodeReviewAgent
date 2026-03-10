using System.Text.Json;
using CodeReviewAgent.Core.Models;
using CodeReviewAgent.Core.Prompts;
using Microsoft.Extensions.Logging;

namespace CodeReviewAgent.Core.Services;

/// <summary>
/// Implements C# code review using the Anthropic Claude AI API.
/// </summary>
public sealed class ClaudeReviewService : IClaudeReviewService
{
    private readonly IAnthropicApiClient _anthropicClient;
    private readonly ILogger<ClaudeReviewService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of <see cref="ClaudeReviewService"/>.
    /// </summary>
    public ClaudeReviewService(
        IAnthropicApiClient anthropicClient,
        ILogger<ClaudeReviewService> logger)
    {
        _anthropicClient = anthropicClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ReviewResult?> ReviewFileAsync(
        string filePath,
        string diff,
        CancellationToken cancellationToken = default)
    {
        var userMessage = CSharpReviewPrompt.BuildUserMessage(filePath, diff);

        // Attempt the API call with one retry on failure
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                _logger.LogDebug(
                    "Calling Claude API for file {File} (attempt {Attempt})", filePath, attempt);

                var rawJson = await _anthropicClient.SendMessageAsync(
                    CSharpReviewPrompt.SystemPrompt,
                    userMessage,
                    cancellationToken);

                var result = JsonSerializer.Deserialize<ReviewResult>(rawJson, JsonOptions);

                if (result is null)
                {
                    _logger.LogWarning(
                        "Claude returned null result for {File}", filePath);
                    return null;
                }

                _logger.LogInformation(
                    "Review for {File}: passed={Passed}, issues={Count}",
                    filePath, result.Passed, result.Issues?.Count ?? 0);

                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "Failed to parse Claude response for {File} (attempt {Attempt})",
                    filePath, attempt);
                // JSON parse errors won't improve on retry — bail out
                return null;
            }
            catch (Exception ex) when (attempt == 1)
            {
                _logger.LogWarning(ex,
                    "Claude API call failed for {File} — retrying (attempt {Attempt})",
                    filePath, attempt);

                // Short delay before retry
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Claude API call failed for {File} after {Attempt} attempts — skipping",
                    filePath, attempt);
                return null;
            }
        }

        return null;
    }
}
