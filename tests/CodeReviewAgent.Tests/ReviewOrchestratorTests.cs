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
    private readonly Mock<IConventionReviewService> _conventionMock;
    private readonly Mock<IPerformanceReviewService> _performanceMock;
    private readonly Mock<ISecurityReviewService> _securityMock;
    private readonly Mock<IBaseReviewService> _baseMock;
    private readonly ILogger<ReviewOrchestrator> _logger;
    private readonly ReviewOrchestrator _orchestrator;

    public ReviewOrchestratorTests()
    {
        _gitLabMock = new Mock<IGitLabService>(MockBehavior.Strict);
        _conventionMock = new Mock<IConventionReviewService>(MockBehavior.Strict);
        _performanceMock = new Mock<IPerformanceReviewService>(MockBehavior.Strict);
        _securityMock = new Mock<ISecurityReviewService>(MockBehavior.Strict);
        _baseMock = new Mock<IBaseReviewService>(MockBehavior.Strict);
        _logger = new LoggerFactory().CreateLogger<ReviewOrchestrator>();
        _orchestrator = new ReviewOrchestrator(
            _gitLabMock.Object,
            _conventionMock.Object,
            _performanceMock.Object,
            _securityMock.Object,
            _baseMock.Object,
            _logger);
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

    private static SpecializedCodeIssue BuildIssue(string severity, string file = "src/OrderService.cs") =>
        new(IssueId: Guid.NewGuid().ToString(),
            File: file,
            LineStart: 10,
            LineEnd: 10,
            Severity: severity,
            Category: "convention",
            Title: "Test issue",
            Description: "A test issue description",
            Reasoning: new IssueReasoning("reason", "verified", "no fp"),
            Suggestion: "Fix it",
            Score: 75);

    private void SetupSpecializedAgents(string filePath,
        List<SpecializedCodeIssue>? conventionResult = null,
        List<SpecializedCodeIssue>? performanceResult = null,
        List<SpecializedCodeIssue>? securityResult = null)
    {
        _conventionMock
            .Setup(s => s.ReviewAsync(It.IsAny<FileReviewContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(conventionResult ?? []);
        _performanceMock
            .Setup(s => s.ReviewAsync(It.IsAny<FileReviewContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(performanceResult ?? []);
        _securityMock
            .Setup(s => s.ReviewAsync(It.IsAny<FileReviewContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(securityResult ?? []);
    }

    // ── Pass Scenario ──────────────────────────────────────────────────────────

    /// <summary>
    /// When no critical or high issues are found, the orchestrator should post a LGTM comment.
    /// </summary>
    [Fact]
    public async Task OrchestrateReviewAsync_WhenNoCriticalOrHighIssues_PostsLgtm()
    {
        // Arrange
        var mrEvent = BuildMrEvent();
        var changes = BuildChanges(CsChange("src/OrderService.cs", "+public void Process() { }"));

        var mediumIssue = BuildIssue("MEDIUM", "src/OrderService.cs");
        var synthesizedIssues = new List<SpecializedCodeIssue> { mediumIssue };

        _gitLabMock
            .Setup(g => g.GetMergeRequestChangesAsync(42, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(changes);
        _gitLabMock
            .Setup(g => g.GetFileContentAsync(42, "src/OrderService.cs", "feature/test", It.IsAny<CancellationToken>()))
            .ReturnsAsync("public class OrderService { }");

        SetupSpecializedAgents("src/OrderService.cs");

        _baseMock
            .Setup(b => b.SynthesizeAsync(It.IsAny<FileReviewContext>(),
                It.IsAny<List<SpecializedCodeIssue>>(),
                It.IsAny<List<SpecializedCodeIssue>>(),
                It.IsAny<List<SpecializedCodeIssue>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(synthesizedIssues);

        _gitLabMock
            .Setup(g => g.PostCommentAsync(42, 1, It.Is<string>(s => s.Contains("LGTM")), It.IsAny<CancellationToken>()))
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
    /// When critical issues are found, the orchestrator should post an issue table comment.
    /// </summary>
    [Fact]
    public async Task OrchestrateReviewAsync_WhenCriticalIssuesFound_PostsIssueTable()
    {
        // Arrange
        var mrEvent = BuildMrEvent();
        var changes = BuildChanges(CsChange("src/AuthService.cs", "+var pwd = \"secret123\";"));

        var criticalIssue = BuildIssue("CRITICAL", "src/AuthService.cs") with
        {
            Title = "Hardcoded password detected",
            Category = "security"
        };
        var synthesizedIssues = new List<SpecializedCodeIssue> { criticalIssue };

        _gitLabMock
            .Setup(g => g.GetMergeRequestChangesAsync(42, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(changes);
        _gitLabMock
            .Setup(g => g.GetFileContentAsync(42, "src/AuthService.cs", "feature/test", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        SetupSpecializedAgents("src/AuthService.cs");

        _baseMock
            .Setup(b => b.SynthesizeAsync(It.IsAny<FileReviewContext>(),
                It.IsAny<List<SpecializedCodeIssue>>(),
                It.IsAny<List<SpecializedCodeIssue>>(),
                It.IsAny<List<SpecializedCodeIssue>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(synthesizedIssues);

        _gitLabMock
            .Setup(g => g.PostCommentAsync(42, 1,
                It.Is<string>(s => s.Contains("Issues found") || s.Contains("CRITICAL")),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _orchestrator.OrchestrateReviewAsync(mrEvent);

        // Assert: comment was posted with issue details
        _gitLabMock.Verify(
            g => g.PostCommentAsync(42, 1,
                It.Is<string>(s => s.Contains("CRITICAL") || s.Contains("Issues found")),
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

        // Assert: specialized agents were never called
        _conventionMock.Verify(
            c => c.ReviewAsync(It.IsAny<FileReviewContext>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Assert: no comment posted
        _gitLabMock.Verify(
            g => g.PostCommentAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
