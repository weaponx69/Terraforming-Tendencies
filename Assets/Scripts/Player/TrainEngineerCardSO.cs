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
            // Load starting unit SO (Rifleman) to get the human model prefab
            var unitSO = Resources.Load<AbstractUnitSO>("Units/Rifleman");
            if (unitSO == null || unitSO.Prefab == null)
            {
                Debug.LogWarning("[TrainEngineerCard] Could not load Units/Rifleman SO/Prefab!");
                return;
            }

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
                // Spawn inside or next to the command post
                spawnPos = spawnBase.transform.position + Vector3.forward * 4f;
            }
            else
            {
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
