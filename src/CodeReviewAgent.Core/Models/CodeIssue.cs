namespace CodeReviewAgent.Core.Models;

/// <summary>
/// Represents a single code issue found during review.
/// </summary>
/// <param name="File">The file path where the issue was found.</param>
/// <param name="Line">The line number of the issue.</param>
/// <param name="Severity">Severity level: critical, high, medium, or low.</param>
/// <param name="Rule">The name of the violated rule.</param>
/// <param name="Message">A description of the issue.</param>
/// <param name="Suggestion">A recommended fix for the issue.</param>
public record CodeIssue(
    string File,
    int Line,
    string Severity,
    string Rule,
    string Message,
    string Suggestion
);
