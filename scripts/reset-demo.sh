#!/usr/bin/env bash
# Resets the harness demo to a clean slate.
# Deletes the runtime folders next to the console executable for every build configuration:
#   <repo>/src/ProcurementCopilot.Console/bin/<Debug|Release>/net10.0/logs
#   <repo>/src/ProcurementCopilot.Console/bin/<Debug|Release>/net10.0/workspace   (output, agent-file-memory and the rfps copies)
# The read-only workspace/rfps, data and skills copies are recreated by the next `dotnet build`.
#
#   --sessions   also delete the persisted sessions (LOCALAPPDATA or ~/.local/share ProcurementCopilot/sessions)
#   --all        --sessions plus the TestResults folders
#   --database   also remove the purchase orders and award recommendations the copilot wrote to SQL Server
#                (runs database/maintenance/reset-demo-awards.sql with sqlcmd; override with SQL_SERVER / SQL_DATABASE)
#   --dry-run    show what would be deleted without deleting
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONSOLE_BIN="$ROOT/src/ProcurementCopilot.Console/bin"

sessions=0; all=0; dry=0; database=0
for arg in "$@"; do
  case "$arg" in
    --sessions) sessions=1 ;;
    --all) all=1; sessions=1 ;;
    --database) database=1 ;;
    --dry-run|-n) dry=1 ;;
    *) echo "unknown option: $arg"; echo "usage: $0 [--sessions] [--all] [--dry-run]"; exit 2 ;;
  esac
done

targets=()
if [ -d "$CONSOLE_BIN" ]; then
  while IFS= read -r -d '' dir; do targets+=("$dir"); done < <(find "$CONSOLE_BIN" -mindepth 3 -maxdepth 3 -type d \( -name logs -o -name workspace \) -print0)
fi
if [ "$sessions" -eq 1 ]; then
  if [ -n "${LOCALAPPDATA:-}" ]; then
    targets+=("$(cygpath -u "$LOCALAPPDATA" 2>/dev/null || echo "$LOCALAPPDATA")/ProcurementCopilot/sessions")
  else
    targets+=("${XDG_DATA_HOME:-$HOME/.local/share}/ProcurementCopilot/sessions")
  fi
fi
if [ "$all" -eq 1 ]; then
  targets+=("$ROOT/TestResults")
  while IFS= read -r -d '' dir; do targets+=("$dir"); done < <(find "$ROOT/tests" -mindepth 2 -maxdepth 2 -type d -name TestResults -print0)
fi

deleted=0
for path in "${targets[@]:-}"; do
  [ -n "$path" ] && [ -e "$path" ] || continue
  if [ "$dry" -eq 1 ]; then
    echo "Would delete $path"
  else
    rm -rf -- "$path"
    echo "Deleted $path"
  fi
  deleted=$((deleted + 1))
done

if [ "$database" -eq 1 ]; then
  script="$ROOT/database/maintenance/reset-demo-awards.sql"
  if [ "$dry" -eq 1 ]; then echo "Would run $script against ${SQL_SERVER:-localhost\SQLEXPRESS}/${SQL_DATABASE:-AdventureWorks2019}"
  else sqlcmd -S "${SQL_SERVER:-localhost\SQLEXPRESS}" -E -d "${SQL_DATABASE:-AdventureWorks2019}" -C -b -i "$script"; fi
fi

if [ "$deleted" -eq 0 ]; then
  echo "Nothing to reset: no runtime folders found."
elif [ "$dry" -eq 0 ]; then
  echo "Reset complete. Run 'dotnet build' (or 'dotnet run') to recreate the read-only workspace inputs."
fi
