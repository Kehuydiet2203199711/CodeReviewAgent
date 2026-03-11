using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeReviewAgent.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeReviewAgent.Infrastructure;

/// <summary>
/// Configuration options for the OpenAI API client.
/// </summary>
public sealed class OpenAiOptions
{
    /// <summary>The OpenAI API key for authentication.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>The model to use (e.g. gpt-4o, gpt-4-turbo).</summary>
    public string Model { get; set; } = "gpt-4o";

    /// <summary>The maximum number of tokens in the response.</summary>
    public int MaxTokens { get; set; } = 4096;
}

/// <summary>
/// Implements <see cref="IOpenAiApiClient"/> using the OpenAI Chat Completions REST API.
/// </summary>
public sealed class OpenAiApiClient : IOpenAiApiClient
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Initializes a new instance of <see cref="OpenAiApiClient"/>.
    /// </summary>
    public OpenAiApiClient(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        ILogger<OpenAiApiClient> logger)
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
        var requestBody = new
        {
            model = _options.Model,
            max_tokens = _options.MaxTokens,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = userMessage  }
            }
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogDebug("Sending request to OpenAI API using model {Model}", _options.Model);

        var response = await _httpClient.PostAsync(
            "/v1/chat/completions", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "OpenAI API returned {StatusCode}: {Body}",
                (int)response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(responseBody);
        var textContent = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(textContent))
            throw new InvalidOperationException("OpenAI API returned an empty response content.");

        _logger.LogDebug("Received response from OpenAI API ({Length} chars)", textContent.Length);

        return textContent;
    }
}
