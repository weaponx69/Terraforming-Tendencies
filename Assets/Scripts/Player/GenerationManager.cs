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
        public bool IsExpansionPhase { get; private set; } = false;

        private int initialAmountInSector = 0;

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
            IsExpansionPhase = false;
            initialAmountInSector = 0;
            OnGenerationStarted?.Invoke(CurrentGeneration, MaxGenerations);
        }

        private void Update()
        {
            if (IsBetweenRounds) return;
            if (IsExpansionPhase) return; // Wait for expansion to complete before tracking resources
            if (SectorManager.Instance == null || SectorManager.Instance.ActiveSector == null) return;

            if (initialAmountInSector <= 0)
            {
                CalculateInitialAmount();
                if (initialAmountInSector <= 0) return; // Still no resources found? Keep waiting.
            }

            var allNodes = UnityEngine.Object.FindObjectsByType<GatherableSupply>(FindObjectsInactive.Exclude);
            int currentAmount = 0;
            foreach (var node in allNodes)
            {
                if (node != null && node.Amount > 0 && SectorManager.Instance.GetNearestSector(node.transform.position) == SectorManager.Instance.ActiveSector)
                {
                    currentAmount += node.Amount;
                }
            }

            float thresholdAmount = initialAmountInSector * 0.8f;
            float amountToMine = initialAmountInSector - thresholdAmount;
            float amountMined = initialAmountInSector - currentAmount;
            
            float progress = amountToMine > 0 ? Mathf.Clamp01(amountMined / amountToMine) : 0f;
            OnGenerationProgressChanged?.Invoke(progress);

            // End generation if 1/5th (20%) of the sector's total resources have been mined
            if (currentAmount <= thresholdAmount)
            {
                TriggerGenerationEnd();
            }
        }

        private void CalculateInitialAmount()
        {
            var allNodes = UnityEngine.Object.FindObjectsByType<GatherableSupply>(FindObjectsInactive.Exclude);
            int totalAmount = 0;
            foreach (var node in allNodes)
            {
                if (node != null && node.Amount > 0 && SectorManager.Instance.GetNearestSector(node.transform.position) == SectorManager.Instance.ActiveSector)
                {
                    totalAmount += node.Amount;
                }
            }
            initialAmountInSector = totalAmount;
            Debug.Log($"[GenerationManager] Active Sector initialized with {initialAmountInSector} total resources. Round will end when {initialAmountInSector * 0.8f} remain.");
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
            
            if (OnGenerationEnded == null)
            {
                Debug.LogError("[GenerationManager] CRITICAL: OnGenerationEnded is NULL! No one is subscribed! GenerationSummaryUI must have unsubscribed or never subscribed!");
            }
            else
            {
                Debug.Log($"[GenerationManager] Invoking OnGenerationEnded. Subscribers count: {OnGenerationEnded.GetInvocationList().Length}");
                foreach (var d in OnGenerationEnded.GetInvocationList())
                {
                    Debug.Log($"[GenerationManager] Subscriber: {d.Target?.GetType().Name}.{d.Method.Name}");
                }
            }

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
                Debug.Log("[GenerationManager] Sector Completed. Entering Expansion Phase.");
                IsExpansionPhase = true;
                IsBetweenRounds = false;
                Time.timeScale = 1f; // Unpause the game so the probe can explore
                return;
            }

            IsBetweenRounds = false;
            Time.timeScale = 1f;

            // Replenish resources on the map
            PlanetGenerator.Instance?.ReplenishResources();
            initialAmountInSector = 0; // Recalculate next frame

            // Fire event
            OnGenerationStarted?.Invoke(CurrentGeneration, MaxGenerations);
        }

        public void CompleteExpansion()
        {
            Debug.Log("[GenerationManager] Expansion Completed. Starting new sector lifecycle.");
            IsExpansionPhase = false;
            CurrentGeneration = 1;
            
            PlanetGenerator.Instance?.ReplenishResources();
            initialAmountInSector = 0; // Recalculate next frame

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
