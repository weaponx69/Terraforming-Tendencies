using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Units;
using System;

namespace GameDevTV.RTS.Player
{
    public enum MilestoneType
    {
        /// <summary>Deprecated — Biomass is not a terraforming goal. Prefer Temperature.</summary>
        Biomass = 0,
        Oxygen = 1,
        Power = 2,
        Population = 3,
        CommandPosts = 4,
        Temperature = 5,
    }

    [System.Serializable]
    public struct SectorMilestone
    {
        public MilestoneType Type;
        public float TargetValue;
        public string GoalDescription;
    }

    public struct ColonizationAdvancePreview
    {
        public bool WillColonize;
        public int TargetSectorIndex;
        public bool EnteringExpansion;
    }

    public struct ColonizationAdvanceResult
    {
        public bool Attempted;
        public bool Succeeded;
        public int SectorIndex;
        public string Message;
    }

    public class GenerationManager : MonoBehaviour
    {
        public static GenerationManager Instance { get; private set; }

        public int CurrentGeneration { get; private set; } = 1;
        public int MaxGenerations { get; private set; } = 5; // Default fallback
        public int TotalTerraCoins { get; private set; } = 0;
        public bool IsBetweenRounds { get; private set; } = false;
        public bool IsExpansionPhase { get; private set; } = false;
        public ColonizationAdvanceResult LastColonizationResult { get; private set; }

        public float CurrentMilestoneValue { get; private set; }
        public float CurrentMilestoneTarget { get; private set; }
        public MilestoneType CurrentMilestoneType { get; private set; }

        /// <summary>
        /// Each sector is a mini-game: from round-start baselines, the player must earn
        /// these climate deltas again — prior sectors do not satisfy the new round.
        /// </summary>
        public const float SectorTemperatureDelta = 15f;
        public const float SectorAtmosphereDelta = 0.25f;
        public const float SectorWaterDelta = 5f;

        public float BaselineTemperature => baselineTemperature;
        public float BaselineAtmosphere => baselineAtmosphere;
        public float BaselineWater => baselineWater;

        /// <summary>Absolute climate floor used for caps / planet display (cumulative).</summary>
        public float GetTargetTemperature(int generation)
        {
            return -60f + (SectorTemperatureDelta * generation);
        }

        public float GetTargetAtmosphere(int generation)
        {
            return SectorAtmosphereDelta * generation;
        }

        public float GetTargetWater(int generation)
        {
            return SectorWaterDelta * generation;
        }

        /// <summary>This sector round's temperature win target (baseline + fixed delta).</summary>
        public float GetRoundTemperatureTarget() => baselineTemperature + SectorTemperatureDelta;
        public float GetRoundAtmosphereTarget() => baselineAtmosphere + SectorAtmosphereDelta;
        public float GetRoundWaterTarget() => baselineWater + SectorWaterDelta;

        public string CurrentMilestoneDescription
        {
            get
            {
                // MVP: one round — clear Temp / Atmos / Water on the whole planet.
                return ColonyActManager.Instance != null
                    ? $"Act {ColonyActManager.Instance.CurrentAct}: {ColonyActManager.Instance.CurrentActName} — Colony Score {ColonyActManager.Instance.ColonyScore}/{ColonyActManager.Instance.TargetScore}"
                    : $"Grow the colony (place tiles for Colony Score)";
            }
        }

        [Header("Milestones Config")]
        [SerializeField] private List<SectorMilestone> milestones = new();

        private float roundStartTime = 0f;
        private float baselineBiomass = 0f;
        private float baselinePower = 0f;
        private float baselineOxygen = 0f;
        private float baselinePopulation = 0f;
        private float baselineTemperature = -60f;
        private float baselineAtmosphere = 0.01f;
        private float baselineWater = 0f;

