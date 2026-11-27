#!/usr/bin/env bash
# One-time migration: MainGame* → MainGame* (types, folder, docs, tools).
# Preserves .meta GUIDs via git mv. Excludes Packages/.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

if [[ ! -d Assets/Scripts/MainGame ]]; then
  echo "Already migrated (no Assets/Scripts/MainGame)?" >&2
  exit 0
fi

echo "== Move script folder =="
git mv Assets/Scripts/MainGame Assets/Scripts/MainGame

echo "== Rename MainGame* files (keep .meta pairs) =="
while IFS= read -r -d '' f; do
  dir=$(dirname "$f")
  base=$(basename "$f")
  newbase="${base//MainGame/MainGame}"
  [[ "$base" == "$newbase" ]] && continue
  if [[ -e "$dir/$newbase" ]]; then
    echo "skip exists: $dir/$newbase" >&2
    continue
  fi
  git mv "$f" "$dir/$newbase"
done < <(find Assets docs tools .cursor AGENTS.md PROJECT_KNOWLEDGE.md GDD.md -type f -name '*MainGame*' -print0 2>/dev/null || true)

while IFS= read -r -d '' f; do
  dir=$(dirname "$f")
  base=$(basename "$f")
  newbase=$(echo "$base" | sed 's/prologue/main-game/g')
  [[ "$base" == "$newbase" ]] && continue
  if [[ -e "$dir/$newbase" ]]; then
    echo "skip exists: $dir/$newbase" >&2
    continue
  fi
  git mv "$f" "$dir/$newbase"
done < <(find docs tools .cursor -type f -name '*prologue*' -print0 2>/dev/null || true)

echo "== Text replace in project files =="
export LC_ALL=C
while IFS= read -r -d '' f; do
  sed -i \
    -e 's/GameDevTV\.RTS\.MainGame/GameDevTV.RTS.MainGame/g' \
    -e 's/MainGame/MainGame/g' \
  "$f"
done < <(find Assets docs tools .cursor AGENTS.md PROJECT_KNOWLEDGE.md GDD.md \
  -type f \( -name '*.cs' -o -name '*.md' -o -name '*.mdc' -o -name '*.sh' -o -name '*.asset' -o -name '*.unity' -o -name '*.prefab' -o -name '*.json' -o -name '*.yml' -o -name '*.yaml' \) \
  ! -path '*/Packages/*' -print0 2>/dev/null)

echo "== Lowercase path tokens (harness / doc slugs) =="
while IFS= read -r -d '' f; do
  sed -i \
    -e 's|Scripts/MainGame|Scripts/MainGame|g' \
    -e 's|/MainGame/|/MainGame/|g' \
    -e 's/main-game-ui-playthrough/main-game-ui-playthrough/g' \
    -e 's/main-game-perf-scenario/main-game-perf-scenario/g' \
    -e 's/main-game-fps-probe/main-game-fps-probe/g' \
    -e 's/main-game-improve-until-win/main-game-improve-until-win/g' \
    -e 's/main-game-playtest/main-game-playtest/g' \
    -e 's/prologue-work/main game work/g' \
    -e 's/prologue-only/main-game-only/g' \
  "$f"
done < <(find Assets docs tools .cursor AGENTS.md PROJECT_KNOWLEDGE.md GDD.md \
  -type f \( -name '*.cs' -o -name '*.md' -o -name '*.mdc' -o -name '*.sh' \) \
  ! -path '*/Packages/*' -print0 2>/dev/null)

echo "Done. Request Unity recompile and run tests."