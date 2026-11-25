using GameDevTV.RTS.Environment;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>
    /// Prologue slice performance: applied when <see cref="PrologueController"/> owns the run.
    /// Target ~30+ FPS in Editor Play on the landing / colony view.
    /// </summary>
    public static class ProloguePerformanceBudget
    {
        public const int TargetFps = 60;
        public const int MinimumAcceptableFps = 30;

        static bool applied;
        static int savedQuality = -1;
        static float savedShadowDistance = -1f;
        static ShadowQuality savedShadows;

        public static bool Applied => applied;

        /// <summary>Call once when the prologue run is live (after planet sync).</summary>
        public static void ApplyIfNeeded()
        {
            if (applied || !PrologueController.IsActive) return;
            applied = true;

            savedQuality = QualitySettings.GetQualityLevel();
            savedShadowDistance = QualitySettings.shadowDistance;
            savedShadows = QualitySettings.shadows;

            int low = PickLowQualityIndex();
            if (low >= 0 && low != savedQuality)
                QualitySettings.SetQualityLevel(low, applyExpensiveChanges: true);

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFps;
            QualitySettings.shadowDistance = Mathf.Min(QualitySettings.shadowDistance, 22f);
            QualitySettings.shadows = ShadowQuality.HardOnly;
            QualitySettings.globalTextureMipmapLimit = Mathf.Max(QualitySettings.globalTextureMipmapLimit, 1);
            QualitySettings.pixelLightCount = 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;

            GameDevTV.RTS.UI.Containers.MinimapUI.SetPrologueSuspended(true);
            DisableTerrainWrapCopies();

            Debug.Log(
                $"[ProloguePerformanceBudget] quality {savedQuality}->{QualitySettings.GetQualityLevel()}, " +
                $"shadowDist {savedShadowDistance:0}->{QualitySettings.shadowDistance:0}, targetFps {TargetFps}");
        }

        /// <summary>Call after planet mesh exists so wrap copies stay disabled.</summary>
        public static void RefreshPlanetRendering()
        {
            if (!applied) ApplyIfNeeded();
            else DisableTerrainWrapCopies();
        }

        public static void RestoreIfApplied()
        {
            if (!applied) return;
            applied = false;
            if (savedQuality >= 0)
                QualitySettings.SetQualityLevel(savedQuality, applyExpensiveChanges: true);
            if (savedShadowDistance >= 0f)
                QualitySettings.shadowDistance = savedShadowDistance;
            QualitySettings.shadows = savedShadows;
            Application.targetFrameRate = -1;
            GameDevTV.RTS.UI.Containers.MinimapUI.SetPrologueSuspended(false);
        }

        static int PickLowQualityIndex()
        {
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].IndexOf("Mobile", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            }
            return names.Length > 0 ? 0 : -1;
        }

        /// <summary>Smoothed FPS for playtest JSON (uses same clock as the HUD).</summary>
        public static int SmoothedFps()
        {
            if (!Application.isPlaying) return 0;
            if (PrologueFpsMonitor.LastSecondFps > 0)
                return PrologueFpsMonitor.LastSecondFps;
            return Mathf.RoundToInt(1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f));
        }

        /// <summary>
        /// Prologue keeps wrap ghosts — streamer only enables tiles near the camera (no full 9× planet).
        /// </summary>
        /// <summary>Slice: drop per-building shadow cost on flat colony view.</summary>
        public static void ApplyBuildingVisuals(GameObject root)
        {
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        static void DisableTerrainWrapCopies()
        {
            var planet = PlanetGenerator.Instance;
            if (planet == null) return;
            if (planet.TryGetComponent<TerrainWrapStreamer>(out var streamer))
            {
                streamer.enabled = true;
                streamer.Bind(planet);
            }
            if (planet.TryGetComponent<MeshRenderer>(out var mainMr))
                mainMr.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}