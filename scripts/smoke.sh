#!/usr/bin/env bash
# Release smoke test: builds the image, starts it, and checks static hosting, SPA fallback,
# routing, SQLite creation + migrations, volume persistence and volume reset.
# Usage: scripts/smoke.sh [compose-dir]   (default: current directory)
# Env:   BASE_URL (default http://localhost:8080), CURL_OPTS (e.g. -k for self-signed HTTPS),
#        COMPOSE_PROJECT_NAME (default pf-smoke, so the user's real volume is never touched).
set -u

cd "${1:-.}" || { echo "SMOKE FAIL: cannot cd to '${1:-.}'"; exit 1; }
export COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-pf-smoke}"
BASE_URL="${BASE_URL:-http://localhost:8080}"
CURL_OPTS="${CURL_OPTS:-}"

cleanup() {
  status=$?
  [ "$status" -ne 0 ] && docker compose logs --tail 200 app
  docker compose down -v >/dev/null 2>&1
  [ -n "${FAILURE:-}" ] && echo "$FAILURE"
}
trap cleanup EXIT

fail() { FAILURE="SMOKE FAIL: $1"; echo "$FAILURE"; exit 1; }

check() {
  desc=$1; shift
  if "$@"; then echo "ok   $desc"; else fail "$desc"; fi
}

body() { curl -s $CURL_OPTS "$BASE_URL$1"; }
status() { curl -s -o /dev/null -w '%{http_code}' $CURL_OPTS "$@"; }
contains() { printf '%s' "$1" | grep -q "$2"; }

wait_healthy() {
  for _ in $(seq 1 90); do
    curl -sf $CURL_OPTS "$BASE_URL/health" >/dev/null && return 0
    sleep 2
  done
  return 1
}

docker compose down -v >/dev/null 2>&1

check "docker compose up --build" docker compose up -d --build
check "/health becomes healthy (<=180s)" wait_healthy
check "GET / serves the SPA" contains "$(body /)" '<app-root'
check "GET /reports falls back to SPA" contains "$(body /reports)" '<app-root'
check "GET /v1/does-not-exist is 404" test "$(status "$BASE_URL/v1/does-not-exist")" = 404
check "GET /v1/instruments is empty" contains "$(body /v1/instruments)" '"rows":\[\]'
check "POST /v1/instruments is 201" \
  test "$(status -X POST -H 'Content-Type: application/json' \
    -d '{"type":"debit","name":"Checking"}' "$BASE_URL/v1/instruments")" = 201

docker compose down >/dev/null 2>&1
check "docker compose up (same volume)" docker compose up -d
check "/health healthy after restart" wait_healthy
check "instrument persisted" contains "$(body /v1/instruments)" '"name":"Checking"'

docker compose down -v >/dev/null 2>&1 
check "docker compose up (fresh volume)" docker compose up -d
check "/health healthy after reset" wait_healthy
check "instruments reset to empty" contains "$(body /v1/instruments)" '"rows":\[\]'

echo "SMOKE OK"
