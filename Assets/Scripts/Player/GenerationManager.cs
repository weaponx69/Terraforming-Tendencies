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
        Biomass,
        Oxygen,
        Power,
        Population,
        CommandPosts
    }

    [System.Serializable]
    public struct SectorMilestone
    {
        public MilestoneType Type;
        public float TargetValue;
        public string GoalDescription;
    }

    public class GenerationManager : MonoBehaviour
    {
        public static GenerationManager Instance { get; private set; }

        public int CurrentGeneration { get; private set; } = 1;
        public int MaxGenerations { get; private set; } = 5; // Default fallback
        public int TotalTerraCoins { get; private set; } = 0;
        public bool IsBetweenRounds { get; private set; } = false;
        public bool IsExpansionPhase { get; private set; } = false;

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
                    new SectorMilestone { Type = MilestoneType.Biomass, TargetValue = 25f, GoalDescription = "Reach 25% Biomass" },
                    new SectorMilestone { Type = MilestoneType.Power, TargetValue = 20f, GoalDescription = "Generate 20 Grid Power" },
                    new SectorMilestone { Type = MilestoneType.Oxygen, TargetValue = 30f, GoalDescription = "Reach 30% Atmospheric Oxygen" },
                    new SectorMilestone { Type = MilestoneType.Population, TargetValue = 10f, GoalDescription = "Establish 10 Colonists" },
                    new SectorMilestone { Type = MilestoneType.CommandPosts, TargetValue = 1f, GoalDescription = "Establish a Command Post in the sector" }
                };
            }

            // Dynamically scale the Oxygen and Biomass milestone targets to (100% / number of sectors) * unlocked sectors count
            if (SectorManager.Instance != null && SectorManager.Instance.Sectors != null && SectorManager.Instance.Sectors.Count > 0)
            {
                int unlockedCount = 0;
                foreach (var sector in SectorManager.Instance.Sectors)
                {
                    if (sector != null && !sector.IsLocked)
                    {
                        unlockedCount++;
                    }
                }
                unlockedCount = Mathf.Max(1, unlockedCount);

                float targetValue = (100f / SectorManager.Instance.Sectors.Count) * unlockedCount;
                for (int i = 0; i < milestones.Count; i++)
                {
                    if (milestones[i].Type == MilestoneType.Oxygen)
                    {
                        var m = milestones[i];
                        m.TargetValue = targetValue;
                        m.GoalDescription = $"Reach {targetValue:F0}% Atmospheric Oxygen";
                        milestones[i] = m;
                    }
                    else if (milestones[i].Type == MilestoneType.Biomass)
                    {
                        var m = milestones[i];
                        m.TargetValue = targetValue;
                        m.GoalDescription = $"Reach {targetValue:F0}% Biomass";
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
        }

        private void Update()
        {
            if (IsBetweenRounds) return;
            if (IsExpansionPhase) return;
            if (Time.time < roundStartTime + 2f) return;

            if (SectorManager.Instance == null || SectorManager.Instance.ActiveSector == null) return;
            if (milestones == null || milestones.Count == 0) InitializeDefaultMilestones();
        }

        public void TriggerGenerationEnd()
        {
            if (IsBetweenRounds) return;
            IsBetweenRounds = true;

            // Liquidate current materials to Terra-Coins
            int earnedTC = LiquidateMaterials();
            TotalTerraCoins += earnedTC;
            OnTerraCoinsChanged?.Invoke(TotalTerraCoins);

            // Pause the game
            Time.timeScale = 0f;

            Debug.Log($"[GenerationManager] Generation {CurrentGeneration} ended. Earned {earnedTC} TC. Total TC: {TotalTerraCoins}");
            
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

            int milestoneIndex = Mathf.Clamp(CurrentGeneration - 1, 0, milestones.Count - 1);
            var milestone = milestones[milestoneIndex];

            float currentValue = 0f;
            switch (milestone.Type)
            {
                case MilestoneType.Biomass:
                    if (Supplies.Biomass != null && Supplies.Biomass.TryGetValue(Owner.Player1, out float bio))
                        currentValue = bio;
                    break;
                case MilestoneType.Oxygen:
                    if (Supplies.Oxygen != null && Supplies.Oxygen.TryGetValue(Owner.Player1, out float ox))
                        currentValue = ox;
                    break;
                case MilestoneType.Power:
                    if (Supplies.Power != null && Supplies.Power.TryGetValue(Owner.Player1, out float pow))
                        currentValue = pow - baselinePower;
                    break;
                case MilestoneType.Population:
                    if (Supplies.Population != null && Supplies.Population.TryGetValue(Owner.Player1, out int pop))
                        currentValue = pop - baselinePopulation;
                    break;
                case MilestoneType.CommandPosts:
                    int cpCount = 0;
                    foreach (var building in BaseBuilding.ActiveBuildings)
                    {
                        if (building != null && building.BuildingSO != null && building.BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase) &&
                            building.Progress.State == BuildingProgress.BuildingState.Completed)
                        {
                            cpCount++;
                        }
                    }
                    currentValue = cpCount;
                    break;
            }

            CurrentMilestoneTarget = milestone.TargetValue;
            CurrentMilestoneType = milestone.Type;
            CurrentMilestoneValue = currentValue;

            float primaryProgress = milestone.TargetValue > 0 ? Mathf.Clamp01(currentValue / milestone.TargetValue) : 1f;

            // Temperature progress
            float currentTemp = Supplies.Temperature.TryGetValue(Owner.Player1, out float tVal) ? tVal : -60f;
            float targetTemp = GetTargetTemperature(CurrentGeneration);
            float tempProgress = Mathf.Clamp01((currentTemp - (-60f)) / (targetTemp - (-60f)));

            // Atmosphere progress
            float currentAtmos = Supplies.Atmosphere.TryGetValue(Owner.Player1, out float aVal) ? aVal : 0.01f;
            float targetAtmos = GetTargetAtmosphere(CurrentGeneration);
            float atmosProgress = Mathf.Clamp01((currentAtmos - 0.01f) / (targetAtmos - 0.01f));

            // Water progress
            float currentWater = Supplies.Water.TryGetValue(Owner.Player1, out float wVal) ? wVal : 0f;
            float targetWater = GetTargetWater(CurrentGeneration);
            float waterProgress = targetWater > 0f ? Mathf.Clamp01(currentWater / targetWater) : 1f;

            // Combined progress is the bottleneck (minimum) of all four parameters
            float progress = Mathf.Min(primaryProgress, Mathf.Min(tempProgress, Mathf.Min(atmosProgress, waterProgress)));

            OnGenerationProgressChanged?.Invoke(progress);

            if (progress >= 1f)
            {
                TriggerGenerationEnd();
            }
        }

        public void CheatCompleteGeneration()
        {
            if (IsBetweenRounds || IsExpansionPhase) return;

            // Fire progress change to 100% so UI elements update and show 100% completion
            OnGenerationProgressChanged?.Invoke(1f);

            TriggerGenerationEnd();
        }

        public void CheatSkipToExpansion()
        {
            if (IsBetweenRounds || IsExpansionPhase) return;

            // Skip straight to the final milestone so the next transition enters the expansion phase
            CurrentGeneration = MaxGenerations;

            // Fire progress change to 100% so UI elements update and show 100% completion
            OnGenerationProgressChanged?.Invoke(1f);

            TriggerGenerationEnd();
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

                // Sector unlocking is now handled entirely through exploration cards
                // (Orbital Scan, Survey Drone). The player must play a scouting card to
                // explore and unlock the next sector before building a Command Post there.

                // Explicitly unlock the Command Post blueprint for the expansion phase
                BlueprintDraftManager.UnlockBuilding("Command Post");

                // Reset progress bar to 0% for the expansion phase
                OnGenerationProgressChanged?.Invoke(0f);

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

            // NOTE: Sector unlocking is now handled by ExplorationManager via scouting cards.
            // Resources are no longer auto-replenished — they persist across rounds.
            roundStartTime = Time.time; // Start the grace period

            UnlockPrerequisitesForMilestone();
            RecordBaselines();

            // Fire event
            OnGenerationStarted?.Invoke(CurrentGeneration, MaxGenerations);
        }

        private void UnlockPrerequisitesForMilestone()
        {
            if (milestones == null || milestones.Count == 0) InitializeDefaultMilestones();
            int milestoneIndex = Mathf.Clamp(CurrentGeneration - 1, 0, milestones.Count - 1);
            var milestone = milestones[milestoneIndex];

            switch (milestone.Type)
            {
                case MilestoneType.Biomass:
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

            if (goal == "TEMPERATURE") return temp < targetTemp;
            if (goal == "ATMOSPHERE") return atmos < targetAtmos;
            if (goal == "WATER") return water < targetWater;

            MilestoneType type = Instance != null ? Instance.CurrentMilestoneType : MilestoneType.Biomass;
            float target = Instance != null ? Instance.CurrentMilestoneTarget : 25f;
            if (!GoalMatchesMilestone(goal, type)) return false;
            return ReadMilestoneValue(type) < target;
        }

        private static bool GoalMatchesMilestone(string goal, MilestoneType type)
        {
            return type switch
            {
                MilestoneType.Biomass => goal == "BIOMASS",
                MilestoneType.Oxygen => goal == "OXYGEN",
                MilestoneType.Power => goal == "POWER",
                MilestoneType.Population => goal == "POPULATION",
                MilestoneType.CommandPosts => goal == "COMMAND POST",
                _ => false
            };
        }

        private static float ReadMilestoneValue(MilestoneType type)
        {
            switch (type)
            {
                case MilestoneType.Biomass:
                    return Supplies.Biomass != null && Supplies.Biomass.TryGetValue(Owner.Player1, out float bio) ? bio : 0f;
                case MilestoneType.Oxygen:
                    return Supplies.Oxygen != null && Supplies.Oxygen.TryGetValue(Owner.Player1, out float ox) ? ox : 0f;
                case MilestoneType.Power:
                    float power = Supplies.Power != null && Supplies.Power.TryGetValue(Owner.Player1, out float pow) ? pow : 0f;
                    return Instance != null ? power - Instance.baselinePower : power;
                case MilestoneType.Population:
                    float pop = Supplies.Population != null && Supplies.Population.TryGetValue(Owner.Player1, out int p) ? p : 0f;
                    return Instance != null ? pop - Instance.baselinePopulation : pop;
                case MilestoneType.CommandPosts:
                    int cpCount = 0;
                    foreach (var building in BaseBuilding.ActiveBuildings)
                    {
                        if (building != null && building.BuildingSO != null &&
                            building.BuildingSO.Name.Contains("Command", StringComparison.OrdinalIgnoreCase) &&
                            building.Progress.State == BuildingProgress.BuildingState.Completed)
                        {
                            cpCount++;
                        }
                    }
                    return cpCount;
                default:
                    return 0f;
            }
        }
    }
}
