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

// ASP.NET Core apps: adds route/method/correlation-id tags to every error from a request
app.UseFaultlineScope();

// after building the host:
app.Services.UseFaultlineUnhandledExceptionCapture();
```

### Scope & breadcrumbs

Every captured error picks up whatever's on the ambient `FaultlineScope` — tags, user,
and the last 50 breadcrumbs — so issues show what led up to them, not just the
exception itself.

```csharp
FaultlineScope.SetTag("tenant", tenantId);
FaultlineScope.SetUser(currentUser.Email);
FaultlineScope.AddBreadcrumb("started checkout", category: "flow");

// scope it to a block (e.g. a background job) instead of the whole request:
using (FaultlineScope.Push())
{
    FaultlineScope.SetTag("job", "nightly-sync");
    // ...
}
```

Wire ordinary `ILogger` calls in as breadcrumbs automatically (Info level and above):

```csharp
builder.Logging.AddFaultlineBreadcrumbs();
```

### Data scrubbing

Every event is scrubbed **before** it leaves the process — this isn't a dashboard
display filter, the data is never sent. Tag/extra keys matching
`opts.ScrubFieldNames` (defaults: `password`, `token`, `authorization`,
`connectionstring`, `secret`, `apikey`) get their value replaced with
`[Filtered]`. Add your own regex patterns to catch anything in free text
(message, stack trace, user context, breadcrumbs):

```csharp
services.AddFaultline(opts =>
{
    // ...
    opts.ScrubFieldNames.Add("ssn");
    opts.ScrubPatterns.Add(@"\b\d{16}\b"); // card numbers
});
```

The SDK never captures raw HTTP request/response bodies on its own — only what
you explicitly put in tags, extra, breadcrumbs, or the exception itself.

### Stack trace quality

Frames are marked in-app vs library by matching the declaring type's assembly
against `opts.InAppAssemblyPrefixes` (defaults to your entry assembly — add more
if your app spans several projects). When the source file is available on disk
(true in dev, and on any deployment where source ships alongside binaries),
`ContextLineCount` (default 3) lines before/after the failing line are captured
too — set it to `0` to disable.

### Reliability

A send that fails (API unreachable, non-2xx) is dropped and counted by default —
counts are logged as a periodic warning (`ClientReportInterval`, default 5 min) so
data loss shows up in your own app's logs instead of vanishing silently. To ride
out short outages instead of dropping, set `OfflineQueueDirectory` — failed events
buffer to disk (capped at `OfflineQueueMaxFiles`, oldest evicted first) and retry
on `OfflineQueueRetryInterval` (default 30s):

```csharp
services.AddFaultline(opts =>
{
    // ...
    opts.OfflineQueueDirectory = Path.Combine(Path.GetTempPath(), "faultline-queue");
});
```

Both the retry loop and the report logger run as hosted services — they need the
generic host running (`app.Run()` / `host.Run()`), same as any ASP.NET Core or
Worker Service app already does.

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

## Deployment

See [deploy/README.md](deploy/README.md) for two paths:
- **Free tier**: Vercel (dashboard) + Render (Api+worker merged into one process,
  since Render's free tier has no standalone background-worker option) + Neon
  (Postgres) + Upstash (Redis) — no credit card needed on any of them, cold starts
  on the free instances.
- **Hetzner VM**: Terraform-provisioned, production docker-compose + Caddy for TLS,
  GitHub Actions CI/CD. The deploy step is gated behind repo secrets/variables
  that aren't set yet, so pushing to `master` builds and pushes images to GHCR
  but doesn't touch any live server until you configure those.

## MVP scope
Ingestion + dedupe/grouping + search/filter/pagination + Teams alerts + basic
dashboard + .NET SDK.
Not yet built: Entra ID auth on the dashboard, source map support, perf tracing,
uptime checks, log aggregation, non-.NET SDKs, Hetzner deployment.
