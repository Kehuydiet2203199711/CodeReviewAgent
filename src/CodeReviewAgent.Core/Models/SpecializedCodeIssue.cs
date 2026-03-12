using System.Text.Json.Serialization;

namespace CodeReviewAgent.Core.Models;

/// <summary>
/// A code issue produced by a specialized review agent or the synthesis agent.
/// Matches the extended JSON schema defined in base_review.md.
/// </summary>
public record SpecializedCodeIssue(
    [property: JsonPropertyName("issue_id")]    string IssueId,
    [property: JsonPropertyName("file")]        string File,
    [property: JsonPropertyName("line_start")]  int LineStart,
    [property: JsonPropertyName("line_end")]    int LineEnd,
    [property: JsonPropertyName("severity")]    string Severity,
    [property: JsonPropertyName("category")]    string Category,
    [property: JsonPropertyName("title")]       string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("reasoning")]   IssueReasoning Reasoning,
    [property: JsonPropertyName("suggestion")]  string Suggestion,
    [property: JsonPropertyName("score")]       int Score
);
