# AI Code Review Agent

An AI-powered code review bot for GitLab merge requests, built with **.NET 10 / ASP.NET Core Minimal API**.

Supports three AI providers interchangeably: **Anthropic Claude**, **OpenAI ChatGPT**, and **Google Gemini**.

When a merge request is opened, updated, or reopened, the agent:
1. Fetches the MR diff and **full file content** from GitLab
2. Reviews every `.cs` file using the configured AI provider against a strict set of C# / DNU coding convention rules
3. **PASS** — posts a ✅ LGTM comment
4. **FAIL** — posts a structured Markdown table of critical/high issues and blocks the merge

---

## Project Structure

```
CodeReviewAgent/
├── src/
│   ├── CodeReviewAgent.Api/              # ASP.NET Core entry point
│   │   ├── Controllers/WebhookController.cs
│   │   ├── Middlewares/GitLabTokenValidationMiddleware.cs
│   │   └── Program.cs
│   │
│   ├── CodeReviewAgent.Core/             # Domain logic (no infrastructure deps)
│   │   ├── Services/
│   │   │   ├── ICodeReviewService.cs           ← base interface for all AI review services
│   │   │   ├── IClaudeReviewService.cs + ClaudeReviewService.cs
│   │   │   ├── IChatGptReviewService.cs + ChatGptReviewService.cs
│   │   │   ├── IGeminiReviewService.cs + GeminiReviewService.cs
│   │   │   ├── IReviewOrchestrator.cs + ReviewOrchestrator.cs
│   │   │   ├── IGitLabService.cs
│   │   │   ├── IAnthropicApiClient.cs
│   │   │   ├── IOpenAiApiClient.cs
│   │   │   └── IGeminiApiClient.cs
│   │   ├── Models/
│   │   │   ├── FileReviewContext.cs      ← rich per-file review context
│   │   │   ├── MergeRequestEvent.cs
│   │   │   ├── MergeRequestChange.cs
│   │   │   ├── ReviewResult.cs
│   │   │   └── CodeIssue.cs
│   │   └── Prompts/
│   │       ├── CSharpReviewPrompt.cs     ← loads prompt from embedded .md at startup
│   │       └── CSharpReviewPrompt.md     ← the actual review rules (edit this to customise)
│   │
│   └── CodeReviewAgent.Infrastructure/  # HTTP clients implementing Core interfaces
│       ├── GitLabApiClient.cs
│       ├── AnthropicApiClient.cs
│       ├── OpenAiApiClient.cs
│       └── GeminiApiClient.cs
│
├── tests/
│   └── CodeReviewAgent.Tests/
│       ├── ReviewOrchestratorTests.cs    # Pass, fail, skip-label, no-cs-files
│       └── ClaudeReviewServiceTests.cs  # Pass, fail, retry, API error, bad JSON
│
├── Dockerfile
├── docker-compose.yml
├── .env.example
└── README.md
```

---

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | 10.0+ | Required |
| Docker | 24+ | Optional |
| GitLab account | — | Personal Access Token with `api` scope |
| Anthropic API key | — | Required if using Claude provider |
| OpenAI API key | — | Required if using ChatGPT provider |
| Google AI Studio API key | — | Required if using Gemini provider |

---

## Quick Start

### 1. Clone & configure

```bash
git clone <your-repo-url>
cd CodeReviewAgent

cp .env.example .env
# Edit .env and fill in your secrets
```

### 2. Set required environment variables

| Variable | Description |
|----------|-------------|
| `GITLAB_BASE_URL` | Your GitLab instance URL (default: `https://gitlab.com`) |
| `GITLAB_PRIVATE_TOKEN` | GitLab Personal Access Token with `api` scope |
| `GITLAB_WEBHOOK_SECRET` | A random secret shared with the GitLab webhook |
| `ANTHROPIC_API_KEY` | Anthropic API key *(if using Claude)* |
| `OPENAI_API_KEY` | OpenAI API key *(if using ChatGPT)* |
| `GEMINI_API_KEY` | Google AI Studio API key *(if using Gemini)* |

### 3. Run locally with .NET CLI

```bash
dotnet restore

export GitLab__PrivateToken="glpat-xxxx"
export GitLab__WebhookSecret="my-secret"
export Anthropic__ApiKey="sk-ant-xxxx"   # or OpenAI__ApiKey / Gemini__ApiKey

dotnet run --project src/CodeReviewAgent.Api
# Listening on http://localhost:5000
```

### 4. Run with Docker Compose

```bash
docker compose up --build -d
# Available at http://localhost:8080
```

---

## AI Providers

Three providers are supported. All use the same review prompt and produce the same JSON output format.

| Provider | Default Model | Config Section |
|----------|--------------|----------------|
| **Anthropic Claude** | `claude-sonnet-4-20250514` | `Anthropic` |
| **OpenAI ChatGPT** | `gpt-4o` | `OpenAI` |
| **Google Gemini** | `gemini-2.0-flash` | `Gemini` |

### Switching providers

In `src/CodeReviewAgent.Api/Program.cs`, uncomment the desired line:

```csharp
// services.AddScoped<ICodeReviewService, ClaudeReviewService>();
// services.AddScoped<ICodeReviewService, ChatGptReviewService>();
   services.AddScoped<ICodeReviewService, GeminiReviewService>();  // ← active
```

---

## Context-Aware Review

