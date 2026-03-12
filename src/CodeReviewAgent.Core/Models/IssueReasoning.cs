using System.Text.Json.Serialization;

namespace CodeReviewAgent.Core.Models;

/// <summary>
/// Chain-of-thought reasoning used by the synthesis agent to confirm an issue.
/// </summary>
public record IssueReasoning(
    [property: JsonPropertyName("why_flagged")]          string WhyFlagged,
    [property: JsonPropertyName("how_verified")]         string HowVerified,
    [property: JsonPropertyName("false_positive_check")] string FalsePositiveCheck
);
