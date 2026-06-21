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

        public string CurrentMilestoneDescription
        {
            get
            {
                if (milestones == null || milestones.Count == 0) InitializeDefaultMilestones();
                int milestoneIndex = Mathf.Clamp(CurrentGeneration - 1, 0, milestones.Count - 1);
                return milestones[milestoneIndex].GoalDescription;
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
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            PlanetGenerator.OnPlanetGenerated += InitializeGenerations;
        }

        private void OnDisable()
        {
            PlanetGenerator.OnPlanetGenerated -= InitializeGenerations;
        }

        private void InitializeGenerations()
        {
            InitializeDefaultMilestones();
            if (SectorManager.Instance != null && SectorManager.Instance.Sectors.Count > 0)
            {
                MaxGenerations = Mathf.Min(SectorManager.Instance.Sectors.Count, milestones.Count);
            }
            CurrentGeneration = 1;
            IsBetweenRounds = false;
            IsExpansionPhase = false;
            UnlockPrerequisitesForMilestone();
            RecordBaselines();
            OnGenerationStarted?.Invoke(CurrentGeneration, MaxGenerations);
        }

        private void InitializeDefaultMilestones()
        {
            if (milestones == null || milestones.Count == 0)
            {
                milestones = new List<SectorMilestone>
                {
                    new SectorMilestone { Type = MilestoneType.Biomass, TargetValue = 250f, GoalDescription = "Accumulate 250 Biomass" },
                    new SectorMilestone { Type = MilestoneType.Power, TargetValue = 20f, GoalDescription = "Generate 20 Grid Power" },
                    new SectorMilestone { Type = MilestoneType.Oxygen, TargetValue = 30f, GoalDescription = "Reach 30% Atmospheric Oxygen" },
                    new SectorMilestone { Type = MilestoneType.Population, TargetValue = 10f, GoalDescription = "Establish 10 Colonists" },
                    new SectorMilestone { Type = MilestoneType.CommandPosts, TargetValue = 1f, GoalDescription = "Establish a Command Post in the sector" }
                };
            }

            // Dynamically scale the Oxygen milestone target to (100% / number of sectors)
            if (SectorManager.Instance != null && SectorManager.Instance.Sectors != null && SectorManager.Instance.Sectors.Count > 0)
            {
                float targetValue = 100f / SectorManager.Instance.Sectors.Count;
                for (int i = 0; i < milestones.Count; i++)
                {
                    if (milestones[i].Type == MilestoneType.Oxygen)
                    {
                        var m = milestones[i];
                        m.TargetValue = targetValue;
                        m.GoalDescription = $"Reach {targetValue:F0}% Atmospheric Oxygen";
                        milestones[i] = m;
                    }
                }
            }
        }

        private void RecordBaselines()
        {
            baselineBiomass = 0f;
            if (Supplies.Biomass != null && Supplies.Biomass.TryGetValue(Owner.Player1, out int bio))
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

            int milestoneIndex = Mathf.Clamp(CurrentGeneration - 1, 0, milestones.Count - 1);
            var milestone = milestones[milestoneIndex];

            float currentValue = 0f;
            switch (milestone.Type)
            {
                case MilestoneType.Biomass:
                    if (Supplies.Biomass != null && Supplies.Biomass.TryGetValue(Owner.Player1, out int bio))
                        currentValue = bio - baselineBiomass;
                    break;
                case MilestoneType.Oxygen:
                    if (Supplies.Oxygen != null && Supplies.Oxygen.TryGetValue(Owner.Player1, out float ox))
                        currentValue = ox - baselineOxygen;
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
                            building.Progress.State == BuildingProgress.BuildingState.Completed &&
                            SectorManager.Instance != null && SectorManager.Instance.GetNearestSector(building.transform.position) == SectorManager.Instance.ActiveSector)
                        {
                            cpCount++;
                        }
                    }
                    currentValue = cpCount;
                    break;
            }

            float progress = milestone.TargetValue > 0 ? Mathf.Clamp01(currentValue / milestone.TargetValue) : 1f;
            OnGenerationProgressChanged?.Invoke(progress);

            if (progress >= 1f)
            {
                TriggerGenerationEnd();
            }
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

                // Unlock the next sector so the player can build a Command Post in it!
                if (SectorManager.Instance != null)
                {
                    SectorManager.Instance.UnlockNextSector();
                }

                return;
            }

            IsBetweenRounds = false;
            Time.timeScale = 1f;

            // Grant starting materials for the next generation
            if (Supplies.Materials != null && Supplies.Materials.ContainsKey(Owner.Player1))
            {
                Supplies.Materials[Owner.Player1] = Supplies.StartingMaterials;
                Supplies.RaiseMaterialsChanged(Owner.Player1, Supplies.Materials[Owner.Player1]);
            }

            // Unlock the next sector!
            if (SectorManager.Instance != null)
            {
                SectorManager.Instance.UnlockNextSector();
            }

            // Replenish resources on the map
            PlanetGenerator.Instance?.ReplenishResources();
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

            PlanetGenerator.Instance?.ReplenishResources();

            UnlockPrerequisitesForMilestone();
            RecordBaselines();

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
    }
}