        public static event Action<int, int> OnGenerationStarted; // current, max
        public static event Action<int, int> OnGenerationEnded;   // earnedTC, totalTC
        public static event Action<int> OnTerraCoinsChanged; // newTC
        public static event Action<float> OnGenerationProgressChanged; // 0f to 1f

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(this);
            }
        }

        private void OnEnable()
        {
            PlanetGenerator.OnPlanetGenerated += InitializeGenerations;
            BlueprintDraftManager.OnDraftCompleted += HandleDraftCompleted;
            EnsureMilestoneSubscription();
        }

        private void OnDisable()
        {
            PlanetGenerator.OnPlanetGenerated -= InitializeGenerations;
            BlueprintDraftManager.OnDraftCompleted -= HandleDraftCompleted;
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.OnTurnMilestones -= CheckMilestones;
            }
        }

        private void Start()
        {
            EnsureMilestoneSubscription();
        }

        private void EnsureMilestoneSubscription()
        {
            if (GameFlowManager.Instance == null) return;
            GameFlowManager.Instance.OnTurnMilestones -= CheckMilestones;
            GameFlowManager.Instance.OnTurnMilestones += CheckMilestones;
        }

        private void HandleDraftCompleted()
        {
            RecordBaselines();
            roundStartTime = Time.time; // Restart grace period after unpause
        }

        private void InitializeGenerations()
        {
            InitializeDefaultMilestones();
            if (SectorManager.Instance != null)
            {
                if (SectorManager.Instance.Sectors.Count == 0)
                {
                    SectorManager.Instance.InitializeSectors();
                }
            }

            // Absolute Combolands run: ColonyActManager owns Acts. Keep one generation slot for legacy UI.
            MaxGenerations = 1;
            CurrentGeneration = 1;
            IsBetweenRounds = false;
            IsExpansionPhase = false;

            if (milestones != null && milestones.Count > 0)
            {
                CurrentMilestoneType = MilestoneType.Temperature;
                CurrentMilestoneTarget = SectorTemperatureDelta;
            }
            CurrentMilestoneValue = 0f;

            UnlockPrerequisitesForMilestone();
            RecordBaselines();
            roundStartTime = Time.time;
            OnGenerationProgressChanged?.Invoke(0f);
            OnGenerationStarted?.Invoke(CurrentGeneration, MaxGenerations);
        }

        private void InitializeDefaultMilestones()
        {
            if (milestones == null || milestones.Count == 0)
            {
                milestones = new List<SectorMilestone>
                {
                    new SectorMilestone { Type = MilestoneType.Temperature, TargetValue = SectorTemperatureDelta, GoalDescription = $"Raise Temperature by {SectorTemperatureDelta:F0}°C this sector" },
                    new SectorMilestone { Type = MilestoneType.Power, TargetValue = 20f, GoalDescription = "Generate 20 Grid Power" },
                    new SectorMilestone { Type = MilestoneType.Oxygen, TargetValue = 30f, GoalDescription = "Reach 30% Atmospheric Oxygen" },
                    new SectorMilestone { Type = MilestoneType.Population, TargetValue = 10f, GoalDescription = "Establish 10 Colonists" },
                    new SectorMilestone { Type = MilestoneType.CommandPosts, TargetValue = 1f, GoalDescription = "Establish a Command Post in the sector" }
                };
            }

            // Migrate deprecated Biomass primaries → Temperature.
            for (int i = 0; i < milestones.Count; i++)
            {
                if (milestones[i].Type != MilestoneType.Biomass) continue;
                var migrated = milestones[i];
                migrated.Type = MilestoneType.Temperature;
                milestones[i] = migrated;
            }

            // Milestone primary targets are an incremental slice per generation round,
            // not a cumulative total that jumps when another sector unlocks.
            if (SectorManager.Instance != null && SectorManager.Instance.Sectors != null && SectorManager.Instance.Sectors.Count > 0)
            {
                int totalSectors = SectorManager.Instance.Sectors.Count;
                float slicePerSector = 100f / totalSectors;
                for (int i = 0; i < milestones.Count; i++)
                {
                    int generation = i + 1;
                    if (milestones[i].Type == MilestoneType.Oxygen)
                    {
                        var m = milestones[i];
                        m.TargetValue = slicePerSector;
                        m.GoalDescription = $"Raise oxygen by {slicePerSector:F0}% this sector";
                        milestones[i] = m;
                    }
                    else if (milestones[i].Type == MilestoneType.Temperature)
                    {
                        var m = milestones[i];
                        m.TargetValue = SectorTemperatureDelta;
                        m.GoalDescription = $"Raise Temperature by {SectorTemperatureDelta:F0}°C this sector";
                        milestones[i] = m;
                    }
                }
            }
        }

        private void RecordBaselines()
        {
            baselineBiomass = 0f;
            if (Supplies.Biomass != null && Supplies.Biomass.TryGetValue(Owner.Player1, out float bio))
                baselineBiomass = bio;

            baselinePower = 0f;
            if (Supplies.Power != null && Supplies.Power.TryGetValue(Owner.Player1, out float pow))
                baselinePower = pow;

            baselineOxygen = 0f;
            if (Supplies.Oxygen != null && Supplies.Oxygen.TryGetValue(Owner.Player1, out float ox))
                baselineOxygen = ox;

            baselinePopulation = 0f;
            if (Supplies.Population != null && Supplies.Population.TryGetValue(Owner.Player1, out int pop))
                baselinePopulation = pop;

            baselineTemperature = -60f;
            if (Supplies.Temperature != null && Supplies.Temperature.TryGetValue(Owner.Player1, out float temp))
                baselineTemperature = temp;

            baselineAtmosphere = 0.01f;
            if (Supplies.Atmosphere != null && Supplies.Atmosphere.TryGetValue(Owner.Player1, out float atmos))
                baselineAtmosphere = atmos;

            baselineWater = 0f;
            if (Supplies.Water != null && Supplies.Water.TryGetValue(Owner.Player1, out float water))
                baselineWater = water;

            // Stop prior-sector climate card tick-ups from auto-completing this round.
            ClimateManager.Instance?.ClearPendingTargets();

            // Pin climate focus to the sector this round is played on.
            if (SectorManager.Instance != null)
            {
                var focus = SectorManager.Instance.GetClimateFocusSector();
                if (focus != null)
                    SectorManager.Instance.BeginTerraformingOn(focus);
            }
        }

        /// <summary>
        /// Progress for this sector round: only gains above the round-start baseline count.
        /// Prior-sector absolute climate values never auto-complete the round.
        /// </summary>
        private static float RoundDeltaProgress(float current, float baseline, float requiredDelta)
        {
            if (requiredDelta <= 0.0001f) return 1f;
            // Float targets like 0.26 atm are not binary-exact; treat near-complete as done
            // so Atmos/Temp/Water can turn green and the sector can advance.
            float gained = current - baseline;
            if (gained + 0.0005f >= requiredDelta) return 1f;
            return Mathf.Clamp01(gained / requiredDelta);
        }

        private void MarkActiveSectorRoundComplete()
        {
            var sector = SectorManager.Instance?.GetClimateFocusSector();
            if (sector == null) return;
            sector.TerraformingCompletionPercent = 1f;
            sector.CompletedGenerationRound = CurrentGeneration;
            Debug.Log($"[GenerationManager] Sector at {sector.Center} marked complete for generation {CurrentGeneration}.");
        }

        private void Update()
        {
            if (IsBetweenRounds) return;
            if (IsExpansionPhase) return;
            if (Time.time < roundStartTime + 2f) return;

            if (milestones == null || milestones.Count == 0) InitializeDefaultMilestones();

            // Climate ticks in real time; do not wait for idle turn resolution to finish the round.
            EnsureMilestoneSubscription();
            CheckMilestones();
        }

        public void TriggerGenerationEnd(bool force = false)
        {
            if (IsBetweenRounds) return;

            if (!force && !IsExpansionPhase && !IsCurrentSectorRoundComplete())
            {
                Debug.LogWarning("[GenerationManager] Blocked generation end — sector terraforming goals are not complete.");
                return;
            }

            IsBetweenRounds = true;

            // Liquidate current materials to Terra-Coins
            int earnedTC = LiquidateMaterials();
            TotalTerraCoins += earnedTC;
            OnTerraCoinsChanged?.Invoke(TotalTerraCoins);

            // Pause the game
            Time.timeScale = 0f;

            Debug.Log($"[GenerationManager] Generation {CurrentGeneration} ended. Earned {earnedTC} TC. Total TC: {TotalTerraCoins}");

            MarkActiveSectorRoundComplete();
            
            // Robust auto-activation of any GenerationSummaryUI in the scene
            var summaryUIs = Resources.FindObjectsOfTypeAll<GameDevTV.RTS.UI.Containers.GenerationSummaryUI>();
            foreach (var ui in summaryUIs)
            {
                if (ui != null && ui.gameObject != null && ui.gameObject.scene.name != null)
                {
                    // Ensure the entire parent hierarchy is active so the UI panel is visible
                    Transform current = ui.transform.parent;
                    while (current != null)
                    {
                        if (!current.gameObject.activeSelf)
                        {
                            current.gameObject.SetActive(true);
                        }
                        current = current.parent;
                    }
                    ui.gameObject.SetActive(true);
                }
            }

            OnGenerationEnded?.Invoke(earnedTC, TotalTerraCoins);
        }

        private void CheckMilestones()
        {
            if (IsBetweenRounds || IsExpansionPhase) return;

            if (milestones == null || milestones.Count == 0) InitializeDefaultMilestones();

            float progress = CalculateCurrentSectorProgress(out _);
            OnGenerationProgressChanged?.Invoke(progress);

            if (progress >= 0.999f)
            {
                // Climate deltas are no longer the win meter — ColonyActManager owns Acts.
                // Keep progress event for any legacy HUD that still reads generation % .
            }
        }

        /// <summary>Called by <see cref="ColonyActManager"/> when the final Act clears.</summary>
        public void NotifyColonyActVictory()
        {
            if (IsBetweenRounds) return;
            IsBetweenRounds = true;
            OnGenerationProgressChanged?.Invoke(1f);
            Time.timeScale = 0f;
            Debug.Log("[GenerationManager] Colony Acts complete — victory.");
            if (GameOverManager.Instance != null)
                GameOverManager.Instance.TriggerVictory();
            else
                Debug.LogError("[GenerationManager] GameOverManager missing — cannot show victory UI.");
        }

        /// <summary>Pause and fire victory when the hamster MVP climate goals are met.</summary>
        [System.Obsolete("Climate MVP victory retired — use ColonyActManager.")]
        private void TriggerMvpVictory()
        {
            NotifyColonyActVictory();
        }

        /// <summary>
        /// MVP progress: Temperature, Atmosphere, and Water only (no primary gate).
        /// Returns 0–1; bottleneck is the lowest climate line.
        /// </summary>
        public float CalculateCurrentSectorProgress(out string bottleneck)
        {
            bottleneck = null;

            float currentTemp = Supplies.Temperature != null && Supplies.Temperature.TryGetValue(Owner.Player1, out float tVal)
                ? tVal : -60f;
            float tempProgress = RoundDeltaProgress(currentTemp, baselineTemperature, SectorTemperatureDelta);

            float currentAtmos = Supplies.Atmosphere != null && Supplies.Atmosphere.TryGetValue(Owner.Player1, out float aVal)
                ? aVal : 0.01f;
            float atmosProgress = RoundDeltaProgress(currentAtmos, baselineAtmosphere, SectorAtmosphereDelta);

            float currentWater = Supplies.Water != null && Supplies.Water.TryGetValue(Owner.Player1, out float wVal)
                ? wVal : 0f;
            float waterProgress = RoundDeltaProgress(currentWater, baselineWater, SectorWaterDelta);

            CurrentMilestoneType = MilestoneType.Temperature;
            CurrentMilestoneTarget = SectorTemperatureDelta;
            CurrentMilestoneValue = currentTemp - baselineTemperature;

            float progress = Mathf.Min(tempProgress, Mathf.Min(atmosProgress, waterProgress));

            if (progress < 1f)
            {
                if (tempProgress <= progress) bottleneck = "TEMPERATURE";
                else if (atmosProgress <= progress) bottleneck = "ATMOSPHERE";
                else bottleneck = "WATER";
            }

            return progress;
        }

        /// <summary>True when Colony Acts have finished the run (final Act cleared).</summary>
        public bool IsCurrentSectorRoundComplete()
        {
            if (IsBetweenRounds || IsExpansionPhase) return false;
            var acts = ColonyActManager.Instance;
            return acts != null && acts.IsRunEnded
                && acts.CurrentAct >= acts.TotalActs
                && acts.ColonyScore >= acts.TargetScore;
        }

        public void CheatCompleteGeneration()
        {
            if (IsBetweenRounds || IsExpansionPhase) return;

            // Fire progress change to 100% so UI elements update and show 100% completion
            OnGenerationProgressChanged?.Invoke(1f);

            TriggerGenerationEnd(force: true);
        }

        public void CheatSkipToExpansion()
        {
            if (IsBetweenRounds || IsExpansionPhase) return;

            // Skip straight to the final milestone so the next transition enters the expansion phase
            CurrentGeneration = MaxGenerations;

            // Fire progress change to 100% so UI elements update and show 100% completion
            OnGenerationProgressChanged?.Invoke(1f);

            TriggerGenerationEnd(force: true);
        }

        private int LiquidateMaterials()
        {
            int tc = 0;
            
            if (Supplies.Materials != null && Supplies.Materials.TryGetValue(Owner.Player1, out int b)) tc += b;

            // Wipe resources after liquidating so the player starts the next generation at 0 (or a baseline)
            if (Supplies.Materials != null && Supplies.Materials.ContainsKey(Owner.Player1))
            {
                Supplies.Materials[Owner.Player1] = 0;
            }
            Supplies.RaiseMaterialsChanged(Owner.Player1, 0);

            return tc / 10;
        }

        public void StartNextGeneration()
        {
            if (!IsBetweenRounds) return;
            
            CurrentGeneration++;

            // Reset progress bar to 0% for the new generation
            OnGenerationProgressChanged?.Invoke(0f);

            TryAutoColonizeClosestSectorAfterRoundComplete();

            if (CurrentGeneration > MaxGenerations)
            {
                Debug.Log("[GenerationManager] All Milestones Completed! Sector Completed.");
                IsExpansionPhase = true;
                IsBetweenRounds = false;
                Time.timeScale = 1f;

                // Grant starting materials for the expansion phase
                if (Supplies.Materials != null && Supplies.Materials.ContainsKey(Owner.Player1))
                {
                    Supplies.Materials[Owner.Player1] = Supplies.StartingMaterials;
                    Supplies.RaiseMaterialsChanged(Owner.Player1, Supplies.Materials[Owner.Player1]);
                }

                // Sector unlocking is now automatic: finishing a terraforming round
                // colonizes the closest sector that still needs a Command Post.

                // Explicitly unlock the Command Post blueprint for the expansion phase
                BlueprintDraftManager.UnlockBuilding("Command Post");
                BlueprintDraftManager.UnlockBuilding("Solar Panel");

                // Reset progress bar to 0% for the expansion phase
                OnGenerationProgressChanged?.Invoke(0f);

                // Keep hand/UI in sync even though we skip normal milestone advance.
                CardDeckController.Instance?.RefreshHand();
                OnGenerationStarted?.Invoke(CurrentGeneration, MaxGenerations);

                return;
            }

            IsBetweenRounds = false;
            Time.timeScale = 1f;

            // Set properties for the new milestone
            int milestoneIndex = Mathf.Clamp(CurrentGeneration - 1, 0, milestones.Count - 1);
            CurrentMilestoneType = milestones[milestoneIndex].Type;
            CurrentMilestoneTarget = milestones[milestoneIndex].TargetValue;
            CurrentMilestoneValue = 0f;

            // Grant starting materials for the next generation
            if (Supplies.Materials != null && Supplies.Materials.ContainsKey(Owner.Player1))
            {
                Supplies.Materials[Owner.Player1] = Supplies.StartingMaterials;
                Supplies.RaiseMaterialsChanged(Owner.Player1, Supplies.Materials[Owner.Player1]);
            }

            // NOTE: Additional map sectors unlock automatically when advancing generations.
            // Resources are no longer auto-replenished — they persist across rounds.
            roundStartTime = Time.time; // Start the grace period

            UnlockPrerequisitesForMilestone();
            RecordBaselines();

            // Fire event
            OnGenerationStarted?.Invoke(CurrentGeneration, MaxGenerations);
        }

        /// <summary>
        /// Predict whether advancing to the next generation will auto-deploy a Command Post.
        /// </summary>
        public ColonizationAdvancePreview PreviewColonizationBeforeAdvance()
        {
            var preview = new ColonizationAdvancePreview();
            if (!IsBetweenRounds || SectorManager.Instance == null) return preview;

            int nextGeneration = CurrentGeneration + 1;
            preview.EnteringExpansion = nextGeneration > MaxGenerations;

            if (SectorManager.Instance.GetNextLockedSectorIndex() < 0
                && !GameDevTV.RTS.Utilities.SectorColonization.HasUnclaimedUnlockedSector())
            {
                return preview;
            }

            bool shouldColonize = IsExpansionPhase
                || preview.EnteringExpansion
                || SectorManager.Instance.GetUnlockedSectorCount() < nextGeneration
                || GameDevTV.RTS.Utilities.SectorColonization.HasUnclaimedUnlockedSector();

            if (!shouldColonize) return preview;

            int index = GameDevTV.RTS.Utilities.SectorColonization.GetClosestSectorNeedingCommandPostIndex();
            if (index < 0) return preview;

            preview.WillColonize = true;
            preview.TargetSectorIndex = index;
            return preview;
        }

        /// <summary>
        /// When a terraforming round completes, open and claim the geographically closest
        /// sector that still needs a Command Post (if progression requires another sector).
        /// </summary>
        private void TryAutoColonizeClosestSectorAfterRoundComplete()
        {
            LastColonizationResult = default;

            if (SectorManager.Instance == null) return;
            if (SectorManager.Instance.GetNextLockedSectorIndex() < 0
                && !GameDevTV.RTS.Utilities.SectorColonization.HasUnclaimedUnlockedSector())
            {
                return;
            }

            // Normal play: unlocked map sectors must keep pace with the generation index.
            // Expansion: any remaining sector may be claimed.
            bool shouldColonize = IsExpansionPhase
                || SectorManager.Instance.GetUnlockedSectorCount() < CurrentGeneration
                || GameDevTV.RTS.Utilities.SectorColonization.HasUnclaimedUnlockedSector();

            if (!shouldColonize) return;

            int targetIndex = GameDevTV.RTS.Utilities.SectorColonization.GetClosestSectorNeedingCommandPostIndex();
            LastColonizationResult = new ColonizationAdvanceResult
            {
                Attempted = targetIndex >= 0,
                SectorIndex = targetIndex
            };

            bool colonized = GameDevTV.RTS.Utilities.SectorColonization.TryColonizeClosestSectorNeedingCommandPost(Owner.Player1);
            bool verified = colonized
                && targetIndex >= 0
                && targetIndex < SectorManager.Instance.Sectors.Count
                && GameDevTV.RTS.Utilities.SectorColonization.SectorHasCommandPost(SectorManager.Instance.Sectors[targetIndex]);

            LastColonizationResult = new ColonizationAdvanceResult
            {
                Attempted = targetIndex >= 0,
                Succeeded = verified,
                SectorIndex = targetIndex,
                Message = verified
                    ? $"Command Post deployed in Sector {targetIndex + 1}."
                    : colonized
                        ? "Sector updated, but the Command Post could not be verified."
                        : "Could not colonize the next sector."
            };

            if (verified)
            {
                Debug.Log($"[GenerationManager] Auto-colonized closest sector after completing generation {CurrentGeneration - 1}.");
            }
            else if (LastColonizationResult.Attempted)
            {
                Debug.LogWarning($"[GenerationManager] Colonization issue after generation {CurrentGeneration - 1}: {LastColonizationResult.Message}");
            }
        }

        private void UnlockPrerequisitesForMilestone()
        {
            // MVP tile kit — always available for the one-round climate win.
            BlueprintDraftManager.UnlockBuilding("Command Post");
            BlueprintDraftManager.UnlockBuilding("Solar Panel");
            BlueprintDraftManager.UnlockBuilding("GHG Factory");
            BlueprintDraftManager.UnlockBuilding("Geothermal Generator");
            BlueprintDraftManager.UnlockBuilding("Methanogenic Microbe Spreader");
            BlueprintDraftManager.UnlockBuilding("Atmospheric Condenser");
            BlueprintDraftManager.UnlockBuilding("Carbon Dioxide Import Laser");
            BlueprintDraftManager.UnlockBuilding("Water Ice Aquifer");
            BlueprintDraftManager.UnlockBuilding("Subglacial Water Extractor");
            BlueprintDraftManager.UnlockBuilding("Oxygen Processor");
        }

        public void CompleteExpansion()
        {
            Debug.Log("[GenerationManager] Expansion Completed. Starting new sector lifecycle.");
            IsExpansionPhase = false;
            CurrentGeneration = 1;
            
            // Grant starting materials for the first generation of the new sector
            if (Supplies.Materials != null && Supplies.Materials.ContainsKey(Owner.Player1))
            {
                Supplies.Materials[Owner.Player1] = Supplies.StartingMaterials;
                Supplies.RaiseMaterialsChanged(Owner.Player1, Supplies.Materials[Owner.Player1]);
            }

            // NOTE: Resources are no longer auto-replenished — they persist across rounds.
            roundStartTime = Time.time; // Start the grace period

            UnlockPrerequisitesForMilestone();
            RecordBaselines();

            // Reset progress bar UI to 0% for the new generation
            OnGenerationProgressChanged?.Invoke(0f);

            OnGenerationStarted?.Invoke(CurrentGeneration, MaxGenerations);
        }

        public bool SpendTerraCoins(int amount)
        {
            if (TotalTerraCoins >= amount)
            {
                TotalTerraCoins -= amount;
                OnTerraCoinsChanged?.Invoke(TotalTerraCoins);
                return true;
            }
            return false;
        }

        /// <summary>
        /// True when the player may scout/unlock the next map sector.
        /// During normal play: unlocked sector count must catch up to the current generation
        /// (finish sector N goals, advance to generation N+1, then open sector N+1).
        /// During expansion: any remaining locked sector may be opened.
        /// </summary>
        public static bool CanUnlockNextMapSector()
        {
            if (Instance == null || Instance.IsBetweenRounds) return false;

            if (SectorManager.Instance == null) return false;
            if (SectorManager.Instance.GetNextLockedSectorIndex() < 0) return false;

            if (Instance.IsExpansionPhase) return true;

            return SectorManager.Instance.GetUnlockedSectorCount() < Instance.CurrentGeneration;
        }

        /// <summary>
        /// True when this card's goal is still required to finish the current sector round
        /// (primary milestone plus temperature, atmosphere, and water).
        /// </summary>
        public static bool IsUnmetSectorGoal(string goal)
        {
            if (string.IsNullOrEmpty(goal)) return false;
            if (Instance != null && Instance.IsBetweenRounds) return false;

            bool expansion = Instance != null && Instance.IsExpansionPhase;

            if (expansion)
            {
                if (goal == "EXPLORATION")
                {
                    return SectorManager.Instance != null && SectorManager.Instance.GetNextLockedSectorIndex() >= 0;
                }

                if (goal == "COMMAND POST")
                {
                    if (SectorManager.Instance?.Sectors == null) return false;
                    foreach (var sector in SectorManager.Instance.Sectors)
                    {
                        if (sector != null && !sector.IsLocked && !sector.IsOccupied) return true;
                    }
                    return false;
                }

                if (goal == "OXYGEN")
                {
                    float oxygen = Supplies.Oxygen != null && Supplies.Oxygen.TryGetValue(Owner.Player1, out float o) ? o : 0f;
                    return oxygen < 99.9f;
                }

                return false;
            }

            float temp = Supplies.Temperature != null && Supplies.Temperature.TryGetValue(Owner.Player1, out float tVal) ? tVal : -60f;
            float atmos = Supplies.Atmosphere != null && Supplies.Atmosphere.TryGetValue(Owner.Player1, out float aVal) ? aVal : 0.01f;
            float water = Supplies.Water != null && Supplies.Water.TryGetValue(Owner.Player1, out float wVal) ? wVal : 0f;

            if (goal == "TEMPERATURE")
                return Instance != null && RoundDeltaProgress(temp, Instance.baselineTemperature, SectorTemperatureDelta) < 1f;
            if (goal == "ATMOSPHERE")
                return Instance != null && RoundDeltaProgress(atmos, Instance.baselineAtmosphere, SectorAtmosphereDelta) < 1f;
            if (goal == "WATER")
                return Instance != null && RoundDeltaProgress(water, Instance.baselineWater, SectorWaterDelta) < 1f;

            MilestoneType type = Instance != null ? Instance.CurrentMilestoneType : MilestoneType.Temperature;
            float target = Instance != null ? Instance.CurrentMilestoneTarget : SectorTemperatureDelta;
            if (!GoalMatchesMilestone(goal, type)) return false;
            return ReadMilestoneValue(type) < target;
        }

        private static bool GoalMatchesMilestone(string goal, MilestoneType type)
        {
            return type switch
            {
                MilestoneType.Temperature => goal == "TEMPERATURE",
                MilestoneType.Oxygen => goal == "OXYGEN",
                MilestoneType.Power => goal == "POWER",
                MilestoneType.Population => goal == "POPULATION",
                MilestoneType.CommandPosts => goal == "COMMAND POST",
                MilestoneType.Biomass => false, // deprecated
                _ => false
            };
        }

        private static float ReadMilestoneValue(MilestoneType type)
        {
            if (Instance == null) return 0f;

            switch (type)
            {
                case MilestoneType.Temperature:
                {
                    float temp = Supplies.Temperature != null && Supplies.Temperature.TryGetValue(Owner.Player1, out float t)
                        ? t
                        : -60f;
                    return temp - Instance.baselineTemperature;
                }
                case MilestoneType.Oxygen:
                {
                    float ox = Supplies.Oxygen != null && Supplies.Oxygen.TryGetValue(Owner.Player1, out float o) ? o : 0f;
                    return ox - Instance.baselineOxygen;
                }
                case MilestoneType.Power:
                {
                    float power = Supplies.Power != null && Supplies.Power.TryGetValue(Owner.Player1, out float pow) ? pow : 0f;
                    return power - Instance.baselinePower;
                }
                case MilestoneType.Population:
                {
                    float pop = Supplies.Population != null && Supplies.Population.TryGetValue(Owner.Player1, out int p) ? p : 0f;
                    return pop - Instance.baselinePopulation;
                }
                case MilestoneType.CommandPosts:
                    if (SectorManager.Instance?.ActiveSector != null && SectorManager.Instance.ActiveSector.IsOccupied)
                        return 1f;
                    return 0f;
                case MilestoneType.Biomass:
                    // Deprecated — never gates sector completion.
                    return float.MaxValue;
                default:
                    return 0f;
            }
        }
    }
}
