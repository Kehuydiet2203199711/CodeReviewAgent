using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeReviewAgent.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeReviewAgent.Infrastructure;

/// <summary>
/// Configuration options for the Google Gemini API client.
/// </summary>
public sealed class GeminiOptions
{
    /// <summary>The Google AI Studio API key for authentication.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>The Gemini model to use (e.g. gemini-2.0-flash, gemini-1.5-pro).</summary>
    public string Model { get; set; } = "gemini-2.0-flash";

    /// <summary>The maximum number of output tokens in the response.</summary>
    public int MaxTokens { get; set; } = 4096;
}

/// <summary>
/// Implements <see cref="IGeminiApiClient"/> using the Google Gemini GenerateContent REST API.
/// </summary>
public sealed class GeminiApiClient : IGeminiApiClient
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Initializes a new instance of <see cref="GeminiApiClient"/>.
    /// </summary>
    public GeminiApiClient(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> SendMessageAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        // Gemini API: POST /v1beta/models/{model}:generateContent?key={apiKey}
        var url = $"/v1beta/models/{_options.Model}:generateContent?key={_options.ApiKey}";

        var requestBody = new
        {
            system_instruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = userMessage } }
                }
            },
            generationConfig = new
            {
                maxOutputTokens = _options.MaxTokens,
                temperature = 0.2   // low temperature for deterministic review output
            }
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogDebug("Sending request to Gemini API using model {Model}", _options.Model);

        var response = await _httpClient.PostAsync(url, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Gemini API returned {StatusCode}: {Body}",
                (int)response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(responseBody);

        // Gemini response: candidates[0].content.parts[0].text
        var textContent = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(textContent))
            throw new InvalidOperationException("Gemini API returned an empty response content.");

        _logger.LogDebug("Received response from Gemini API ({Length} chars)", textContent.Length);

        return textContent;
    }
}
