#!/usr/bin/env bash
#
# One-time setup for the EC2 instance. Ubuntu 22.04 or 24.04, t3.micro is
# enough: no Unity server runs here. Matches go through Unity Relay and this
# machine only serves files and forwards voice requests.
#
#   sudo PAWLICE_DOMAIN=pawlice.duckdns.org bash setup-ec2.sh
#
# Run it again safely — every step checks before it acts.

set -euo pipefail

DOMAIN="${PAWLICE_DOMAIN:-}"
if [[ -z "$DOMAIN" ]]; then
  echo "PAWLICE_DOMAIN is required, e.g. PAWLICE_DOMAIN=pawlice.duckdns.org" >&2
  exit 1
fi

if [[ $EUID -ne 0 ]]; then
  echo "Run this with sudo." >&2
  exit 1
fi

echo "== 1/7  packages"
apt-get update -qq
apt-get install -y -qq curl ca-certificates gnupg debian-keyring debian-archive-keyring apt-transport-https

echo "== 2/7  Node.js 22"
if ! command -v node >/dev/null || [[ "$(node -v | cut -c2-3)" -lt 20 ]]; then
  curl -fsSL https://deb.nodesource.com/setup_22.x | bash -
  apt-get install -y -qq nodejs
fi
node -v

echo "== 3/7  make room on port 80"
# Caddy and nginx both want :80, and the loser fails to start with nothing more
# than "address already in use" in a journal nobody is reading. Whatever was
# there first is stopped explicitly, so the reason is on screen.
#
# Only stopped and disabled, never removed. If this box turns out to be serving
# something else, `systemctl enable --now nginx` puts it back.
for other in nginx apache2 httpd; do
  if systemctl is-active --quiet "$other" 2>/dev/null; then
    echo "   stopping $other (it holds port 80)"
    systemctl stop "$other"
    systemctl disable "$other" 2>/dev/null || true
  fi
done

echo "== 4/7  Caddy"
if ! command -v caddy >/dev/null; then
  curl -1sLf https://dl.cloudsmith.io/public/caddy/stable/gpg.key \
    | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
  curl -1sLf https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt \
    > /etc/apt/sources.list.d/caddy-stable.list
  apt-get update -qq
  apt-get install -y -qq caddy
fi
caddy version

echo "== 5/7  service account and folders"
id -u pawlice >/dev/null 2>&1 || useradd --system --create-home --home-dir /srv/pawlice --shell /usr/sbin/nologin pawlice
mkdir -p /srv/pawlice/web /srv/pawlice/server
chown -R pawlice:pawlice /srv/pawlice

# Caddy runs as its own user and has to be able to read what we upload.
chmod 755 /srv/pawlice /srv/pawlice/web

echo "== 6/7  Caddy site"
install -d /etc/caddy
install -m 0644 "$(dirname "$0")/Caddyfile" /etc/caddy/Caddyfile

# The domain is passed in rather than written into the file, so the same
# Caddyfile is committed once and works for whatever name this instance ends up
# with. systemd's drop-in is the only place it is stored.
install -d /etc/systemd/system/caddy.service.d
cat >/etc/systemd/system/caddy.service.d/pawlice.conf <<EOF
[Service]
Environment=PAWLICE_DOMAIN=$DOMAIN
EOF

echo "== 7/7  voice service unit"
install -m 0644 "$(dirname "$0")/pawlice-voice.service" /etc/systemd/system/pawlice-voice.service

systemctl daemon-reload
systemctl enable caddy
systemctl restart caddy

cat <<EOF

Done. What is not done yet, in order:

  1. Point $DOMAIN at this machine's public IP (DuckDNS).
     Caddy asks Let's Encrypt for a certificate on the next request and will
     fail until the name resolves here.

  2. Upload the WebGL build to /srv/pawlice/web
     and the built voice server to /srv/pawlice/server
     (deploy/upload.ps1 from the development machine does both).

  3. Write /srv/pawlice/server/.env  — see deploy/voice.env.example.
     Then: systemctl enable --now pawlice-voice

  4. Check:  curl -I https://$DOMAIN/        -> 200
             curl    https://$DOMAIN/health  -> {"status":"ok"}

EOF
