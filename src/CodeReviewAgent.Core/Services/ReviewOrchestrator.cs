using System.Text;
using System.Text.Json;
using CodeReviewAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeReviewAgent.Core.Services;

/// <summary>
/// Implements the full code review orchestration pipeline using the multi-agent approach:
/// Convention, Performance, and Security agents run in parallel per file,
/// then BaseReviewService synthesizes their findings into a final curated list.
/// </summary>
public sealed class ReviewOrchestrator : IReviewOrchestrator
{
    private const string SkipLabel = "skip-ai-review";

    private readonly IGitLabService _gitLabService;
    private readonly IConventionReviewService _conventionService;
    private readonly IPerformanceReviewService _performanceService;
    private readonly ISecurityReviewService _securityService;
    private readonly IBaseReviewService _baseReviewService;
    private readonly ILogger<ReviewOrchestrator> _logger;

    public ReviewOrchestrator(
        IGitLabService gitLabService,
        IConventionReviewService conventionService,
        IPerformanceReviewService performanceService,
        ISecurityReviewService securityService,
        IBaseReviewService baseReviewService,
        ILogger<ReviewOrchestrator> logger)
    {
        _gitLabService = gitLabService;
        _conventionService = conventionService;
        _performanceService = performanceService;
        _securityService = securityService;
        _baseReviewService = baseReviewService;
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

        if (HasSkipLabel(mergeRequestEvent))
        {
            _logger.LogInformation(
                "MR !{MrIid} has label '{Label}' — skipping AI review",
                mrIid, SkipLabel);
            return;
        }

        var changes = await _gitLabService.GetMergeRequestChangesAsync(
            projectId, mrIid, cancellationToken);

        if (changes is null || changes.Changes.Count == 0)
        {
            _logger.LogWarning("No changes found for MR !{MrIid}", mrIid);
            return;
        }

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
            "Reviewing {FileCount} C# file(s) in MR !{MrIid} using multi-agent pipeline",
            csFiles.Count, mrIid);

        var allChangedPaths = csFiles.Select(c => c.NewPath).ToList();
        var allIssues = new List<SpecializedCodeIssue>();
        var failedFiles = new List<string>();

        foreach (var change in csFiles)
        {
            if (string.IsNullOrWhiteSpace(change.Diff))
            {
                _logger.LogDebug("Skipping {File} — empty diff", change.NewPath);
                continue;
            }

            _logger.LogDebug("Reviewing file: {File}", change.NewPath);

            string? fullContent = null;
            try
            {
                fullContent = await _gitLabService.GetFileContentAsync(
                    projectId, change.NewPath, mr.SourceBranch, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Could not fetch full content for {File} — continuing with diff only",
                    change.NewPath);
            }

            var context = new FileReviewContext(
                FilePath: change.NewPath,
                Diff: change.Diff,
                MrTitle: mr.Title,
                SourceBranch: mr.SourceBranch,
                TargetBranch: mr.TargetBranch,
                IsNewFile: change.NewFile,
                IsRenamedFile: change.RenamedFile,
                OldPath: change.RenamedFile ? change.OldPath : null,
                FullContent: fullContent,
                OtherChangedFiles: allChangedPaths.Where(p => p != change.NewPath).ToList()
            );

            var fileIssues = await RunMultiAgentPipelineAsync(context, cancellationToken);

            if (fileIssues is null)
            {
                _logger.LogWarning(
                    "Review failed for {File} — synthesis agent returned null, will block MR",
                    change.NewPath);
                failedFiles.Add(change.NewPath);
                continue;
            }

            allIssues.AddRange(fileIssues);
        }

        if (failedFiles.Count > 0)
        {
            _logger.LogError(
                "MR !{MrIid} blocked — {Count} file(s) could not be reviewed: {Files}",
                mrIid, failedFiles.Count, string.Join(", ", failedFiles));

            await PostReviewErrorCommentAsync(projectId, mrIid, failedFiles, cancellationToken);
            return;
        }

