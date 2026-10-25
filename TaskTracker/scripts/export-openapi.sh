#!/usr/bin/env bash
# Saves the generated OpenAPI document to docs/openapi.json.
# Start the API first (Development): dotnet run --project src/TaskTracker.Api
set -euo pipefail
BASE="${BASE_URL:-http://localhost:5080}"
OUT="$(dirname "$0")/../docs/openapi.json"
curl -fsS "$BASE/swagger/v1/swagger.json" -o "$OUT"
echo "Saved $OUT"
