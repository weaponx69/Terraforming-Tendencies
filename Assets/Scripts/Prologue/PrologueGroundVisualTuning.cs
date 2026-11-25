using GameDevTV.RTS.Environment;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>
    /// Keeps Mars ground readable at RTS camera angles without the old
    /// "force Mobile + disable aniso + skip a mip" blur. Adjusts every few seconds from live FPS.
    /// </summary>
    public static class PrologueGroundVisualTuning
    {
        const int FpsHigh = 46;
        const int FpsMid = 34;
        const int FpsLow = 26;

        static int level = -1;
        static float nextReview;
        static bool savedQualityState;
        static int savedGlobalMipLimit;
        static AnisotropicFiltering savedAniso;

        /// <summary>Call once when prologue performance budget applies.</summary>
        public static void Bootstrap()
        {
            savedGlobalMipLimit = QualitySettings.globalTextureMipmapLimit;
            savedAniso = QualitySettings.anisotropicFiltering;
            savedQualityState = true;
            level = -1;
            nextReview = 0f;
            ReviewSoon();
        }

        public static void Restore()
        {
            if (savedQualityState)
            {
                QualitySettings.globalTextureMipmapLimit = savedGlobalMipLimit;
                QualitySettings.anisotropicFiltering = savedAniso;
                savedQualityState = false;
            }
            level = -1;
        }

        public static void Tick()
        {
            if (!PrologueController.IsActive || !Application.isPlaying) return;
            if (Time.unscaledTime < nextReview) return;
            nextReview = Time.unscaledTime + 2.5f;

            int fps = PrologueFpsMonitor.LastSecondFps;
            if (fps <= 0)
                fps = Mathf.RoundToInt(1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f));

            int want = fps >= FpsHigh ? 2 : fps >= FpsMid ? 1 : fps >= FpsLow ? 0 : -1;
            if (want < 0)
            {
                // Performance emergency — only then soften ground textures.
                if (level != -2)
                    ApplyEmergencySoftening();
                return;
            }

            if (want != level)
                ApplyLevel(want);
        }

        static void ReviewSoon() => nextReview = 0f;

        static void ApplyLevel(int want)
        {
            level = want;
            QualitySettings.globalTextureMipmapLimit = 0;
            QualitySettings.anisotropicFiltering = want >= 2
                ? AnisotropicFiltering.ForceEnable
                : AnisotropicFiltering.Enable;
            int aniso = want >= 2 ? 8 : 4;
            SharpenPlanetTextures(aniso);
        }

        static void ApplyEmergencySoftening()
        {
            level = -2;
            QualitySettings.globalTextureMipmapLimit = 1;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        }

        /// <summary>After planet material is built or config changes.</summary>
        public static void SharpenPlanetTextures(int anisoLevel = 4)
        {
            var planet = PlanetGenerator.Instance;
            var config = planet != null ? planet.Config : null;
            if (config == null)
            {
                config = Resources.Load<PlanetConfig>("Planet 1 - Easy");
            }
            if (config == null) return;

            TouchTexture(config.AlbedoMap, anisoLevel);
            TouchTexture(config.NormalMap, Mathf.Min(anisoLevel, 4));
            TouchTexture(config.DetailAlbedoMap, Mathf.Min(anisoLevel, 2));
            TouchTexture(config.DetailNormalMap, Mathf.Min(anisoLevel, 2));
        }

        static void TouchTexture(Texture2D tex, int aniso)
        {
            if (tex == null || aniso <= 0) return;
            tex.anisoLevel = Mathf.Clamp(aniso, 1, 16);
            tex.filterMode = FilterMode.Trilinear;
        }
    }

}