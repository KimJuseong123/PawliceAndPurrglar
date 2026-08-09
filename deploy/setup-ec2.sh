#!/usr/bin/env bash
#
# One-time setup for the EC2 instance.
#
# Written for what this project actually runs on: Amazon Linux 2023 on
# aarch64, with nginx already terminating TLS through a Certbot certificate.
# t3/t4g.micro is enough — no Unity server runs here. Matches go through Unity
# Relay and this machine only serves files and forwards voice requests.
#
#   sudo PAWLICE_DOMAIN=pawlice.duckdns.org bash setup-ec2.sh
#
# Run it again safely. Every step checks before it acts, and the existing
# certificate is read rather than reissued: Let's Encrypt rate-limits issuance,
# and throwing away a working certificate to prove a script can get one is a
# bad trade at any time and a terrible one near a deadline.

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

HERE="$(cd "$(dirname "$0")" && pwd)"
LIVE="/etc/letsencrypt/live/$DOMAIN"

echo "== 1/6  checking what is already here"
command -v nginx >/dev/null || { echo "nginx is not installed." >&2; exit 1; }
if [[ ! -s "$LIVE/fullchain.pem" ]]; then
  echo "No certificate at $LIVE." >&2
  echo "Issue one first:  sudo certbot --nginx -d $DOMAIN" >&2
  exit 1
fi
echo "   nginx: $(nginx -v 2>&1)"
echo "   certificate: present"

echo "== 2/6  Node.js 22"
# Amazon Linux 2023 ships Node 18 and the voice server's Fastify 5 needs 20 or
# newer. Installed from NodeSource rather than pinned to what dnf happens to
# have, because "it starts but throws on an import" is a much worse failure
# than "the package is missing".
if ! command -v node >/dev/null || [[ "$(node -v | sed 's/v\([0-9]*\).*/\1/')" -lt 20 ]]; then
  curl -fsSL https://rpm.nodesource.com/setup_22.x | bash -
  dnf install -y -q nodejs
fi
echo "   node $(node -v)"

echo "== 3/6  service account and folders"
id -u pawlice >/dev/null 2>&1 \
  || useradd --system --create-home --home-dir /srv/pawlice --shell /sbin/nologin pawlice
mkdir -p /srv/pawlice/web /srv/pawlice/server
chown -R pawlice:pawlice /srv/pawlice
# nginx runs as its own user and has to be able to read what we upload.
chmod 755 /srv/pawlice /srv/pawlice/web

echo "== 4/6  nginx site"
# The previous file is kept, once. It is Certbot's output and the only record
# of what the TLS block looked like before this script touched it.
if [[ -f /etc/nginx/conf.d/pawlice.conf && ! -f /etc/nginx/conf.d/pawlice.conf.pre-pawlice ]]; then
  cp /etc/nginx/conf.d/pawlice.conf /etc/nginx/conf.d/pawlice.conf.pre-pawlice
  echo "   kept the previous config as pawlice.conf.pre-pawlice"
fi

{
  sed "s|__PAWLICE_DOMAIN__|$DOMAIN|g" "$HERE/pawlice.nginx.conf" | sed '$d'
  cat <<EOF

    listen 443 ssl; # managed by Certbot
    ssl_certificate $LIVE/fullchain.pem; # managed by Certbot
    ssl_certificate_key $LIVE/privkey.pem; # managed by Certbot
    include /etc/letsencrypt/options-ssl-nginx.conf; # managed by Certbot
    ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem; # managed by Certbot
}

server {
    if (\$host = $DOMAIN) {
        return 301 https://\$host\$request_uri;
    } # managed by Certbot

    listen 80;
    server_name $DOMAIN;
    return 404; # managed by Certbot
}
EOF
} > /etc/nginx/conf.d/pawlice.conf

# Checked before reloading. A bad config makes `nginx -s reload` fail and leaves
# the old one running, which looks like the change did nothing.
nginx -t
systemctl reload nginx
echo "   nginx reloaded"

echo "== 5/6  voice service unit"
install -m 0644 "$HERE/pawlice-voice.service" /etc/systemd/system/pawlice-voice.service
systemctl daemon-reload

echo "== 6/6  certificate renewal"
# Certbot issued a certificate and left nothing to renew it. It expires in
# ninety days, silently, and the failure is a game nobody can open — after the
# person who deployed it has stopped watching.
cat >/etc/systemd/system/certbot-renew.service <<'EOF'
[Unit]
Description=Renew Let's Encrypt certificates
[Service]
Type=oneshot
ExecStart=/usr/bin/certbot renew --quiet --deploy-hook "systemctl reload nginx"
EOF

cat >/etc/systemd/system/certbot-renew.timer <<'EOF'
[Unit]
Description=Renew Let's Encrypt certificates twice a day
[Timer]
OnCalendar=*-*-* 03,15:00:00
RandomizedDelaySec=3600
Persistent=true
[Install]
WantedBy=timers.target
EOF

systemctl daemon-reload
systemctl enable --now certbot-renew.timer
echo "   renewal timer armed"

cat <<EOF

Done. What is left:

  1. Write /srv/pawlice/server/.env  — see deploy/voice.env.example
     Then: sudo systemctl enable --now pawlice-voice

  2. Upload the WebGL build to /srv/pawlice/web
     (deploy/upload.ps1 from the development machine)

  3. Check:  curl -I https://$DOMAIN/
             curl    https://$DOMAIN/health

EOF
