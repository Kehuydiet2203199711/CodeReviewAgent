using System.Text.Json.Serialization;

namespace CodeReviewAgent.Core.Models;

/// <summary>
/// Represents the list of changes (diffs) in a merge request.
/// </summary>
public record MergeRequestChanges(
    [property: JsonPropertyName("iid")] int Iid,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("changes")] List<MergeRequestChange> Changes
);

/// <summary>
/// Represents a single file change (diff) within a merge request.
/// </summary>
public record MergeRequestChange(
    [property: JsonPropertyName("old_path")] string OldPath,
    [property: JsonPropertyName("new_path")] string NewPath,
    [property: JsonPropertyName("diff")] string Diff,
    [property: JsonPropertyName("new_file")] bool NewFile,
    [property: JsonPropertyName("renamed_file")] bool RenamedFile,
    [property: JsonPropertyName("deleted_file")] bool DeletedFile
);
