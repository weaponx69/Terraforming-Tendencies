#!/usr/bin/env bash
# Automated ClickOrderDiagnostic matrix (Play Mode). Exercises real Prologue survey paint orders.
#
# Usage:
#   1. Press Play on Assets/Scenes/Terraforming-Tendencies.unity (Prologue active recommended)
#   2. SKIP_ENTER_PLAY=1 ./tools/click-order-diagnostic.sh
#
# Env:
#   FPS_SAMPLE_COUNT=30        — live FPS samples per phase (default 30)
#   FPS_SAMPLE_INTERVAL_SEC=2  — seconds between samples (default 2; use 2+ when FPS &lt; 15)
#   BASELINE_SETTLE_SEC=6      — seconds before baseline FPS read
#   SKIP_WARMUP=1              — skip C# warm-up when board is already loaded (7–9 FPS)
#
set -euo pipefail
FPS_SAMPLE_COUNT="${FPS_SAMPLE_COUNT:-30}"
FPS_SAMPLE_INTERVAL_SEC="${FPS_SAMPLE_INTERVAL_SEC:-2}"
BASELINE_SETTLE_SEC="${BASELINE_SETTLE_SEC:-6}"
SKIP_WARMUP="${SKIP_WARMUP:-0}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
OUT_DIR="${OUT_DIR:-Assets/Playtest/click-order-diagnostic-$(date +%Y%m%d-%H%M%S)}"
mkdir -p "$OUT_DIR"

command -v unity >/dev/null || { echo "unity CLI not found"; exit 1; }
command -v python3 >/dev/null || { echo "python3 required"; exit 1; }

eval_result() {
  python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('data',{}).get('result',{}).get('result',''))" 2>/dev/null || true
}

playing="$(unity command eval 'return UnityEditor.EditorApplication.isPlaying;' --json --timeout 30 2>/dev/null | eval_result)"
if [[ "${SKIP_ENTER_PLAY:-0}" != "1" && "$playing" != "True" ]]; then
  unity command eval 'UnityEditor.EditorApplication.isPlaying=true; return "play";' --json --timeout 120 >/dev/null || true
  for _ in $(seq 1 90); do
    playing="$(unity command eval 'return UnityEditor.EditorApplication.isPlaying;' --json --timeout 20 2>/dev/null | eval_result)"
    [[ "$playing" == "True" ]] && break
    sleep 2
  done
fi

playing="$(unity command eval 'return UnityEditor.EditorApplication.isPlaying;' --json --timeout 20 2>/dev/null | eval_result)"
if [[ "$playing" != "True" ]]; then
  echo "Editor is not in Play Mode. Open the scene and press Play, or unset SKIP_ENTER_PLAY."
  exit 2
fi

echo "Baseline FPS (${BASELINE_SETTLE_SEC}s settle)..."
sleep "$BASELINE_SETTLE_SEC"
fps_before="$(unity command eval 'return GameDevTV.RTS.Prologue.PrologueFpsMonitor.LastSecondFps;' --json --timeout 45 2>/dev/null | eval_result)"
fps_before="${fps_before:-0}"

if [[ "$SKIP_WARMUP" == "1" ]]; then
  echo "Step 1 — SKIP_WARMUP=1 (measuring current burdened Play Mode as-is)..."
  echo '{"step":"warmup","skipped":true,"note":"board_already_burdened"}' > "$OUT_DIR/warmup.json"
else
  echo "Step 1 — gameplay warm-up simulation (4 survey legs, buildings, trails)..."
  warmup_raw="$(unity command eval 'return GameDevTV.RTS.Diagnostics.ClickOrderDiagnosticAutomation.RunWarmupOnly();' --json --timeout 90 2>/dev/null | eval_result)"
  if [[ -z "$warmup_raw" ]]; then
    echo "Warm-up eval failed or timed out — set SKIP_WARMUP=1 if Play Mode is already under load." >&2
    exit 6
  fi
  echo "$warmup_raw" > "$OUT_DIR/warmup.json"
fi

sample_fps() {
  local out="$1"
  local stats_out="$2"
  : > "$out"
  echo "  Sampling ${FPS_SAMPLE_COUNT}× every ${FPS_SAMPLE_INTERVAL_SEC}s → $out"
  for _ in $(seq 1 "$FPS_SAMPLE_COUNT"); do
    v="$(unity command eval 'return GameDevTV.RTS.Prologue.PrologueFpsMonitor.LastSecondFps;' --json --timeout 45 2>/dev/null | eval_result)"
    v="${v:-0}"
    echo "$v" >> "$out"
    sleep "$FPS_SAMPLE_INTERVAL_SEC"
  done
  python3 - "$out" "$stats_out" <<'PY'
import json, statistics, pathlib, sys
out, stats_out = sys.argv[1], sys.argv[2]
vals = [int(x) for x in open(out) if x.strip().isdigit()]
if not vals:
    pathlib.Path(stats_out).write_text("{}")
    raise SystemExit(0)
s = sorted(vals)

def pct(p):
    i = max(0, min(len(s) - 1, int(round(p / 100 * (len(s) - 1)))))
    return s[i]

stats = {
    "count": len(vals),
    "min": min(vals),
    "max": max(vals),
    "mean": round(statistics.mean(vals), 2),
    "median": int(round(statistics.median(vals))),
    "p10": pct(10),
    "p90": pct(90),
    "samples": vals,
}
pathlib.Path(stats_out).write_text(json.dumps(stats))
PY
}

