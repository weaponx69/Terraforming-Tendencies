using UnityEngine;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Player
{
    public abstract class BlueprintCardSO : ScriptableObject
    {
        [Header("Card Metadata")]
        public string cardName;
        [TextArea(3, 5)]
        public string cardDescription;
        public Sprite icon;

        public abstract void Apply();
        public virtual bool IsGateMet() => true;
        public virtual string GetCardGoal() => "BLUEPRINT";
    }

    [CreateAssetMenu(fileName = "Unlock Building Card", menuName = "Blueprints/Unlock Building Card")]
    public class UnlockBuildingCardSO : BlueprintCardSO
    {
        public BuildingSO buildingToUnlock;

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

    [CreateAssetMenu(fileName = "Resource Shipment Card", menuName = "Blueprints/Resource Shipment Card")]
    public class ResourceShipmentCardSO : BlueprintCardSO
    {
        public int materialsAmount = 0;
        public int biomassAmount = 0;
        public int oxygenAmount = 0;

        public override string GetCardGoal()
        {
            if (biomassAmount > 0) return "BIOMASS";
            if (oxygenAmount > 0) return "OXYGEN";
            if (materialsAmount > 0) return "MATERIALS";
            return "RESOURCES";
        }

        public override void Apply()
        {
            if (materialsAmount > 0)
            {
                int cur = Supplies.Materials.TryGetValue(Owner.Player1, out int m) ? m : 0;
                Supplies.Materials[Owner.Player1] = cur + materialsAmount;
                Supplies.RaiseMaterialsChanged(Owner.Player1, cur + materialsAmount);
                Debug.Log($"[Blueprint] Materials shipment delivered: +{materialsAmount}");
            }
            if (biomassAmount > 0)
            {
                float cur = Supplies.Biomass.TryGetValue(Owner.Player1, out float b) ? b : 0f;
                Supplies.UpdateBiomass(Owner.Player1, cur + biomassAmount);
                Debug.Log($"[Blueprint] Biomass shipment delivered: +{biomassAmount}");
            }
            if (oxygenAmount > 0)
            {
                float cur = Supplies.Oxygen.TryGetValue(Owner.Player1, out float o) ? o : 0f;
                Supplies.UpdateOxygen(Owner.Player1, cur + oxygenAmount);
                Debug.Log($"[Blueprint] Oxygen shipment delivered: +{oxygenAmount}");
            }
        }
    }

    [CreateAssetMenu(fileName = "Spawn Unit Card", menuName = "Blueprints/Spawn Unit Card")]
    public class SpawnUnitCardSO : BlueprintCardSO
    {
        public GameObject unitPrefab;

        public override string GetCardGoal()
        {
            if (unitPrefab != null)
            {
                string name = unitPrefab.name.ToLower();
                if (name.Contains("repair")) return "MAINTENANCE";
                if (name.Contains("mining")) return "MINING";
            }
            return "UNIT SUPPORT";
        }

        public override void Apply()
        {
            if (unitPrefab == null) return;

            // Find command post to spawn at
            var bldgs = UnityEngine.Object.FindObjectsByType<BaseBuilding>(FindObjectsInactive.Exclude);
            BaseBuilding spawnBase = null;
            foreach (var b in bldgs)
            {
                if (b != null && b.Owner == Owner.Player1 && b.BuildingSO != null && b.BuildingSO.Name.Contains("Command"))
                {
                    spawnBase = b;
                    break;
                }
            }

            Vector3 spawnPos = Vector3.zero;
            if (spawnBase != null)
            {
                spawnPos = spawnBase.transform.position + Vector3.forward * 4f;
            }
            else
            {
                // Fallback to active camera projection center or origin
                var mainCam = Camera.main;
                if (mainCam != null)
                {
                    Ray ray = mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                    if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                    {
                        spawnPos = hit.point;
                    }
                }
            }

            GameObject spawnedUnit = UnityEngine.Object.Instantiate(unitPrefab, spawnPos, Quaternion.identity);
            
            // Set Owner to Player1
            var abstractUnit = spawnedUnit.GetComponent<AbstractUnit>();
            if (abstractUnit != null)
            {
                abstractUnit.Owner = Owner.Player1;
            }
            
            Debug.Log($"[Blueprint] Spawned free unit: {unitPrefab.name} at {spawnPos}");
        }
    }

    [CreateAssetMenu(fileName = "Passive Buff Card", menuName = "Blueprints/Passive Buff Card")]
    public class PassiveBuffCardSO : BlueprintCardSO
    {
        public enum BuffType { GatherSpeed, PowerGeneration }
        public BuffType buffType;
        public float multiplier = 1.2f;

        public override string GetCardGoal()
        {
            if (buffType == BuffType.GatherSpeed) return "MINING";
            if (buffType == BuffType.PowerGeneration) return "POWER";
            return "PASSIVE BUFF";
        }

        public override void Apply()
        {
            if (buffType == BuffType.GatherSpeed)
            {
                BlueprintDraftManager.GatherSpeedMultiplier *= multiplier;
                Debug.Log($"[Blueprint] Active gather speed multiplier is now: {BlueprintDraftManager.GatherSpeedMultiplier}");
            }
            else if (buffType == BuffType.PowerGeneration)
            {
                BlueprintDraftManager.PowerGenMultiplier *= multiplier;
                Debug.Log($"[Blueprint] Active power generation multiplier is now: {BlueprintDraftManager.PowerGenMultiplier}");
            }
        }
    }

    [CreateAssetMenu(fileName = "Terraforming Card", menuName = "Blueprints/Terraforming Card")]
    public class TerraformingCardSO : UnlockBuildingCardSO
    {
        [Header("Climate Gates")]
        public float minTemperature = float.MinValue;
        public float maxTemperature = float.MaxValue;
        public float minOxygen = float.MinValue;
        public float maxOxygen = float.MaxValue;
        public float minAtmosphere = float.MinValue;
        public float maxAtmosphere = float.MaxValue;
        public float minWater = float.MinValue;
        public float maxWater = float.MaxValue;
        public GameDevTV.RTS.Environment.SectorManager.SectorFeature requiredSectorFeature = GameDevTV.RTS.Environment.SectorManager.SectorFeature.None;

        public override bool IsGateMet()
        {
            float currentTemp = Supplies.Temperature.TryGetValue(Owner.Player1, out float t) ? t : -60f;
            if (currentTemp < minTemperature || currentTemp > maxTemperature) return false;

            float currentOxygen = Supplies.Oxygen.TryGetValue(Owner.Player1, out float o) ? o : 0f;
            if (currentOxygen < minOxygen || currentOxygen > maxOxygen) return false;

            float currentAtmosphere = Supplies.Atmosphere.TryGetValue(Owner.Player1, out float a) ? a : 0.01f;
            if (currentAtmosphere < minAtmosphere || currentAtmosphere > maxAtmosphere) return false;

            float currentWater = Supplies.Water.TryGetValue(Owner.Player1, out float w) ? w : 0f;
            if (currentWater < minWater || currentWater > maxWater) return false;

            if (requiredSectorFeature != GameDevTV.RTS.Environment.SectorManager.SectorFeature.None)
            {
                if (GameDevTV.RTS.Environment.SectorManager.Instance == null) return false;
                bool hasFeature = false;
                foreach (var sector in GameDevTV.RTS.Environment.SectorManager.Instance.Sectors)
                {
                    if (!sector.IsLocked && sector.Feature == requiredSectorFeature)
                    {
                        hasFeature = true;
                        break;
                    }
                }
                if (!hasFeature) return false;
            }

            return true;
        }

        public override void Apply()
        {
            base.Apply();
            if (buildingToUnlock != null)
            {
                BlueprintDraftManager.RegisterBuildingSO(buildingToUnlock);
            }
        }
    }

    [CreateAssetMenu(fileName = "Discovery Card", menuName = "Blueprints/Discovery Card")]
    public class DiscoveryCardSO : BlueprintCardSO
    {
        public enum DiscoveryType
        {
            IronVein,        // Reveals Iron deposits in explored sectors
            GasPocket,       // Reveals Gas deposits
            RegolithField,   // Reveals Regolith deposits
            MineralSurvey,   // Reveals Minerals deposits
            DeepCoreScan,    // Reveals hidden high-value deposits + bonus
            DebrisField      // Drones can salvage debris
        }

        public DiscoveryType discoveryType;
        public int bonusMaterials = 0;

        public override string GetCardGoal()
        {
            return discoveryType switch
            {
                DiscoveryType.IronVein => "MATERIALS",
                DiscoveryType.GasPocket => "GAS",
                DiscoveryType.RegolithField => "MATERIALS",
                DiscoveryType.MineralSurvey => "MATERIALS",
                DiscoveryType.DeepCoreScan => "MATERIALS",
                DiscoveryType.DebrisField => "SALVAGE",
                _ => "DISCOVERY"
            };
        }

        public override void Apply()
        {
            string typeName = discoveryType switch
            {
                DiscoveryType.IronVein => "Iron",
                DiscoveryType.GasPocket => "Gas",
                DiscoveryType.RegolithField => "Regolith",
                DiscoveryType.MineralSurvey => "Minerals",
                DiscoveryType.DeepCoreScan => null, // Handled specially
                DiscoveryType.DebrisField => null,   // Handled specially
                _ => null
            };

            if (discoveryType == DiscoveryType.DeepCoreScan)
            {
                // Reveal ALL resource types in explored sectors
                var allTypes = Environment.DiscoverySystem.GetResourceTypesInExploredSectors();
                foreach (var t in allTypes)
                {
                    Environment.DiscoverySystem.RevealResourceType(t);
                }
                Debug.Log("[Blueprint] Deep Core Scan: All resource types in explored sectors revealed!");
            }
            else if (discoveryType == DiscoveryType.DebrisField)
            {
                // Enable salvage mechanic — flag set for WorkerBrainController to check
                BlueprintDraftManager.SalvageEnabled = true;
                Debug.Log("[Blueprint] Debris Field Spotted: Salvage enabled for destroyed buildings.");
            }
            else if (!string.IsNullOrEmpty(typeName))
            {
                Environment.DiscoverySystem.RevealResourceType(typeName);
                Debug.Log($"[Blueprint] Discovery: {typeName} deposits now visible in explored sectors!");
            }

            // Grant any bonus materials
            if (bonusMaterials > 0)
            {
                int cur = Supplies.Materials.TryGetValue(Owner.Player1, out int m) ? m : 0;
                Supplies.Materials[Owner.Player1] = cur + bonusMaterials;
                Supplies.RaiseMaterialsChanged(Owner.Player1, cur + bonusMaterials);
                Debug.Log($"[Blueprint] Bonus salvage: +{bonusMaterials} Materials");
            }
        }

        public override bool IsGateMet()
        {
            // Discovery cards only appear for types that exist in explored sectors
            if (discoveryType == DiscoveryType.DeepCoreScan || discoveryType == DiscoveryType.DebrisField)
                return true;

            string typeName = discoveryType switch
            {
                DiscoveryType.IronVein => "Iron",
                DiscoveryType.GasPocket => "Gas",
                DiscoveryType.RegolithField => "Regolith",
                DiscoveryType.MineralSurvey => "Minerals",
                _ => null
            };

            if (string.IsNullOrEmpty(typeName)) return true;

            // Only show this card if the resource type exists in explored sectors
            var existingTypes = Environment.DiscoverySystem.GetResourceTypesInExploredSectors();
            return existingTypes.Contains(typeName);
        }
    }

    [CreateAssetMenu(fileName = "Scouting Card", menuName = "Blueprints/Scouting Card")]
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

        public override void Apply()
        {
            var explorationMgr = Environment.ExplorationManager.Instance;

            switch (scoutingType)
            {
                case ScoutingType.OrbitalScan:
                    if (explorationMgr != null)
                    {
                        explorationMgr.InstantExplore();
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
                    Debug.Log("[Blueprint] Survey Drone deployed to scout ahead!");
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
            // Scouting cards are always valid as long as locked sectors exist
            if (scoutingType == ScoutingType.EmergencyCaches) return true;

            var sectorMgr = Environment.SectorManager.Instance;
            if (sectorMgr == null) return false;
            return sectorMgr.GetNextLockedSectorIndex() >= 0;
        }
    }

    [CreateAssetMenu(fileName = "Drill Breakthrough Card", menuName = "Blueprints/Drill Breakthrough Card")]
    public class DrillBreakthroughCardSO : BlueprintCardSO
    {
        public float gatherSpeedMultiplier = 1.5f;

        public override string GetCardGoal() => "MINING";

        public override void Apply()
        {
            BlueprintDraftManager.GatherSpeedMultiplier *= gatherSpeedMultiplier;
            Debug.Log($"[Blueprint] Drill Breakthrough: Gather speed multiplier now {BlueprintDraftManager.GatherSpeedMultiplier:F1}x");
        }
    }
}