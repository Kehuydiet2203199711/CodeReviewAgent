using System.Text.Json;
using CodeReviewAgent.Core.Models;
using CodeReviewAgent.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CodeReviewAgent.Tests;

/// <summary>
/// Unit tests for <see cref="ClaudeReviewService"/> covering pass, fail, and error scenarios.
/// </summary>
public sealed class ClaudeReviewServiceTests
{
    private readonly Mock<IAnthropicApiClient> _anthropicMock;
    private readonly ILogger<ClaudeReviewService> _logger;
    private readonly ClaudeReviewService _service;

    public ClaudeReviewServiceTests()
    {
        _anthropicMock = new Mock<IAnthropicApiClient>(MockBehavior.Strict);
        _logger = new LoggerFactory().CreateLogger<ClaudeReviewService>();
        _service = new ClaudeReviewService(_anthropicMock.Object, _logger);
    }

    // ── Pass Scenario ──────────────────────────────────────────────────────────

    /// <summary>
    /// When the Claude API returns a valid JSON with passed=true and no issues,
    /// the service should return a ReviewResult with Passed=true.
    /// </summary>
    [Fact]
    public async Task ReviewFileAsync_WhenClaudeReturnsPass_ReturnsPassedResult()
    {
        // Arrange
        var expectedJson = JsonSerializer.Serialize(new
        {
            passed = true,
            summary = "Code follows all C# best practices",
            issues = Array.Empty<object>()
        });

        _anthropicMock
            .Setup(a => a.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedJson);

        // Act
        var result = await _service.ReviewFileAsync(
            BuildContext("src/Foo.cs", "+public void Bar() { }"));

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Passed);
        Assert.Equal("Code follows all C# best practices", result.Summary);
        Assert.Empty(result.Issues);
    }

    // ── Fail Scenario ──────────────────────────────────────────────────────────

    /// <summary>
    /// When Claude returns a JSON with passed=false and critical issues,
    /// the service should return a ReviewResult reflecting those issues.
    /// </summary>
    [Fact]
    public async Task ReviewFileAsync_WhenClaudeReturnsCriticalIssues_ReturnsFailedResult()
    {
        // Arrange
        var expectedJson = JsonSerializer.Serialize(new
        {
            passed = false,
            summary = "Found security vulnerability",
            issues = new[]
            {
                new
                {
                    file = "src/Auth.cs",
                    line = 15,
                    severity = "critical",
                    rule = "Security",
                    message = "Hardcoded API key detected",
                    suggestion = "Use environment variable or secret manager"
                }
            }
        });

        _anthropicMock
            .Setup(a => a.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedJson);

        // Act
        var result = await _service.ReviewFileAsync(
            BuildContext("src/Auth.cs", "+var key = \"abc123\";"));

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Passed);
        Assert.Equal("Found security vulnerability", result.Summary);
        Assert.Single(result.Issues);

        var issue = result.Issues[0];
        Assert.Equal("src/Auth.cs", issue.File);
        Assert.Equal(15, issue.Line);
        Assert.Equal("critical", issue.Severity);
        Assert.Equal("Security", issue.Rule);
    }

    // ── Retry Scenario ─────────────────────────────────────────────────────────

    /// <summary>
    /// When the first API call throws a transient exception, the service retries once
    /// and returns the result from the second successful call.
    /// </summary>
    [Fact]
    public async Task ReviewFileAsync_WhenFirstCallFails_RetriesAndReturnsResult()
    {
        // Arrange
        var successJson = JsonSerializer.Serialize(new
        {
            passed = true,
            summary = "Retry succeeded",
            issues = Array.Empty<object>()
        });

        var callCount = 0;
        _anthropicMock
            .Setup(a => a.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new HttpRequestException("Transient network error");
                return successJson;
            });

        // Act
        var result = await _service.ReviewFileAsync(
            BuildContext("src/Retry.cs", "+int x = 42;"));

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Passed);
        Assert.Equal(2, callCount);
    }

    // ── API Failure Scenario ───────────────────────────────────────────────────

    /// <summary>
    /// When the API fails on both attempts, the service should return null and not throw.
    /// </summary>
    [Fact]
    public async Task ReviewFileAsync_WhenAllAttemptsFail_ReturnsNull()
    {
        // Arrange
        _anthropicMock
            .Setup(a => a.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        // Act
        var result = await _service.ReviewFileAsync(
            BuildContext("src/Broken.cs", "+var x = 1;"));

        // Assert
        Assert.Null(result);
    }

    // ── Invalid JSON Scenario ──────────────────────────────────────────────────

    /// <summary>
    /// When Claude returns malformed JSON, the service should return null without retrying.
    /// </summary>
    [Fact]
    public async Task ReviewFileAsync_WhenClaudeReturnsMalformedJson_ReturnsNull()
    {
        // Arrange
        _anthropicMock
            .Setup(a => a.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("This is not JSON at all");

        // Act
        var result = await _service.ReviewFileAsync(
            BuildContext("src/Bad.cs", "+int y = 0;"));

        // Assert
        Assert.Null(result);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static FileReviewContext BuildContext(string filePath, string diff) =>
        new(
            FilePath: filePath,
            Diff: diff,
            MrTitle: "Test MR",
            SourceBranch: "feature/test",
            TargetBranch: "main",
            IsNewFile: false,
            IsRenamedFile: false,
            OldPath: null,
            FullContent: null,
            OtherChangedFiles: []
        );
}
