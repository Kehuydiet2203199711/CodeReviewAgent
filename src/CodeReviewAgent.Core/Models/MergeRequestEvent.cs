using System.Text.Json.Serialization;

namespace CodeReviewAgent.Core.Models;

/// <summary>
/// Represents the GitLab webhook payload for a merge request event.
/// </summary>
public record MergeRequestEvent(
    [property: JsonPropertyName("object_kind")] string ObjectKind,
    [property: JsonPropertyName("object_attributes")] MergeRequestAttributes ObjectAttributes,
    [property: JsonPropertyName("labels")] List<GitLabLabel>? Labels
);

/// <summary>
/// Contains the attributes of the merge request from the webhook payload.
/// </summary>
public record MergeRequestAttributes(
    [property: JsonPropertyName("iid")] int Iid,
    [property: JsonPropertyName("source_project_id")] int SourceProjectId,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("source_branch")] string SourceBranch,
    [property: JsonPropertyName("target_branch")] string TargetBranch
);

/// <summary>
/// Represents a GitLab label attached to a merge request.
/// </summary>
public record GitLabLabel(
    [property: JsonPropertyName("title")] string Title
);
