using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Environment
{
    public class HiddenResourceSpawner : MonoBehaviour
    {
        public void SpawnResources()
        {
            if (PlanetGenerator.Instance == null || PlanetGenerator.Instance.Config == null) return;
            
            GameObject[] prefabs = PlanetGenerator.Instance.Config.ResourcePrefabs;
            if (prefabs == null || prefabs.Length == 0) return;

            int count = PlanetGenerator.Instance.Config.ResourceCount;
            float mapWidth = PlanetGenerator.Instance.Config.MapWidth * PlanetGenerator.Instance.CellSize;
            float mapHeight = PlanetGenerator.Instance.Config.MapHeight * PlanetGenerator.Instance.CellSize;

            for (int i = 0; i < count; i++)
            {
                Vector3 randomPos = new Vector3(Random.Range(0, mapWidth), 0, Random.Range(0, mapHeight));
                
                // Try to find a valid spot on the NavMesh
                if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                {
                    GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
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
