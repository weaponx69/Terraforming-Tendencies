using System;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// §1A Visual Climate Stages — step 1: look progress + ground tint.
    /// Barren → Thaw → Wet → Living as Temp/Atmos/Water climb toward the MVP win deltas.
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
            Supplies.OnTemperatureChanged += HandleClimateChanged;
            Supplies.OnAtmosphereChanged += HandleClimateChanged;
            Supplies.OnWaterChanged += HandleClimateChanged;
            GenerationManager.OnGenerationStarted += HandleGenerationStarted;
            PlanetGenerator.OnPlanetGenerated += HandlePlanetGenerated;
        }

        private void OnDisable()
        {
            Supplies.OnTemperatureChanged -= HandleClimateChanged;
            Supplies.OnAtmosphereChanged -= HandleClimateChanged;
            Supplies.OnWaterChanged -= HandleClimateChanged;
            GenerationManager.OnGenerationStarted -= HandleGenerationStarted;
            PlanetGenerator.OnPlanetGenerated -= HandlePlanetGenerated;
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

            // Rebuild the 256px gradient at most ~10 Hz while lerping (force always applies).
            if (force
                || Mathf.Abs(displayedLookProgress - lastAppliedProgress) >= 0.02f
                || !appliedOnce)
            {
                ApplyGroundForProgress(displayedLookProgress, force: force);
            }
        }

        private void HandleClimateChanged(Owner owner, float _)
        {
            if (owner != Owner.Player1) return;
            RecalculateTarget();
        }

        private void HandleGenerationStarted(int current, int max) => RecalculateTarget();

        private void HandlePlanetGenerated()
        {
            RecalculateTarget();
            displayedLookProgress = targetLookProgress;
            ApplyGroundForProgress(displayedLookProgress, force: true);
        }

        /// <summary>Recompute look progress from current Supplies vs generation baselines.</summary>
        public void RecalculateTarget()
        {
            float temp = Supplies.Temperature != null && Supplies.Temperature.TryGetValue(Owner.Player1, out float t)
                ? t : -60f;
            float atmos = Supplies.Atmosphere != null && Supplies.Atmosphere.TryGetValue(Owner.Player1, out float a)
                ? a : 0.01f;
            float water = Supplies.Water != null && Supplies.Water.TryGetValue(Owner.Player1, out float w)
                ? w : 0f;

            float baselineTemp = -60f;
            float baselineAtmos = 0.01f;
            float baselineWater = 0f;
            float deltaTemp = GenerationManager.SectorTemperatureDelta;
            float deltaAtmos = GenerationManager.SectorAtmosphereDelta;
            float deltaWater = GenerationManager.SectorWaterDelta;

            if (GenerationManager.Instance != null)
            {
                baselineTemp = GenerationManager.Instance.BaselineTemperature;
                baselineAtmos = GenerationManager.Instance.BaselineAtmosphere;
                baselineWater = GenerationManager.Instance.BaselineWater;
            }

            float tempProgress = DeltaProgress(temp, baselineTemp, deltaTemp);
            float atmosProgress = DeltaProgress(atmos, baselineAtmos, deltaAtmos);
            float waterProgress = DeltaProgress(water, baselineWater, deltaWater);

            targetLookProgress = Mathf.Min(tempProgress, Mathf.Min(atmosProgress, waterProgress));
        }

        private static float DeltaProgress(float current, float baseline, float requiredDelta)
        {
            if (requiredDelta <= 0.0001f) return 1f;
            float gained = current - baseline;
            if (gained + 0.0005f >= requiredDelta) return 1f;
            return Mathf.Clamp01(gained / requiredDelta);
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
