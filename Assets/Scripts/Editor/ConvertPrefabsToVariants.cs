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
            if (EditorPrefs.GetBool("PrefabVariantConversionRan_v1", false))
                return;

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

            EditorPrefs.SetBool("PrefabVariantConversionRan_v1", true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ConvertPrefabs] Successfully converted Command Post and Solar Panel standalone prefabs to BaseBuilding variants!");
        }

        private static void ConvertPrefab(string targetPrefabPath, GameObject baseBuildingPrefab, string name, float indicatorScale)
        {
            string backupPath = targetPrefabPath.Replace(".prefab", "_Backup.prefab");
            
            // Backup the original standalone prefab
            string moveError = AssetDatabase.MoveAsset(targetPrefabPath, backupPath);
            if (!string.IsNullOrEmpty(moveError))
            {
                Debug.LogError($"[ConvertPrefabs] Failed to backup prefab {targetPrefabPath} to {backupPath}: {moveError}");
                return;
            }
            AssetDatabase.Refresh();

            GameObject backupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(backupPath);
            if (backupPrefab == null)
            {
                Debug.LogError($"[ConvertPrefabs] Backup prefab not found at {backupPath}!");
                return;
            }

            // Create variant prefab of BaseBuilding at targetPrefabPath
            GameObject variantPrefab = PrefabUtility.CreateVariantPrefabOf(baseBuildingPrefab, targetPrefabPath);
            if (variantPrefab == null)
            {
                Debug.LogError($"[ConvertPrefabs] Failed to create variant prefab at {targetPrefabPath}!");
                AssetDatabase.MoveAsset(backupPath, targetPrefabPath); // Restore
                return;
            }

            // Load contents of both prefabs for editing
            GameObject backupRoot = PrefabUtility.LoadPrefabContents(backupPath);
            GameObject variantRoot = PrefabUtility.LoadPrefabContents(targetPrefabPath);

            // Rename root GameObject
            variantRoot.name = name;

            // Copy component values from backupRoot to variantRoot
            var variantBuilding = variantRoot.GetComponent<BaseBuilding>();
            var backupBuilding = backupRoot.GetComponent<BaseBuilding>();
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
            var backupLifeSupport = backupRoot.GetComponent<GameDevTV.RTS.Environment.LifeSupportNode>();
            var variantLifeSupport = variantRoot.GetComponent<GameDevTV.RTS.Environment.LifeSupportNode>();
            if (backupLifeSupport != null && variantLifeSupport != null)
            {
                variantLifeSupport.Radius = backupLifeSupport.Radius;
            }

            // Copy NavMeshObstacle component values if present
            var backupNavMesh = backupRoot.GetComponent<UnityEngine.AI.NavMeshObstacle>();
            var variantNavMesh = variantRoot.GetComponent<UnityEngine.AI.NavMeshObstacle>();
            if (backupNavMesh != null && variantNavMesh != null)
            {
                variantNavMesh.size = backupNavMesh.size;
                variantNavMesh.center = backupNavMesh.center;
                variantNavMesh.carving = backupNavMesh.carving;
            }

            // Delete variant's default mesh children (keep Selection Indicator and Vision)
            for (int i = variantRoot.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = variantRoot.transform.GetChild(i);
                if (child.name != "Selection Indicator" && child.name != "Vision")
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            // Copy visual mesh/model children from backupRoot to variantRoot
            for (int i = 0; i < backupRoot.transform.childCount; i++)
            {
                Transform child = backupRoot.transform.GetChild(i);
                if (child.name != "Selection Indicator" && child.name != "Vision")
                {
                    GameObject childCopy = Object.Instantiate(child.gameObject, variantRoot.transform);
                    childCopy.name = child.name;
                }
            }

            // Configure Selection Indicator scale
            Transform selectionIndicatorTrans = variantRoot.transform.Find("Selection Indicator");
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

            // Save and Unload
            PrefabUtility.SaveAsPrefabAsset(variantRoot, targetPrefabPath);
            PrefabUtility.UnloadPrefabContents(variantRoot);
            PrefabUtility.UnloadPrefabContents(backupRoot);

            // Delete backup asset
            AssetDatabase.DeleteAsset(backupPath);
            AssetDatabase.Refresh();
        }
    }
}
