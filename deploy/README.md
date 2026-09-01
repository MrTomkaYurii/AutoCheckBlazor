# Deploying AutoCheck to the VPS

The VPS is an **unprivileged LXC container** (vps1euro) — Docker can't run there (no
`nesting`), and the host is **IPv6-only**. Public access goes through the provider's
**WebGate** (IPv4/IPv6 ingress + Let's Encrypt TLS termination), which forwards plain
HTTP to the VPS on **port 80**. So the deploy is **native**: a self-contained .NET
publish, run by systemd, behind a local Caddy that only reverse-proxies HTTP (no TLS).

CI/CD flow: **push to `main` → GitHub Actions builds a self-contained publish → uploads
it over SSH → swaps the release symlink and restarts `autocheck` + `caddy`.**

- App URL: the WebGate free domain, e.g. **https://autocheck.fr-host.fr**
  (WebGate panel → Free domains → add, then Add a domain → attach to this VPS, port 80).
- WebGate ingress for a custom domain: `A 103.102.135.112`, `AAAA 2a0c:b641:1a0:800::1`.
- Server layout under `/opt/autocheck`:
  - `releases/<timestamp>/` — published app (last 5 kept)
  - `current` → symlink to the active release
  - `data/` — SQLite db, `settings.override.json`, `dp-keys/`, `backups/`, `repos/` (all state)
  - `app.env` — secrets, written from GitHub secrets each deploy (root:autocheck, 0640)

> WebGate bandwidth is capped at 25 Mbps per VPS. If that ever bites, add a paid
> dedicated IPv4 and switch Caddy back to terminating TLS itself.

## One-time server setup (run once, as root on the VPS)

```bash
curl -fsSL https://raw.githubusercontent.com/MrTomkaYurii/AutoCheckBlazor/main/deploy/setup.sh | bash
```

or copy `deploy/setup.sh` over and `bash setup.sh`. It installs `git`, `curl`, Caddy,
creates the `autocheck` service user and `/opt/autocheck/*`, and opens ports 80/22.

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
| `GEMINI_API_KEY` | your Gemini API key |

(`APP_DOMAIN` is no longer used — WebGate owns the public hostname.)

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
