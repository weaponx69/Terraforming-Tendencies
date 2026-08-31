using UnityEngine;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Player
{
    public class ScoutingCardSO : BlueprintCardSO
        {
            public enum ScoutingType
            {
                OrbitalScan,      // Instantly reveal next sector
                PipelineBoost,    // Exploration 2x faster this round
                SurveyDrone,      // Deploy probe to scout ahead
                EmergencyCaches   // Flat Materials (always available safety net)
            }
    
            public ScoutingType scoutingType;
            public int materialsAmount = 0;
    
            public override string GetCardGoal()
            {
                return scoutingType switch
                {
                    ScoutingType.OrbitalScan => "EXPLORATION",
                    ScoutingType.PipelineBoost => "EXPLORATION",
                    ScoutingType.SurveyDrone => "EXPLORATION",
                    ScoutingType.EmergencyCaches => "MATERIALS",
                    _ => "SCOUTING"
                };
            }
    
            public override bool CanApply()
            {
                if (!IsGateMet()) return false;

                if (scoutingType == ScoutingType.EmergencyCaches || scoutingType == ScoutingType.PipelineBoost)
                {
                    return true;
                }

                var explorationMgr = Environment.ExplorationManager.Instance;
                if (explorationMgr == null) return false;

                // Orbital Scan opens the next locked sector even when no frontier "?" exists yet.
                if (scoutingType == ScoutingType.OrbitalScan)
                {
                    return explorationMgr.CanOrbitalScan();
                }

                if (!explorationMgr.HasFrontierNode(out _, out _)) return false;
                return explorationMgr.CanAffordExploration();
            }

            public override void Apply()
            {
                var explorationMgr = Environment.ExplorationManager.Instance;
    
                switch (scoutingType)
                {
                    case ScoutingType.OrbitalScan:
                        if (explorationMgr != null)
                        {
                            explorationMgr.TryOrbitalScan();
                        }
                        else
                        {
                            Debug.LogWarning("[Blueprint] Orbital Scan: No ExplorationManager found in scene!");
                        }
                        break;
    
                    case ScoutingType.PipelineBoost:
                        if (explorationMgr != null)
                        {
                            explorationMgr.BoostExplorationSpeed(2f, 60f);
                        }
                        Debug.Log("[Blueprint] Pipeline Boost: Exploration speed doubled for 60 seconds!");
                        break;
    
                    case ScoutingType.SurveyDrone:
                        if (explorationMgr != null)
                        {
                            explorationMgr.DeploySurveyDrone();
                        }
                        else
                        {
                            Debug.LogWarning("[Blueprint] Survey Drone: No ExplorationManager found in scene!");
                        }
                        break;
    
                    case ScoutingType.EmergencyCaches:
                        if (materialsAmount > 0)
                        {
                            int cur = Supplies.Materials.TryGetValue(Owner.Player1, out int m) ? m : 0;
                            Supplies.Materials[Owner.Player1] = cur + materialsAmount;
                            Supplies.RaiseMaterialsChanged(Owner.Player1, cur + materialsAmount);
                            Debug.Log($"[Blueprint] Emergency Caches: +{materialsAmount} Materials from salvage.");
                        }
                        break;
                }
            }
    
            public override bool IsGateMet()
            {
                if (scoutingType == ScoutingType.EmergencyCaches) return true;
                if (scoutingType == ScoutingType.PipelineBoost) return true;

                var sectorMgr = Environment.SectorManager.Instance;
                if (sectorMgr == null) return false;
                if (sectorMgr.GetNextLockedSectorIndex() < 0) return false;

                if (scoutingType == ScoutingType.OrbitalScan)
                {
                    return GenerationManager.CanUnlockNextMapSector();
                }

                return true;
            }
        }
}
