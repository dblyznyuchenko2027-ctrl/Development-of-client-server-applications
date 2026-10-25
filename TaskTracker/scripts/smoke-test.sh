#!/usr/bin/env bash
# Black-box check of stages 1-3. Start the API first: dotnet run --project src/TaskTracker.Api
# Requires: bash, curl, sed. The API must run in Development (seed data + X-Db-Query-Count header).
set -u
BASE="${BASE_URL:-http://localhost:5080}"
fail=0
STATUS=""; BODY=""; CT=""

pass() { echo "PASS  $1"; }
bad()  { echo "FAIL  $1"; fail=1; }

check() { # name expected actual
  if [ "$2" = "$3" ]; then pass "$1 ($3)"; else bad "$1 (expected $2, got $3)"; fi
}

contains() { # name needle haystack
  case "$3" in *"$2"*) pass "$1" ;; *) bad "$1 (missing '$2')" ;; esac
}

# req METHOD PATH [JSON] -> sets STATUS, CT, BODY
req() {
  local method="$1" path="$2" data="${3-}" out
  if [ -n "$data" ]; then
    out=$(curl -s -w $'\n%{http_code}\n%{content_type}' -X "$method" "$BASE$path" -H 'Content-Type: application/json' -d "$data")
  else
    out=$(curl -s -w $'\n%{http_code}\n%{content_type}' -X "$method" "$BASE$path")
  fi
  CT=$(printf '%s\n' "$out" | tail -n 1)
  STATUS=$(printf '%s\n' "$out" | tail -n 2 | head -n 1)
  BODY=$(printf '%s\n' "$out" | sed '$d' | sed '$d')
}

id_of() { printf '%s' "$1" | sed -n 's/^{"id":\([0-9]*\).*/\1/p'; }

query_count() { # path -> value of X-Db-Query-Count (empty if header absent)
  curl -s -D - -o /dev/null "$BASE$1" | tr -d '\r' | sed -n 's/^[Xx]-[Dd]b-[Qq]uery-[Cc]ount: *//p'
}

echo "== Stage 1 =="
req GET /health;                 check "GET /health" 200 "$STATUS"

echo "== Pagination / filtering / sorting =="
req GET "/api/projects";                       check "GET /api/projects" 200 "$STATUS"
req GET "/api/tasks?pageSize=10&page=2";       check "GET /api/tasks page 2" 200 "$STATUS"
contains "paged envelope has totalPages" '"totalPages"' "$BODY"
req GET "/api/tasks?status=Done&priority=High"; check "filter by status+priority" 200 "$STATUS"
req GET "/api/tasks?sort=-dueDate,title";      check "sort desc dueDate, title" 200 "$STATUS"
req GET "/api/tasks?search=demo&pageSize=3";   check "search" 200 "$STATUS"
req GET "/api/users?role=Admin";               check "GET /api/users?role=Admin" 200 "$STATUS"
req GET "/api/tasks?sort=password";            check "unknown sort field -> 400" 400 "$STATUS"
req GET "/api/tasks?page=0&pageSize=1000";     check "invalid paging -> 400" 400 "$STATUS"
req GET "/api/tasks?status=Nope";              check "invalid enum filter -> 400" 400 "$STATUS"
req GET "/api/tasks?dueFrom=2030-01-01&dueTo=2020-01-01"; check "dueFrom > dueTo -> 400" 400 "$STATUS"

echo "== Errors are RFC 7807 =="
req GET /api/tasks/999999
check "GET unknown task -> 404" 404 "$STATUS"
contains "404 content type" "application/problem+json" "$CT"
req GET /api/nope;               check "unknown route -> 404" 404 "$STATUS"
contains "unknown route is problem+json" "application/problem+json" "$CT"
req DELETE /api/projects;        check "wrong method -> 405" 405 "$STATUS"
req POST /api/projects '{"name":"","ownerId":0}'
check "invalid project -> 400" 400 "$STATUS"
contains "400 content type" "application/problem+json" "$CT"
contains "400 has errors map (camelCase)" '"ownerId"' "$BODY"
req POST /api/projects '{"name":"X","ownerId":999999}'; check "unknown owner -> 400" 400 "$STATUS"
req POST /api/projects '{not json';                     check "malformed JSON -> 400" 400 "$STATUS"
req POST /api/projects/1/tasks '{"title":"T","priority":"Urgent"}'; check "unknown enum in body -> 400" 400 "$STATUS"

