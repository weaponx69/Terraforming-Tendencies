using UnityEngine;
using UnityEditor;
using GameDevTV.RTS.Units;
using Unity.Behavior;

public class FixHeroDronePrefab
{
    [MenuItem("Tools/Refactor Hero Drone Prefab")]
    public static void RefactorPrefab()
    {
        string prefabPath = "Assets/Resources/Units/Hero Drone.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"Could not find prefab at {prefabPath}");
            return;
        }

        // Open prefab for editing
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

        // 1. Add HeroDrone
        HeroDrone newHero = instance.GetComponent<HeroDrone>();
        if (newHero == null)
        {
            newHero = instance.AddComponent<HeroDrone>();
        }

        // 2. Remove old scripts
        Worker worker = instance.GetComponent<Worker>();
        if (worker != null) Object.DestroyImmediate(worker, true);
        
        WorkerBrainController brain = instance.GetComponent<WorkerBrainController>();
        if (brain != null) Object.DestroyImmediate(brain, true);

        BehaviorGraphAgent graph = instance.GetComponent<BehaviorGraphAgent>();
        if (graph != null) Object.DestroyImmediate(graph, true);

        // Save changes
        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);

        Debug.Log("Successfully refactored Hero Drone prefab! (Removed Worker, Brain, BehaviorGraph; Added HeroDrone)");
    }
}
