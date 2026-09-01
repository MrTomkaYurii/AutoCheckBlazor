# Deploying AutoCheck to the VPS

The VPS is an **unprivileged LXC container** (vps1euro) — Docker can't run there (no
`nesting`), and the host is **IPv6-only** (SSH is reached through the provider's IPv4
proxy). So the deploy is **native**: a self-contained .NET publish, run by systemd,
behind a native Caddy that does automatic HTTPS.

CI/CD flow: **push to `main` → GitHub Actions builds a self-contained publish → uploads
it over SSH → swaps the release symlink and restarts `autocheck` + `caddy`.**

- App URL after setup: **https://&lt;APP_DOMAIN&gt;** (e.g. an sslip.io name for the
  server's IPv6: `2a0c-b641-1a0-800--e5.sslip.io`).
- Server layout under `/opt/autocheck`:
  - `releases/<timestamp>/` — published app (last 5 kept)
  - `current` → symlink to the active release
  - `data/` — SQLite db, `settings.override.json`, `dp-keys/`, `backups/`, `repos/` (all state)
  - `app.env` — secrets, written from GitHub secrets each deploy (root:autocheck, 0640)

> ⚠️ IPv6-only: visitors without IPv6 can't reach an sslip.io/direct name. If that's a
> problem, either enable the provider's **WebGate** (IPv4 reverse proxy + TLS — then point
> `APP_DOMAIN` at the WebGate hostname and drop Caddy) or add a paid IPv4.

## One-time server setup (run once, as root on the VPS)

```bash
curl -fsSL https://raw.githubusercontent.com/MrTomkaYurii/AutoCheckBlazor/main/deploy/setup.sh | bash
```

or copy `deploy/setup.sh` over and `bash setup.sh`. It installs `git`, `curl`, Caddy,
creates the `autocheck` service user and `/opt/autocheck/*`, and opens ports 80/443/22.

Then create the SSH deploy key **on your own machine**:

```bash
ssh-keygen -t ed25519 -f autocheck_deploy -N ""
```

and append the **public** key on the server (through the SSH proxy):

```bash
ssh -p 9054 root@VPS-200123.ssh.vps1euro.fr 'cat >> ~/.ssh/authorized_keys' < autocheck_deploy.pub
```

The **private** key (`autocheck_deploy`) goes into the `VPS_SSH_KEY` secret.

## GitHub secrets (repo → Settings → Secrets and variables → Actions)

| Secret | Value |
|---|---|
| `VPS_HOST` | `VPS-200123.ssh.vps1euro.fr` (the provider's SSH proxy host) |
| `VPS_PORT` | `9054` |
| `VPS_USER` | `root` |
| `VPS_SSH_KEY` | contents of the **private** key `autocheck_deploy` |
| `APP_DOMAIN` | `2a0c-b641-1a0-800--e5.sslip.io` (or your own domain) |
| `GEMINI_API_KEY` | your Gemini API key |

Optional secrets (unset ⇒ feature disabled): `SEED_DEFAULT_PASSWORD`, `GOOGLE_CLIENT_ID`,
`GOOGLE_CLIENT_SECRET`, `SMTP_HOST`, `SMTP_USER`, `SMTP_PASSWORD`, `SMTP_FROM`,
`BACKUP_GIT_REMOTE_URL`, `BACKUP_GIT_TOKEN`.

Optional **variables** (Actions → Variables, non-secret): `GEMINI_MODEL`,
`GEMINI_DAILY_LIMIT`, `SMTP_PORT`, `SMTP_FROM_NAME`.

## First deploy

Push to `main`, or Actions tab → **Deploy** → *Run workflow*. The job fails if the app
doesn't answer `/health` after start, dumping the last log lines.

## After it's up

- First production boot seeds a teacher account **`tomka.yurii@gmail.com`**; the password
  is `SEED_DEFAULT_PASSWORD` if set, otherwise the committed fallback in `DatabaseSeeder.cs`.
  **Change it after first login.**
- Logs: `journalctl -u autocheck -f`
- Restart: `systemctl restart autocheck`  ·  Status: `systemctl status autocheck`
- Caddy / TLS: `journalctl -u caddy -f`
