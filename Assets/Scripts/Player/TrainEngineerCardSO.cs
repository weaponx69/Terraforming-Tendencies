using UnityEngine;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// A draftable action card that deploys a skilled Colony Engineer 
    /// at the Command Post to automate building/tube maintenance.
    /// </summary>
    [CreateAssetMenu(fileName = "Train Engineer Card", menuName = "RTS/Cards/Train Engineer")]
    public class TrainEngineerCardSO : BlueprintCardSO
    {
        public override string GetCardGoal()
        {
            return "MAINTENANCE";
        }

        public override void Apply()
        {
            AbstractUnitSO unitSO = null;
#if UNITY_EDITOR
            unitSO = UnityEditor.AssetDatabase.LoadAssetAtPath<AbstractUnitSO>("Assets/Units/Rifleman/Rifleman.asset");
#endif
            if (unitSO == null)
            {
                unitSO = Resources.Load<AbstractUnitSO>("Units/Rifleman");
            }

            if (unitSO == null || unitSO.Prefab == null)
            {
                Debug.LogWarning("[TrainEngineerCard] Could not load Units/Rifleman SO/Prefab!");
                return;
            }

            // Find command post or starting base (GlobalCommander) to spawn at
            Vector3 spawnPos = Vector3.zero;
            bool foundBase = false;

            var bldgs = UnityEngine.Object.FindObjectsByType<BaseBuilding>(FindObjectsInactive.Exclude);
            foreach (var b in bldgs)
            {
                if (b != null && b.Owner == Owner.Player1 && b.BuildingSO != null && b.BuildingSO.Name.Contains("Command"))
                {
                    Vector3 candidatePos = b.transform.position + Vector3.forward * 4f;
                    if (UnityEngine.AI.NavMesh.SamplePosition(candidatePos, out UnityEngine.AI.NavMeshHit hit, 8f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        spawnPos = hit.position;
                    }
                    else
                    {
                        spawnPos = b.transform.position;
                    }
                    foundBase = true;
                    break;
                }
            }

            if (!foundBase)
            {
                var globalCmdr = UnityEngine.Object.FindAnyObjectByType<GlobalCommander>(FindObjectsInactive.Exclude);
                if (globalCmdr != null)
                {
                    Vector3 candidatePos = globalCmdr.transform.position + Vector3.forward * 4f;
                    if (UnityEngine.AI.NavMesh.SamplePosition(candidatePos, out UnityEngine.AI.NavMeshHit hit, 8f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        spawnPos = hit.position;
                    }
                    else
                    {
                        spawnPos = globalCmdr.transform.position;
                    }
                    foundBase = true;
                }
            }

            if (!foundBase)
            {
                var mainCam = Camera.main;
                if (mainCam != null)
                {
                    Ray ray = mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                    if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                    {
                        Vector3 candidatePos = hit.point;
                        if (UnityEngine.AI.NavMesh.SamplePosition(candidatePos, out UnityEngine.AI.NavMeshHit navHit, 15f, UnityEngine.AI.NavMesh.AllAreas))
                        {
                            spawnPos = navHit.position;
                            foundBase = true;
                        }
                    }
                }
            }

            GameObject spawnedUnit = UnityEngine.Object.Instantiate(unitSO.Prefab, spawnPos, Quaternion.identity);
            
            // Set Owner to Player1
            var abstractUnit = spawnedUnit.GetComponent<AbstractUnit>();
            if (abstractUnit != null)
            {
                abstractUnit.Owner = Owner.Player1;
                abstractUnit.gameObject.name = "Colony Engineer";
            }

            // Attach the ColonyEngineer loop
            spawnedUnit.AddComponent<ColonyEngineer>();
            
            Debug.Log($"[Blueprint] Spawned Colony Engineer at {spawnPos}");
        }
    }
}
