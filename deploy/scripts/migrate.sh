#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
ENV_FILE="${ROOT}/deploy/.env"

if [[ -f "${ENV_FILE}" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "${ENV_FILE}"
  set +a
fi

if [[ -z "${ConnectionStrings__ApplyVault:-}" ]]; then
  echo "ConnectionStrings__ApplyVault is not set. Copy deploy/.env.example to deploy/.env and configure it." >&2
  exit 1
fi

echo "Applying EF migrations to the database configured in deploy/.env ..."
cd "${ROOT}"
dotnet ef database update --project api/ApplyVault.Api

echo "Migrations applied."