Each file review now sends rich context to the AI, not just the diff:

| Context | Source | Description |
|---------|--------|-------------|
| **MR Title** | Webhook payload | Intent of the merge request |
| **Branch** | Webhook payload | `feature/xxx` → `main` — helps assess risk level |
| **File Status** | MR changes API | `NEW FILE`, `MODIFIED`, or `RENAMED from <path>` |
| **Full File Content** | GitLab Repository API | Complete source for accurate rule checking (method length, dependency count, etc.) |
| **Other Changed Files** | MR changes API | Paths of other `.cs` files in the same MR — cross-file awareness |

The `FileReviewContext` record (`src/CodeReviewAgent.Core/Models/FileReviewContext.cs`) carries all of the above per review call.

> If the full file content cannot be fetched (e.g. permissions, network error), the review falls back to diff-only mode gracefully — it never blocks the pipeline.

---

## Customizing the Review Prompt

The system prompt is stored as a standalone Markdown file:

```
src/CodeReviewAgent.Core/Prompts/CSharpReviewPrompt.md
```

**To change review rules**, simply edit `CSharpReviewPrompt.md`. The file is compiled into the assembly as an embedded resource and loaded at startup — all three AI providers pick up the change automatically on the next deploy.

No C# code changes are needed to modify rules.

---

## Review Rules

Based on DNU Coding Convention v1.0. The AI evaluates all 11 categories:

| # | Category | Key Rules |
|---|----------|-----------|
| 1 | **Naming** | PascalCase classes/methods, `_camelCase` private fields, `I` prefix interfaces, `Async` suffix, `Is/Can/Has` bool prefix, no magic numbers/strings |
| 2 | **Layout & Formatting** | 4-space indent, Allman braces, max 120 chars/line, one class per file |
| 3 | **Classes & Methods** | Methods ≤ 30 lines, ≤ 3 parameters (use DTO/record otherwise), `sealed` for non-inherited classes |
| 4 | **Properties & Fields** | `_camelCase` private fields, prefer auto-properties, `readonly`/`init` for immutable, `const`/`static readonly` instead of magic values |
| 5 | **Error Handling** | No empty catch, no `throw ex` (use `throw`), always check nullable, custom exception classes |
| 6 | **Async & Await** | `Async` suffix, `ConfigureAwait(false)` in library code, no `.Result`/`.Wait()`, no `async void`, `CancellationToken` for long calls |
| 7 | **LINQ & Collections** | Method syntax preferred, no nested LINQ, `var` only when type is obvious, `StringBuilder` in loops |
| 8 | **DI & SOLID** | Constructor injection, ≤ 4 dependencies per class, all 5 SOLID principles enforced |
| 9 | **Code Quality** | Cyclomatic complexity ≤ 10, no dead code, no commented-out blocks, `using` for `IDisposable` |
| 10 | **Comments & Docs** | Comment "why" not "what", XML `///` docs for all public APIs |
| 11 | **Unit Tests** | `MethodName_State_Expected` naming, AAA pattern |

---

## Decision Logic

| Condition | Action |
|-----------|--------|
| Zero `critical` or `high` issues | Post ✅ LGTM comment |
| Any `critical` or `high` issue | Post ❌ issue table, block merge |
| AI API failure on any file | Post ⚠️ error comment, block merge as precaution |

---

## Bypass

Add the label **`skip-ai-review`** to any MR to skip the automated review entirely.

---

## GitLab Webhook Setup

1. In your GitLab project, go to **Settings → Webhooks**
2. Set the URL: `https://your-domain.com/webhook/gitlab`
3. Set the **Secret Token** to match `GITLAB_WEBHOOK_SECRET`
4. Enable **Merge request events**
5. Click **Add webhook**

The agent responds immediately with `200 OK` and processes the review asynchronously.

---

## Running Tests

```bash
dotnet test tests/CodeReviewAgent.Tests
```

Tests cover:
- `ReviewOrchestratorTests` — pass scenario, fail scenario, skip label, no C# files
- `ClaudeReviewServiceTests` — pass result, fail with critical issues, retry on transient error, all-attempts-fail, malformed JSON

---

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/webhook/gitlab` | GitLab MR webhook receiver |
| `GET` | `/health` | Health check |

---

## Security Notes

- The `X-Gitlab-Token` header is validated by `GitLabTokenValidationMiddleware` before any request reaches the endpoint.
- All secrets must be provided as environment variables — never commit `.env` or `appsettings.Production.json`.
- The Docker container runs as a non-root user (`appuser`).

---

## Configuration Reference

```json
{
  "GitLab": {
    "BaseUrl": "https://gitlab.com",
    "PrivateToken": "",        // override via GitLab__PrivateToken env var
    "WebhookSecret": ""        // override via GitLab__WebhookSecret env var
  },
  "Anthropic": {
    "ApiKey": "",              // override via Anthropic__ApiKey env var
    "Model": "claude-sonnet-4-20250514",
    "MaxTokens": 4096
  },
  "OpenAI": {
    "ApiKey": "",              // override via OpenAI__ApiKey env var
    "Model": "gpt-4o",
    "MaxTokens": 4096
  },
  "Gemini": {
    "ApiKey": "",              // override via Gemini__ApiKey env var
    "Model": "gemini-2.0-flash",
    "MaxTokens": 4096
  }
}
```

---

## License

MIT
