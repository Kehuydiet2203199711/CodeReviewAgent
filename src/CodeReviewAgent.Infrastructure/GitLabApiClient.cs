using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeReviewAgent.Core.Models;
using CodeReviewAgent.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeReviewAgent.Infrastructure;

/// <summary>
/// Configuration options for the GitLab API client.
/// </summary>
public sealed class GitLabOptions
{
    /// <summary>The base URL of the GitLab instance (e.g., https://gitlab.com).</summary>
    public string BaseUrl { get; set; } = "https://gitlab.com";

    /// <summary>The GitLab private access token used for authentication.</summary>
    public string PrivateToken { get; set; } = string.Empty;

    /// <summary>The secret token used to validate incoming webhook requests.</summary>
    public string WebhookSecret { get; set; } = string.Empty;
}

/// <summary>
/// Implements <see cref="IGitLabService"/> using the GitLab REST API v4 via <see cref="HttpClient"/>.
/// </summary>
public sealed class GitLabApiClient : IGitLabService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitLabApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Initializes a new instance of <see cref="GitLabApiClient"/>.
    /// </summary>
    public GitLabApiClient(
        HttpClient httpClient,
        ILogger<GitLabApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<MergeRequestChanges?> GetMergeRequestChangesAsync(
        int projectId,
        int mrIid,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v4/projects/{projectId}/merge_requests/{mrIid}/changes";

        _logger.LogDebug("GET {Url}", url);

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var changes = await response.Content.ReadFromJsonAsync<MergeRequestChanges>(
            JsonOptions, cancellationToken);

        _logger.LogInformation(
            "Fetched {Count} changes for MR !{MrIid}",
            changes?.Changes.Count ?? 0, mrIid);

        return changes;
    }

    /// <inheritdoc />
    public async Task PostCommentAsync(
        int projectId,
        int mrIid,
        string markdown,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v4/projects/{projectId}/merge_requests/{mrIid}/notes";

        _logger.LogDebug("POST comment to MR !{MrIid}", mrIid);

        var payload = new { body = markdown };
        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Posted comment to MR !{MrIid}", mrIid);
    }

    /// <inheritdoc />
    public async Task ApproveMergeRequestAsync(
        int projectId,
        int mrIid,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v4/projects/{projectId}/merge_requests/{mrIid}/approve";

        _logger.LogDebug("POST approve MR !{MrIid}", mrIid);

        var response = await _httpClient.PostAsync(url, null, cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Approved MR !{MrIid}", mrIid);
    }

    /// <inheritdoc />
    public async Task MergeMergeRequestAsync(
        int projectId,
        int mrIid,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v4/projects/{projectId}/merge_requests/{mrIid}/merge";

        _logger.LogDebug("PUT merge MR !{MrIid}", mrIid);

        var payload = new { should_remove_source_branch = false, merge_when_pipeline_succeeds = false };
        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PutAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Merged MR !{MrIid}", mrIid);
    }
}
