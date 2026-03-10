using CodeReviewAgent.Api.Controllers;
using CodeReviewAgent.Api.Middlewares;
using CodeReviewAgent.Core.Services;
using CodeReviewAgent.Infrastructure;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ─── Configuration ────────────────────────────────────────────────────────────
builder.Services.Configure<GitLabOptions>(
    builder.Configuration.GetSection("GitLab"));

builder.Services.Configure<AnthropicOptions>(
    builder.Configuration.GetSection("Anthropic"));

// ─── HTTP Clients ─────────────────────────────────────────────────────────────

// GitLab typed client
builder.Services.AddHttpClient<IGitLabService, GitLabApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<GitLabOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.DefaultRequestHeaders.Add("PRIVATE-TOKEN", options.PrivateToken);
    client.DefaultRequestHeaders.Add("User-Agent", "CodeReviewAgent/1.0");
    client.Timeout = TimeSpan.FromSeconds(60);
});

// Anthropic typed client
builder.Services.AddHttpClient<IAnthropicApiClient, AnthropicApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<AnthropicOptions>>().Value;
    client.BaseAddress = new Uri("https://api.anthropic.com");
    client.DefaultRequestHeaders.Add("x-api-key", options.ApiKey);
    client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    client.DefaultRequestHeaders.Add("User-Agent", "CodeReviewAgent/1.0");
    client.Timeout = TimeSpan.FromSeconds(120);
});

// ─── Application Services ────────────────────────────────────────────────────
builder.Services.AddScoped<IClaudeReviewService, ClaudeReviewService>();
builder.Services.AddScoped<IReviewOrchestrator, ReviewOrchestrator>();

// ─── Logging ─────────────────────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

if (builder.Environment.IsDevelopment())
{
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
}

// ─── OpenAPI / Swagger ────────────────────────────────────────────────────────
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info = new()
        {
            Title = "AI Code Review Agent",
            Version = "v1",
            Description = """
                Automated C# code review agent powered by Anthropic Claude.

                Receives GitLab merge request webhook events, reviews `.cs` diffs,
                and posts structured review comments. Auto-approves and merges
                if no critical/high issues are found.
                """,
            Contact = new() { Name = "Code Review Agent", Url = new Uri("https://github.com") }
        };
        return Task.CompletedTask;
    });
});

// ─── ASP.NET Core ────────────────────────────────────────────────────────────
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

var app = builder.Build();

// ─── OpenAPI & Scalar UI ──────────────────────────────────────────────────────
// Available only in Development & Staging for security
if (!app.Environment.IsProduction())
{
    // OpenAPI JSON document at: /openapi/v1.json
    app.MapOpenApi();

    // Scalar UI at: /scalar/v1
    app.MapScalarApiReference(options =>
    {
        options.Title = "AI Code Review Agent";
        options.Theme = ScalarTheme.DeepSpace;
        options.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
        options.WithPreferredScheme("Bearer");
    });
}

// ─── Middleware Pipeline ──────────────────────────────────────────────────────
app.UseMiddleware<GitLabTokenValidationMiddleware>();

// ─── Endpoints ────────────────────────────────────────────────────────────────
app.MapWebhookEndpoints();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTimeOffset.UtcNow,
    version = "1.0.0"
}))
.WithName("HealthCheck")
.WithSummary("Returns service health status")
.WithDescription("Liveness probe — returns 200 OK with service metadata.")
.WithTags("System")
.ExcludeFromDescription(); // hide from Scalar if desired — remove this line to show it

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
