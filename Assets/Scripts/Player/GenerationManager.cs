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

        public float GetTargetTemperature(int generation)
        {
            return -60f + (15f * generation);
        }

        public float GetTargetAtmosphere(int generation)
        {
            return 0.25f * generation;
        }

        public float GetTargetWater(int generation)
        {
            return 10.0f * generation - 5.0f;
        }

        public string CurrentMilestoneDescription
        {
            get
            {
                if (milestones == null || milestones.Count == 0) InitializeDefaultMilestones();
                int milestoneIndex = Mathf.Clamp(CurrentGeneration - 1, 0, milestones.Count - 1);
                string baseDesc = milestones[milestoneIndex].GoalDescription;
                if (IsExpansionPhase)
                {
                    return baseDesc;
                }
                float targetTemp = GetTargetTemperature(CurrentGeneration);
                float targetAtmos = GetTargetAtmosphere(CurrentGeneration);
                float targetWater = GetTargetWater(CurrentGeneration);
                return $"{baseDesc} (Temp >= {targetTemp:F0}°C, Atmos >= {targetAtmos:F2} atm, Water >= {targetWater:F0}%)";
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
            
            // Subscribe to GameFlowManager's milestone event
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.OnTurnMilestones += CheckMilestones;
            }
        }

        private void OnDisable()
        {
            PlanetGenerator.OnPlanetGenerated -= InitializeGenerations;
            BlueprintDraftManager.OnDraftCompleted -= HandleDraftCompleted;
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
                MaxGenerations = Mathf.Min(SectorManager.Instance.Sectors.Count, milestones.Count);
            }
            CurrentGeneration = 1;
            IsBetweenRounds = false;
            IsExpansionPhase = false;

            if (milestones != null && milestones.Count > 0)
            {
                CurrentMilestoneType = milestones[0].Type;
                CurrentMilestoneTarget = milestones[0].TargetValue;
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
                    new SectorMilestone { Type = MilestoneType.Temperature, TargetValue = -45f, GoalDescription = "Raise Temperature to -45°C" },
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
                        float targetTemp = GetTargetTemperature(generation);
                        var m = milestones[i];
                        m.TargetValue = targetTemp;
                        m.GoalDescription = $"Raise Temperature to {targetTemp:F0}°C";
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
        }

        /// <summary>Progress toward target from the round-start baseline (0–1).</summary>
        private static float IncrementalProgress(float current, float baseline, float target)
        {
            // Higher-is-better metrics: already at the absolute target counts as done.
            // Prevents softlocks when a sector reset records a baseline above the formula target.
            if (current >= target - 0.0001f) return 1f;

            float delta = target - baseline;
            if (delta <= 0.0001f) return 1f;
            return Mathf.Clamp01((current - baseline) / delta);
        }

        private void MarkActiveSectorRoundComplete()
        {
            if (SectorManager.Instance?.ActiveSector == null) return;
            var sector = SectorManager.Instance.ActiveSector;
            sector.TerraformingCompletionPercent = 1f;
            sector.CompletedGenerationRound = CurrentGeneration;
            Debug.Log($"[GenerationManager] Sector at {sector.Center} marked complete for generation {CurrentGeneration}.");
        }

        private void Update()
        {
            if (IsBetweenRounds) return;
            if (IsExpansionPhase) return;
            if (Time.time < roundStartTime + 2f) return;

            if (SectorManager.Instance == null || SectorManager.Instance.ActiveSector == null) return;
            if (milestones == null || milestones.Count == 0) InitializeDefaultMilestones();
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

            if (SectorManager.Instance == null || SectorManager.Instance.ActiveSector == null) return;
            if (milestones == null || milestones.Count == 0) InitializeDefaultMilestones();

            float progress = CalculateCurrentSectorProgress(out _);
            OnGenerationProgressChanged?.Invoke(progress);

            if (progress >= 1f)
            {
                TriggerGenerationEnd();
            }
        }

        /// <summary>
        /// Combined sector progress: primary milestone plus temperature, atmosphere, and water.
        /// Returns 0–1; the bottleneck metric is written to <paramref name="bottleneck"/>.
        /// </summary>
        public float CalculateCurrentSectorProgress(out string bottleneck)
        {
            bottleneck = null;
            if (milestones == null || milestones.Count == 0) InitializeDefaultMilestones();

            int milestoneIndex = Mathf.Clamp(CurrentGeneration - 1, 0, milestones.Count - 1);
            var milestone = milestones[milestoneIndex];

            float currentValue = ReadMilestoneValue(milestone.Type);
            CurrentMilestoneTarget = milestone.TargetValue;
            CurrentMilestoneType = milestone.Type;
            CurrentMilestoneValue = currentValue;

            float primaryProgress = milestone.Type == MilestoneType.Temperature
                ? IncrementalProgress(currentValue, baselineTemperature, milestone.TargetValue)
                : milestone.TargetValue > 0
                    ? Mathf.Clamp01(currentValue / milestone.TargetValue)
                    : 1f;

            float currentTemp = Supplies.Temperature.TryGetValue(Owner.Player1, out float tVal) ? tVal : -60f;
            float targetTemp = GetTargetTemperature(CurrentGeneration);
            float tempProgress = IncrementalProgress(currentTemp, baselineTemperature, targetTemp);

            float currentAtmos = Supplies.Atmosphere.TryGetValue(Owner.Player1, out float aVal) ? aVal : 0.01f;
            float targetAtmos = GetTargetAtmosphere(CurrentGeneration);
            float atmosProgress = IncrementalProgress(currentAtmos, baselineAtmosphere, targetAtmos);

            float currentWater = Supplies.Water.TryGetValue(Owner.Player1, out float wVal) ? wVal : 0f;
            float targetWater = GetTargetWater(CurrentGeneration);
            float waterProgress = IncrementalProgress(currentWater, baselineWater, targetWater);

            float progress = Mathf.Min(primaryProgress, Mathf.Min(tempProgress, Mathf.Min(atmosProgress, waterProgress)));

            if (progress < 1f)
            {
                if (primaryProgress <= progress) bottleneck = milestone.Type.ToString();
                else if (tempProgress <= progress) bottleneck = "TEMPERATURE";
                else if (atmosProgress <= progress) bottleneck = "ATMOSPHERE";
                else bottleneck = "WATER";
            }

            return progress;
        }

        /// <summary>
        /// True when the primary milestone and all climate targets for the current generation are met.
        /// </summary>
        public bool IsCurrentSectorRoundComplete()
        {
            if (IsBetweenRounds || IsExpansionPhase) return false;
            if (SectorManager.Instance == null || SectorManager.Instance.ActiveSector == null) return false;
            return CalculateCurrentSectorProgress(out _) >= 1f;
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
            if (milestones == null || milestones.Count == 0) InitializeDefaultMilestones();
            int milestoneIndex = Mathf.Clamp(CurrentGeneration - 1, 0, milestones.Count - 1);
            var milestone = milestones[milestoneIndex];

            switch (milestone.Type)
            {
                case MilestoneType.Temperature:
                    BlueprintDraftManager.UnlockBuilding("GHG Factory");
                    BlueprintDraftManager.UnlockBuilding("Oxygen Processor");
                    BlueprintDraftManager.UnlockBuilding("Solar Panel");
                    break;
                case MilestoneType.Oxygen:
                    BlueprintDraftManager.UnlockBuilding("Oxygen Processor");
                    BlueprintDraftManager.UnlockBuilding("Solar Panel");
                    break;
                case MilestoneType.Power:
                    BlueprintDraftManager.UnlockBuilding("Solar Panel");
                    break;
                case MilestoneType.Population:
                    BlueprintDraftManager.UnlockBuilding("Habitat");
                    BlueprintDraftManager.UnlockBuilding("Spaceport");
                    break;
                case MilestoneType.CommandPosts:
                    BlueprintDraftManager.UnlockBuilding("Command Post");
                    break;
                case MilestoneType.Biomass:
                    // Deprecated terraforming goal — treat like Temperature bootstrap.
                    BlueprintDraftManager.UnlockBuilding("GHG Factory");
                    BlueprintDraftManager.UnlockBuilding("Oxygen Processor");
                    BlueprintDraftManager.UnlockBuilding("Solar Panel");
                    break;
            }
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

            int generation = Instance != null ? Mathf.Max(1, Instance.CurrentGeneration) : 1;
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

            float targetTemp = Instance != null ? Instance.GetTargetTemperature(generation) : -60f + 15f * generation;
            float targetAtmos = Instance != null ? Instance.GetTargetAtmosphere(generation) : 0.25f * generation;
            float targetWater = Instance != null ? Instance.GetTargetWater(generation) : 10f * generation - 5f;

            if (goal == "TEMPERATURE")
                return Instance != null && IncrementalProgress(temp, Instance.baselineTemperature, targetTemp) < 1f;
            if (goal == "ATMOSPHERE")
                return Instance != null && IncrementalProgress(atmos, Instance.baselineAtmosphere, targetAtmos) < 1f;
            if (goal == "WATER")
                return Instance != null && IncrementalProgress(water, Instance.baselineWater, targetWater) < 1f;

            MilestoneType type = Instance != null ? Instance.CurrentMilestoneType : MilestoneType.Temperature;
            float target = Instance != null ? Instance.CurrentMilestoneTarget : -45f;
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
                    return Supplies.Temperature != null && Supplies.Temperature.TryGetValue(Owner.Player1, out float t)
                        ? t
                        : -60f;
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
