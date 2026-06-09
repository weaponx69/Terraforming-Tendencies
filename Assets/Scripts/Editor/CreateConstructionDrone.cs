using UnityEngine;
using UnityEditor;
using GameDevTV.RTS.Units;

public class CreateConstructionDrone
{
    [MenuItem("Tools/Create Construction Drone Assets")]
    public static void Execute()
    {
        string miningDroneSOPath = "Assets/Resources/Units/MiningDrone.asset";
        string constructionDroneSOPath = "Assets/Resources/Units/ConstructionDrone.asset";

        AbstractUnitSO miningDroneSO = AssetDatabase.LoadAssetAtPath<AbstractUnitSO>(miningDroneSOPath);
        if (miningDroneSO == null)
        {
            Debug.LogError("Could not find MiningDrone SO at " + miningDroneSOPath);
            return;
        }

        string miningDronePrefabPath = AssetDatabase.GetAssetPath(miningDroneSO.Prefab);
        string constructionDronePrefabPath = miningDronePrefabPath.Replace("Mining Drone", "Construction Drone").Replace("MiningDrone", "ConstructionDrone");

        if (!AssetDatabase.CopyAsset(miningDronePrefabPath, constructionDronePrefabPath))
        {
            Debug.LogError("Failed to copy prefab to " + constructionDronePrefabPath);
            return;
        }

        if (!AssetDatabase.CopyAsset(miningDroneSOPath, constructionDroneSOPath))
        {
            Debug.LogError("Failed to copy SO to " + constructionDroneSOPath);
            return;
        }

        AssetDatabase.Refresh();

        GameObject constructionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(constructionDronePrefabPath);
        AbstractUnitSO constructionSO = AssetDatabase.LoadAssetAtPath<AbstractUnitSO>(constructionDroneSOPath);

        // Update SO to point to new prefab
        SerializedObject so = new SerializedObject(constructionSO);
        so.FindProperty("<Prefab>k__BackingField").objectReferenceValue = constructionPrefab;
        so.FindProperty("<Name>k__BackingField").stringValue = "Construction Drone";
        so.ApplyModifiedProperties();

        // Update Prefab material to Orange
        MeshRenderer[] renderers = constructionPrefab.GetComponentsInChildren<MeshRenderer>();
        foreach (var renderer in renderers)
        {
            if (renderer.sharedMaterial != null)
            {
                string matPath = AssetDatabase.GetAssetPath(renderer.sharedMaterial);
                if (!string.IsNullOrEmpty(matPath) && !matPath.Contains("Construction"))
                {
                    string newMatPath = matPath.Replace(".mat", "_Construction.mat");
                    if (!AssetDatabase.CopyAsset(matPath, newMatPath)) continue;
                    
                    Material newMat = AssetDatabase.LoadAssetAtPath<Material>(newMatPath);
                    newMat.color = new Color(1.0f, 0.5f, 0.0f); // Orange
                    newMat.SetColor("_Color", new Color(1.0f, 0.5f, 0.0f));
                    newMat.SetColor("_BaseColor", new Color(1.0f, 0.5f, 0.0f));
                    
                    renderer.sharedMaterial = newMat;
                }
            }
        }

        // Save prefab changes
        PrefabUtility.SavePrefabAsset(constructionPrefab);

        Debug.Log("Successfully created Construction Drone assets!");
    }
}
