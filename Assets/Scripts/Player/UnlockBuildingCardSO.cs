using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Utilities;
using UnityEngine;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Player
{
    public class UnlockBuildingCardSO : BlueprintCardSO
        {
            public BuildingSO buildingToUnlock;
    
            public override bool CanApply()
            {
                if (buildingToUnlock == null) return false;
                if (!CanAffordMaterials()) return false;
                return ReservedSiteBuildUtility.CanBuildAtReservedSite(
                    buildingToUnlock, Owner.Player1, out _, requireUnlocked: false);
            }

            public override int GetMaterialsPlayCost()
            {
                int fromBuilding = ReservedSiteBuildUtility.GetMaterialsCost(buildingToUnlock);
                int fromCard = MaterialsCost;
                int priced = Mathf.Max(fromBuilding, fromCard);
                if (priced > 0) return priced;
                return DefaultCostForGoal(GetCardGoal());
            }

            private static int DefaultCostForGoal(string goal)
            {
                return goal switch
                {
                    "COMMAND POST" => 400,
                    "POWER" => 100,
                    "ATMOSPHERE" or "TEMPERATURE" or "WATER" or "OXYGEN" => 150,
                    "POPULATION" => 150,
                    "MATERIALS" => 200,
                    _ => 150
                };
            }

            public override bool IsGateMet()
            {
                // Materials only — site availability is CanApply. Keeps bootstrap cards
                // (Command Post / Solar) drawable before PlanetGenerator places pads.
                if (buildingToUnlock == null || buildingToUnlock.Prefab == null) return false;
                return ReservedSiteBuildUtility.CanAffordBuilding(buildingToUnlock, Owner.Player1);
            }
    
            public override string GetCardGoal()
            {
                return ClassifyBuildingGoal(buildingToUnlock);
            }

            public static string ClassifyBuildingGoal(BuildingSO building)
            {
                if (building == null) return "CONSTRUCTION";

                string name = building.Name != null
                    ? building.Name.ToLowerInvariant()
                    : string.Empty;

                if (name.Contains("command post")) return "COMMAND POST";
                if (name.Contains("algae") || name.Contains("oxygen processor") || name.Contains("atmosphere processor"))
                    return "OXYGEN";
                if (name.Contains("ghg") || name.Contains("microbe") || name.Contains("geothermal"))
                    return "TEMPERATURE";
                if (name.Contains("condenser") || name.Contains("import"))
                    return "ATMOSPHERE";
                if (name.Contains("aquifer") || name.Contains("water"))
                    return "WATER";
                if (name.Contains("greenhouse") || name.Contains("nursery") || name.Contains("greenery")
                    || name.Contains("biosphere") || name.Contains("biomass"))
                    return "CONSTRUCTION"; // Biomass terraforming deprecated
                if (name.Contains("habitat") || name.Contains("bio-dome") || name.Contains("biodome")
                    || name.Contains("commons") || name.Contains("apartment") || name.Contains("housing"))
                    return "POPULATION";
                if (name.Contains("solar") || name.Contains("magnetic shield") || name.Contains("power"))
                    return "POWER";
                if (name.Contains("mine") || name.Contains("strip") || name.Contains("mining laser"))
                    return "MATERIALS";

                var config = building.BuildingConfig;
                if (config != null)
                {
                    if (config.TemperatureGeneration > 0f) return "TEMPERATURE";
                    if (config.AtmosphereGeneration > 0f) return "ATMOSPHERE";
                    if (config.WaterGeneration > 0f) return "WATER";
                    if (config.PowerGeneration > 0f) return "POWER";
                    if (config.HousingCapacity > 0) return "POPULATION";
                    // BiomassGeneration no longer maps to a sector terraforming goal.
                }

                if (name.Contains("command")) return "COMMAND POST";
                return "CONSTRUCTION";
            }
    
            public override void Apply()
            {
                if (buildingToUnlock != null)
                {
                    BlueprintDraftManager.UnlockBuilding(buildingToUnlock.Name);
                    Debug.Log($"[Blueprint] Unlocked building: {buildingToUnlock.Name}");
                }
            }
        }
}
