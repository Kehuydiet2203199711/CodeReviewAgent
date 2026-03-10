using CodeReviewAgent.Infrastructure;
using Microsoft.Extensions.Options;

namespace CodeReviewAgent.Api.Middlewares;

/// <summary>
/// Middleware that validates the <c>X-Gitlab-Token</c> header on all requests
/// to the webhook endpoint, rejecting unauthorized calls with HTTP 401.
/// </summary>
public sealed class GitLabTokenValidationMiddleware
{
    private const string GitLabTokenHeader = "X-Gitlab-Token";
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of <see cref="GitLabTokenValidationMiddleware"/>.
    /// </summary>
    public GitLabTokenValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Invokes the middleware. Validates the GitLab token header for webhook paths.
    /// </summary>
    public async Task InvokeAsync(HttpContext context, IOptions<GitLabOptions> options)
    {
        // Only validate on the webhook path
        if (context.Request.Path.StartsWithSegments("/webhook"))
        {
            var expectedToken = options.Value.WebhookSecret;

            if (string.IsNullOrWhiteSpace(expectedToken))
            {
                // Log a warning but allow through if no secret is configured (dev mode)
                var logger = context.RequestServices
                    .GetRequiredService<ILogger<GitLabTokenValidationMiddleware>>();
                logger.LogWarning(
                    "GitLab webhook secret is not configured — token validation is disabled");
            }
            else
            {
                if (!context.Request.Headers.TryGetValue(GitLabTokenHeader, out var tokenValue) ||
                    !string.Equals(tokenValue, expectedToken, StringComparison.Ordinal))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Unauthorized: Invalid X-Gitlab-Token");
                    return;
                }
            }
        }

        await _next(context);
    }
}
