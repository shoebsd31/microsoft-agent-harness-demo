#!/usr/bin/env bash
# Applies the pending database migrations in database/migrations to the configured SQL Server
# (SqlServer:ConnectionString in src/ProcurementCopilot.Admin/appsettings.json, or the SqlServer__ConnectionString
# environment variable). Scripts are idempotent and record themselves in Procurement.SchemaMigration.
#   --status   only print which scripts are applied / pending
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
flag="--migrate"
[ "${1:-}" = "--status" ] && flag="--status"
dotnet run --project "$ROOT/src/ProcurementCopilot.Admin" -- "$flag"