echo "== Users CRUD =="
EMAIL="smoke.$(date +%s)@example.com"
req POST /api/users "{\"email\":\"$EMAIL\",\"displayName\":\"Smoke\"}"
check "POST user" 201 "$STATUS"; USER_ID=$(id_of "$BODY")
req POST /api/users "{\"email\":\"$EMAIL\",\"displayName\":\"Dup\"}"; check "duplicate e-mail -> 409" 409 "$STATUS"
req GET "/api/users/$USER_ID";   check "GET user" 200 "$STATUS"
req PUT "/api/users/$USER_ID" "{\"email\":\"$EMAIL\",\"displayName\":\"Smoke 2\",\"role\":\"Member\"}"
check "PUT user" 200 "$STATUS"
req PUT "/api/users/$USER_ID" '{"email":"admin@example.com","displayName":"X","role":"Member"}'
check "PUT user with taken e-mail -> 409" 409 "$STATUS"

echo "== Projects CRUD =="
req POST /api/projects '{"name":"Smoke project","description":"tmp","ownerId":2}'
check "POST project" 201 "$STATUS"; PROJECT_ID=$(id_of "$BODY")
contains "nested members in project" '"members"' "$BODY"
req GET "/api/projects/$PROJECT_ID";                                   check "GET project" 200 "$STATUS"
req PUT "/api/projects/$PROJECT_ID" '{"name":"Smoke project v2"}';     check "PUT project" 200 "$STATUS"
req PUT "/api/projects/$PROJECT_ID/members/$USER_ID";                  check "add member" 204 "$STATUS"
req PUT "/api/projects/$PROJECT_ID/members/$USER_ID";                  check "add member twice (idempotent)" 204 "$STATUS"
req DELETE "/api/projects/$PROJECT_ID/members/2";                      check "remove owner -> 409" 409 "$STATUS"

echo "== Tasks CRUD =="
req POST "/api/projects/$PROJECT_ID/tasks" "{\"title\":\"Smoke task\",\"priority\":\"High\",\"assigneeId\":$USER_ID}"
check "POST task" 201 "$STATUS"; TASK_ID=$(id_of "$BODY")
req POST "/api/projects/$PROJECT_ID/tasks" '{"title":"Not a member","assigneeId":1}'
check "assignee not a member -> 422" 422 "$STATUS"
req POST "/api/projects/$PROJECT_ID/tasks" '{"title":"Past","dueDate":"2000-01-01"}'
check "due date in the past -> 400" 400 "$STATUS"
req POST /api/projects/999999/tasks '{"title":"Orphan"}';              check "task in unknown project -> 404" 404 "$STATUS"
req GET "/api/tasks/$TASK_ID";                                         check "GET task" 200 "$STATUS"
req PUT "/api/tasks/$TASK_ID" "{\"title\":\"Smoke task v2\",\"status\":\"InProgress\",\"priority\":\"Low\",\"assigneeId\":$USER_ID}"
check "PUT task" 200 "$STATUS"
req PUT "/api/tasks/$TASK_ID" '{"title":"No status"}';                 check "PUT task without status -> 400" 400 "$STATUS"
req PATCH "/api/tasks/$TASK_ID/status" '{"status":"Done"}';            check "PATCH task status" 200 "$STATUS"
req GET "/api/projects/$PROJECT_ID/tasks?status=Done";                 check "nested task list" 200 "$STATUS"
req DELETE "/api/projects/$PROJECT_ID/members/$USER_ID";               check "remove member with tasks -> 409" 409 "$STATUS"

echo "== Comments CRUD =="
req POST "/api/tasks/$TASK_ID/comments" "{\"text\":\"Hello\",\"authorId\":$USER_ID}"
check "POST comment" 201 "$STATUS"; COMMENT_ID=$(id_of "$BODY")
req POST "/api/tasks/$TASK_ID/comments" '{"text":"","authorId":1}';    check "empty comment -> 400" 400 "$STATUS"
req GET "/api/tasks/$TASK_ID/comments";                                check "list comments" 200 "$STATUS"
req PUT "/api/comments/$COMMENT_ID" '{"text":"Edited"}';               check "PUT comment" 200 "$STATUS"
req DELETE "/api/users/$USER_ID";                                      check "delete user with comments -> 409" 409 "$STATUS"
req DELETE "/api/comments/$COMMENT_ID";                                check "DELETE comment" 204 "$STATUS"
req GET "/api/comments/$COMMENT_ID";                                   check "deleted comment -> 404" 404 "$STATUS"

echo "== Stage 3: file attachments =="
TMP_DIR=$(mktemp -d); trap 'rm -rf "$TMP_DIR"' EXIT
printf '\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR' > "$TMP_DIR/pixel.png"
printf 'hello attachment\n' > "$TMP_DIR/notes.txt"
printf 'MZ\x90\x00\x03\x00' > "$TMP_DIR/setup.exe"
printf 'MZ\x90\x00\x03\x00' > "$TMP_DIR/fake.png"
head -c $((5 * 1024 * 1024 + 1)) /dev/zero > "$TMP_DIR/huge.txt"

