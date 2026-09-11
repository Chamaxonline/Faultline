# Faultline

In-house error tracking service — an internal alternative to Sentry for Bistec's
internal apps, scoped to error/exception tracking only (no perf tracing, logs, or
uptime monitoring).

See [docs/adr/0001-faultline-architecture.md](docs/adr/0001-faultline-architecture.md)
for the architecture decision record.

## Projects

| Project | Purpose |
|---|---|
| `src/Faultline.Domain` | Entities (Organization, Project, Issue, Event), fingerprinting |
| `src/Faultline.Infrastructure` | EF Core (Postgres) DbContext, Redis Streams queue |
| `src/Faultline.Api` | Ingestion API — `POST /api/v1/{projectKey}/store` |
| `src/Faultline.Worker` | Consumes queue, dedupes/groups events into Issues |
| `src/Faultline.Sdk` | .NET client SDK for apps to report errors |
| `src/Faultline.Tests` | xUnit tests |
| `dashboard/` | Next.js dashboard (issue list, detail, resolve/ignore) |

## Local dev

```bash
docker compose up -d              # Postgres + Redis
dotnet ef database update \
  --project src/Faultline.Infrastructure \
  --startup-project src/Faultline.Api
dotnet run --project src/Faultline.Api      # ingestion API
dotnet run --project src/Faultline.Worker   # grouping worker
cd dashboard && npm run dev                 # dashboard UI
```

## Using the SDK in another app

```csharp
services.AddFaultline(opts =>
{
    opts.ServerUrl = "https://faultline.internal.bistec.com";
    opts.ProjectKey = "<project public key from dashboard>";
    opts.Environment = "production";
});

// in Program.cs, after building the host:
app.Services.UseFaultlineUnhandledExceptionCapture();
```

## Alerting

Set `Alerts:TeamsWebhookUrl` in `src/Faultline.Worker/appsettings.json` (or an env
var override) to get a Teams card on every new issue and every regression (an event
on a previously-resolved issue). Leave it blank to disable — the worker no-ops
silently. Point it at a Power Automate "When a Teams webhook request is received"
flow, since Teams retired the legacy Incoming Webhook connector.

## Issue search/filter

`GET /api/v1/projects/{projectId}/issues` supports `q` (title search), `status`,
`environment`, `release`, `sort` (`lastSeen` | `firstSeen` | `count`), `page`,
`pageSize` (max 100). The dashboard's project page exposes all of these as a filter
bar + pagination.

## MVP scope
Ingestion + dedupe/grouping + search/filter/pagination + Teams alerts + basic
dashboard + .NET SDK.
Not yet built: Entra ID auth on the dashboard, source map support, perf tracing,
uptime checks, log aggregation, non-.NET SDKs, Hetzner deployment.
