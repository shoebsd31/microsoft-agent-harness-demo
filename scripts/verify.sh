#!/usr/bin/env bash
# Full verification: restore, build (warnings as errors), test with coverage, vulnerability scan, file-length check, self-check.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

step() { echo; echo "==> $1"; }

step "dotnet restore";                   dotnet restore ProcurementCopilot.slnx
step "dotnet build -warnaserror";        dotnet build ProcurementCopilot.slnx --no-restore -warnaserror
step "dotnet test (with coverage)";      dotnet test --solution ProcurementCopilot.slnx --no-build --coverage --coverage-settings "$ROOT/coverage.runsettings" --coverage-output-format cobertura --results-directory "$ROOT/TestResults"
step "dotnet list package --vulnerable"
out=$(dotnet list ProcurementCopilot.slnx package --vulnerable --include-transitive 2>&1 || true)
echo "$out"
if echo "$out" | grep -q "has the following vulnerable packages"; then echo "Vulnerable packages found."; exit 1; fi
step "150-line limit";                   bash "$ROOT/scripts/check-file-length.sh"
step "self-check (no model call)";       dotnet run --project src/ProcurementCopilot.Console --no-build -- --self-check

echo
echo "All verification steps passed."
