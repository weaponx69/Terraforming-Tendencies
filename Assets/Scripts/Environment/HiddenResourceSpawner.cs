using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Environment
{
    public class HiddenResourceSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject[] resourcePrefabs;

        private void Start()
        {
            if (PlanetGenerator.Instance != null && PlanetGenerator.Instance.Config != null)
            {
                int count = PlanetGenerator.Instance.Config.ResourceCount;
                SpawnResources(count);
            }
        }

        private void SpawnResources(int count)
        {
            if (resourcePrefabs == null || resourcePrefabs.Length == 0) return;

            float mapWidth = PlanetGenerator.Instance.Config.MapWidth * PlanetGenerator.Instance.CellSize;
            float mapHeight = PlanetGenerator.Instance.Config.MapHeight * PlanetGenerator.Instance.CellSize;

            for (int i = 0; i < count; i++)
            {
                Vector3 randomPos = new Vector3(Random.Range(0, mapWidth), 0, Random.Range(0, mapHeight));
                
                // Try to find a valid spot on the NavMesh
                if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                {
                    GameObject prefab = resourcePrefabs[Random.Range(0, resourcePrefabs.Length)];
                    GameObject instance = Instantiate(prefab, hit.position, Quaternion.identity);
                    
                    if (instance.GetComponent<HiddenResource>() == null)
                    {
                        instance.AddComponent<HiddenResource>();
                    }
                }
            }
        }
    }
}
