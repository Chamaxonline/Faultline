# Deploying Faultline

## 1. Provision the VM (Terraform)

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

## 2. First-time server setup

SSH in (`ssh root@<ip>`) and drop these files into `/opt/faultline/`:
- `docker-compose.prod.yml` (repo root)
- `Caddyfile` (repo root)
- `.env` — **not committed** — with:
  ```
  POSTGRES_PASSWORD=<generate one>
  FAULTLINE_DOMAIN=faultline.internal.bistec.com
  ALERTS_TEAMS_WEBHOOK_URL=<optional>
  GHCR_IMAGE_PREFIX=ghcr.io/chamaxonline/faultline
  ```

Then:
```bash
docker login ghcr.io   # needs a token with read:packages, once
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml up -d
```

## 3. CI/CD

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
