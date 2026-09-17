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

## Epic: Auth (done — pivoted away from Entra ID)

Originally scoped as Entra ID SSO (NextAuth + Microsoft.Identity.Web), but that was
blocked on an App Registration. Pivoted to a simpler self-contained model: one
bootstrap admin, admin creates every other user (email + name + password), JWT
bearer auth on the Api. Revisit Entra ID later if Bistec wants SSO instead.

- [x] `User` entity (email, name, password hash, role: Admin/Member) — single-org,
      matches the existing Project/Organization model
- [x] `POST /api/v1/auth/login` issues a JWT (HMAC-signed, 8h default lifetime);
      `GET /api/v1/auth/me` returns the caller's identity from the token
- [x] `POST/GET/DELETE /api/v1/users` — admin-only (`AdminOnly` policy), can't
      delete yourself or the last remaining admin
- [x] Every existing read/management endpoint now requires authentication
      (`RequireAuthorization()`); ingestion (`POST /api/v1/{projectKey}/store`)
      stays anonymous — project-key auth, same model as Sentry's DSN
- [x] Bootstrap: first admin created from `Auth:DefaultAdminEmail`/
      `DefaultAdminPassword` config on first run (there's no signup flow, so the
      very first admin has to come from somewhere) — logs a warning and skips if
      unset, rather than creating a guessable default
- [x] Dashboard: login page, middleware-gated routes (redirects to `/login`),
      admin-only Users page (create/list/remove), shared header with sign-out

**Known limitation (documented, not fixed):** the JWT is stored in a
non-httpOnly cookie so both server components and client components can read it
without extra plumbing — trades some XSS exposure for simplicity, matching the
"initial stage" scope this was asked for. Hardening later means moving writes
behind Next.js Route Handlers that hold an httpOnly cookie server-side only.

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

### Free-tier path (Vercel + Render + Neon + Upstash)

- [x] Merge the grouping worker into the Api process (`RunWorkerInProcess` config
      flag) — Render's free tier has no standalone background-worker offering
- [x] `RedisConnectionStringHelper` accepts a `redis://`/`rediss://` URI (what
      Upstash gives you) as well as the native options-string format
- [x] `render.yaml` blueprint + CORS made configurable (`Cors:AllowedOrigins`) for
      a split dashboard/API deployment — previously CORS only applied in
      Development, which would've silently broken this
- [x] Fixed: DB migrations only ran inside dev-only seeding, so a fresh Production
      database (Neon, or anything else) would have no tables at all
- [x] Fixed: no way to create an Organization via the API, so a fresh Production
      deploy could never create its first Project — now auto-created on startup
      if none exists
- Full walkthrough in `deploy/README.md`

## Epic: Issue detail UI — parity with Sentry's issue page (P1)

Reference: a real Sentry issue page (JS/React sample project). Faultline already
captures nearly all the underlying data (`Frames` with `InApp`/`ContextLines`,
`Tags`, `Extra`, `Breadcrumbs`, `UserContext`) — it's just dumped as one raw JSON
blob on the issue detail page instead of rendered. Most of this epic is UI work
against data that already exists, not new capture work.

### Story 1: Structured stack trace rendering (P0 — data already exists, zero backend work) — done
- [x] Parse `RawPayload` client-side into typed sections instead of one raw JSON dump
- [x] Render `Frames` as a formatted list: function, file:line, in-app frames
      visually distinct from library frames (`InApp` is already computed)
- [x] Show `ContextLines`/`ContextStartLine` as a code snippet under each frame,
      failing line highlighted
- [x] Collapse library frames by default with a "Show N library frames" toggle
      (rest of the raw payload — tags/breadcrumbs/context — kept as a collapsible
      fallback `<details>` until Story 2 splits it into proper tabs)

### Story 2: Tabbed issue detail layout (P0 — depends on Story 1) — done
- [x] Replace the raw-JSON fallback with tabs: **Stack Trace** / **Tags** /
      **Breadcrumbs** / **Context** / **Raw** (mirrors the screenshot's tab row
      minus Replay/Trace, which are out of scope — see below; kept a Raw tab as
      a debugging safety net)
- [x] Tags tab: render the event's `Tags` dict as a simple list (aggregation
      across events is Story 4, not this one)
- [x] Breadcrumbs tab: render the `Breadcrumbs` array as a timeline (timestamp,
      category, level, message)
- [x] Context tab: `UserContext` + `Extra`

### Story 3: Per-event navigation (P1)
- [ ] Replace the flat "last 20 events" list with First/Previous/Next/Latest
      navigation through an issue's individual events (matches the screenshot's
      event pager)
- [ ] Api: paginate `GET /api/v1/issues/{id}/events` instead of a fixed `Take(20)`
      inline on the issue payload

### Story 4: Events-over-time + tag distribution (P1 — needs light aggregation)
- [ ] Small sparkline/histogram of event counts over time on the issue page
      (group by hour/day; a simple `GROUP BY date_trunc` query, not a new metrics
      system)
- [ ] Tag value distribution (e.g. "environment: 100% Development") computed
      over the issue's recent events — no schema change needed, just aggregate
      the same `Tags` dict already stored per event

### Story 5: Assignee (P1 — builds on the Users epic already done)
- [ ] `Issue.AssignedToUserId` (nullable FK to `User`)
- [ ] `PATCH /api/v1/issues/{id}/assign`, dashboard dropdown using the existing
      Users list
- [ ] "Assigned to me" filter on the issue list page

### Story 6: Richer resolve/ignore (P2)
- [ ] "Ignore until N more occurrences" / "until a date" instead of today's
      binary Resolved/Ignored/Unresolved
- [ ] Priority field (High/Medium/Low), separate from `Level` — only worth
      building if `Level` (error/warning/fatal/info) turns out not to cover what
      triage actually needs; confirm before building

### Story 7: Activity feed (P2)
- [ ] `IssueComment` entity (issue id, user id, body, timestamp)
- [ ] Simple comment thread on the issue detail page — status changes
      (resolved/ignored/assigned) logged as system entries in the same feed

### Explicitly out of scope for this epic (same reasoning as ADR-0001)
- **Session Replay** — needs a browser SDK recording DOM/video-like sessions;
  Faultline has no browser SDK at all yet, and this is APM-adjacent, not error
  tracking
- **Seer / Autofix (AI root cause analysis)** — a paid Sentry-specific feature,
  not something to build in-house right now
- **Trace ID / distributed tracing links** — perf tracing is explicitly out of
  scope per ADR-0001
- **Third-party issue linking** (Jira/Linear) — genuinely useful eventually, but
  not part of "look like Sentry's issue page," revisit as its own epic later

## Explicitly out of scope (Sentry has these, Faultline doesn't need them)

Per ADR-0001's "error tracking only" decision — revisit only if a real need shows up:

- Sessions / release health (crash-free rate)
- Distributed tracing, spans, performance monitoring
- Profiling, session replay
- Attachments (screenshots, minidumps)
- Check-ins (cron monitoring)
- Structured logs / metrics as first-class telemetry (separate from breadcrumbs)
