using System.Text;
using CodeReviewAgent.Core.Models;

namespace CodeReviewAgent.Core.Prompts;

/// <summary>
/// Provides the system prompt used for C# code review by AI models.
/// The prompt is loaded from the embedded CSharpReviewPrompt.md resource file.
/// </summary>
public static class CSharpReviewPrompt
{
    /// <summary>
    /// The system prompt instructing the AI to perform a structured C# code review.
    /// Loaded at startup from the embedded Prompts/CSharpReviewPrompt.md resource.
    /// </summary>
    public static readonly string SystemPrompt;

    static CSharpReviewPrompt()
    {
        var assembly = typeof(CSharpReviewPrompt).Assembly;
        const string resourceName = "CodeReviewAgent.Core.Prompts.CSharpReviewPrompt.md";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        SystemPrompt = reader.ReadToEnd();
    }

    /// <summary>
    /// Builds the user message sent to the AI, incorporating all available context:
    /// MR metadata, file status, full file content, list of other changed files, and the diff.
    /// </summary>
    /// <param name="context">The rich review context for the file being reviewed.</param>
    /// <returns>A formatted user message for the AI API.</returns>
    public static string BuildUserMessage(FileReviewContext context)
    {
        var fileStatus = context.IsNewFile ? "NEW FILE"
            : context.IsRenamedFile ? $"RENAMED from `{context.OldPath}`"
            : "MODIFIED";

        var sb = new StringBuilder();

        sb.AppendLine("## Merge Request Context");
        sb.AppendLine($"- **Title**: {context.MrTitle}");
        sb.AppendLine($"- **Branch**: `{context.SourceBranch}` → `{context.TargetBranch}`");

        if (context.OtherChangedFiles.Count > 0)
        {
            var others = string.Join(", ", context.OtherChangedFiles.Select(f => $"`{f}`"));
            sb.AppendLine($"- **Other C# files changed in this MR**: {others}");
        }

        sb.AppendLine();
        sb.AppendLine($"## File: `{context.FilePath}` [{fileStatus}]");

        if (!string.IsNullOrWhiteSpace(context.FullContent))
        {
            sb.AppendLine();
            sb.AppendLine("### Full File Content (for reference):");
            sb.AppendLine("```csharp");
            sb.AppendLine(context.FullContent);
            sb.AppendLine("```");
        }

        sb.AppendLine();
        sb.AppendLine("### Changes (diff):");
        sb.AppendLine("```diff");
        sb.AppendLine(context.Diff);
        sb.AppendLine("```");

        return sb.ToString();
    }
}
