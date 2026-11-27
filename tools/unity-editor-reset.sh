#!/usr/bin/env bash
# Reset Editor view for automation: exit Play, open main scene, close extra Game views.
set -euo pipefail
unity status --format json | python3 -c "import sys,json; d=json.load(sys.stdin); s=d['data']['instances'][0]['state'] if d.get('success') else ''; sys.exit(0 if s=='ready' else 1)" || { echo "Unity Editor not ready"; exit 1; }

unity command eval "$(cat <<'CS'
if (UnityEditor.EditorApplication.isPlaying)
    UnityEditor.EditorApplication.isPlaying = false;
const string scenePath = "Assets/Scenes/Terraforming-Tendencies.unity";
GameDevTV.RTS.EditorTools.MainGameEditorSceneUtility.RequestOpenScene(scenePath);
var asm = typeof(UnityEditor.Editor).Assembly;
var gvType = asm.GetType("UnityEditor.GameView") ?? asm.GetType("UnityEditor.PlayModeView");
int closed = 0;
UnityEditor.EditorWindow keeper = null;
foreach (var w in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>())
{
    if (w == null) continue;
    bool isGame = gvType != null && gvType.IsInstanceOfType(w);
    if (!isGame)
    {
        var t = w.titleContent.text ?? "";
        if (t.IndexOf("Game", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
    }
    if (keeper == null) { keeper = w; continue; }
    w.Close();
    closed++;
}
if (keeper != null) keeper.Focus();
return $"scene={UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name} closedGameViews={closed} playing={UnityEditor.EditorApplication.isPlaying}";
CS
)" --json --timeout 120
echo "Editor reset complete."