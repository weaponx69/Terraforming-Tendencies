using UnityEngine;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Environment
{
    [RequireComponent(typeof(AbstractUnit))]
    public class ProbeLogic : MonoBehaviour
    {
        public float ScanRadius = 20f;
        private AbstractUnit unit;

        private void Awake()
        {
            unit = GetComponent<AbstractUnit>();
        }

        private void Start()
        {
            if (PlanetGenerator.Instance != null && PlanetGenerator.Instance.Config != null)
            {
                float mapWidth = PlanetGenerator.Instance.Config.MapWidth * PlanetGenerator.Instance.CellSize;
                float mapHeight = PlanetGenerator.Instance.Config.MapHeight * PlanetGenerator.Instance.CellSize;

                Vector3 targetPos = new Vector3(Random.Range(0, mapWidth), 0, Random.Range(0, mapHeight));
                unit.MoveTo(targetPos);
            }
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
        }
    }
}
