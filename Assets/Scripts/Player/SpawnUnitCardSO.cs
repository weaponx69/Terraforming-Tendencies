using UnityEngine.AI;
using UnityEngine;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Player
{
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
    
                // Validate spawn position is on NavMesh to prevent agent creation failures
                NavMeshQueryFilter navFilter = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
                if (NavMesh.SamplePosition(spawnPos, out NavMeshHit navHit, 10f, navFilter))
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
                
                Debug.Log($"[Blueprint] Spawned free unit: {unitPrefab.name} at {spawnPos}");
            }
        }
}