        var criticalIssues = allIssues
            .Where(i => i.Severity.Equals("CRITICAL", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var highIssues = allIssues
            .Where(i => i.Severity.Equals("HIGH", StringComparison.OrdinalIgnoreCase))
            .ToList();

        bool passed = criticalIssues.Count == 0 && highIssues.Count == 0;

        _logger.LogInformation(
            "Review complete for MR !{MrIid}: passed={Passed}, critical={Critical}, high={High}",
            mrIid, passed, criticalIssues.Count, highIssues.Count);

        if (passed)
            await HandlePassAsync(projectId, mrIid, cancellationToken);
        else
            await HandleFailAsync(projectId, mrIid, allIssues, cancellationToken);
    }

    /// <summary>
    /// Fans out to 3 specialized agents in parallel, then synthesizes with the base agent.
    /// Returns null only if the synthesis (base) agent fails — specialized agent failures
    /// are treated as degraded mode (empty issue list) so the pipeline continues.
    /// </summary>
    private async Task<List<SpecializedCodeIssue>?> RunMultiAgentPipelineAsync(
        FileReviewContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[MultiAgent] Starting parallel specialized review for {File}", context.FilePath);

        var conventionTask  = _conventionService.ReviewAsync(context, cancellationToken);
        var performanceTask = _performanceService.ReviewAsync(context, cancellationToken);
        var securityTask    = _securityService.ReviewAsync(context, cancellationToken);

        await Task.WhenAll(conventionTask, performanceTask, securityTask);

        var conventionIssues  = conventionTask.Result  ?? [];
        var performanceIssues = performanceTask.Result ?? [];
        var securityIssues    = securityTask.Result    ?? [];

        _logger.LogInformation(
            "[MultiAgent] {File}: convention={C}, performance={P}, security={S} raw issues — calling synthesis",
            context.FilePath,
            conventionIssues.Count,
            performanceIssues.Count,
            securityIssues.Count);

        return await _baseReviewService.SynthesizeAsync(
            context, conventionIssues, performanceIssues, securityIssues, cancellationToken);
    }

    private async Task HandlePassAsync(
        int projectId, int mrIid, CancellationToken cancellationToken)
    {
        _logger.LogInformation("MR !{MrIid} passed review — posting LGTM", mrIid);

        var comment = """
            ✅ **LGTM!** AI Code Review passed — no critical or high issues found.

            *Reviewed by AI Agent (multi-agent pipeline: Convention · Performance · Security · Synthesis)*
            """;

        await _gitLabService.PostCommentAsync(projectId, mrIid, comment, cancellationToken);
        //await _gitLabService.ApproveMergeRequestAsync(projectId, mrIid, cancellationToken);
        //await _gitLabService.MergeMergeRequestAsync(projectId, mrIid, cancellationToken);
    }

    private async Task HandleFailAsync(
        int projectId,
        int mrIid,
        List<SpecializedCodeIssue> allIssues,
        CancellationToken cancellationToken)
    {
        var criticalCount = allIssues.Count(i =>
            i.Severity.Equals("CRITICAL", StringComparison.OrdinalIgnoreCase));
        var highCount = allIssues.Count(i =>
            i.Severity.Equals("HIGH", StringComparison.OrdinalIgnoreCase));

        _logger.LogInformation(
            "MR !{MrIid} failed review — posting issue table (critical={C}, high={H})",
            mrIid, criticalCount, highCount);

        await _gitLabService.PostCommentAsync(
            projectId, mrIid,
            BuildFailComment(allIssues, criticalCount, highCount),
            cancellationToken);
    }

    private async Task PostReviewErrorCommentAsync(
        int projectId,
        int mrIid,
        List<string> failedFiles,
        CancellationToken cancellationToken)
    {
        var fileList = string.Join("\n", failedFiles.Select(f => $"- `{f}`"));
        var comment = $"""
            ## 🤖 AI Code Review — ⚠️ Review Error

            > The AI review service failed to analyse the following file(s). The merge has been **blocked** as a precaution:

            {fileList}

            **Please retry** by pushing a new commit, or add then remove the `skip-ai-review` label to bypass.

            ---
            *Reviewed by AI Agent*
            """;

        await _gitLabService.PostCommentAsync(projectId, mrIid, comment, cancellationToken);
    }

    private static string BuildFailComment(
        List<SpecializedCodeIssue> issues, int criticalCount, int highCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 🤖 AI Code Review — ❌ Issues found");
        sb.AppendLine();
        sb.AppendLine($"> Found **{criticalCount} critical**, **{highCount} high** issues. Please fix before merging.");
        sb.AppendLine();

        var blocking = issues
            .Where(i =>
                i.Severity.Equals("CRITICAL", StringComparison.OrdinalIgnoreCase) ||
                i.Severity.Equals("HIGH", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (blocking.Count > 0)
        {
            sb.AppendLine("### 🔴 Critical & 🟠 High Issues");
            sb.AppendLine();
            sb.AppendLine("| File | Lines | Severity | Category | Issue | Suggestion | Score |");
            sb.AppendLine("|------|-------|----------|----------|-------|------------|-------|");
            foreach (var issue in blocking)
            {
                var icon = issue.Severity.Equals("CRITICAL", StringComparison.OrdinalIgnoreCase)
                    ? "🔴 CRITICAL" : "🟠 HIGH";
                var lines = issue.LineStart == issue.LineEnd
                    ? $"{issue.LineStart}"
                    : $"{issue.LineStart}–{issue.LineEnd}";
                sb.AppendLine(
                    $"| `{EscapeMarkdown(issue.File)}` | {lines} | {icon} " +
                    $"| {EscapeMarkdown(issue.Category)} | **{EscapeMarkdown(issue.Title)}**: {EscapeMarkdown(issue.Description)} " +
                    $"| {EscapeMarkdown(issue.Suggestion)} | {issue.Score} |");
            }
            sb.AppendLine();
        }

        var nonBlocking = issues
            .Where(i => i.Severity.Equals("MEDIUM", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (nonBlocking.Count > 0)
        {
            sb.AppendLine("### 🟡 Medium Issues (non-blocking)");
            sb.AppendLine();
            sb.AppendLine("| File | Lines | Category | Issue | Suggestion | Score |");
            sb.AppendLine("|------|-------|----------|-------|------------|-------|");
            foreach (var issue in nonBlocking)
            {
                var lines = issue.LineStart == issue.LineEnd
                    ? $"{issue.LineStart}"
                    : $"{issue.LineStart}–{issue.LineEnd}";
                sb.AppendLine(
                    $"| `{EscapeMarkdown(issue.File)}` | {lines} | {EscapeMarkdown(issue.Category)} " +
                    $"| **{EscapeMarkdown(issue.Title)}**: {EscapeMarkdown(issue.Description)} " +
                    $"| {EscapeMarkdown(issue.Suggestion)} | {issue.Score} |");
            }
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine("*Reviewed by AI Agent (Convention · Performance · Security · Synthesis) · Add label `skip-ai-review` to bypass*");
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
