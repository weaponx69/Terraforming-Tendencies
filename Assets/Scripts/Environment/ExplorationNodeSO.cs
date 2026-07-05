using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Defines a type of exploration bonus/reward that can be found when exploring a sector.
    /// These replace the old direct-play climate boost cards (Thermal Surge, Atmospheric Compression, etc.)
    /// </summary>
    [CreateAssetMenu(fileName = "Exploration Node", menuName = "Environment/Exploration Node")]
    public class ExplorationNodeSO : ScriptableObject
    {
        public string nodeName;
        [TextArea(2, 4)] public string description;
        [TextArea(1, 3)] public string flavorText;  // "A geothermal vent spews superheated gases..."
        public Sprite icon;

        public NodeRewardType rewardType;
        public float rewardAmount;
        public int rewardCount = 1;
        public float weight = 1f;  // Higher = more likely to appear

        public enum NodeRewardType
        {
            Materials,        // +rewardAmount Materials
            Biomass,          // +rewardAmount Biomass
            Temperature,      // +rewardAmount °C temperature
            Atmosphere,       // +rewardAmount atm atmosphere
            Water,            // +rewardAmount % water
            SpawnMiningDrone  // Spawn rewardCount Mining Drones at command post
        }

        /// <summary>
        /// Apply this node's reward to the player.
        /// </summary>
        public void ApplyReward()
        {
            switch (rewardType)
            {
                case NodeRewardType.Materials:
                    if (Player.Supplies.Materials != null && Player.Supplies.Materials.ContainsKey(Units.Owner.Player1))
                    {
                        Player.Supplies.Materials[Units.Owner.Player1] += (int)rewardAmount;
                        Player.Supplies.RaiseMaterialsChanged(Units.Owner.Player1, Player.Supplies.Materials[Units.Owner.Player1]);
                    }
                    Debug.Log($"[ExplorationNode] +{rewardAmount} Materials from '{nodeName}'");
                    break;

                case NodeRewardType.Biomass:
                    float curBio = Player.Supplies.Biomass != null && Player.Supplies.Biomass.TryGetValue(Units.Owner.Player1, out float b) ? b : 0f;
                    Player.Supplies.UpdateBiomass(Units.Owner.Player1, curBio + rewardAmount);
                    Debug.Log($"[ExplorationNode] +{rewardAmount} Biomass from '{nodeName}'");
                    break;

                case NodeRewardType.Temperature:
                    float curTemp = Player.Supplies.Temperature != null && Player.Supplies.Temperature.TryGetValue(Units.Owner.Player1, out float t) ? t : -60f;
                    float target = curTemp + rewardAmount;
                    Player.Supplies.UpdateTemperature(Units.Owner.Player1, target);
                    if (Player.ClimateManager.Instance != null)
                        Player.ClimateManager.Instance.SetTemperatureTarget(target);
                    Debug.Log($"[ExplorationNode] +{rewardAmount}°C Temperature from '{nodeName}'");
                    break;

                case NodeRewardType.Atmosphere:
                    float curAtmos = Player.Supplies.Atmosphere != null && Player.Supplies.Atmosphere.TryGetValue(Units.Owner.Player1, out float a) ? a : 0.01f;
                    float atmosTarget = curAtmos + rewardAmount;
                    Player.Supplies.UpdateAtmosphere(Units.Owner.Player1, atmosTarget);
                    if (Player.ClimateManager.Instance != null)
                        Player.ClimateManager.Instance.SetAtmosphereTarget(atmosTarget);
                    Debug.Log($"[ExplorationNode] +{rewardAmount} atm Atmosphere from '{nodeName}'");
                    break;

                case NodeRewardType.Water:
                    float curWater = Player.Supplies.Water != null && Player.Supplies.Water.TryGetValue(Units.Owner.Player1, out float w) ? w : 0f;
                    float waterTarget = curWater + rewardAmount;
                    Player.Supplies.UpdateWater(Units.Owner.Player1, waterTarget);
                    if (Player.ClimateManager.Instance != null)
                        Player.ClimateManager.Instance.SetWaterTarget(waterTarget);
                    Debug.Log($"[ExplorationNode] +{rewardAmount}% Water from '{nodeName}'");
                    break;

                case NodeRewardType.SpawnMiningDrone:
                    var droneSO = Resources.Load<Units.AbstractUnitSO>("Units/MiningDrone");
                    if (droneSO != null && droneSO.Prefab != null)
                    {
                        // Spawn at the player's command post
                        foreach (var building in Units.BaseBuilding.ActiveBuildings)
                        {
                            if (building != null && building.Owner == Units.Owner.Player1 &&
                                building.BuildingSO != null && building.BuildingSO.Name.Contains("Command"))
                            {
                                for (int i = 0; i < rewardCount; i++)
                                {
                                    Vector3 spawnPos = building.transform.position + new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 3f));
                                    GameObject.Instantiate(droneSO.Prefab, spawnPos, Quaternion.identity);
                                }
                                break;
                            }
                        }
                    }
                    Debug.Log($"[ExplorationNode] Spawned {rewardCount} Mining Drone(s) from '{nodeName}'");
                    break;
            }
        }
    }
}