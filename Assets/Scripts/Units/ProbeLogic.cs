using UnityEngine;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Environment
{
    [RequireComponent(typeof(AbstractUnit))]
    public class ProbeLogic : MonoBehaviour
    {
        public float ScanRadius = 20f;
        private AbstractUnit unit;
        private float nextSectorCheckTime = 0f;

        private void Awake()
        {
            unit = GetComponent<AbstractUnit>();
        }

        private void Start()
        {
            // Movement is handled by the ProbeMovement component
        }

        private void Update()
        {
            // Performance note: In a larger game, we'd use a LayerMask OverlapSphere or spatial partitioning
            // For MVP, FindObjectsByType is sufficient for a low number of resources
            HiddenResource[] hiddenResources = FindObjectsByType<HiddenResource>(FindObjectsInactive.Exclude);
            foreach (var res in hiddenResources)
            {
                if (!res.IsDiscovered)
                {
                    if (Vector3.Distance(transform.position, res.transform.position) <= ScanRadius)
                    {
                        res.Discover();
                    }
                }
            }

            if (Time.time >= nextSectorCheckTime)
            {
                nextSectorCheckTime = Time.time + 1.5f;

                if (SectorManager.Instance != null && ColonyExpansionManager.Instance != null)
                {
                    var sector = SectorManager.Instance.GetNearestSector(transform.position);
                    if (sector != null && !sector.IsOccupied && !ColonyExpansionManager.Instance.IsExpandingToSector(sector) && !ColonyExpansionManager.Instance.IsSectorVetoed(sector))
                    {
                        Vector3 buildPos = transform.position;
                        UnityEngine.AI.NavMeshQueryFilter filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = 0, areaMask = UnityEngine.AI.NavMesh.AllAreas };
                        if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out UnityEngine.AI.NavMeshHit hit, 20f, filter))
                        {
                            buildPos = hit.position;
                        }
                        else
                        {
                            Ray ray = new Ray(transform.position + Vector3.up * 50f, Vector3.down);
                            if (Physics.Raycast(ray, out RaycastHit groundHit, 100f, LayerMask.GetMask("Default", "Terrain")))
                            {
                                buildPos = groundHit.point;
                            }
                        }

                        // Ensure building position is actually in the target sector, fallback to sector center if not
                        if (SectorManager.Instance.GetNearestSector(buildPos) != sector)
                        {
                            buildPos = sector.Center;
                        }

                        ColonyExpansionManager.Instance.StartExpansion(buildPos, sector);
                    }
                }
            }
        }
    }
}
