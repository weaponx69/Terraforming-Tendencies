using UnityEngine.AI;
using UnityEngine;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;

namespace GameDevTV.RTS.Player
{
    public class SpawnUnitCardSO : BlueprintCardSO
        {
            public GameObject unitPrefab;

            public override bool IsGateMet()
            {
                return FindPlayerCommandPost() != null;
            }

            public override bool CanApply()
            {
                if (!IsGateMet()) return false;
                return CanAffordMaterials();
            }

            public override int GetMaterialsPlayCost()
            {
                if (MaterialsCost > 0) return MaterialsCost;
                var unit = unitPrefab != null ? unitPrefab.GetComponent<AbstractUnit>() : null;
                if (unit?.UnitSO?.Cost != null)
                {
                    int fromUnit = Mathf.FloorToInt(
                        unit.UnitSO.Cost.Minerals * Supplies.MineralsToMaterialsRateStatic
                        + unit.UnitSO.Cost.Gas * Supplies.GasToMaterialsRateStatic);
                    if (fromUnit > 0) return fromUnit;
                }
                return 100;
            }

            public override void Apply()
            {
                if (unitPrefab == null) return;
    
                BaseBuilding spawnBase = FindPlayerCommandPost();
                if (spawnBase == null)
                {
                    Debug.LogWarning($"[Blueprint] Cannot spawn '{cardName}' without a player Command Post.");
                    return;
                }

                Vector3 spawnPos = spawnBase.transform.position + Vector3.forward * 4f;

                int agentType = 0;
                var prefabAgent = unitPrefab.GetComponent<NavMeshAgent>();
                if (prefabAgent != null) agentType = prefabAgent.agentTypeID;

                if (!NavMeshSpawnUtility.TryGetSpawnPosition(spawnPos, agentType, out spawnPos))
                {
                    Debug.LogWarning($"[Blueprint] Could not find NavMesh for '{cardName}' near {spawnBase.name}. Spawn may fail to move.");
                }

                GameObject spawnedUnit = UnityEngine.Object.Instantiate(unitPrefab, spawnPos, Quaternion.identity);

                var abstractUnit = spawnedUnit.GetComponent<AbstractUnit>();
                if (abstractUnit != null)
                {
                    abstractUnit.Owner = Owner.Player1;
                }

                if (spawnedUnit.TryGetComponent(out NavMeshAgent agent))
                {
                    NavMeshSpawnUtility.EnsureAgentOnNavMesh(agent);
                }

                if (spawnedUnit.TryGetComponent(out Worker worker))
                {
                    worker.BeginAutoGather(spawnBase);
                }

                Debug.Log($"[Blueprint] Spawned unit: {unitPrefab.name} at {spawnPos}");
            }

            private static BaseBuilding FindPlayerCommandPost()
            {
                BaseBuilding[] buildings = UnityEngine.Object.FindObjectsByType<BaseBuilding>(FindObjectsInactive.Exclude);
                foreach (BaseBuilding building in buildings)
                {
                    if (building != null && building.Owner == Owner.Player1 && building.BuildingSO != null &&
                        building.BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase))
                    {
                        return building;
                    }
                }

                return null;
            }
        }
}