fps_stats_median() {
  python3 -c "import json; print(int(json.load(open('$1')).get('median',0)))" 2>/dev/null || echo 0
}

echo "Step 2 — live FPS after warm-up..."
sample_fps "$OUT_DIR/fps-after-warmup.txt" "$OUT_DIR/fps-after-warmup-stats.json"
fps_after_warmup="$(fps_stats_median "$OUT_DIR/fps-after-warmup-stats.json")"
fps_after_warmup="${fps_after_warmup:-0}"

echo "Step 3 — stress matrix P1–P3 on burdened board..."
raw="$(unity command eval 'return GameDevTV.RTS.Diagnostics.ClickOrderDiagnosticAutomation.RunStressMatrixOnly();' --json --timeout 90 2>/dev/null | eval_result)"

json="${raw#*|}"
if [[ -z "$json" || "$json" == "$raw" ]]; then
  json="$raw"
fi
printf '%s' "$json" > "$OUT_DIR/matrix-step.json"

export OUT_DIR FPS_BEFORE="$fps_before" FPS_AFTER_WARMUP="$fps_after_warmup"
python3 <<'PY'
import json, os, pathlib

out = pathlib.Path(os.environ["OUT_DIR"])
warm = json.loads((out / "warmup.json").read_text()) if (out / "warmup.json").is_file() else {}
mat = json.loads((out / "matrix-step.json").read_text()) if (out / "matrix-step.json").is_file() else {}
merged = dict(mat)
if "warmup" in warm:
    merged["warmup"] = warm["warmup"]
merged["fps_before_baseline"] = int(os.environ.get("FPS_BEFORE", "0") or 0)
merged["fps_live_after_warmup_median"] = int(os.environ.get("FPS_AFTER_WARMUP", "0") or 0)
merged["fps_before_matrix"] = merged["fps_live_after_warmup_median"]
warm_stats = out / "fps-after-warmup-stats.json"
if warm_stats.is_file():
    merged["fps_live_after_warmup"] = json.loads(warm_stats.read_text())
    merged["fps_sample_count"] = merged["fps_live_after_warmup"].get("count")
(out / "matrix.json").write_text(json.dumps(merged))
PY
cp -f "$OUT_DIR/matrix.json" Assets/Playtest/click-order-diagnostic-report.json 2>/dev/null || true

echo "Step 4 — live FPS after matrix..."
sample_fps "$OUT_DIR/fps-after-matrix.txt" "$OUT_DIR/fps-after-matrix-stats.json"
fps_after="$(fps_stats_median "$OUT_DIR/fps-after-matrix-stats.json")"
fps_after="${fps_after:-0}"

export OUT_DIR FPS_BEFORE="$fps_before" FPS_AFTER="$fps_after" FPS_AFTER_WARMUP="$fps_after_warmup"
python3 <<'PY'
import json, os, pathlib
out = pathlib.Path(os.environ["OUT_DIR"])
p = out / "matrix.json"
if p.is_file():
    data = json.loads(p.read_text())
    data["fps_live_after_matrix_median"] = int(os.environ.get("FPS_AFTER", "0") or 0)
    data["fps_after_matrix"] = data["fps_live_after_matrix_median"]
    mat_stats = out / "fps-after-matrix-stats.json"
    if mat_stats.is_file():
        data["fps_live_after_matrix"] = json.loads(mat_stats.read_text())
    p.write_text(json.dumps(data))
PY
python3 <<'PY'
import json, pathlib, os

out = pathlib.Path(os.environ["OUT_DIR"])
fps_before = int(os.environ.get("FPS_BEFORE", "0") or 0)
fps_after = int(os.environ.get("FPS_AFTER", "0") or 0)
matrix_path = out / "matrix.json"
if not matrix_path.is_file():
    matrix_path = pathlib.Path("Assets/Playtest/click-order-diagnostic-report.json")
data = json.loads(matrix_path.read_text())
fps_after_warmup = int(data.get("fps_live_after_warmup_median", 0) or os.environ.get("FPS_AFTER_WARMUP", "0") or 0)
cases = data.get("cases", [])
kind = data.get("matrixKind", "legacy")

def verdict(c):
    ok = c.get("success", True)
    spike = float(c.get("maxDispatchSpikeMs", 0))
    click = float(c.get("clickFrameDispatchMs", 0))
    total = float(c.get("totalWallMs", click))
    if not ok:
        return f"FAIL — {c.get('error', 'unknown')}"
    if spike > 16.0:
        return f"SPIKE — max frame {spike:.1f} ms (~{1000.0/spike:.0f} FPS floor)"
    if total > 33:
        return f"HEAVY — total wall {total:.1f} ms"
    return f"OK — click {click:.2f} ms, spike {spike:.2f} ms"

