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
                return ReservedSiteBuildUtility.CanBuildAtReservedSite(
                    buildingToUnlock, Owner.Player1, out _, requireUnlocked: false);
            }

            public override bool IsGateMet()
            {
                if (buildingToUnlock == null) return false;
                // Same rules as playability so gated cards never linger as "almost" drawable.
                return ReservedSiteBuildUtility.CanBuildAtReservedSite(
                    buildingToUnlock, Owner.Player1, out _, requireUnlocked: false);
            }
    
            public override string GetCardGoal()
            {
                if (buildingToUnlock != null)
                {
                    string name = buildingToUnlock.Name.ToLower();
                    if (name.Contains("algae") || name.Contains("oxygen") || name.Contains("atmosphere processor"))
                        return "OXYGEN";
                    if (name.Contains("solar") || name.Contains("geothermal") || name.Contains("magnetic shield") || name.Contains("power"))
                        return "POWER";
                    if (name.Contains("habitat") || name.Contains("bio-dome") || name.Contains("commons") || name.Contains("apartment") || name.Contains("housing") || name.Contains("command center"))
                        return "POPULATION";
                    if (name.Contains("aquifer") || name.Contains("greenhouse") || name.Contains("extractor") || name.Contains("nursery") || name.Contains("greenery") || name.Contains("biosphere") || name.Contains("biomass"))
                        return "BIOMASS";
                    if (name.Contains("mine") || name.Contains("laser"))
                        return "MATERIALS";
                    if (name.Contains("GHG") || name.Contains("microbe"))
                        return "TEMPERATURE";
                    if (name.Contains("condenser") || name.Contains("import"))
                        return "ATMOSPHERE";
                    if (name.Contains("command post"))
                        return "COMMAND POST";
    
                    // Fallback check on config stats
                    var config = buildingToUnlock.BuildingConfig;
                    if (config != null)
                    {
                        if (config.PowerGeneration > 0) return "POWER";
                        if (config.BiomassGeneration > 0) return "BIOMASS";
                        if (config.HousingCapacity > 0) return "POPULATION";
                    }
                }
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
