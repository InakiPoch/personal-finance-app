#!/usr/bin/env bash
# Projects the committed HEAD tree into the release tree that lands on `main`:
# allowlist (git archive) -> strip -> leak guard. Fails non-zero on any violation.
set -eu

out="${1:-}"
[ -n "$out" ] || { echo "usage: $0 <out-dir>" >&2; exit 2; }

cd "$(git rev-parse --show-toplevel)"

if [ -n "$(git status --porcelain)" ]; then
  echo "PROJECT FAIL: working tree is dirty; this script projects HEAD, commit first" >&2
  exit 1
fi

if [ -e "$out" ] && [ -n "$(ls -A "$out" 2>/dev/null)" ]; then
  echo "PROJECT FAIL: '$out' exists and is not empty" >&2
  exit 1
fi
mkdir -p "$out"

git archive HEAD -- \
  Dockerfile \
  .dockerignore \
  compose.yaml \
  README.md \
  LICENSE \
  .gitignore \
  app/api/global.json \
  app/api/Directory.Build.props \
  app/api/Directory.Packages.props \
  app/api/src \
  app/client/package.json \
  app/client/pnpm-lock.yaml \
  app/client/pnpm-workspace.yaml \
  app/client/angular.json \
  app/client/tsconfig.json \
  app/client/tsconfig.app.json \
  app/client/.postcssrc.json \
  app/client/public \
  app/client/src \
  | tar -x -C "$out"

find "$out/app/client/src" -type f -name '*.spec.ts' -exec rm -f {} +
find "$out/app/api/src" -type f -name '*.http' -exec rm -f {} +

forbidden='(^|/)tests?/|(^|/)docs/|(^|/)\.claude/|(^|/)\.github/|(^|/)(CLAUDE|TASK|AGENTS)\.md$|\.spec\.ts$|\.http$|\.Tests\.csproj$|\.sln$|\.db(-wal|-shm|-journal)?$|\.bak$|(^|/)\.env$|(^|/)node_modules/|(^|/)(bin|obj)/'

paths=$(cd "$out" && find . -type f | sed 's|^\./||' | sort)
leaks=$(printf '%s\n' "$paths" | grep -E "$forbidden" || true)
big=$(find "$out" -type f -size +5242880c | sed "s|^$out/||" || true)

if [ -n "$leaks" ] || [ -n "$big" ]; then
  echo "PROJECT FAIL: leak guard tripped" >&2
  [ -z "$leaks" ] || { echo "forbidden paths:" >&2; printf '%s\n' "$leaks" | sed 's/^/  /' >&2; }
  [ -z "$big" ] || { echo "files over 5 MB:" >&2; printf '%s\n' "$big" | sed 's/^/  /' >&2; }
  exit 1
fi

echo "PROJECT OK: $(printf '%s\n' "$paths" | wc -l | tr -d ' ') files in $out"
ls -A "$out"
