#!/usr/bin/env bash
# One-time server setup for a native (no Docker) AutoCheck deploy.
# Run once as root on the VPS:  bash setup.sh
#
# After this, configure the GitHub secrets (see deploy/README.md) and push to main —
# the Deploy workflow ships a self-contained .NET publish and (re)starts the service.
set -euo pipefail

APP=/opt/autocheck

echo "==> Base packages"
export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y git curl ca-certificates gnupg debian-keyring debian-archive-keyring apt-transport-https

echo "==> Service user + directories"
id -u autocheck >/dev/null 2>&1 || useradd -r -d "$APP" -s /usr/sbin/nologin autocheck
mkdir -p "$APP"/releases "$APP"/data "$APP"/incoming
touch "$APP/app.env"; chown root:autocheck "$APP/app.env"; chmod 640 "$APP/app.env"
chown -R autocheck:autocheck "$APP/releases" "$APP/data" "$APP/incoming"

echo "==> Caddy (apt package)"
if ! command -v caddy >/dev/null 2>&1; then
  curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' \
    | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
  curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' \
    > /etc/apt/sources.list.d/caddy-stable.list
  apt-get update
  apt-get install -y caddy
fi

echo "==> Firewall"
if command -v ufw >/dev/null 2>&1; then
  ufw allow 22/tcp || true
  ufw allow 80/tcp || true
  ufw allow 443/tcp || true
fi

cat <<'DONE'

==> Done.

Next:
  1. On your own machine, create a deploy key:
       ssh-keygen -t ed25519 -f autocheck_deploy -N ""
     Append the PUBLIC key on the server:
       cat autocheck_deploy.pub >> /root/.ssh/authorized_keys
  2. Add the GitHub secrets from deploy/README.md (VPS_HOST, VPS_PORT, VPS_SSH_KEY,
     VPS_USER, APP_DOMAIN, GEMINI_API_KEY, ...).
  3. Push to main (or run the Deploy workflow manually).

Read the seeded teacher password after the first deploy:
  journalctl -u autocheck | grep -i "акаунт викладача"
DONE
