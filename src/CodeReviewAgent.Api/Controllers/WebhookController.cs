using CodeReviewAgent.Core.Models;
using CodeReviewAgent.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace CodeReviewAgent.Api.Controllers;

/// <summary>
/// Handles incoming GitLab webhook events for merge request automation.
/// </summary>
public static class WebhookController
{
    private static readonly HashSet<string> TriggerActions =
        new(StringComparer.OrdinalIgnoreCase) { "open", "update", "reopen" };

    /// <summary>
    /// Registers the webhook endpoint on the Minimal API route builder.
    /// </summary>
    /// <param name="app">The <see cref="WebApplication"/> to register routes on.</param>
    public static void MapWebhookEndpoints(this WebApplication app)
    {
        app.MapPost("/webhook/gitlab", HandleGitLabWebhookAsync)
           .WithName("GitLabWebhook")
           .WithTags("Webhook")
           .WithSummary("GitLab MR webhook receiver")
           .WithDescription("""
               Receives GitLab merge request webhook events.

               **Authentication:** Validated via the `X-Gitlab-Token` header.
               Must match the `GitLab:WebhookSecret` configuration value.

               **Triggered on MR actions:** `open`, `update`, `reopen`

               **Behaviour:**
               - Returns `200 OK` immediately (fire-and-forget).
               - Filters `.cs` files from the diff and sends each to Claude for review.
               - **PASS** (no critical/high issues) → posts LGTM comment, approves, and merges.
               - **FAIL** → posts a Markdown issue table; no merge.
               - If the MR has label `skip-ai-review`, the pipeline is skipped entirely.
               """)
           .Produces<WebhookResponse>(StatusCodes.Status200OK)
           .Produces<string>(StatusCodes.Status400BadRequest)
           .Produces<string>(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> HandleGitLabWebhookAsync(
        [FromBody] MergeRequestEvent? webhookEvent,
        IReviewOrchestrator orchestrator,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (webhookEvent is null)
        {
            logger.LogWarning("Received empty or invalid webhook payload");
            return Results.BadRequest("Invalid webhook payload.");
        }

        if (!string.Equals(webhookEvent.ObjectKind, "merge_request", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogDebug(
                "Ignoring non-merge-request event: {ObjectKind}", webhookEvent.ObjectKind);
            return Results.Ok(new WebhookResponse("Event ignored (not a merge_request)."));
        }

        var action = webhookEvent.ObjectAttributes?.Action ?? string.Empty;

        if (!TriggerActions.Contains(action))
        {
            logger.LogDebug(
                "Ignoring MR action '{Action}' for MR !{MrIid}",
                action, webhookEvent.ObjectAttributes?.Iid);
            return Results.Ok(new WebhookResponse($"Action '{action}' does not trigger review."));
        }

        logger.LogInformation(
            "Received GitLab webhook: MR !{MrIid} action='{Action}' project={ProjectId}",
            webhookEvent.ObjectAttributes!.Iid,
            action,
            webhookEvent.ObjectAttributes.SourceProjectId);

        // Fire-and-forget: respond immediately, process in background
        _ = Task.Run(async () =>
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            try
            {
                await orchestrator.OrchestrateReviewAsync(webhookEvent, cts.Token);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Unhandled error during review orchestration for MR !{MrIid}",
                    webhookEvent.ObjectAttributes.Iid);
            }
        }, CancellationToken.None);

        return Results.Ok(new WebhookResponse("Webhook received. Review started."));
    }
}

/// <summary>Standard response envelope returned by the webhook endpoint.</summary>
/// <param name="Message">Human-readable status message.</param>
public record WebhookResponse(string Message);
