#!/usr/bin/env bash
# Fails when any .cs file under src/ or tests/ exceeds 150 lines (excluding using directives and blank lines).
set -euo pipefail
MAX=${1:-150}
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
failed=0
while IFS= read -r -d '' file; do
  count=$(grep -Ev '^\s*$|^\s*(global\s+)?using\s' "$file" | wc -l | tr -d ' ')
  if [ "$count" -gt "$MAX" ]; then
    echo "TOO LONG ($count lines): $file"
    failed=1
  fi
done < <(find "$ROOT/src" "$ROOT/tests" -name '*.cs' -not -path '*/bin/*' -not -path '*/obj/*' -print0)
if [ "$failed" -ne 0 ]; then exit 1; fi
echo "All .cs files are within $MAX lines."
