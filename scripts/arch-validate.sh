#!/usr/bin/env bash
# PostToolUse hook: grep-based architecture validation.
# Exit code 2 blocks the agent until violations are fixed.
# stderr is the only feedback channel (additionalContext unsupported in PostToolUse).
#
# CRITICAL: strip $CLAUDE_PROJECT_DIR from absolute paths, same as inject-context.mjs.
# RATCHET RULE: only add a check when existing hits in the codebase are 0-2.
#   Test first: grep -rl 'pattern' src/ | wc -l

set -euo pipefail

INPUT=$(cat)
PROJECT_ROOT="${CLAUDE_PROJECT_DIR:-$(pwd)}"
ABS_FILE=$(echo "$INPUT" | jq -r '.tool_input.file_path // ""')
FILE="${ABS_FILE#"$PROJECT_ROOT/"}"
VIOLATIONS=""

[ -z "$FILE" ] && exit 0
[ ! -f "$ABS_FILE" ] && exit 0

# ─── customize per project ───────────────────────────────────────────────────

# Layer boundary: no server imports in frontend
# if [[ "$FILE" == src/frontend/* ]]; then
#   grep -q "@server/" "$ABS_FILE" && \
#     VIOLATIONS+="  - Frontend imports server layer (@server/)\n"
# fi

# Layer boundary: no frontend imports in server
# if [[ "$FILE" == src/server/* ]]; then
#   grep -q "@frontend/" "$ABS_FILE" && \
#     VIOLATIONS+="  - Server imports frontend layer (@frontend/)\n"
# fi

# Data access: direct DB calls outside repository files
# if [[ "$FILE" == src/server/* ]] && [[ "$FILE" != *-repo.ts ]] && [[ "$FILE" != *-repo/* ]]; then
#   grep -qE "\.(findMany|findUnique|create|update|delete|upsert)\(" "$ABS_FILE" && \
#     VIOLATIONS+="  - Direct DB call outside repository layer (use a *-repo.ts file)\n"
# fi

# Code quality: console.* outside tests and logger
# if [[ "$FILE" != *.test.* ]] && [[ "$FILE" != *logger* ]] && [[ "$FILE" != *logging* ]]; then
#   grep -qE "console\.(log|warn|error|info|debug)" "$ABS_FILE" && \
#     VIOLATIONS+="  - console.* outside tests/logger (use structured logger)\n"
# fi

# Code quality: export default outside framework routing files
# if [[ "$FILE" != src/app/* ]] && [[ "$FILE" != src/pages/* ]]; then
#   grep -q "^export default" "$ABS_FILE" && \
#     VIOLATIONS+="  - export default outside framework files (use named exports)\n"
# fi

# TypeScript: any instead of unknown
# if [[ "$FILE" == *.ts ]] || [[ "$FILE" == *.tsx ]]; then
#   grep -qE ": any[^;]|<any>" "$ABS_FILE" && \
#     VIOLATIONS+="  - 'any' type used (prefer 'unknown' and narrow)\n"
# fi

# ─────────────────────────────────────────────────────────────────────────────

if [ -n "$VIOLATIONS" ]; then
  echo -e "[arch-validate] Violations in ${FILE}:\n${VIOLATIONS}" >&2
  exit 2
fi

exit 0
