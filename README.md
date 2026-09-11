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

## MVP scope
Ingestion + dedupe/grouping + basic dashboard + .NET SDK + Teams alert on new issue.
Not in v1: perf tracing, uptime checks, log aggregation, non-.NET SDKs.
