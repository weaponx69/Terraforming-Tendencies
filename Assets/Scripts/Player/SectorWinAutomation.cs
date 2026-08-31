using System.Collections.Generic;
using System.Text;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// Live Play Mode / CLI automation for verifying a sector can be finished.
    /// Invoke from the connected Editor:
    ///   unity command eval "return GameDevTV.RTS.Player.SectorWinAutomation.Report();" --json
    ///   unity command eval "return GameDevTV.RTS.Player.SectorWinAutomation.TryWinCurrentSector();" --json
    /// Prefer these over spawning <c>unity test</c> while the Editor is already open.
    /// </summary>
    public static class SectorWinAutomation
    {
        public static string Report()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Sector Win Automation Report ===");

            if (!Application.isPlaying)
            {
                sb.AppendLine("FAIL: Editor is not in Play Mode. Run: unity command editor_play");
                return sb.ToString();
            }

            var gm = GenerationManager.Instance;
            if (gm == null)
            {
                sb.AppendLine("FAIL: GenerationManager.Instance is null.");
                return sb.ToString();
            }

            sb.AppendLine($"Playing={Application.isPlaying} BetweenRounds={gm.IsBetweenRounds} Expansion={gm.IsExpansionPhase}");
            sb.AppendLine($"Generation={gm.CurrentGeneration}/{gm.MaxGenerations} Milestone={gm.CurrentMilestoneType} ({gm.CurrentMilestoneValue:F1}/{gm.CurrentMilestoneTarget:F1})");

            float progress = gm.CalculateCurrentSectorProgress(out string bottleneck);
            sb.AppendLine($"Progress={progress:P0} Bottleneck={bottleneck ?? "none"} Complete={gm.IsCurrentSectorRoundComplete()}");

            float temp = Supplies.Temperature.TryGetValue(Owner.Player1, out float t) ? t : -60f;
            float atmos = Supplies.Atmosphere.TryGetValue(Owner.Player1, out float a) ? a : 0.01f;
            float water = Supplies.Water.TryGetValue(Owner.Player1, out float w) ? w : 0f;
            sb.AppendLine($"Climate Temp={temp:F1}/{gm.GetTargetTemperature(gm.CurrentGeneration):F1} " +
                          $"Atmos={atmos:F2}/{gm.GetTargetAtmosphere(gm.CurrentGeneration):F2} " +
                          $"Water={water:F0}/{gm.GetTargetWater(gm.CurrentGeneration):F0}");

            int materials = Supplies.Materials.TryGetValue(Owner.Player1, out int m) ? m : 0;
            sb.AppendLine($"Materials={materials}");

            AppendDeckSummary(sb);
            AppendPadSummary(sb);
            AppendUnmetGoals(sb);
            return sb.ToString();
        }

        /// <summary>
        /// Meets climate + primary milestone targets for the current generation, then
        /// ends the round if all goals are complete. Returns a result string for CLI.
        /// </summary>
        public static string TryWinCurrentSector()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Report());

            if (!Application.isPlaying)
            {
                return sb.ToString();
            }

            var gm = GenerationManager.Instance;
            if (gm == null)
            {
                sb.AppendLine("RESULT: FAIL (no GenerationManager)");
                return sb.ToString();
            }

            if (gm.IsBetweenRounds)
            {
                sb.AppendLine("RESULT: ALREADY_BETWEEN_ROUNDS");
                return sb.ToString();
            }

            if (gm.IsExpansionPhase)
            {
                sb.AppendLine("RESULT: SKIP (expansion phase — no standard sector round)");
                return sb.ToString();
            }

            MeetCurrentSectorGoals(sb);

            float progress = gm.CalculateCurrentSectorProgress(out string bottleneck);
            sb.AppendLine($"After meet: Progress={progress:P0} Bottleneck={bottleneck ?? "none"} Complete={gm.IsCurrentSectorRoundComplete()}");

            if (!gm.IsCurrentSectorRoundComplete())
            {
                sb.AppendLine($"RESULT: FAIL (still incomplete — bottleneck {bottleneck})");
                return sb.ToString();
            }

            gm.TriggerGenerationEnd();
            sb.AppendLine($"After TriggerGenerationEnd: BetweenRounds={gm.IsBetweenRounds}");
            sb.AppendLine(gm.IsBetweenRounds ? "RESULT: PASS" : "RESULT: FAIL (generation did not end)");
            return sb.ToString();
        }

        /// <summary>
        /// Push supplies to at least the current sector targets (climate + primary milestone).
        /// Does not place buildings — verifies the win gate / progress math.
        /// </summary>
        public static void MeetCurrentSectorGoals(StringBuilder log = null)
        {
            var gm = GenerationManager.Instance;
            if (gm == null) return;

            // Sync CurrentMilestone* from the active generation.
            gm.CalculateCurrentSectorProgress(out _);

            int gen = Mathf.Max(1, gm.CurrentGeneration);
            float targetTemp = gm.GetTargetTemperature(gen);
            float targetAtmos = gm.GetTargetAtmosphere(gen);
            float targetWater = gm.GetTargetWater(gen);

            float temp = Supplies.Temperature.TryGetValue(Owner.Player1, out float t) ? t : -60f;
            float atmos = Supplies.Atmosphere.TryGetValue(Owner.Player1, out float a) ? a : 0.01f;
            float water = Supplies.Water.TryGetValue(Owner.Player1, out float w) ? w : 0f;

            if (temp < targetTemp)
            {
                Supplies.UpdateTemperature(Owner.Player1, targetTemp);
                ClimateManager.Instance?.SetTemperatureTarget(targetTemp);
                log?.AppendLine($"Set Temperature -> {targetTemp:F1}");
            }

            if (atmos < targetAtmos)
            {
                Supplies.UpdateAtmosphere(Owner.Player1, targetAtmos);
                ClimateManager.Instance?.SetAtmosphereTarget(targetAtmos);
                log?.AppendLine($"Set Atmosphere -> {targetAtmos:F2}");
            }

            if (water < targetWater)
            {
                Supplies.UpdateWater(Owner.Player1, targetWater);
                ClimateManager.Instance?.SetWaterTarget(targetWater);
                log?.AppendLine($"Set Water -> {targetWater:F0}");
            }

            MeetPrimaryMilestone(gm, log);

            if (Supplies.Materials.TryGetValue(Owner.Player1, out int mats) && mats < 500)
            {
                Supplies.UpdateMaterials(Owner.Player1, 2000);
                log?.AppendLine("Granted Materials -> 2000 (automation buffer)");
            }
        }

        private static void MeetPrimaryMilestone(GenerationManager gm, StringBuilder log)
        {
            float target = gm.CurrentMilestoneTarget;
            switch (gm.CurrentMilestoneType)
            {
                case MilestoneType.Biomass:
                {
                    float bio = Supplies.Biomass.TryGetValue(Owner.Player1, out float b) ? b : 0f;
                    float needed = target + 0.05f; // clear float edge below TargetValue
                    if (bio < needed)
                    {
                        Supplies.UpdateBiomass(Owner.Player1, needed);
                        log?.AppendLine($"Set Biomass -> {needed:F2}");
                    }
                    break;
                }
                case MilestoneType.Oxygen:
                {
                    float ox = Supplies.Oxygen.TryGetValue(Owner.Player1, out float o) ? o : 0f;
                    float needed = target + 0.05f;
                    if (ox < needed)
                    {
                        Supplies.UpdateOxygen(Owner.Player1, needed);
                        log?.AppendLine($"Set Oxygen -> {needed:F2}");
                    }
                    break;
                }
                case MilestoneType.Power:
                {
                    float pow = Supplies.Power.TryGetValue(Owner.Player1, out float p) ? p : 0f;
                    // Power milestone uses baseline-relative value; push absolute high enough.
                    float needed = target + 50f;
                    if (pow < needed)
                    {
                        Supplies.UpdatePower(Owner.Player1, needed);
                        log?.AppendLine($"Set Power -> {needed:F0}");
                    }
                    break;
                }
                case MilestoneType.Population:
                {
                    int pop = Supplies.Population.TryGetValue(Owner.Player1, out int v) ? v : 0;
                    int needed = Mathf.CeilToInt(target) + 5;
                    if (pop < needed)
                    {
                        Supplies.UpdatePopulation(Owner.Player1, needed);
                        log?.AppendLine($"Set Population -> {needed}");
                    }
                    break;
                }
                case MilestoneType.CommandPosts:
                    log?.AppendLine("CommandPosts milestone: ensure a completed Command Post exists in-scene.");
                    break;
            }
        }

        private static void AppendDeckSummary(StringBuilder sb)
        {
            var deck = CardDeckController.Instance;
            if (deck == null)
            {
                sb.AppendLine("Deck: CardDeckController missing");
                return;
            }

            int winInHand = 0;
            int winInMaster = 0;
            var handGoals = new List<string>();
            foreach (var card in deck.Hand)
            {
                string goal = TerraformingGoalColors.GetSectorGoalForCard(card);
                if (goal == null) continue;
                winInHand++;
                handGoals.Add($"{card.cardName}[{goal}]");
            }

            if (deck.MasterDeck != null)
            {
                foreach (var card in deck.MasterDeck)
                {
                    if (TerraformingGoalColors.GetSectorGoalForCard(card) != null) winInMaster++;
                }
            }

            sb.AppendLine($"Deck: hand={deck.Hand.Count} sectorWinInHand={winInHand} sectorWinInMasterUnique={winInMaster}");
            if (handGoals.Count > 0)
            {
                sb.AppendLine("  Hand win cards: " + string.Join(", ", handGoals));
            }
        }

        private static void AppendPadSummary(StringBuilder sb)
        {
            if (SectorManager.Instance?.Sectors == null)
            {
                sb.AppendLine("Pads: no sectors");
                return;
            }

            int solarOpen = 0;
            int pairedOpen = 0;
            int pairedPoweredOpen = 0;
            foreach (var sector in SectorManager.Instance.Sectors)
            {
                if (sector == null || sector.IsLocked || !sector.IsExplored) continue;
                if (sector.BuildingClusters == null) continue;
                foreach (var cluster in sector.BuildingClusters)
                {
                    if (cluster == null) continue;
                    if (cluster.CanPlaceSolar) solarOpen++;
                    if (cluster.BuildingSlot != null && !cluster.BuildingSlot.IsOccupied)
                    {
                        pairedOpen++;
                        if (cluster.SolarBuilding != null) pairedPoweredOpen++;
                    }
                }
            }

            sb.AppendLine($"Pads: openSolar={solarOpen} openPaired={pairedOpen} openPairedWithSolar={pairedPoweredOpen}");
        }

        private static void AppendUnmetGoals(StringBuilder sb)
        {
            string[] goals =
            {
                "TEMPERATURE", "ATMOSPHERE", "WATER",
                "BIOMASS", "OXYGEN", "POWER", "POPULATION", "COMMAND POST"
            };

            var unmet = new List<string>();
            foreach (var goal in goals)
            {
                if (GenerationManager.IsUnmetSectorGoal(goal)) unmet.Add(goal);
            }

            sb.AppendLine(unmet.Count == 0
                ? "Unmet sector goals: none"
                : "Unmet sector goals: " + string.Join(", ", unmet));
        }
    }
}
