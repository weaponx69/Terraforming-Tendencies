using System;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Prologue
{
    public static class PrologueCardMap
    {
        public static bool TryGetPrologueId(BlueprintCardSO card, out PrologueBuildingId id)
        {
            id = PrologueBuildingId.None;
            if (card == null) return false;

            if (card is UnlockBuildingCardSO unlock && unlock.buildingToUnlock != null)
                return TryGetPrologueId(unlock.buildingToUnlock, out id);

            string n = card.cardName ?? card.name ?? "";
            return MatchName(n, out id);
        }

        public static bool TryGetPrologueId(BuildingSO building, out PrologueBuildingId id)
        {
            id = PrologueBuildingId.None;
            if (building == null) return false;
            return MatchName(building.Name ?? building.name ?? "", out id);
        }

        /// <summary>Minerals this hand card spends when placed. Orbital uses energy (see EnergyCost), not minerals.</summary>
        public static int MineralCost(BlueprintCardSO card)
        {
            if (card is UnlockBuildingCardSO unlock)
                return MineralCost(unlock.buildingToUnlock);
            // Orbital Scan / Survey: not a mineral spend.
            return 0;
        }

        /// <summary>Energy this hand card spends when played (Orbital Scan pool spend).</summary>
        public static int EnergyCost(BlueprintCardSO card)
        {
            if (card is ScoutingCardSO scout
                && scout.scoutingType == ScoutingCardSO.ScoutingType.OrbitalScan)
                return PrologueData.OrbitalScanEnergyCost;
            return 0;
        }

        public static int MineralCost(BuildingSO building)
        {
            if (building == null) return 0;
            if (BuildingSiteRegistry.IsMineBuilding(building))
                return PrologueData.MineMineralCost;
            if (!TryGetPrologueId(building, out var id) || id == PrologueBuildingId.None || id == PrologueBuildingId.CommandPost)
                return 0;
            return PrologueData.MineralCost(id);
        }

        public static bool IsPrologueScanCard(BlueprintCardSO card)
        {
            if (card == null) return false;
            if (card is ScoutingCardSO scout)
            {
                return scout.scoutingType == ScoutingCardSO.ScoutingType.SurveyDrone
                    || scout.scoutingType == ScoutingCardSO.ScoutingType.OrbitalScan;
            }
            string n = card.cardName ?? card.name ?? "";
            return n.IndexOf("Survey", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Orbital", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Scan", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool MatchName(string n, out PrologueBuildingId id)
        {
            id = PrologueBuildingId.None;
            if (string.IsNullOrEmpty(n)) return false;

            if (n.IndexOf("Aquifer", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Water Ice", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                id = PrologueBuildingId.Aquifer;
                return true;
            }
            // Life-support plant — not Atmospheric Condenser.
            if (n.IndexOf("Atmosphere Processor", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Oxygen Processor", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                id = PrologueBuildingId.Oxygen;
                return true;
            }
            if (n.IndexOf("Oxygen", StringComparison.OrdinalIgnoreCase) >= 0
                && n.IndexOf("Atmos", StringComparison.OrdinalIgnoreCase) < 0)
            {
                id = PrologueBuildingId.Oxygen;
                return true;
            }
            // Solar Panel / Solar Array only — not Solar Greenhouse.
            if ((n.IndexOf("Solar Panel", StringComparison.OrdinalIgnoreCase) >= 0
                 || n.IndexOf("Solar Array", StringComparison.OrdinalIgnoreCase) >= 0)
                && n.IndexOf("Greenhouse", StringComparison.OrdinalIgnoreCase) < 0)
            {
                id = PrologueBuildingId.Solar;
                return true;
            }
            if (n.IndexOf("Habitat", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("BioDome", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Bio Dome", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                id = PrologueBuildingId.Habitat;
                return true;
            }
            // Prologue storm power — hill windmill (not volcano Geothermal).
            if (n.IndexOf("Wind", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Windmill", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                id = PrologueBuildingId.WindTurbine;
                return true;
            }
            if (n.IndexOf("Command Post", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                id = PrologueBuildingId.CommandPost;
                return true;
            }
            return false;
        }
    }
}
