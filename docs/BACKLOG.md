# Faultline backlog

Derived from a pass over [Sentry's SDK development docs](https://develop.sentry.dev/sdk/) —
borrowing the architectural ideas that matter for an error-tracker, skipping the
ones that only matter for full APM (see "Explicitly out of scope" below, per
[ADR-0001](adr/0001-faultline-architecture.md)).

Priority: **P0** = do next, **P1** = do soon, **P2** = nice to have, later.

## Epic: SDK scope & breadcrumbs (P0)

Sentry's SDKs are built around a **Client → Scope → Hub** model: a Scope carries
tags, user context, and a breadcrumb trail that gets attached to whatever error
fires next. Faultline's SDK currently only does `CaptureExceptionAsync(ex)` with
no context — every issue looks the same regardless of what led up to it.

- [x] `FaultlineScope`: settable tags (`Dictionary<string,string>`), user context,
      "extra" free-form data — ambient (AsyncLocal-based) so it flows through async
      call chains
- [x] Breadcrumb trail: manual `AddBreadcrumb(message, category, level)` API, capped
      ring buffer (last ~50), attached to the event on capture
- [x] ASP.NET Core middleware: push a per-request scope (route, method, correlation
      ID) so errors show which request they came from
- [x] `ILogger` provider integration: `LogWarning`/`LogError` calls become
      breadcrumbs automatically, not just unhandled exceptions

## Epic: Envelope wire format — reconsidered, deferred

Sentry's envelope batches event + breadcrumbs + context into one send because its
SDKs emit many small standalone telemetry items (sessions, standalone breadcrumbs,
attachments). Faultline's `ErrorEvent` already carries breadcrumbs/tags/extra
inline in one JSON POST per captured error — there's no second item type to batch
with it yet, and attachments/sessions are explicitly out of scope (see below). A
real envelope format would add wire-protocol complexity and an Api migration path
for no current benefit. Revisit only if something genuinely needs multi-item
batching (e.g. attachments, if that scope ever changes).

## Epic: Data scrubbing (P0 — security-relevant)

Sentry SDKs scrub sensitive fields **before** the event leaves the process. Faultline
has none of this today — anything an app puts in `UserContext`, tags, or a captured
exception's message goes over the wire and into Postgres as-is.

- [x] Default scrub list: fields named `password`, `token`, `authorization`,
      `connectionstring`, `secret`, `apikey` (case-insensitive) get replaced with
      `[Filtered]` before serialization
- [x] Configurable scrub rules (regex or field-name list) via `FaultlineOptions`
- [x] Document that the SDK never captures raw HTTP request/response bodies by
      default (opt-in only)

## Epic: Stack trace quality (P1)

- [x] Mark frames as in-app vs library (namespace prefix match against the app's
      own assembly names) — dashboard can collapse/dim library frames
- [x] Source context: a few lines around the failing line, read from PDB sequence
      points when available (falls back gracefully without them)

## Epic: SDK reliability / self-reporting (P2)

- [x] Client reports: SDK tracks counts of dropped events (queue full, send failed
      after retries) and periodically reports them — surfaces silent data loss
      (logged via ILogger, not sent to the server — see README)
- [x] Local disk buffering: if the ingestion API is unreachable, queue events to
      disk and retry on a timer instead of dropping after 3 attempts (opt-in via
      `OfflineQueueDirectory`)

## Epic: Auth (P0 — blocked on you)

Already scoped in a previous conversation — waiting on an Entra ID App Registration
(Tenant ID, Client ID, Client secret, Application ID URI).

- [ ] NextAuth (Auth.js) login gate on the dashboard, Microsoft Entra ID provider
- [ ] Microsoft.Identity.Web bearer validation on the Api's read/management
      endpoints (ingestion endpoint stays project-key-only, like Sentry's DSN)

## Epic: Deployment (P1)

- [x] Dockerfiles for Api, Worker, dashboard (all build-tested locally, Api image
      smoke-tested — boots and answers `/health`)
- [x] Terraform for the Hetzner VM — single CPX31 running the whole stack via
      compose (see `deploy/terraform/`; not `terraform apply`'d — needs a real
      Hetzner token and your SSH key, see `deploy/README.md`)
- [x] Production docker-compose (`docker-compose.prod.yml`) + Caddy for TLS
      (`Caddyfile`)
- [x] GitHub Actions: `ci.yml` builds+tests on every push/PR; `deploy.yml` builds
      + pushes images to GHCR on push to master, then SSH-deploys — gated behind
      a `DEPLOY_ENABLED` repo variable so it no-ops until `DEPLOY_HOST`/`DEPLOY_USER`/
      `DEPLOY_SSH_KEY` secrets exist (see `deploy/README.md`)

Not done: DNS record creation, `.env` secret provisioning on the box, backup/restore
drill for Postgres.

## Explicitly out of scope (Sentry has these, Faultline doesn't need them)

Per ADR-0001's "error tracking only" decision — revisit only if a real need shows up:

- Sessions / release health (crash-free rate)
- Distributed tracing, spans, performance monitoring
- Profiling, session replay
- Attachments (screenshots, minidumps)
- Check-ins (cron monitoring)
- Structured logs / metrics as first-class telemetry (separate from breadcrumbs)
