using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
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
        const int FpsLow = 24;
        const int FpsEmergency = 20;
        const int EmergencyStreakRequired = 3;

        static int level = -1;
        static int emergencyStreak;
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
            if (level == -2 && fps >= FpsMid)
            {
                emergencyStreak = 0;
                ApplyLevel(fps >= FpsHigh ? 2 : 1);
                return;
            }
            if (fps <= 0)
                fps = Mathf.RoundToInt(1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f));

            int want = fps >= FpsHigh ? 2 : fps >= FpsMid ? 1 : fps >= FpsLow ? 0 : -1;
            want = BoostForCameraZoom(want, fps);

            if (fps < FpsEmergency || want < 0)
            {
                emergencyStreak++;
                if (emergencyStreak >= EmergencyStreakRequired && level != -2)
                    ApplyEmergencySoftening();
                return;
            }

            emergencyStreak = 0;
            if (want != level)
                ApplyLevel(want);
        }

        /// <summary>Close or far zoom needs aniso even when FPS is only "mid".</summary>
        static int BoostForCameraZoom(int want, int fps)
        {
            float zoom = PlayerInput.Instance != null ? PlayerInput.Instance.PlaytestZoomDistance : 0f;
            if (zoom <= 0f) return want;
            float min = PlayerInput.Instance.PlaytestMinZoom;
            float max = PlayerInput.Instance.PlaytestMaxZoom;
            bool extreme = zoom <= min * 1.25f || zoom >= max * 0.85f;
            if (!extreme || fps < FpsLow) return want;
            return Mathf.Max(want, fps >= FpsMid ? 2 : 1);
        }

        public static int CurrentLevel => level;

        /// <summary>After burst spawn, hold sharp ground while FPS is acceptable (QC / screenshots).</summary>
        public static void ForceHighQualityForSampling()
        {
            if (!PrologueController.IsActive) return;
            emergencyStreak = 0;
            int fps = PrologueFpsMonitor.LastSecondFps;
            if (fps <= 0)
                fps = Mathf.RoundToInt(1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f));
            int want = fps >= FpsHigh ? 2 : fps >= FpsMid ? 1 : fps >= FpsLow ? 0 : -1;
            if (want >= 0)
            {
                want = BoostForCameraZoom(want, fps);
                ApplyLevel(want);
            }
            nextReview = Time.unscaledTime + 12f;
        }

        public static string DiagnosticsLine()
        {
            return $"groundTuning level={level} mip={QualitySettings.globalTextureMipmapLimit} " +
                   $"aniso={QualitySettings.anisotropicFiltering} emergencyStreak={emergencyStreak}";
        }

        static void ReviewSoon() => nextReview = 0f;

        static void ApplyLevel(int want)
        {
            level = want;
            QualitySettings.globalTextureMipmapLimit = 0;
            QualitySettings.anisotropicFiltering = want >= 2
                ? AnisotropicFiltering.ForceEnable
                : AnisotropicFiltering.Enable;
            int aniso = want >= 2 ? 16 : 6;
            SharpenPlanetTextures(aniso);
            BoostPlanetDetailOnMaterial(want);
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
            TouchTexture(config.NormalMap, Mathf.Min(anisoLevel, 8));
            TouchTexture(config.DetailAlbedoMap, anisoLevel);
            TouchTexture(config.DetailNormalMap, Mathf.Min(anisoLevel, 8));
            SyncPlanetMaterialAniso(anisoLevel);
        }

        static void SyncPlanetMaterialAniso(int anisoLevel)
        {
            var planet = PlanetGenerator.Instance;
            if (planet == null || !planet.TryGetComponent<MeshRenderer>(out var mr) || mr.sharedMaterial == null)
                return;
            var mat = mr.sharedMaterial;
            if (mat.mainTexture is Texture2D albedo)
                albedo.anisoLevel = Mathf.Clamp(anisoLevel, 1, 16);
            if (mat.HasProperty("_BumpMap") && mat.GetTexture("_BumpMap") is Texture2D nrm)
                nrm.anisoLevel = Mathf.Clamp(anisoLevel, 1, 8);
            if (mat.HasProperty("_DetailAlbedoMap") && mat.GetTexture("_DetailAlbedoMap") is Texture2D det)
                det.anisoLevel = Mathf.Clamp(anisoLevel, 1, 16);
        }

        static void BoostPlanetDetailOnMaterial(int want)
        {
            var planet = PlanetGenerator.Instance;
            if (planet == null || !planet.TryGetComponent<MeshRenderer>(out var mr) || mr.sharedMaterial == null)
                return;
            var mat = mr.sharedMaterial;
            if (mat.HasProperty("_DetailAlbedoMapScale"))
            {
                float grit = want >= 2 ? 1.25f : want >= 1 ? 0.95f : 0.55f;
                mat.SetFloat("_DetailAlbedoMapScale", grit);
            }
            if (mat.HasProperty("_BumpScale") && want >= 2)
                mat.SetFloat("_BumpScale", 0.85f);
        }

        static void TouchTexture(Texture2D tex, int aniso)
        {
            if (tex == null || aniso <= 0) return;
            tex.anisoLevel = Mathf.Clamp(aniso, 1, 16);
            tex.filterMode = FilterMode.Trilinear;
        }
    }

}