lines = []
lines.append("# Click order diagnostic report")
lines.append("")
lines.append(f"- **Matrix:** `{kind}`")
warmup = data.get("warmup") or {}
if warmup:
    lines.append(f"- **Warmup:** legs={warmup.get('surveyLegs')}, paint tiles={warmup.get('paintTiles')}, "
                 f"buildings={warmup.get('buildingsSpawned')}, nav obstacles={warmup.get('navMeshObstacles')}, "
                 f"FPS after warm-up={warmup.get('fpsAfterWarmup')}")
lines.append(f"- **Out dir:** `{out}`")
def fmt_fps_block(label, block):
    if not block:
        lines.append(f"- **{label}:** (no samples)")
        return
    lines.append(
        f"- **{label}:** n={block.get('count')} "
        f"min={block.get('min')} median={block.get('median')} mean={block.get('mean')} "
        f"p10={block.get('p10')} p90={block.get('p90')} max={block.get('max')}"
    )

lines.append(f"- **FPS baseline (pre warm-up):** {fps_before}")
fmt_fps_block("FPS live after warm-up", data.get("fps_live_after_warmup"))
fmt_fps_block("FPS live after matrix", data.get("fps_live_after_matrix"))
lines.append(f"- **FPS delta warm-up vs baseline:** {fps_after_warmup - int(data.get('fps_before_baseline', fps_before))}")
lines.append(f"- **FPS delta matrix vs post-warm-up:** {fps_after - fps_after_warmup}")
sc = data.get("fps_sample_count") or (data.get("fps_live_after_warmup") or {}).get("count")
if sc:
    lines.append(f"- **Live sample config:** {sc} samples (see shell FPS_SAMPLE_COUNT / FPS_SAMPLE_INTERVAL_SEC)")
lines.append(f"- **Mark tile:** {data.get('markTile', '—')}")
lines.append(f"- **Prologue active:** {data.get('prologueActive')}")
lines.append(f"- **Avg frame time (matrix):** {float(data.get('avgFrameTimeMs', 0)):.2f} ms")
lines.append(f"- **Max dispatch spike (matrix):** {float(data.get('maxDispatchSpikeMs', 0)):.2f} ms")
lines.append(f"- **Goal:** {data.get('goal')}")
lines.append("")
lines.append("## Case results")
lines.append("")
lines.append("| Case | OK | Click ms | Path drain ms | Vision ms | Total ms | Avg frame ms | Max spike ms | Verdict |")
lines.append("|------|----|---------|--------------|-----------|----------|--------------|-------------|---------|")
for c in cases:
    lines.append(
        f"| {c['id']} | {c.get('success')} | {float(c.get('clickFrameDispatchMs',0)):.2f} | "
        f"{float(c.get('pathDrainMs',0)):.2f} | {float(c.get('visionFollowUpMs',0)):.2f} | "
        f"{float(c.get('totalWallMs',0)):.2f} | {float(c.get('avgFrameTimeMs',0)):.2f} | "
        f"{float(c.get('maxDispatchSpikeMs',0)):.2f} | {verdict(c)} |"
    )

lines.append("")
lines.append("## Interpretation")
lines.append("")
if not data.get("prologueActive"):
    lines.append("- **Prologue inactive:** boot Prologue (play survey slice) before trusting survey numbers.")
p_sync = next((x for x in cases if x.get("id") == "P2_mark_click_sync_bfs"), None)
p_async = next((x for x in cases if x.get("id") == "P1_mark_click_async"), None)
if p_sync and p_async:
    sync_spike = float(p_sync.get("maxDispatchSpikeMs", 0))
    async_spike = float(p_async.get("maxDispatchSpikeMs", 0))
    if sync_spike > async_spike * 1.5 and sync_spike > 10:
        lines.append(f"- **P2 sync spike ({sync_spike:.0f} ms) >> P1 async ({async_spike:.0f} ms):** time-sliced hex BFS is doing real work.")
p3 = next((x for x in cases if x.get("id") == "P3_confirm_survey_team"), None)
if p3 and float(p3.get("maxDispatchSpikeMs", 0)) > 16:
    lines.append("- **P3 confirm:** `RevealTiles` / fog / road refresh still heavy on survey confirm — check vision throttle.")

report = "\n".join(lines) + "\n"
(out / "REPORT.md").write_text(report)
latest = pathlib.Path("Assets/Playtest/click-order-diagnostic-latest")
if latest.is_symlink() or latest.exists():
    try:
        if latest.is_symlink():
            latest.unlink()
        elif latest.is_dir():
            pass
    except OSError:
        pass
try:
    latest.symlink_to(out.name)
except OSError:
  latest.mkdir(exist_ok=True)
  (latest / "REPORT.md").write_text(report)
  (latest / "matrix.json").write_text(matrix_path.read_text())

print(report)
PY

echo "Wrote $OUT_DIR/REPORT.md and $OUT_DIR/matrix.json"