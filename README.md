# CharlesSchwabAssessment

A .NET 10 Blazor application that combines a URL shortener with a lightweight workflow orchestration API.

## Features

- Shorten HTTP/HTTPS URLs
- Optional custom aliases and expiration
- Redirect short URLs and track analytics
- Start, resume, stop, and replan workflow runs
- Human approval flow for high-impact workflow steps
- Policy guardrail support for workflow execution
- In-memory runtime storage for URLs, approvals, and workflow state

## Tech Stack

- .NET 10
- ASP.NET Core
- Blazor Interactive Server Components
- Minimal APIs

## Run

```bash
dotnet restore
dotnet run
```

## Main Endpoints

### URL Shortener
- `POST /api/urls` - create a short URL
- `GET /u/{code}` - redirect to the original URL
- `GET /api/urls/{code}/analytics` - get redirect analytics

### Workflow Orchestration
- `POST /api/orchestration/runs` - start a workflow
- `POST /api/orchestration/runs/{workflowId}/resume` - resume a workflow
- `POST /api/orchestration/runs/{workflowId}/safe-stop` - request safe stop
- `POST /api/orchestration/runs/{workflowId}/replan` - replan from a node
- `GET /api/orchestration/runs` - list workflow runs
- `GET /api/orchestration/runs/{workflowId}` - get workflow snapshot
- `GET /api/orchestration/approvals` - list pending approvals
- `POST /api/orchestration/approvals/approve` - approve a request

## Notes

- Data is stored in memory only
- All runtime data is lost when the app restarts
- The Blazor UI shell exists, while core business functionality is exposed through APIs
