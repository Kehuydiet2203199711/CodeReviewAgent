# AI Code Review Agent

An AI-powered code review bot for GitLab merge requests, built with **.NET 10 / ASP.NET Core Minimal API** and powered by **Anthropic Claude** (`claude-sonnet-4-20250514`).

When a merge request is opened, updated, or reopened, the agent:
1. Fetches the MR diff from GitLab
2. Reviews every `.cs` file using Claude AI against a strict set of C# rules
3. **PASS** — posts a ✅ LGTM comment, approves, and auto-merges
4. **FAIL** — posts a structured Markdown table of critical/high issues and blocks the merge

---

## Project Structure

```
CodeReviewAgent/
├── src/
│   ├── CodeReviewAgent.Api/              # ASP.NET Core entry point
│   │   ├── Controllers/WebhookController.cs
│   │   ├── Middlewares/GitLabTokenValidationMiddleware.cs
│   │   ├── Program.cs
│   │   └── appsettings.json
│   │
│   ├── CodeReviewAgent.Core/             # Domain logic (no infrastructure deps)
│   │   ├── Services/
│   │   │   ├── IGitLabService.cs + ReviewOrchestrator.cs
│   │   │   ├── IClaudeReviewService.cs + ClaudeReviewService.cs
│   │   │   ├── IReviewOrchestrator.cs
│   │   │   └── IAnthropicApiClient.cs
│   │   ├── Models/
│   │   │   ├── MergeRequestEvent.cs
│   │   │   ├── MergeRequestChange.cs
│   │   │   ├── ReviewResult.cs
│   │   │   └── CodeIssue.cs
│   │   └── Prompts/CSharpReviewPrompt.cs
│   │
│   └── CodeReviewAgent.Infrastructure/  # HTTP clients implementing Core interfaces
│       ├── GitLabApiClient.cs
│       └── AnthropicApiClient.cs
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

| Tool | Version |
|------|---------|
| .NET SDK | 10.0+ |
| Docker | 24+ (optional) |
| GitLab account | with Personal Access Token |
| Anthropic API key | [console.anthropic.com](https://console.anthropic.com) |

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
| `ANTHROPIC_API_KEY` | Your Anthropic API key |

### 3. Run locally with .NET CLI

```bash
# Restore dependencies
dotnet restore

# Set secrets as environment variables (or use dotnet user-secrets)
export GitLab__PrivateToken="glpat-xxxx"
export GitLab__WebhookSecret="my-secret"
export Anthropic__ApiKey="sk-ant-xxxx"

# Run the API
dotnet run --project src/CodeReviewAgent.Api
# Listening on http://localhost:5000
```

### 4. Run with Docker Compose

```bash
# Build and start
docker compose up --build

# Or detached
docker compose up -d --build
```

The service will be available at `http://localhost:8080`.

---

## GitLab Webhook Setup

1. In your GitLab project, go to **Settings → Webhooks**
2. Set the URL: `https://your-domain.com/webhook/gitlab`
3. Set the **Secret Token** to match `GITLAB_WEBHOOK_SECRET`
4. Enable **Merge request events**
5. Click **Add webhook**

The agent responds immediately with `200 OK` and processes the review asynchronously.

---

## Review Rules

The agent evaluates C# code against:

| Category | Rules |
|----------|-------|
| **Naming** | PascalCase for classes/methods/properties, camelCase for variables, interfaces start with `I`, no magic numbers/strings |
| **Code Quality** | Methods ≤ 50 lines, cyclomatic complexity ≤ 10, no commented-out code, no dead code |
| **C# Best Practices** | `async/await` only (no `.Result`/`.Wait()`), `using` for `IDisposable`, no bare `catch (Exception)`, prefer `?.` and `??` |
| **Security** | No hardcoded secrets, input validation, no logging of sensitive data |
| **Performance** | `StringBuilder` in loops, careful LINQ usage |

---

## Decision Logic

| Condition | Action |
|-----------|--------|
| Zero `critical` or `high` issues | Post LGTM ✅ → Approve → Merge |
| Any `critical` or `high` issue | Post issue table ❌ → No merge |

---

## Bypass

Add the label **`skip-ai-review`** to any MR to skip the automated review entirely.

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
    "PrivateToken": "",       // override via GitLab__PrivateToken env var
    "WebhookSecret": ""       // override via GitLab__WebhookSecret env var
  },
  "Anthropic": {
    "ApiKey": "",             // override via Anthropic__ApiKey env var
    "Model": "claude-sonnet-4-20250514",
    "MaxTokens": 2000
  },
  "ReviewPolicy": {
    "BlockOnSeverities": ["critical", "high"],
    "MaxMediumIssues": 5
  }
}
```

---

## License

MIT
