# Deploying AutoCheck to the VPS

CI/CD flow: **push to `main` → GitHub Actions builds the image, pushes it to GHCR, then
SSHes into the VPS and restarts the stack.** Caddy provides automatic HTTPS.

- App URL after setup: **https://79-108-161-80.nip.io**
- Server files live in `/opt/autocheck` (compose + Caddyfile, written by CI; `.env` written from secrets).

## One-time server setup (run once, as root on the VPS)

```bash
# 1. Install Docker + compose plugin
curl -fsSL https://get.docker.com | sh

# 2. Deploy directory
mkdir -p /opt/autocheck

# 3. SSH key for GitHub Actions (run on your OWN machine, not the server):
#    ssh-keygen -t ed25519 -f autocheck_deploy -N ""
#    Then append the PUBLIC key to the server:
#    ssh root@79.108.161.80 'cat >> ~/.ssh/authorized_keys' < autocheck_deploy.pub
#    The PRIVATE key (autocheck_deploy) goes into the VPS_SSH_KEY GitHub secret.
```

Open ports **80** and **443** (and keep **22**) in the Kamatera firewall / console.

## GitHub secrets (repo → Settings → Secrets and variables → Actions → Secrets)

| Secret | Value |
|---|---|
| `VPS_HOST` | `79.108.161.80` |
| `VPS_USER` | `root` |
| `VPS_SSH_KEY` | contents of the **private** key `autocheck_deploy` |
| `APP_DOMAIN` | `79-108-161-80.nip.io` |
| `GEMINI_API_KEY` | your Gemini API key |

Optional secrets (leave unset to disable the feature): `GOOGLE_CLIENT_ID`,
`GOOGLE_CLIENT_SECRET`, `SMTP_HOST`, `SMTP_USER`, `SMTP_PASSWORD`, `SMTP_FROM`,
`BACKUP_GIT_REMOTE_URL`, `BACKUP_GIT_TOKEN`, `VPS_PORT` (if SSH ≠ 22).

Optional **variables** (Actions → Variables, non-secret): `GEMINI_MODEL`,
`GEMINI_DAILY_LIMIT`, `SMTP_PORT`, `SMTP_FROM_NAME`.

## First deploy

Once the server and secrets are ready, trigger the workflow (push to `main`, or
Actions tab → Deploy → *Run workflow*). Watch it in the **Actions** tab.

## After it's up

- First run auto-creates a teacher account and prints its password **once** — read it with:
  `cd /opt/autocheck && docker compose logs autocheck | grep -i "акаунт викладача"`
- Logs: `docker compose logs -f`  ·  Restart: `docker compose restart`  ·  Status: `docker compose ps`
