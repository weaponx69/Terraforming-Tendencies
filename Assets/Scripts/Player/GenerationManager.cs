using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Units;
using System;

namespace GameDevTV.RTS.Player
{
    public class GenerationManager : MonoBehaviour
    {
        public static GenerationManager Instance { get; private set; }

        public int CurrentGeneration { get; private set; } = 1;
        public int MaxGenerations { get; private set; } = 5; // Default fallback
        public int TotalTerraCoins { get; private set; } = 0;
        public bool IsBetweenRounds { get; private set; } = false;

        private int initialNodesInSector = 0;

        public static event Action<int, int> OnGenerationStarted; // current, max
        public static event Action<int, int> OnGenerationEnded;   // earnedTC, totalTC
        public static event Action<int> OnTerraCoinsChanged; // newTC

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

        private void Start()
        {
            PlanetGenerator.OnPlanetGenerated += InitializeGenerations;
        }

        private void OnDestroy()
        {
            PlanetGenerator.OnPlanetGenerated -= InitializeGenerations;
        }

        private void InitializeGenerations()
        {
            if (SectorManager.Instance != null && SectorManager.Instance.Sectors.Count > 0)
            {
                MaxGenerations = SectorManager.Instance.Sectors.Count * 5;
            }
            CurrentGeneration = 1;
            IsBetweenRounds = false;
            initialNodesInSector = 0;
            OnGenerationStarted?.Invoke(CurrentGeneration, MaxGenerations);
        }

        private void Update()
        {
            if (IsBetweenRounds) return;
            if (SectorManager.Instance == null || SectorManager.Instance.ActiveSector == null) return;

            // Give the colony 30 seconds to bootstrap on the very first start
            if (Time.timeSinceLevelLoad < 30f && CurrentGeneration == 1) return;

            if (initialNodesInSector <= 0)
            {
                CalculateInitialNodes();
                if (initialNodesInSector <= 0) return; // Still no nodes found? Keep waiting.
            }

            var allNodes = UnityEngine.Object.FindObjectsByType<GatherableSupply>(FindObjectsInactive.Exclude);
            int currentNodes = 0;
            foreach (var node in allNodes)
            {
                if (node != null && node.Amount > 0 && SectorManager.Instance.GetNearestSector(node.transform.position) == SectorManager.Instance.ActiveSector)
                {
                    currentNodes++;
                }
            }

            // End generation if 1/5th (20%) of the sector has been mined
            if (currentNodes <= initialNodesInSector * 0.8f)
            {
                TriggerGenerationEnd();
            }
        }

        private void CalculateInitialNodes()
        {
            var allNodes = UnityEngine.Object.FindObjectsByType<GatherableSupply>(FindObjectsInactive.Exclude);
            int count = 0;
            foreach (var node in allNodes)
            {
                if (node != null && node.Amount > 0 && SectorManager.Instance.GetNearestSector(node.transform.position) == SectorManager.Instance.ActiveSector)
                {
                    count++;
                }
            }
            initialNodesInSector = count;
            Debug.Log($"[GenerationManager] Active Sector initialized with {initialNodesInSector} nodes. Round will end when {initialNodesInSector * 0.8f} remain.");
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
            OnGenerationEnded?.Invoke(earnedTC, TotalTerraCoins);
        }

        private int LiquidateMaterials()
        {
            int tc = 0;
            
            if (Supplies.Biomass != null && Supplies.Biomass.TryGetValue(Owner.Player1, out int b)) tc += b;

            // Wipe resources after liquidating so the player starts the next generation at 0 (or a baseline)
            if (Supplies.Biomass != null && Supplies.Biomass.ContainsKey(Owner.Player1))
            {
                Supplies.Biomass[Owner.Player1] = 0;
            }
            Supplies.RaiseBiomassChanged(Owner.Player1, 0);

            return tc / 10;
        }

        public void StartNextGeneration()
        {
            if (!IsBetweenRounds) return;
            
            CurrentGeneration++;
            if (CurrentGeneration > MaxGenerations)
            {
                // Trigger actual Win/Game Over evaluation here based on vegetation
                Debug.Log("[GenerationManager] Final Generation Completed. Triggering End Game Evaluation.");
                // For now, we will just call a victory
                GameOverManager.Instance?.TriggerVictory(); // We need to expose this safely
                return;
            }

            IsBetweenRounds = false;
            Time.timeScale = 1f;

            // Replenish resources on the map
            PlanetGenerator.Instance?.ReplenishResources();
            initialNodesInSector = 0; // Recalculate next frame

            // Fire event
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
