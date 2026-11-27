using GameDevTV.RTS.Environment;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace GameDevTV.RTS.MainGame
{
    /// <summary>
    /// MainGame slice performance: applied when <see cref="MainGameController"/> owns the run.
    /// Target ~30+ FPS in Editor Play on the landing / colony view.
    /// </summary>
    public static class MainGamePerformanceBudget
    {
        public const int TargetFps = 60;
        public const int MinimumAcceptableFps = 30;

        static bool applied;
        static int savedQuality = -1;
        static float savedShadowDistance = -1f;
        static UnityEngine.ShadowQuality savedShadows;

        public static bool Applied => applied;

        static bool earlyPresentationApplied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            applied = false;
            earlyPresentationApplied = false;
        }

        /// <summary>Frame zero: kill post/HDR wash before planet + Cinemachine warm up (fixes white Game view flash).</summary>
        public static void ApplyEarlyPresentationForPlayScene()
        {
            if (!MainGameController.IsMainGamePlayScene()) return;
            if (earlyPresentationApplied) return;
            earlyPresentationApplied = true;
            ApplyMainGameCameraPresentation();
            EnsureMainGameSunlight();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AfterSceneLoadPresentation()
        {
            if (MainGameController.IsMainGamePlayScene())
                ApplyEarlyPresentationForPlayScene();
        }

        /// <summary>Call once when the prologue run is live (after planet sync).</summary>
        public static void ApplyIfNeeded()
        {
            ApplyEarlyPresentationForPlayScene();
            if (applied || !MainGameController.IsActive) return;
            applied = true;

            savedQuality = QualitySettings.GetQualityLevel();
            savedShadowDistance = QualitySettings.shadowDistance;
            savedShadows = QualitySettings.shadows;

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFps;
            QualitySettings.shadowDistance = Mathf.Min(QualitySettings.shadowDistance, 22f);
            QualitySettings.shadows = UnityEngine.ShadowQuality.HardOnly;
            QualitySettings.pixelLightCount = 0;

            GameDevTV.RTS.UI.Containers.MinimapUI.SetMainGameSuspended(true);
            DisableTerrainWrapCopies();
            MainGameGroundVisualTuning.Bootstrap();
            MainGameGroundVisualTuning.SharpenPlanetTextures(4);
            ApplyMainGameCameraPresentation();
            EnsureMainGameSunlight();
            SuppressLegacyWorldBlockingHudPlates();
            SuppressSightVisionMeshes();
            Object.FindAnyObjectByType<GameDevTV.RTS.UI.Containers.BottomBarActionsUI>()
                ?.RefreshBar();

            Debug.Log(
                $"[MainGamePerformanceBudget] quality kept at {QualitySettings.GetQualityLevel()} (sharp ground tuning on), " +
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
            GameDevTV.RTS.UI.Containers.MinimapUI.SetMainGameSuspended(false);
            MainGameGroundVisualTuning.Restore();
        }

        /// <summary>Smoothed FPS for playtest JSON (uses same clock as the HUD).</summary>
        public static int SmoothedFps()
        {
            if (!Application.isPlaying) return 0;
            if (MainGameFpsMonitor.LastSecondFps > 0)
                return MainGameFpsMonitor.LastSecondFps;
            return Mathf.RoundToInt(1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f));
        }

        /// <summary>
        /// MainGame keeps wrap ghosts — streamer only enables tiles near the camera (no full 9× planet).
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

        static void EnsureMainGameSunlight()
        {
            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l == null || l.type != LightType.Directional) continue;
                sun = l;
                break;
            }

            if (sun == null)
            {
                var go = new GameObject("MainGame Sun");
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            sun.enabled = true;
            sun.intensity = Mathf.Max(sun.intensity, 1.05f);
            sun.color = new Color(1f, 0.96f, 0.90f);
            RenderSettings.sun = sun;
            if (RenderSettings.ambientIntensity < 0.85f)
                RenderSettings.ambientIntensity = 1f;
        }

        /// <summary>Keep ground readable: no grey post stack, no distant dust wall on the slice.</summary>
        static void ApplyMainGameCameraPresentation()
        {
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                if (cam == null) continue;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.48f, 0.42f, 0.36f, 1f);
                cam.allowHDR = false;
                if (cam.TryGetComponent<UniversalAdditionalCameraData>(out var urp))
                {
                    urp.renderPostProcessing = false;
                    urp.volumeLayerMask = 0;
                    urp.antialiasing = AntialiasingMode.None;
                }
            }

            DisableGlobalPostVolumes();
            SuppressSightVisionMeshes();

            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
        }

        /// <summary>Acts sight discs are huge white CurvedWorld meshes — hide on the tactical slice.</summary>
        public static void SuppressSightVisionMeshes()
        {
            if (!MainGameController.IsMainGamePlayScene() && !MainGameController.IsActive) return;
            foreach (var tr in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (tr == null || tr.name != "Vision") continue;
                foreach (var r in tr.GetComponentsInChildren<Renderer>(true))
                {
                    if (r != null) r.enabled = false;
                }
            }
        }

        /// <summary>Acts RuntimeUI sometimes leaves a full-bleed white Center Container plate over the map.</summary>
        static void SuppressLegacyWorldBlockingHudPlates()
        {
            foreach (var rootName in new[] { "Center Container", "Center Container Debug Box" })
            {
                var go = GameObject.Find(rootName);
                if (go == null) continue;
                foreach (var img in go.GetComponentsInChildren<Image>(true))
                {
                    if (img == null) continue;
                    var c = img.color;
                    c.a = 0f;
                    img.color = c;
                    img.raycastTarget = false;
                    img.enabled = false;
                }
            }

            foreach (var img in Object.FindObjectsByType<Image>(FindObjectsInactive.Include))
            {
                if (img == null || img.canvas == null) continue;
                if (IsGameplayHudImage(img)) continue;
                if (img.canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                var rt = img.rectTransform;
                if (rt == null) continue;
                Vector2 span = rt.anchorMax - rt.anchorMin;
                if (span.x < 0.85f || span.y < 0.85f) continue;
                Color c = img.color;
                if (c.a < 0.4f || c.r < 0.82f || c.g < 0.82f || c.b < 0.82f) continue;
                c.a = 0f;
                img.color = c;
                img.raycastTarget = false;
            }
        }

        /// <summary>Volume lives in the RP Core assembly — disable by type name so MainGame.asmdef stays self-contained.</summary>
        static void DisableGlobalPostVolumes()
        {
            foreach (var c in Object.FindObjectsByType<Component>(FindObjectsInactive.Include))
            {
                if (c == null) continue;
                if (c.GetType().Name != "Volume" || c is not Behaviour behaviour) continue;
                var weightProp = c.GetType().GetProperty("weight");
                if (weightProp != null && weightProp.CanWrite)
                    weightProp.SetValue(c, 0f);
                behaviour.enabled = false;
            }
        }

        static bool IsGameplayHudImage(Image img)
        {
            if (img == null) return false;
            var t = img.transform;
            if (t.GetComponentInParent<GameDevTV.RTS.UI.Containers.BottomBarActionsUI>(true) != null)
                return true;
            if (t.GetComponentInParent<GameDevTV.RTS.UI.Components.UIActionButton>(true) != null)
                return true;
            if (t.GetComponentInParent<GameDevTV.RTS.UI.MainGameHUD>(true) != null)
                return true;
            var p = t.parent;
            while (p != null)
            {
                if (p.name == "Bottom Bar" || p.name == "Actions Container" || p.name == "Cards Container")
                    return true;
                p = p.parent;
            }
            return false;
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