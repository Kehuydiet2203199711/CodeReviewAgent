using System.Text;
using CodeReviewAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeReviewAgent.Core.Services;

/// <summary>
/// Implements the full code review orchestration pipeline.
/// </summary>
public sealed class ReviewOrchestrator : IReviewOrchestrator
{
    private static readonly HashSet<string> ReviewedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".cs" };

    private static readonly HashSet<string> IgnoredExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".json", ".xml", ".csproj", ".md", ".yml", ".yaml",
            ".txt", ".sln", ".config", ".props", ".targets"
        };

    private const string SkipLabel = "skip-ai-review";

    private readonly IGitLabService _gitLabService;
    private readonly IClaudeReviewService _claudeReviewService;
    private readonly ILogger<ReviewOrchestrator> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ReviewOrchestrator"/>.
    /// </summary>
    public ReviewOrchestrator(
        IGitLabService gitLabService,
        IClaudeReviewService claudeReviewService,
        ILogger<ReviewOrchestrator> logger)
    {
        _gitLabService = gitLabService;
        _claudeReviewService = claudeReviewService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task OrchestrateReviewAsync(
        MergeRequestEvent mergeRequestEvent,
        CancellationToken cancellationToken = default)
    {
        var mr = mergeRequestEvent.ObjectAttributes;
        var projectId = mr.SourceProjectId;
        var mrIid = mr.Iid;

        _logger.LogInformation(
            "Starting review for MR !{MrIid} in project {ProjectId} (action: {Action})",
            mrIid, projectId, mr.Action);

        // Check for skip label
        if (HasSkipLabel(mergeRequestEvent))
        {
            _logger.LogInformation(
                "MR !{MrIid} has label '{Label}' — skipping AI review",
                mrIid, SkipLabel);
            return;
        }

        // Fetch changes
        var changes = await _gitLabService.GetMergeRequestChangesAsync(
            projectId, mrIid, cancellationToken);

        if (changes is null || changes.Changes.Count == 0)
        {
            _logger.LogWarning("No changes found for MR !{MrIid}", mrIid);
            return;
        }

        // Filter C# files only
        var csFiles = changes.Changes
            .Where(c => !c.DeletedFile && IsCSharpFile(c.NewPath))
            .ToList();

        if (csFiles.Count == 0)
        {
            _logger.LogInformation(
                "No C# files found in MR !{MrIid} — skipping review", mrIid);
            return;
        }

        _logger.LogInformation(
            "Reviewing {FileCount} C# file(s) in MR !{MrIid}", csFiles.Count, mrIid);

        // Review each file
        var allIssues = new List<CodeIssue>();

        foreach (var change in csFiles)
        {
            if (string.IsNullOrWhiteSpace(change.Diff))
            {
                _logger.LogDebug("Skipping {File} — empty diff", change.NewPath);
                continue;
            }

            _logger.LogDebug("Reviewing file: {File}", change.NewPath);

            var result = await _claudeReviewService.ReviewFileAsync(
                change.NewPath, change.Diff, cancellationToken);

            if (result is null)
            {
                _logger.LogWarning(
                    "Review skipped for {File} due to API error", change.NewPath);
                continue;
            }

            allIssues.AddRange(result.Issues);
        }

        // Apply decision logic
        var criticalIssues = allIssues
            .Where(i => i.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var highIssues = allIssues
            .Where(i => i.Severity.Equals("high", StringComparison.OrdinalIgnoreCase))
            .ToList();

        bool passed = criticalIssues.Count == 0 && highIssues.Count == 0;

        _logger.LogInformation(
            "Review complete for MR !{MrIid}: passed={Passed}, critical={Critical}, high={High}",
            mrIid, passed, criticalIssues.Count, highIssues.Count);

        if (passed)
        {
            await HandlePassAsync(projectId, mrIid, cancellationToken);
        }
        else
        {
            await HandleFailAsync(projectId, mrIid, allIssues, cancellationToken);
        }
    }

    private async Task HandlePassAsync(
        int projectId, int mrIid, CancellationToken cancellationToken)
    {
        _logger.LogInformation("MR !{MrIid} passed review — posting LGTM, approving, merging", mrIid);

        var comment = """
            ✅ **LGTM!** AI Code Review passed — no critical or high issues found.

            *Reviewed by AI Agent · Merged automatically.*
            """;

        await _gitLabService.PostCommentAsync(projectId, mrIid, comment, cancellationToken);
        await _gitLabService.ApproveMergeRequestAsync(projectId, mrIid, cancellationToken);
        await _gitLabService.MergeMergeRequestAsync(projectId, mrIid, cancellationToken);
    }

    private async Task HandleFailAsync(
        int projectId,
        int mrIid,
        List<CodeIssue> allIssues,
        CancellationToken cancellationToken)
    {
        var criticalCount = allIssues.Count(i =>
            i.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase));
        var highCount = allIssues.Count(i =>
            i.Severity.Equals("high", StringComparison.OrdinalIgnoreCase));

        _logger.LogInformation(
            "MR !{MrIid} failed review — posting issue table (critical={C}, high={H})",
            mrIid, criticalCount, highCount);

        var comment = BuildFailComment(allIssues, criticalCount, highCount);
        await _gitLabService.PostCommentAsync(projectId, mrIid, comment, cancellationToken);
    }

    private static string BuildFailComment(
        List<CodeIssue> issues, int criticalCount, int highCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 🤖 AI Code Review — ❌ Issues found");
        sb.AppendLine();
        sb.AppendLine($"> Found **{criticalCount} critical**, **{highCount} high** issues. Please fix before merging.");
        sb.AppendLine();

        // Critical & High table
        var blocking = issues
            .Where(i =>
                i.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase) ||
                i.Severity.Equals("high", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (blocking.Count > 0)
        {
            sb.AppendLine("### 🔴 Critical & 🟠 High Issues");
            sb.AppendLine();
            sb.AppendLine("| File | Line | Severity | Rule | Issue | Suggestion |");
            sb.AppendLine("|------|------|----------|------|-------|------------|");

            foreach (var issue in blocking)
            {
                var severityIcon = issue.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase)
                    ? "🔴 critical" : "🟠 high";
                sb.AppendLine(
                    $"| `{EscapeMarkdown(issue.File)}` " +
                    $"| {issue.Line} " +
                    $"| {severityIcon} " +
                    $"| {EscapeMarkdown(issue.Rule)} " +
                    $"| {EscapeMarkdown(issue.Message)} " +
                    $"| {EscapeMarkdown(issue.Suggestion)} |");
            }

            sb.AppendLine();
        }

        // Medium & Low issues
        var nonBlocking = issues
            .Where(i =>
                i.Severity.Equals("medium", StringComparison.OrdinalIgnoreCase) ||
                i.Severity.Equals("low", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (nonBlocking.Count > 0)
        {
            sb.AppendLine("### 🟡 Medium & 🟢 Low Issues (non-blocking)");
            sb.AppendLine();
            sb.AppendLine("| File | Line | Severity | Rule | Issue | Suggestion |");
            sb.AppendLine("|------|------|----------|------|-------|------------|");

            foreach (var issue in nonBlocking)
            {
                var severityIcon = issue.Severity.Equals("medium", StringComparison.OrdinalIgnoreCase)
                    ? "🟡 medium" : "🟢 low";
                sb.AppendLine(
                    $"| `{EscapeMarkdown(issue.File)}` " +
                    $"| {issue.Line} " +
                    $"| {severityIcon} " +
                    $"| {EscapeMarkdown(issue.Rule)} " +
                    $"| {EscapeMarkdown(issue.Message)} " +
                    $"| {EscapeMarkdown(issue.Suggestion)} |");
            }

            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine("*Reviewed by AI Agent · Add label `skip-ai-review` to bypass*");

        return sb.ToString();
    }

    private static bool IsCSharpFile(string path) =>
        Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase);

    private static bool HasSkipLabel(MergeRequestEvent mrEvent) =>
        mrEvent.Labels?.Any(l =>
            l.Title.Equals(SkipLabel, StringComparison.OrdinalIgnoreCase)) ?? false;

    private static string EscapeMarkdown(string text) =>
        text.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
}
