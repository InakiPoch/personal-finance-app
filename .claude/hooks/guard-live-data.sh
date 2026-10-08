#!/usr/bin/env bash
# PreToolUse(Bash): protect the live Docker stack (project "personal-finance", volume personal-finance_pf-data)
# and SQLite sidecar files. Scoped scratch projects (pf-*) are allowed.
cmd=$(jq -r '.tool_input.command // ""')
block() { echo "Blocked: $1. Ask the user first; run destructive Docker/DB steps only against a scratch project (COMPOSE_PROJECT_NAME=pf-scratch or -p pf-scratch)." >&2; exit 2; }

scoped='(-p|--project-name)[[:space:]=]+pf-|COMPOSE_PROJECT_NAME=pf-'
if [[ $cmd =~ docker[[:space:]]+compose[[:space:]].*(down|stop|rm|restart|kill) ]] && ! [[ $cmd =~ $scoped ]]; then
  block "docker compose down/stop/rm/restart on the default (live) project"
fi
if [[ $cmd =~ docker[[:space:]]+(stop|rm|kill|restart)[[:space:]] ]] \
  || [[ $cmd =~ docker[[:space:]]+(volume[[:space:]]+(rm|prune)|system[[:space:]]+prune) ]]; then
  block "docker stop/rm/kill/volume rm/prune"
fi
if [[ $cmd =~ rm[[:space:]].*(\.db-wal|\.db-shm|-wal|-shm) ]]; then
  block "removing a SQLite -wal/-shm file (checkpoint with sqlite3 first, or copy the db plus sidecars)"
fi
