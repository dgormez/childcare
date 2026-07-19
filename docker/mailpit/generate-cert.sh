#!/usr/bin/env bash
# Generates a throwaway self-signed cert/key so Mailpit's SMTP listener can advertise STARTTLS
# locally (EmailService.cs always requests StartTls for any port other than 465 — Mailpit needs
# a cert to support that, unlike MailHog which has no TLS support at all). Not a real secret:
# it protects nothing, it only satisfies the TLS handshake for local dev email testing. Re-run
# any time to regenerate; idempotent (skips if both files already exist).
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

if [[ -f cert.pem && -f key.pem ]]; then
  echo "docker/mailpit/cert.pem and key.pem already exist — skipping. Delete them first to regenerate."
  exit 0
fi

openssl req -x509 -newkey rsa:2048 -nodes \
  -keyout key.pem -out cert.pem -days 3650 \
  -subj "/CN=localhost" \
  -addext "subjectAltName=DNS:localhost,DNS:mailpit,IP:127.0.0.1"

echo "Generated docker/mailpit/cert.pem and key.pem."
