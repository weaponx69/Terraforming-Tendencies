using UnityEditor;
using UnityEngine;

namespace GameDevTV.RTS.EditorTools
{
    public static class MainGamePlaythroughMenu
    {
        const string ShellPath = "tools/main-game-ui-playthrough.sh";

        [MenuItem("Terraforming Tendencies/MainGame/Run player UI playthrough (see shell)")]
        public static void ShowPlaythroughInstructions()
        {
            EditorUtility.DisplayDialog(
                "MainGame player playthrough",
                "MainGame is verified only via the player UI harness (bootstrap + real clicks).\n\n"
                + "From the project root in a terminal:\n"
                + "  ./" + ShellPath + "\n\n"
                + "Requires: Unity Editor Play Mode, Pipeline ready, xdotool, import/scrot.\n"
                + "Project policy: player UI only (see docs/specs/2026-11-23-player-verification-policy.md).",
                "OK");
            Debug.Log(
                "[MainGame] Run: ./" + ShellPath
                + " — screenshots under Assets/Playtest/ui-prologue-*");
        }
    }
}