using System.Text;
using System.Text.Json;
using CodeReviewAgent.Core.Models;

namespace CodeReviewAgent.Core.Prompts;

/// <summary>
/// Loads the four specialized review prompt files from embedded resources
/// and provides user message builders for each agent type.
/// </summary>
public static class SpecializedReviewPrompts
{
    public static readonly string ConventionSystemPrompt;
    public static readonly string PerformanceSystemPrompt;
    public static readonly string SecuritySystemPrompt;
    public static readonly string BaseSystemPrompt;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    static SpecializedReviewPrompts()
    {
        var outputFormat = LoadResource("CodeReviewAgent.Core.Prompts.PromptCSharp.output_format.md");

        ConventionSystemPrompt  = LoadResource("CodeReviewAgent.Core.Prompts.PromptCSharp.convention_review.md") + "\n\n" + outputFormat;
        PerformanceSystemPrompt = LoadResource("CodeReviewAgent.Core.Prompts.PromptCSharp.performance_review.md") + "\n\n" + outputFormat;
        SecuritySystemPrompt    = LoadResource("CodeReviewAgent.Core.Prompts.PromptCSharp.security_review.md") + "\n\n" + outputFormat;
        BaseSystemPrompt        = LoadResource("CodeReviewAgent.Core.Prompts.PromptCSharp.base_review.md") + "\n\n" + outputFormat;
    }

    private static string LoadResource(string resourceName)
    {
        var assembly = typeof(SpecializedReviewPrompts).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found. " +
                "Verify the file exists and is marked as EmbeddedResource in the .csproj.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Builds the user message for the 3 specialized agents (convention, performance, security).
    /// Identical structure to CSharpReviewPrompt.BuildUserMessage.
    /// </summary>
    public static string BuildSpecializedUserMessage(FileReviewContext context)
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

    /// <summary>
    /// Builds the user message for the BaseReviewService synthesis agent.
    /// Includes full file context followed by issues from all 3 specialized agents.
    /// </summary>
    public static string BuildBaseUserMessage(
        FileReviewContext context,
        List<SpecializedCodeIssue> conventionIssues,
        List<SpecializedCodeIssue> performanceIssues,
        List<SpecializedCodeIssue> securityIssues)
    {
        var sb = new StringBuilder();

        // Section 1: file context — agent reads code before seeing pre-flagged issues
        sb.Append(BuildSpecializedUserMessage(context));

        // Section 2: findings from the 3 specialized agents
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Specialized Agent Findings");
        sb.AppendLine();
        sb.AppendLine("The following JSON arrays were produced by three specialized review agents.");
        sb.AppendLine("Your task is to synthesize these into a final curated list:");
        sb.AppendLine("- Remove duplicates (same issue flagged by multiple agents)");
        sb.AppendLine("- Remove false positives based on your own analysis of the code above");
        sb.AppendLine("- Adjust severity/score if the combined evidence changes your confidence");
        sb.AppendLine("- Output ONLY issues with score >= 50");
        sb.AppendLine();

        AppendAgentSection(sb, "Convention Agent", conventionIssues);
        AppendAgentSection(sb, "Performance Agent", performanceIssues);
        AppendAgentSection(sb, "Security Agent", securityIssues);

        sb.AppendLine("Return the final curated JSON array. If no issues pass the threshold, return `[]`.");

        return sb.ToString();
    }

    private static void AppendAgentSection(
        StringBuilder sb,
        string agentName,
        List<SpecializedCodeIssue> issues)
    {
        sb.AppendLine($"### {agentName} Issues ({issues.Count} found):");
        sb.AppendLine("```json");
        sb.AppendLine(JsonSerializer.Serialize(issues, JsonOptions));
        sb.AppendLine("```");
        sb.AppendLine();
    }
}
