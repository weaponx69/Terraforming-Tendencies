using UnityEngine.AI;
using UnityEngine;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Player
{
    public class SpawnUnitCardSO : BlueprintCardSO
        {
            public GameObject unitPrefab;

            public override bool IsGateMet()
            {
                return FindPlayerCommandPost() != null;
            }
    
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
    
                BaseBuilding spawnBase = FindPlayerCommandPost();
                if (spawnBase == null)
                {
                    Debug.LogWarning($"[Blueprint] Cannot spawn '{cardName}' without a player Command Post.");
                    return;
                }

                Vector3 spawnPos = spawnBase.transform.position + Vector3.forward * 4f;
    
                // Validate spawn position is on NavMesh to prevent agent creation failures
                int agentType = 0;
                var prefabAgent = unitPrefab.GetComponent<NavMeshAgent>();
                if (prefabAgent != null) agentType = prefabAgent.agentTypeID;
                
                NavMeshQueryFilter navFilter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
                if (NavMesh.SamplePosition(spawnPos, out NavMeshHit navHit, 20f, navFilter))
                {
                    spawnPos = navHit.position;
                }
                
                GameObject spawnedUnit = UnityEngine.Object.Instantiate(unitPrefab, spawnPos, Quaternion.identity);
                
                // Set Owner to Player1
                var abstractUnit = spawnedUnit.GetComponent<AbstractUnit>();
                if (abstractUnit != null)
                {
                    abstractUnit.Owner = Owner.Player1;
                }

                if (spawnedUnit.TryGetComponent(out Worker worker))
                {
                    worker.BeginAutoGather(spawnBase);
                }
                
                Debug.Log($"[Blueprint] Spawned free unit: {unitPrefab.name} at {spawnPos}");
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
