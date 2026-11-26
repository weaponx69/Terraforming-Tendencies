using UnityEngine;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Prologue;
using GameDevTV.RTS.UI;

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

            [Tooltip("Prologue Survey Team: max tiles you can paint before ✓.")]
            public int surveyTileBudget = PrologueSurveyMission.DefaultTileBudget;
            [Tooltip("Prologue Survey Team: colony days the search takes after ✓.")]
            public int surveyDays = PrologueSurveyMission.DefaultDays;
    
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
                if (PrologueController.IsActive && scoutingType == ScoutingType.SurveyDrone)
                    return true;
                if (PrologueController.IsActive && scoutingType == ScoutingType.OrbitalScan)
                {
                    var colony = PrologueController.Instance?.Colony;
                    if (colony == null || !colony.HasUnrevealedTargets()) return false;
                    return colony.HasUnrevealedTargets()
                           && colony.Energy >= PrologueData.OrbitalScanEnergyCost;
                }

                if (!IsGateMet()) return false;
                if (!CanAffordMaterials()) return false;

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

            public override int GetMaterialsPlayCost()
            {
                if (PrologueController.IsActive && scoutingType == ScoutingType.SurveyDrone)
                    return 0;
                if (PrologueController.IsActive && scoutingType == ScoutingType.OrbitalScan)
                    return 0;

                if (MaterialsCost > 0) return MaterialsCost;
                return scoutingType switch
                {
                    ScoutingType.OrbitalScan => 50,
                    ScoutingType.SurveyDrone => 75,
                    ScoutingType.PipelineBoost => 50,
                    ScoutingType.EmergencyCaches => 0, // grants materials — keep free
                    _ => 50
                };
            }

            public override void Apply()
            {
                if (PrologueController.IsActive && scoutingType == ScoutingType.SurveyDrone)
                {
                    int tiles = surveyTileBudget > 0 ? surveyTileBudget : PrologueSurveyMission.DefaultTileBudget;
                    int days = surveyDays > 0 ? surveyDays : PrologueSurveyMission.DefaultDays;
                    var mission = PrologueSurveyMission.Instance
                                  ?? new GameObject("PrologueSurveyMission").AddComponent<PrologueSurveyMission>();
                    mission.BeginFromCard(tiles, days);
                    // Survey Team only: no camera fly — .cursor/rules/survey-team-no-camera-fly.mdc
                    PrologueIceMarkers.Instance?.EnsureMarkersForActiveObjectives();
                    Debug.Log($"[Prologue] Survey Team card → pick a ?, paint up to {tiles} tiles / {days} days.");
                    return;
                }
                if (PrologueController.IsActive && scoutingType == ScoutingType.OrbitalScan)
                {
                    var colony = PrologueController.Instance?.Colony;
                    if (colony == null || !colony.HasUnrevealedTargets()) return;
                    int cost = PrologueData.OrbitalScanEnergyCost;
                    if (!colony.TrySpendEnergy(cost)) return;
                    string ping = PrologueIceMarkers.Instance != null
                        ? PrologueIceMarkers.Instance.PingNearestTarget()
                        : "No survey target.";
                    PrologueIceMarkers.Instance?.FlyCameraToNearestSurveyTarget(pulseMarker: false);
                    PrologueHUD.Instance?.ShowNote(new System.Collections.Generic.List<string>
                    {
                        $"Orbital Scan (−{cost} energy).",
                        ping
                    });
                    PrologueController.Instance?.RefreshHandUnlocks();
                    return;
                }

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
                        // Emergency Caches excluded during Colony Acts — earn via placement + week mining.
                        if (materialsAmount > 0
                            && (ColonyActManager.Instance == null || !ColonyActManager.Instance.IsRunActive))
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

                // Prologue Survey Team / Orbital Scan do not use sector unlock gates.
                if (PrologueController.IsActive
                    && (scoutingType == ScoutingType.OrbitalScan || scoutingType == ScoutingType.SurveyDrone))
                    return true;

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
