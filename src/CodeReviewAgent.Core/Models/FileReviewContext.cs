namespace CodeReviewAgent.Core.Models;

/// <summary>
/// Carries all context needed for an AI to perform a meaningful review of a single file.
/// </summary>
/// <param name="FilePath">The current file path (NewPath).</param>
/// <param name="Diff">The unified diff string for this file.</param>
/// <param name="MrTitle">The merge request title, used to understand intent.</param>
/// <param name="SourceBranch">The source branch of the merge request.</param>
/// <param name="TargetBranch">The target branch of the merge request.</param>
/// <param name="IsNewFile">True when the file is newly created in this MR.</param>
/// <param name="IsRenamedFile">True when the file was renamed in this MR.</param>
/// <param name="OldPath">The previous file path when <paramref name="IsRenamedFile"/> is true.</param>
/// <param name="FullContent">
/// The full source content of the file fetched from GitLab (may be null if the fetch failed).
/// </param>
/// <param name="OtherChangedFiles">
/// Paths of other C# files changed in the same MR (excluding this file),
/// providing cross-file awareness to the AI.
/// </param>
public record FileReviewContext(
    string FilePath,
    string Diff,
    string MrTitle,
    string SourceBranch,
    string TargetBranch,
    bool IsNewFile,
    bool IsRenamedFile,
    string? OldPath,
    string? FullContent,
    IReadOnlyList<string> OtherChangedFiles
);
