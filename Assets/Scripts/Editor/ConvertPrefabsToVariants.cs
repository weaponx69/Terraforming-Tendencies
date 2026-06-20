using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.EditorScripts
{
    [InitializeOnLoad]
    public class ConvertPrefabsToVariants
    {
        static ConvertPrefabsToVariants()
        {
            EditorApplication.delayCall += ConvertPrefabs;
        }

        private static void ConvertPrefabs()
        {
            if (EditorPrefs.GetBool("PrefabVariantConversionRan_v2", false))
                return;

            // Set the flag immediately to prevent re-entry loops during domain reload
            EditorPrefs.SetBool("PrefabVariantConversionRan_v2", true);

            // Load the BaseBuilding prefab (source)
            string baseBuildingPath = "Assets/Units/Buildings/BaseBuilding.prefab";
            GameObject baseBuildingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(baseBuildingPath);
            if (baseBuildingPrefab == null)
            {
                Debug.LogError("[ConvertPrefabs] BaseBuilding prefab not found!");
                return;
            }

            // Define target paths
            string commandPostPath = "Assets/Units/Buildings/Command Post/Command Post.prefab";
            string solarPanelPath = "Assets/Units/Buildings/Solar Panel/Solar Panel.prefab";

            // Convert Command Post
            ConvertPrefab(commandPostPath, baseBuildingPrefab, "Command Post", 15f);

            // Convert Solar Panel
            ConvertPrefab(solarPanelPath, baseBuildingPrefab, "Solar Panel", 10f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ConvertPrefabs] Successfully converted Command Post and Solar Panel standalone prefabs to BaseBuilding variants!");
        }

        private static void ConvertPrefab(string targetPrefabPath, GameObject baseBuildingPrefab, string name, float indicatorScale)
        {
            string backupPath = targetPrefabPath.Replace(".prefab", "_Backup.prefab");
            
            // Copy the original standalone prefab to a backup path (preserving targetPrefab's original GUID on the original file)
            if (!AssetDatabase.CopyAsset(targetPrefabPath, backupPath))
            {
                Debug.LogError($"[ConvertPrefabs] Failed to copy prefab {targetPrefabPath} to {backupPath}!");
                return;
            }

            GameObject backupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(backupPath);
            if (backupPrefab == null)
            {
                Debug.LogError($"[ConvertPrefabs] Backup prefab not found at {backupPath}!");
                AssetDatabase.DeleteAsset(backupPath);
                return;
            }

            // Instantiate both prefabs in the scene to copy settings/structure
            GameObject backupInstance = PrefabUtility.InstantiatePrefab(backupPrefab) as GameObject;
            GameObject variantInstance = PrefabUtility.InstantiatePrefab(baseBuildingPrefab) as GameObject;

            if (backupInstance == null || variantInstance == null)
            {
                if (backupInstance != null) Object.DestroyImmediate(backupInstance);
                if (variantInstance != null) Object.DestroyImmediate(variantInstance);
                AssetDatabase.DeleteAsset(backupPath);
                return;
            }

            // Rename root GameObject
            variantInstance.name = name;

            // Copy component values from backupInstance to variantInstance
            var variantBuilding = variantInstance.GetComponent<BaseBuilding>();
            var backupBuilding = backupInstance.GetComponent<BaseBuilding>();
            if (variantBuilding != null && backupBuilding != null)
            {
                SerializedObject backupSO = new SerializedObject(backupBuilding);
                SerializedObject variantSO = new SerializedObject(variantBuilding);

                SerializedProperty prop = backupSO.GetIterator();
                if (prop.NextVisible(true))
                {
                    do
                    {
                        if (prop.name != "m_Script" && prop.name != "selectionIndicator" && prop.name != "VisionTransform")
                        {
                            variantSO.CopyFromSerializedProperty(prop);
                        }
                    } while (prop.NextVisible(false));
                }
                variantSO.ApplyModifiedPropertiesWithoutUndo();
            }

            // Copy LifeSupportNode component values if present
            var backupLifeSupport = backupInstance.GetComponent<GameDevTV.RTS.Environment.LifeSupportNode>();
            var variantLifeSupport = variantInstance.GetComponent<GameDevTV.RTS.Environment.LifeSupportNode>();
            if (backupLifeSupport != null && variantLifeSupport != null)
            {
                variantLifeSupport.Radius = backupLifeSupport.Radius;
            }

            // Copy NavMeshObstacle component values if present
            var backupNavMesh = backupInstance.GetComponent<UnityEngine.AI.NavMeshObstacle>();
            var variantNavMesh = variantInstance.GetComponent<UnityEngine.AI.NavMeshObstacle>();
            if (backupNavMesh != null && variantNavMesh != null)
            {
                variantNavMesh.size = backupNavMesh.size;
                variantNavMesh.center = backupNavMesh.center;
                variantNavMesh.carving = backupNavMesh.carving;
            }

            // Delete variant's default mesh children (keep Selection Indicator and Vision)
            for (int i = variantInstance.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = variantInstance.transform.GetChild(i);
                if (child.name != "Selection Indicator" && child.name != "Vision")
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            // Copy visual mesh/model children from backupInstance to variantInstance
            for (int i = 0; i < backupInstance.transform.childCount; i++)
            {
                Transform child = backupInstance.transform.GetChild(i);
                if (child.name != "Selection Indicator" && child.name != "Vision")
                {
                    GameObject childCopy = Object.Instantiate(child.gameObject, variantInstance.transform);
                    childCopy.name = child.name;
                }
            }

            // Configure Selection Indicator scale
            Transform selectionIndicatorTrans = variantInstance.transform.Find("Selection Indicator");
            if (selectionIndicatorTrans != null)
            {
                selectionIndicatorTrans.localScale = new Vector3(indicatorScale, indicatorScale, 1f);
                
                // Wire it up on variantBuilding
                if (variantBuilding != null)
                {
                    SerializedObject variantBuildingSO = new SerializedObject(variantBuilding);
                    SerializedProperty selectProp = variantBuildingSO.FindProperty("selectionIndicator");
                    if (selectProp != null)
                    {
                        selectProp.objectReferenceValue = selectionIndicatorTrans.gameObject;
                        variantBuildingSO.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }

            // Save variantInstance as Prefab Variant at targetPrefabPath
            PrefabUtility.SaveAsPrefabAsset(variantInstance, targetPrefabPath);

            // Clean up scene instances
            Object.DestroyImmediate(backupInstance);
            Object.DestroyImmediate(variantInstance);

            // Delete backup asset
            AssetDatabase.DeleteAsset(backupPath);
        }
    }
}
