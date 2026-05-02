#!/usr/bin/env bash
# DS-041 Validation Script
# Runs a reproducible validation pass: build, unit tests, Web tests (including
# Viewport-Matrix and UI-assertion checks). All output is captured so results
# can be referenced as CI artefacts.
#
# Usage:
#   bash scripts/validate-ds041.sh [--configuration <Debug|Release>]
#
# Output:
#   .validation-output/validate-ds041-<timestamp>.log  – full log
#   .validation-output/validate-ds041-<timestamp>.json – summary

set -euo pipefail

CONFIGURATION="Debug"
while [[ $# -gt 0 ]]; do
    case "$1" in
        --configuration) CONFIGURATION="$2"; shift 2 ;;
        *) echo "Unknown argument: $1" && exit 1 ;;
    esac
done

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
OUTPUT_DIR="$REPO_ROOT/.validation-output"
LOG_FILE="$OUTPUT_DIR/validate-ds041-$TIMESTAMP.log"
SUMMARY_FILE="$OUTPUT_DIR/validate-ds041-$TIMESTAMP.json"

mkdir -p "$OUTPUT_DIR"

log() { echo "$*" | tee -a "$LOG_FILE"; }
header() { log ""; log "=== $* ==="; }

log "DS-041 Validation – $(date -u)"
log "Configuration: $CONFIGURATION"
log "Repo: $REPO_ROOT"
log ""

PASS=0
FAIL=0
SKIPPED=0

run_step() {
    local NAME="$1"; shift
    header "$NAME"
    if "$@" 2>&1 | tee -a "$LOG_FILE"; then
        log "✓ $NAME – PASSED"
        PASS=$((PASS + 1))
    else
        log "✗ $NAME – FAILED"
        FAIL=$((FAIL + 1))
    fi
}

cd "$REPO_ROOT"

# Step 1: Restore
run_step "NuGet Restore" dotnet restore

# Step 2: Build
run_step "Build ($CONFIGURATION)" dotnet build --no-restore --configuration "$CONFIGURATION"

# Step 3: Domain Tests
run_step "Domain Tests" dotnet test tests/ddpc.DartSuite.Domain.Tests \
    --no-build --configuration "$CONFIGURATION" \
    --logger "console;verbosity=normal"

# Step 4: Application Tests
run_step "Application Tests" dotnet test tests/ddpc.DartSuite.Application.Tests \
    --no-build --configuration "$CONFIGURATION" \
    --logger "console;verbosity=normal"

# Step 5: Infrastructure Tests
run_step "Infrastructure Tests" dotnet test tests/ddpc.DartSuite.Infrastructure.Tests \
    --no-build --configuration "$CONFIGURATION" \
    --logger "console;verbosity=normal"

# Step 6: Web Component & UI Tests (includes MatchCard rendering matrix, viewport assertions,
#         badge tooltip checks, overflow-hidden regression, culture-invariant formatting)
run_step "Web Tests (UI Matrix)" dotnet test tests/ddpc.DartSuite.Web.Tests \
    --no-build --configuration "$CONFIGURATION" \
    --logger "console;verbosity=normal"

# Summary JSON
TOTAL=$((PASS + FAIL + SKIPPED))
STATUS=$([ "$FAIL" -eq 0 ] && echo "PASSED" || echo "FAILED")

cat > "$SUMMARY_FILE" <<JSON
{
  "timestamp": "$TIMESTAMP",
  "configuration": "$CONFIGURATION",
  "status": "$STATUS",
  "steps": { "total": $TOTAL, "passed": $PASS, "failed": $FAIL, "skipped": $SKIPPED },
  "logFile": "$LOG_FILE"
}
JSON

header "Summary"
log "Status  : $STATUS"
log "Passed  : $PASS / $TOTAL"
log "Failed  : $FAIL / $TOTAL"
log "Log     : $LOG_FILE"
log "Summary : $SUMMARY_FILE"

exit "$FAIL"
