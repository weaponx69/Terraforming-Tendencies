using System;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Visual climate stages — ground tint, fog, and ambient sky from Colony Habitability
    /// (climate-tagged tiles), not from Supplies Temp/Atmos/Water win deltas.
    /// </summary>
    public class ClimateVisualStages : MonoBehaviour
    {
        public enum Stage
        {
            Barren = 0,
            Thaw = 1,
            Wet = 2,
            Living = 3
        }

        public static ClimateVisualStages Instance { get; private set; }

        public static event Action<Stage, float> OnStageChanged;

        [Header("Smoothing")]
        [SerializeField] private float lerpSpeed = 0.65f;
        [SerializeField] private float applyEpsilon = 0.002f;

        [Header("Stage thresholds (lookProgress)")]
        [SerializeField] private float thawAt = 0.33f;
        [SerializeField] private float wetAt = 0.66f;
        [SerializeField] private float livingAt = 0.999f;

        [Header("Atmosphere (fog / ambient)")]
        [SerializeField] private bool driveFogAndAmbient = true;
        [SerializeField] private float barrenFogDensity = 0.028f;
        [SerializeField] private float livingFogDensity = 0.008f;

        // Gradient low / mid / high + albedo tint multiplier per stage.
        private static readonly Color BarrenLow = new Color(0.55f, 0.25f, 0.15f);
        private static readonly Color BarrenMid = new Color(0.65f, 0.35f, 0.20f);
        private static readonly Color BarrenHigh = new Color(0.75f, 0.45f, 0.25f);
        private static readonly Color BarrenTint = Color.white;

        private static readonly Color ThawLow = new Color(0.52f, 0.32f, 0.18f);
        private static readonly Color ThawMid = new Color(0.62f, 0.42f, 0.28f);
        private static readonly Color ThawHigh = new Color(0.72f, 0.52f, 0.35f);
        private static readonly Color ThawTint = new Color(1.00f, 0.92f, 0.82f);

        private static readonly Color WetLow = new Color(0.35f, 0.38f, 0.22f);
        private static readonly Color WetMid = new Color(0.42f, 0.48f, 0.28f);
        private static readonly Color WetHigh = new Color(0.50f, 0.55f, 0.32f);
        private static readonly Color WetTint = new Color(0.85f, 0.95f, 0.80f);

        private static readonly Color LivingLow = new Color(0.22f, 0.42f, 0.18f);
        private static readonly Color LivingMid = new Color(0.28f, 0.55f, 0.22f);
        private static readonly Color LivingHigh = new Color(0.35f, 0.65f, 0.28f);
        private static readonly Color LivingTint = new Color(0.75f, 1.00f, 0.75f);

        // Fog + ambient sky per stage (dusty red → clear blue-green).
        private static readonly Color BarrenFog = new Color(0.62f, 0.38f, 0.28f);
        private static readonly Color BarrenAmbient = new Color(0.45f, 0.32f, 0.28f);
        private static readonly Color ThawFog = new Color(0.55f, 0.42f, 0.35f);
        private static readonly Color ThawAmbient = new Color(0.52f, 0.45f, 0.40f);
        private static readonly Color WetFog = new Color(0.42f, 0.48f, 0.40f);
        private static readonly Color WetAmbient = new Color(0.48f, 0.55f, 0.50f);
        private static readonly Color LivingFog = new Color(0.55f, 0.68f, 0.72f);
        private static readonly Color LivingAmbient = new Color(0.55f, 0.70f, 0.78f);

        private float targetLookProgress;
        private float displayedLookProgress = -1f;
        private float lastAppliedProgress = -1f;
        private Stage currentStage = Stage.Barren;
        private bool appliedOnce;

        public float LookProgress => displayedLookProgress < 0f ? 0f : displayedLookProgress;
        public float TargetLookProgress => targetLookProgress;
        public Stage CurrentStage => currentStage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject(nameof(ClimateVisualStages));
            go.AddComponent<ClimateVisualStages>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            GenerationManager.OnGenerationStarted += HandleGenerationStarted;
            PlanetGenerator.OnPlanetGenerated += HandlePlanetGenerated;
            ColonyActManager.OnActStateChanged += HandleActStateChanged;
        }

        private void OnDisable()
        {
            GenerationManager.OnGenerationStarted -= HandleGenerationStarted;
            PlanetGenerator.OnPlanetGenerated -= HandlePlanetGenerated;
            ColonyActManager.OnActStateChanged -= HandleActStateChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            RecalculateTarget();
            displayedLookProgress = targetLookProgress;
            ApplyGroundForProgress(displayedLookProgress, force: true);
        }

        private void Update()
        {
            if (Mathf.Abs(displayedLookProgress - targetLookProgress) <= applyEpsilon)
            {
                if (!appliedOnce || Mathf.Abs(lastAppliedProgress - targetLookProgress) > applyEpsilon)
                {
                    displayedLookProgress = targetLookProgress;
                    ApplyGroundForProgress(displayedLookProgress, force: true);
                }
                return;
            }

            displayedLookProgress = Mathf.MoveTowards(
                displayedLookProgress < 0f ? targetLookProgress : displayedLookProgress,
                targetLookProgress,
                lerpSpeed * Time.unscaledDeltaTime);

            // Rebuild the 256px gradient at most ~10 Hz while lerping.
            if (Mathf.Abs(displayedLookProgress - lastAppliedProgress) >= 0.02f || !appliedOnce)
            {
                ApplyGroundForProgress(displayedLookProgress, force: false);
            }
        }

        private void HandleActStateChanged() => RecalculateTarget();

        private void HandleGenerationStarted(int current, int max) => RecalculateTarget();

        private void HandlePlanetGenerated()
        {
            RecalculateTarget();
            displayedLookProgress = targetLookProgress;
            ApplyGroundForProgress(displayedLookProgress, force: true);
        }

        /// <summary>Called when Habitability changes from ColonyActManager tile grants.</summary>
        public void NotifyHabitabilityChanged() => RecalculateTarget();

        /// <summary>Look progress from cumulative Habitability (climate-tagged tiles).</summary>
        public void RecalculateTarget()
        {
            if (ColonyActManager.Instance != null)
                targetLookProgress = ColonyActManager.Instance.HabitabilityProgress;
            else
                targetLookProgress = 0f;
        }

        public Stage StageForProgress(float progress)
        {
            if (progress >= livingAt) return Stage.Living;
            if (progress >= wetAt) return Stage.Wet;
            if (progress >= thawAt) return Stage.Thaw;
            return Stage.Barren;
        }

        private void ApplyGroundForProgress(float progress, bool force)
        {
            Stage stage = StageForProgress(progress);

            GetStagePalette(stage, out Color lowA, out Color midA, out Color highA, out Color tintA);
            Stage next = stage < Stage.Living ? stage + 1 : Stage.Living;
            GetStagePalette(next, out Color lowB, out Color midB, out Color highB, out Color tintB);

            float blend = StageBlend(progress, stage);
            Color low = Color.Lerp(lowA, lowB, blend);
            Color mid = Color.Lerp(midA, midB, blend);
            Color high = Color.Lerp(highA, highB, blend);
            Color tint = Color.Lerp(tintA, tintB, blend);

            if (PlanetGenerator.Instance != null)
            {
                PlanetGenerator.Instance.ApplyClimateGroundPalette(low, mid, high, tint);
            }

            ApplyAtmosphereForProgress(progress, stage, next, blend);

            lastAppliedProgress = progress;

            if (stage != currentStage || !appliedOnce)
            {
                currentStage = stage;
                appliedOnce = true;
                OnStageChanged?.Invoke(currentStage, progress);
                Debug.Log($"[ClimateVisualStages] Stage={currentStage} look={progress:P0} (target={targetLookProgress:P0})");
            }
            else
            {
                appliedOnce = true;
            }
        }

        private void ApplyAtmosphereForProgress(float progress, Stage stage, Stage next, float blend)
        {
            if (!driveFogAndAmbient) return;

            GetStageAtmosphere(stage, out Color fogA, out Color ambientA);
            GetStageAtmosphere(next, out Color fogB, out Color ambientB);

            Color fog = Color.Lerp(fogA, fogB, blend);
            Color ambient = Color.Lerp(ambientA, ambientB, blend);
            float density = Mathf.Lerp(barrenFogDensity, livingFogDensity, Mathf.Clamp01(progress));

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = density;

            // Flat ambient (typical scene AmbientMode) — sky / ambientLight carry the tint.
            RenderSettings.ambientSkyColor = ambient;
            if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight
                || RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Flat)
            {
                RenderSettings.ambientLight = ambient;
            }
        }

        private static void GetStageAtmosphere(Stage stage, out Color fog, out Color ambient)
        {
            switch (stage)
            {
                case Stage.Thaw:
                    fog = ThawFog; ambient = ThawAmbient;
                    break;
                case Stage.Wet:
                    fog = WetFog; ambient = WetAmbient;
                    break;
                case Stage.Living:
                    fog = LivingFog; ambient = LivingAmbient;
                    break;
                default:
                    fog = BarrenFog; ambient = BarrenAmbient;
                    break;
            }
        }

        private float StageBlend(float progress, Stage stage)
        {
            float start;
            float end;
            switch (stage)
            {
                case Stage.Barren:
                    start = 0f;
                    end = thawAt;
                    break;
                case Stage.Thaw:
                    start = thawAt;
                    end = wetAt;
                    break;
                case Stage.Wet:
                    start = wetAt;
                    end = livingAt;
                    break;
                default:
                    return 0f;
            }

            if (end <= start) return 0f;
            return Mathf.Clamp01((progress - start) / (end - start));
        }

        private static void GetStagePalette(Stage stage, out Color low, out Color mid, out Color high, out Color tint)
        {
            switch (stage)
            {
                case Stage.Thaw:
                    low = ThawLow; mid = ThawMid; high = ThawHigh; tint = ThawTint;
                    break;
                case Stage.Wet:
                    low = WetLow; mid = WetMid; high = WetHigh; tint = WetTint;
                    break;
                case Stage.Living:
                    low = LivingLow; mid = LivingMid; high = LivingHigh; tint = LivingTint;
                    break;
                default:
                    low = BarrenLow; mid = BarrenMid; high = BarrenHigh; tint = BarrenTint;
                    break;
            }
        }
    }
}
