# ADR-0001: Faultline — in-house error tracking architecture

## Status
Accepted

## Date
2026-09-11

## Context
Sentry per-project pricing scales expensive across many internal apps. Company wants
an in-house error tracker for internal apps only (not full APM — no perf tracing,
logs, or uptime monitoring in v1). Multi-tenant: many teams/apps will send errors.

## Decision
Build "Faultline": a .NET 8 ingestion API + worker + Postgres/Redis backend, with a
Next.js dashboard, deployed on Hetzner (cost-sensitive internal tool).

## Bistec Alignment
- Backend: .NET 8 Minimal API (primary stack) — deviates from SQL Server to
  PostgreSQL for cost (Hetzner-hosted Postgres vs Azure SQL); justified below.
- Cloud: Hetzner (budget tier) — internal tool, cost-sensitive, matches Bistec's
  "internal tools, dev/staging" Hetzner guidance.
- Auth: Entra ID SSO for dashboard access only (ingestion uses per-project key, not
  Entra ID — matches Sentry's DSN model, avoids requiring service auth in every app).

## Alternatives Considered

### Option A: Self-host Sentry OSS
- Pros: zero build cost, mature, full-featured
- Cons: heavy resource footprint (Sentry OSS needs Kafka, ClickHouse, Postgres,
  Redis, Zookeeper — overkill for internal-only error tracking), ops burden
- Monthly cost: ~$60-100/mo (bigger VM needed for the stack)

### Option B: Build Faultline (chosen)
- Pros: minimal footprint (Postgres + Redis only), full control, fits exact need
  (error tracking only, no APM bloat), one-time build cost then near-zero marginal
  cost per additional app/team
- Cons: build + maintenance time, fewer features than Sentry initially (no perf
  tracing, no release health, no source map deminification yet)
- Monthly cost: ~$35-40/mo (2x Hetzner CPX31)

### Option C: GlitchTip / Bugsink (lighter self-hosted Sentry-API-compatible alts)
- Pros: Sentry SDK/API compatible, less build time
- Cons: still Python/Django stack unfamiliar to team, less control over roadmap
- Monthly cost: ~$25-35/mo

## Consequences
- Positive: near-zero marginal cost as more apps onboard; full control of data
  retention and feature roadmap.
- Negative: team owns bug-for-bug behavior, no vendor support line.
- Risks: worker/queue backlog if event volume spikes — mitigated by Redis Streams
  consumer group (horizontally scalable workers) and per-key rate limiting at
  ingestion.

## Cost Analysis
```
2x Hetzner CPX31 (app + worker + Postgres + Redis)   ~$28/mo
Volume storage (50GB)                                 ~$2/mo
Backups                                               ~$3/mo
─────────────────────────────────────────────────────────
Total                                                 ~$35-40/mo
```
Optimization: raw event payloads TTL 30-90 days; issue aggregates kept indefinitely.
