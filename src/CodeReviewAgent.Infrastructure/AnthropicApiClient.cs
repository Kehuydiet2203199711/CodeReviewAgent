using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeReviewAgent.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeReviewAgent.Infrastructure;

/// <summary>
/// Configuration options for the Anthropic API client.
/// </summary>
public sealed class AnthropicOptions
{
    /// <summary>The Anthropic API key for authentication.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>The Claude model identifier to use for reviews.</summary>
    public string Model { get; set; } = "claude-sonnet-4-20250514";

    /// <summary>The maximum number of tokens in the model's response.</summary>
    public int MaxTokens { get; set; } = 2000;
}

/// <summary>
/// Implements <see cref="IAnthropicApiClient"/> using the Anthropic Messages REST API.
/// </summary>
public sealed class AnthropicApiClient : IAnthropicApiClient
{
    private readonly HttpClient _httpClient;
    private readonly AnthropicOptions _options;
    private readonly ILogger<AnthropicApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Initializes a new instance of <see cref="AnthropicApiClient"/>.
    /// </summary>
    public AnthropicApiClient(
        HttpClient httpClient,
        IOptions<AnthropicOptions> options,
        ILogger<AnthropicApiClient> logger)
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
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = userMessage }
            }
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogDebug("Sending request to Anthropic API using model {Model}", _options.Model);

        var response = await _httpClient.PostAsync(
            "/v1/messages", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Anthropic API returned {StatusCode}: {Body}",
                (int)response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(responseBody);
        var textContent = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(textContent))
        {
            throw new InvalidOperationException(
                "Anthropic API returned an empty response content.");
        }

        _logger.LogDebug("Received response from Anthropic API ({Length} chars)", textContent.Length);

        return textContent;
    }
}
