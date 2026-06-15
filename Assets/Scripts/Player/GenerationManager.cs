using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameDevTV.RTS.Environment;
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

        public static event Action<int, int> OnGenerationStarted; // current, max
        public static event Action<int, int> OnGenerationEnded;   // earnedTC, totalTC

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
            OnGenerationStarted?.Invoke(CurrentGeneration, MaxGenerations);
        }

        public void TriggerGenerationEnd()
        {
            if (IsBetweenRounds) return;
            IsBetweenRounds = true;

            // Liquidate current materials to Terra-Coins
            int earnedTC = LiquidateMaterials();
            TotalTerraCoins += earnedTC;

            // Pause the game
            Time.timeScale = 0f;

            Debug.Log($"[GenerationManager] Generation {CurrentGeneration} ended. Earned {earnedTC} TC. Total TC: {TotalTerraCoins}");
            OnGenerationEnded?.Invoke(earnedTC, TotalTerraCoins);
        }

        private int LiquidateMaterials()
        {
            int tc = 0;
            // E.g., 100 Biomass = 10 TC, 100 Minerals = 5 TC... 
            // Simplified for now: 10 Materials of any type = 1 TC
            if (Supplies.Biomass != null && Supplies.Biomass.TryGetValue(Owner.Player1, out int b)) tc += b;
            if (Supplies.Minerals != null && Supplies.Minerals.TryGetValue(Owner.Player1, out float m)) tc += Mathf.FloorToInt(m);
            if (Supplies.Gas != null && Supplies.Gas.TryGetValue(Owner.Player1, out float g)) tc += Mathf.FloorToInt(g);
            if (Supplies.Iron != null && Supplies.Iron.TryGetValue(Owner.Player1, out float i)) tc += Mathf.FloorToInt(i);
            if (Supplies.Regolith != null && Supplies.Regolith.TryGetValue(Owner.Player1, out float r)) tc += Mathf.FloorToInt(r);

            // Wipe resources after liquidating so the player starts the next generation at 0 (or a baseline)
            Supplies.RaiseBiomassChanged(Owner.Player1, 0);
            Supplies.RaiseMineralsChanged(Owner.Player1, 0);
            Supplies.RaiseGasChanged(Owner.Player1, 0);
            Supplies.RaiseIronChanged(Owner.Player1, 0);
            Supplies.RaiseRegolithChanged(Owner.Player1, 0);

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

            // Fire event
            OnGenerationStarted?.Invoke(CurrentGeneration, MaxGenerations);
        }
    }
}
