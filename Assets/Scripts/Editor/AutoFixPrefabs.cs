using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;
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

            // 1. Find Rock Prefabs
            string[] rockGuids = AssetDatabase.FindAssets("RandomRock_Rock_ t:GameObject", new[] { "Assets/ProceduralAssets/Prefabs" });
            GameObject[] rockPrefabs = rockGuids
                .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null)
                .ToArray();

            // 2. Find Crystal Prefabs (Minerals)
            string[] crystalGuids = AssetDatabase.FindAssets("RandomCrystal_Crystal_ t:GameObject", new[] { "Assets/ProceduralAssets/Prefabs" });
            GameObject[] crystalPrefabs = crystalGuids
                .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null)
                .ToArray();

            // 3. Update Planet Config
            PlanetConfig config = AssetDatabase.LoadAssetAtPath<PlanetConfig>("Assets/Settings/Planet 1 - Easy.asset");
            if (config != null)
            {
                config.SurfaceFeaturePrefabs = rockPrefabs.Concat(crystalPrefabs).ToArray();
                config.SurfaceRockPrefabs = rockPrefabs; // Rocks for overlay
                
                // Add crystals to ResourcePrefabs too
                List<GameObject> resourceList = new List<GameObject>();
                GameObject gas = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Gatherable Supplies/Gas.prefab");
                GameObject mineral = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Gatherable Supplies/Minerals.prefab");
                if (gas != null) resourceList.Add(gas);
                if (mineral != null) resourceList.Add(mineral);
                resourceList.AddRange(crystalPrefabs);
                config.ResourcePrefabs = resourceList.ToArray();

                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                Debug.Log($"Fixed planet config: Assigned {rockPrefabs.Length} rocks and {crystalPrefabs.Length} crystals (minerals).");
            }
        }
    }
}
