# Deploying Faultline

Two paths: the Hetzner VM (below) for a real production instance, or the free-tier
stack for trying it out / a low-traffic team without infra cost.

## Free-tier stack (Vercel + Render + Neon + Upstash)

No credit card needed on any of these. Api and the grouping worker run merged into
one process (`RunWorkerInProcess=true`) since Render's free tier has no standalone
background-worker option — only Web Services, which need to answer HTTP to stay
alive.

1. **Postgres — [Neon](https://neon.tech)**: create a project, copy the connection
   string it gives you (already has `sslmode=require` — Npgsql handles it as-is).

2. **Redis — [Upstash](https://upstash.com)**: create a Redis database, copy the
   `rediss://default:<password>@<host>:<port>` URI from its dashboard. Faultline
   accepts this format directly (see `RedisConnectionStringHelper`) — no manual
   reformatting needed.

3. **Api + worker — [Render](https://render.com)**: New → Blueprint → point at this
   repo (`render.yaml` at the root does the rest). Set these env vars in the Render
   dashboard once the service exists (all marked `sync: false` in the blueprint, so
   Render prompts for them):
   - `ConnectionStrings__Postgres` — the Neon connection string
   - `ConnectionStrings__Redis` — the Upstash `rediss://...` URI
   - `Cors__AllowedOrigins__0` — your Vercel dashboard URL (step 4), once you have it
   - `Alerts__DashboardBaseUrl` — same URL, used to build "view issue" links in Teams alerts
   - `Alerts__TeamsWebhookUrl` — optional
   - `Auth__DefaultAdminEmail` / `Auth__DefaultAdminPassword` — bootstraps the first
     admin on a fresh database (change the password after first login)
   - `Auth__JwtSigningKey` — Render generates this one for you (`generateValue: true`
     in the blueprint), nothing to fill in

   Render's free web service spins down after ~15 min idle; the first request after
   that takes 30-60s to wake up. Same for Neon/Upstash waking from their own idle
   suspend — worst case, one request pays all three wake-up costs at once.

4. **Dashboard — [Vercel](https://vercel.com)**: New Project → import this repo →
   set **Root Directory** to `dashboard` (monorepo — Vercel needs this set in project
   settings, not in a config file). Set the env var:
   - `NEXT_PUBLIC_API_URL` — your Render service's URL (e.g. `https://faultline-api.onrender.com`)

   Deploys automatically on every push to `master`.

Once both are up, go back to Render and set `Cors__AllowedOrigins__0` /
`Alerts__DashboardBaseUrl` to the Vercel URL from step 4, then redeploy the Render
service (env var changes require a manual redeploy on Render's free tier).

## Hetzner VM (self-hosted, no cold starts)

### 1. Provision the VM (Terraform)

```bash
cd deploy/terraform
export TF_VAR_hcloud_token=<Hetzner Cloud API token>
terraform init
terraform plan -var='admin_ip_cidrs=["<your office/VPN IP>/32"]'
terraform apply -var='admin_ip_cidrs=["<your office/VPN IP>/32"]'
```

Outputs the server's public IPv4. Point `FAULTLINE_DOMAIN`'s DNS A record at it.

This provisions one CPX31 VM with Docker installed (via cloud-init) and a firewall
that only allows SSH from `admin_ip_cidrs` — everything else (80/443) is open since
Caddy handles TLS and routing on the box itself.

### 2. First-time server setup

SSH in (`ssh root@<ip>`) and drop these files into `/opt/faultline/`:
- `docker-compose.prod.yml` (repo root)
- `Caddyfile` (repo root)
- `.env` — **not committed** — with:
  ```
  POSTGRES_PASSWORD=<generate one>
  FAULTLINE_DOMAIN=faultline.internal.bistec.com
  ALERTS_TEAMS_WEBHOOK_URL=<optional>
  GHCR_IMAGE_PREFIX=ghcr.io/chamaxonline/faultline
  AUTH_JWT_SIGNING_KEY=<generate a long random string>
  AUTH_DEFAULT_ADMIN_EMAIL=<bootstraps the first admin on a fresh database>
  AUTH_DEFAULT_ADMIN_PASSWORD=<change after first login>
  ```

Then:
```bash
docker login ghcr.io   # needs a token with read:packages, once
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml up -d
```

### 3. CI/CD

`.github/workflows/deploy.yml` builds and pushes the three images (api, worker,
dashboard) to GHCR on every push to `master`, then SSHes into the box to pull and
restart. Requires these GitHub Actions secrets:

| Secret | Value |
|---|---|
| `DEPLOY_HOST` | server's IP or hostname |
| `DEPLOY_SSH_KEY` | private key matching `ssh_public_key_path` in Terraform |
| `DEPLOY_USER` | SSH user (`root` unless you set one up) |

`docker-compose.prod.yml` and `Caddyfile` still need to exist in `/opt/faultline`
on the box (step 2) — the workflow only refreshes images and restarts, it doesn't
provision.

## Not yet automated

- DNS record creation
- `.env` secret provisioning on the box (currently manual — a Key Vault-backed
  approach would fit Bistec's usual pattern better, see docs/BACKLOG.md)
- Database backups / restore drill