# upload FILE_PATH FILE_NAME MIME [TASK] -> sets STATUS, CT, BODY
upload() {
  local out
  out=$(curl -s -w $'\n%{http_code}\n%{content_type}' -X POST "$BASE/api/tasks/${4:-$TASK_ID}/attachments" -F "file=@$1;filename=$2;type=$3")
  CT=$(printf '%s\n' "$out" | tail -n 1)
  STATUS=$(printf '%s\n' "$out" | tail -n 2 | head -n 1)
  BODY=$(printf '%s\n' "$out" | sed '$d' | sed '$d')
}

upload "$TMP_DIR/pixel.png" pixel.png image/png;  check "upload png" 201 "$STATUS"; ATT_ID=$(id_of "$BODY")
contains "upload response has file name" '"fileName":"pixel.png"' "$BODY"
upload "$TMP_DIR/notes.txt" notes.txt text/plain; check "upload txt" 201 "$STATUS"; ATT2_ID=$(id_of "$BODY")
upload "$TMP_DIR/setup.exe" setup.exe application/octet-stream; check "exe -> 415" 415 "$STATUS"
contains "415 is problem+json" "application/problem+json" "$CT"
upload "$TMP_DIR/fake.png" fake.png image/png;    check "exe renamed to png -> 415" 415 "$STATUS"
upload "$TMP_DIR/huge.txt" huge.txt text/plain;   check "file over 5 MiB -> 413" 413 "$STATUS"
upload "$TMP_DIR/pixel.png" pixel.png image/png 999999; check "upload to unknown task -> 404" 404 "$STATUS"
req GET "/api/tasks/$TASK_ID/attachments";        check "list attachments" 200 "$STATUS"
req GET "/api/tasks/$TASK_ID";                    contains "task details list attachments" '"notes.txt"' "$BODY"
curl -s -o "$TMP_DIR/downloaded.txt" -D "$TMP_DIR/headers.txt" "$BASE/api/attachments/$ATT2_ID/download"
if cmp -s "$TMP_DIR/notes.txt" "$TMP_DIR/downloaded.txt"; then pass "downloaded file is identical"; else bad "downloaded file differs"; fi
contains "download content type" "text/plain" "$(tr -d '\r' < "$TMP_DIR/headers.txt")"
req DELETE "/api/attachments/$ATT_ID";            check "DELETE attachment" 204 "$STATUS"
req GET "/api/attachments/$ATT_ID/download";      check "deleted attachment -> 404" 404 "$STATUS"

echo "== Cleanup =="
req DELETE "/api/tasks/$TASK_ID";                                      check "DELETE task" 204 "$STATUS"
req DELETE "/api/projects/$PROJECT_ID/members/$USER_ID";               check "remove member" 204 "$STATUS"
req DELETE "/api/projects/$PROJECT_ID";                                check "DELETE project" 204 "$STATUS"
req GET "/api/projects/$PROJECT_ID";                                   check "deleted project -> 404" 404 "$STATUS"
req DELETE "/api/users/$USER_ID";                                      check "DELETE user" 204 "$STATUS"
req DELETE /api/users/2;                                               check "delete project owner -> 409" 409 "$STATUS"

echo "== Stage 3: OpenAPI / Swagger (Development) =="
req GET /swagger/v1/swagger.json;                 check "GET swagger.json" 200 "$STATUS"
contains "openapi 3" '"openapi": "3.' "$BODY"
contains "attachments upload is documented" '/api/tasks/{taskId}/attachments' "$BODY"
req GET /swagger/index.html;                      check "Swagger UI page" 200 "$STATUS"

echo "== N+1 guard (Development only: X-Db-Query-Count header) =="
guard() { # name path max
  local n; n=$(query_count "$2")
  if [ -z "$n" ]; then echo "SKIP  $1 (no X-Db-Query-Count header; run in Development)"; return; fi
  if [ "$n" -le "$3" ]; then pass "$1: $n SQL commands (limit $3)"; else bad "$1: $n SQL commands (limit $3) -> N+1?"; fi
}
guard "GET /api/tasks?pageSize=100"             "/api/tasks?pageSize=100"             2
guard "GET /api/projects?pageSize=100"          "/api/projects?pageSize=100"          2
guard "GET /api/users?pageSize=100"             "/api/users?pageSize=100"             2
guard "GET /api/tasks/3/comments"               "/api/tasks/3/comments"               2
guard "GET /api/tasks/3 (nested details)"       "/api/tasks/3"                        4
guard "GET /api/projects/1 (nested details)"    "/api/projects/1"                     4

exit $fail
