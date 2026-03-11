using CodeReviewAgent.Core.Models;
using CodeReviewAgent.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CodeReviewAgent.Tests;

/// <summary>
/// Unit tests for <see cref="ReviewOrchestrator"/> covering pass and fail scenarios.
/// </summary>
public sealed class ReviewOrchestratorTests
{
    private readonly Mock<IGitLabService> _gitLabMock;
    private readonly Mock<IClaudeReviewService> _claudeMock;
    private readonly ILogger<ReviewOrchestrator> _logger;
    private readonly ReviewOrchestrator _orchestrator;

    public ReviewOrchestratorTests()
    {
        _gitLabMock = new Mock<IGitLabService>(MockBehavior.Strict);
        _claudeMock = new Mock<IClaudeReviewService>(MockBehavior.Strict);
        _logger = new LoggerFactory().CreateLogger<ReviewOrchestrator>();
        _orchestrator = new ReviewOrchestrator(_gitLabMock.Object, _claudeMock.Object, _logger);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static MergeRequestEvent BuildMrEvent(
        int iid = 1,
        int projectId = 42,
        string action = "open",
        List<GitLabLabel>? labels = null) =>
        new(
            ObjectKind: "merge_request",
            ObjectAttributes: new MergeRequestAttributes(
                Iid: iid,
                SourceProjectId: projectId,
                Action: action,
                Title: "Test MR",
                SourceBranch: "feature/test",
                TargetBranch: "main"),
            Labels: labels);

    private static MergeRequestChanges BuildChanges(params MergeRequestChange[] changes) =>
        new(Iid: 1, Title: "Test MR", Changes: changes.ToList());

    private static MergeRequestChange CsChange(string path = "src/MyService.cs", string diff = "+var x = 1;") =>
        new(OldPath: path, NewPath: path, Diff: diff,
            NewFile: false, RenamedFile: false, DeletedFile: false);

    private static MergeRequestChange NonCsChange(string path = "README.md") =>
        new(OldPath: path, NewPath: path, Diff: "+# readme",
            NewFile: false, RenamedFile: false, DeletedFile: false);

    // ── Pass Scenario ──────────────────────────────────────────────────────────

    /// <summary>
    /// When no critical or high issues are found, the orchestrator should post a LGTM comment.
    /// </summary>
    [Fact]
    public async Task OrchestrateReviewAsync_WhenNoCriticalOrHighIssues_ApprovesAndMerges()
    {
        // Arrange
        var mrEvent = BuildMrEvent();
        var changes = BuildChanges(CsChange("src/OrderService.cs", "+public void Process() { }"));

        var reviewResult = new ReviewResult(
            Passed: true,
            Summary: "Code looks good",
            Issues: new List<CodeIssue>
            {
                new("src/OrderService.cs", 10, "low", "Naming", "Variable name could be better", "Use descriptive name")
            });

        _gitLabMock
            .Setup(g => g.GetMergeRequestChangesAsync(42, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(changes);

        _gitLabMock
            .Setup(g => g.GetFileContentAsync(42, "src/OrderService.cs", "feature/test", It.IsAny<CancellationToken>()))
            .ReturnsAsync("public class OrderService { }");

        _claudeMock
            .Setup(c => c.ReviewFileAsync(It.IsAny<FileReviewContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reviewResult);

        _gitLabMock
            .Setup(g => g.PostCommentAsync(42, 1, It.Is<string>(s => s.Contains("LGTM")), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _gitLabMock
            .Setup(g => g.ApproveMergeRequestAsync(42, 1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _gitLabMock
            .Setup(g => g.MergeMergeRequestAsync(42, 1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _orchestrator.OrchestrateReviewAsync(mrEvent);

        // Assert
        _gitLabMock.Verify(
            g => g.PostCommentAsync(42, 1, It.Is<string>(s => s.Contains("LGTM")), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Fail Scenario ──────────────────────────────────────────────────────────

    /// <summary>
    /// When critical or high issues are found, the orchestrator should post an issue
    /// table comment and must NOT approve or merge.
    /// </summary>
    [Fact]
    public async Task OrchestrateReviewAsync_WhenCriticalIssuesFound_PostsIssueTableAndSkipsMerge()
    {
        // Arrange
        var mrEvent = BuildMrEvent();
        var changes = BuildChanges(CsChange("src/AuthService.cs", "+var pwd = \"secret123\";"));

        var reviewResult = new ReviewResult(
            Passed: false,
            Summary: "Found hardcoded credentials",
            Issues: new List<CodeIssue>
            {
                new("src/AuthService.cs", 5, "critical", "Security",
                    "Hardcoded password detected", "Move to environment variable or secret store"),
                new("src/AuthService.cs", 12, "high", "C# Best Practices",
                    "Using .Result blocks async thread", "Use await instead of .Result")
            });

        _gitLabMock
            .Setup(g => g.GetMergeRequestChangesAsync(42, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(changes);

        _gitLabMock
            .Setup(g => g.GetFileContentAsync(42, "src/AuthService.cs", "feature/test", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _claudeMock
            .Setup(c => c.ReviewFileAsync(It.IsAny<FileReviewContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reviewResult);

        _gitLabMock
            .Setup(g => g.PostCommentAsync(42, 1,
                It.Is<string>(s => s.Contains("Issues found") || s.Contains("critical")),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _orchestrator.OrchestrateReviewAsync(mrEvent);

        // Assert: comment was posted with issue details
        _gitLabMock.Verify(
            g => g.PostCommentAsync(42, 1,
                It.Is<string>(s => s.Contains("critical") || s.Contains("Issues found")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert: approve and merge were NOT called
        _gitLabMock.Verify(
            g => g.ApproveMergeRequestAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _gitLabMock.Verify(
            g => g.MergeMergeRequestAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Skip Label Scenario ────────────────────────────────────────────────────

    /// <summary>
    /// When the MR has the 'skip-ai-review' label, the orchestrator should do nothing.
    /// </summary>
    [Fact]
    public async Task OrchestrateReviewAsync_WhenSkipLabelPresent_DoesNothing()
    {
        // Arrange
        var mrEvent = BuildMrEvent(labels: new List<GitLabLabel>
        {
            new GitLabLabel("skip-ai-review")
        });

        // Act
        await _orchestrator.OrchestrateReviewAsync(mrEvent);

        // Assert: no GitLab calls made
        _gitLabMock.Verify(
            g => g.GetMergeRequestChangesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Non-CS Files Scenario ─────────────────────────────────────────────────

    /// <summary>
    /// When the MR contains only non-C# files, no review should be performed.
    /// </summary>
    [Fact]
    public async Task OrchestrateReviewAsync_WhenNoCsFiles_SkipsReview()
    {
        // Arrange
        var mrEvent = BuildMrEvent();
        var changes = BuildChanges(NonCsChange("README.md"), NonCsChange("appsettings.json"));

        _gitLabMock
            .Setup(g => g.GetMergeRequestChangesAsync(42, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(changes);

        // Act
        await _orchestrator.OrchestrateReviewAsync(mrEvent);

        // Assert: Claude was never called
        _claudeMock.Verify(
            c => c.ReviewFileAsync(It.IsAny<FileReviewContext>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Assert: no comment, approve, or merge
        _gitLabMock.Verify(
            g => g.PostCommentAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
