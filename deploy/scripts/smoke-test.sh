#!/usr/bin/env bash
set -euo pipefail

API_URL="${1:-}"
SUPABASE_JWT="${2:-}"

if [[ -z "${API_URL}" ]]; then
  echo "Usage: $0 <api-base-url> [supabase-jwt-for-session-test]" >&2
  echo "Example: $0 https://api.example.com" >&2
  exit 1
fi

API_URL="${API_URL%/}"

echo "Checking GET ${API_URL}/health ..."
health_status="$(curl -fsS -o /tmp/applyvault-health.json -w '%{http_code}' "${API_URL}/health")"
if [[ "${health_status}" != "200" ]]; then
  echo "Health check failed with HTTP ${health_status}" >&2
  cat /tmp/applyvault-health.json >&2 || true
  exit 1
fi

echo "Health check OK:"
cat /tmp/applyvault-health.json
echo

if [[ -n "${SUPABASE_JWT}" ]]; then
  echo "Checking GET ${API_URL}/api/auth/session ..."
  session_status="$(curl -fsS -o /tmp/applyvault-session.json -w '%{http_code}' \
    -H "Authorization: Bearer ${SUPABASE_JWT}" \
    "${API_URL}/api/auth/session")"
  if [[ "${session_status}" != "200" ]]; then
    echo "Session check failed with HTTP ${session_status}" >&2
    cat /tmp/applyvault-session.json >&2 || true
    exit 1
  fi
  echo "Session check OK:"
  cat /tmp/applyvault-session.json
  echo
else
  echo "Skipping /api/auth/session (pass a Supabase access token as the second argument to test auth)."
fi

echo "Smoke tests passed."
