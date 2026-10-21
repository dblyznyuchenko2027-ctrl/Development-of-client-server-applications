#!/usr/bin/env bash
# Quick check of stage 1. Start the API first: dotnet run --project src/TaskTracker.Api
set -u
BASE="${BASE_URL:-http://localhost:5080}"
fail=0

check() { # name expected actual
  if [ "$2" = "$3" ]; then echo "PASS  $1 ($3)"; else echo "FAIL  $1 (expected $2, got $3)"; fail=1; fi
}

code() { curl -s -o /dev/null -w '%{http_code}' "$@"; }

check "GET /health"                200 "$(code "$BASE/health")"
check "GET /api/projects"          200 "$(code "$BASE/api/projects")"
check "GET /api/projects/1"        200 "$(code "$BASE/api/projects/1")"
check "GET /api/projects/999999"   404 "$(code "$BASE/api/projects/999999")"
check "POST valid project"         201 "$(code -X POST "$BASE/api/projects" -H 'Content-Type: application/json' -d '{"name":"Smoke","ownerId":2}')"
check "POST invalid payload"       400 "$(code -X POST "$BASE/api/projects" -H 'Content-Type: application/json' -d '{"name":"","ownerId":0}')"
check "POST unknown owner"         400 "$(code -X POST "$BASE/api/projects" -H 'Content-Type: application/json' -d '{"name":"X","ownerId":999999}')"

exit $fail
