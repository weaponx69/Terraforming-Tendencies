#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

rename_pair() {
  local from="$1"
  local to="$2"
  [[ -e "$from" ]] || return 0
  [[ -e "$to" ]] && return 0
  if git ls-files --error-unmatch "$from" &>/dev/null; then
    git mv "$from" "$to"
  else
    mv "$from" "$to"
    [[ -e "${from}.meta" ]] && mv "${from}.meta" "${to}.meta"
  fi
}

while IFS= read -r -d '' f; do
  dir=$(dirname "$f")
  base=$(basename "$f")
  newbase="${base//MainGame/MainGame}"
  [[ "$base" == "$newbase" ]] && continue
  rename_pair "$f" "$dir/$newbase"
done < <(find Assets docs tools .cursor -type f -name '*MainGame*' -print0 2>/dev/null)

while IFS= read -r -d '' f; do
  dir=$(dirname "$f")
  base=$(basename "$f")
  newbase=$(echo "$base" | sed 's/prologue/main-game/g')
  [[ "$base" == "$newbase" ]] && continue
  rename_pair "$f" "$dir/$newbase"
done < <(find docs tools .cursor -type f -name '*prologue*' -print0 2>/dev/null)

export LC_ALL=C
while IFS= read -r -d '' f; do
  grep -q 'MainGame\|prologue-ui\|Scripts/MainGame' "$f" 2>/dev/null || continue
  sed -i \
    -e 's/GameDevTV\.RTS\.MainGame/GameDevTV.RTS.MainGame/g' \
    -e 's/MainGame/MainGame/g' \
    -e 's|Scripts/MainGame|Scripts/MainGame|g' \
    -e 's|/MainGame/|/MainGame/|g' \
    -e 's/main-game-ui-playthrough/main-game-ui-playthrough/g' \
    -e 's/main-game-perf-scenario/main-game-perf-scenario/g' \
    -e 's/main-game-fps-probe/main-game-fps-probe/g' \
    -e 's/main-game-improve-until-win/main-game-improve-until-win/g' \
    -e 's/main-game-playtest/main-game-playtest/g' \
  "$f"
done < <(find Assets docs tools .cursor AGENTS.md PROJECT_KNOWLEDGE.md GDD.md \
  -type f \( -name '*.cs' -o -name '*.md' -o -name '*.mdc' -o -name '*.sh' -o -name '*.asset' -o -name '*.unity' -o -name '*.prefab' \) \
  ! -path '*/Packages/*' -print0 2>/dev/null)

echo "Finish pass complete."