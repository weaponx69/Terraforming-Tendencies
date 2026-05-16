using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using GameDevTV.RTS.Environment;

namespace GameDevTV.RTS.EditorScripts
{
    [InitializeOnLoad]
    public class AutoFixPrefabs
    {
        static AutoFixPrefabs()
        {
            EditorApplication.delayCall += FixPrefabs;
        }

        private static void FixPrefabs()
        {
            if (EditorPrefs.GetBool("AutoFixPrefabsRan", false))
                return;

            EditorPrefs.SetBool("AutoFixPrefabsRan", true);

            // 1. Delete Crystal Prefabs
            string[] crystalGuids = AssetDatabase.FindAssets("RandomCrystal_Crystal_ t:GameObject", new[] { "Assets/ProceduralAssets/Prefabs" });
            foreach (string guid in crystalGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.DeleteAsset(path);
            }

            // 2. Find Rock Prefabs
            string[] rockGuids = AssetDatabase.FindAssets("RandomRock_Rock_ t:GameObject", new[] { "Assets/ProceduralAssets/Prefabs" });
            GameObject[] rockPrefabs = rockGuids
                .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null)
                .ToArray();

            // 3. Update Planet Config
            PlanetConfig config = AssetDatabase.LoadAssetAtPath<PlanetConfig>("Assets/Settings/Planet 1 - Easy.asset");
            if (config != null)
            {
                config.SurfaceFeaturePrefabs = rockPrefabs;
                config.SurfaceRockPrefabs = rockPrefabs;
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                Debug.Log($"Fixed planet config: Assigned {rockPrefabs.Length} rocks and removed crystals/grey items.");
            }
        }
    }
}
