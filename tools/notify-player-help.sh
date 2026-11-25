#!/usr/bin/env bash
# Ping the human when playtest automation needs help (Grok Bot / X Chat / custom hook).
#
# Usage: ./tools/notify-player-help.sh "short message"
#
# Env:
#   GROK_HELP_CMD   — if set, invoked as: $GROK_HELP_CMD "message" (your Grok/SMS bridge)
#   NOTIFY_X_CHAT=1 — try X Chat DM to self when X plugin is authenticated (needs xchat_lite setup)
#
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
MSG="${1:-playtest needs help}"
STAMP="$(date -Is)"
INBOX="$ROOT/Assets/Playtest/player-help-inbox.txt"
mkdir -p "$(dirname "$INBOX")"
echo "[$STAMP] $MSG" >>"$INBOX"

if [[ -n "${GROK_HELP_CMD:-}" ]]; then
  "$GROK_HELP_CMD" "$MSG" && exit 0
fi

if command -v notify-send >/dev/null 2>&1 && [[ -n "${DISPLAY:-}" ]]; then
  notify-send "Terraforming Tendencies bot" "$MSG" 2>/dev/null || true
fi

if [[ "${NOTIFY_X_CHAT:-0}" == "1" ]] && [[ -x "${XCHAT_ROOT:-$HOME/xchat-lite}/.venv/bin/python" ]]; then
  echo "(NOTIFY_X_CHAT=1 set — wire send path in notify-player-help.sh when X Chat is unlocked.)" >&2
fi

echo "Help logged: $INBOX"
echo "$MSG"