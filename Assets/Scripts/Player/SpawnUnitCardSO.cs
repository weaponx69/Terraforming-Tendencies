using UnityEngine.AI;
using UnityEngine;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;

namespace GameDevTV.RTS.Player
{
    public class SpawnUnitCardSO : BlueprintCardSO
        {
            public GameObject unitPrefab;

            /// <summary>When true, play cost is 0 (one free Mining Drone per sector Act).</summary>
            [System.NonSerialized] public bool waiveMaterialsCost;

            public override bool IsGateMet()
            {
                return SectorColonization.FindFocusedPlayerCommandPost() != null;
            }

            public override bool CanApply()
            {
                if (!IsGateMet()) return false;
                return CanAffordMaterials();
            }

            public override int GetMaterialsPlayCost()
            {
                if (ColonyActManager.Instance != null)
                    return 0;
                if (waiveMaterialsCost) return 0;
                if (MaterialsCost > 0) return MaterialsCost;
                var unit = unitPrefab != null ? unitPrefab.GetComponent<AbstractUnit>() : null;
                if (unit?.UnitSO?.Cost != null)
                {
                    int fromUnit = Mathf.FloorToInt(
                        unit.UnitSO.Cost.Minerals * Supplies.MineralsToMaterialsRateStatic
                        + unit.UnitSO.Cost.Gas * Supplies.GasToMaterialsRateStatic);
                    if (fromUnit > 0) return fromUnit;
                }
                return 25;
            }

            public override void Apply()
            {
                if (unitPrefab == null) return;
    
                BaseBuilding spawnBase = SectorColonization.FindFocusedPlayerCommandPost();
                if (spawnBase == null)
                {
                    Debug.LogWarning($"[Blueprint] Cannot spawn '{cardName}' without a player Command Post.");
                    GameDevTV.RTS.Environment.ExplorationManager.NotifyPlacementFailed(
                        "No Command Post in this sector. Q/E to a claimed sector, then spawn the drone.",
                        Camera.main != null ? Camera.main.transform.position : Vector3.zero);
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

                Debug.Log($"[Blueprint] Spawned unit: {unitPrefab.name} at {spawnPos} (focused sector CP)");
            }
        }
}
