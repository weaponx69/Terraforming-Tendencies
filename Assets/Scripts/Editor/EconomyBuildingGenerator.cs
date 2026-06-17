using UnityEditor;
using UnityEngine;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Editor
{
    public class EconomyBuildingGenerator : EditorWindow
    {
        [MenuItem("Tools/Generate Economy Buildings (Habitat & Solar Panel)")]
        public static void GenerateBuildings()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Units"))
                AssetDatabase.CreateFolder("Assets", "Units");
            if (!AssetDatabase.IsValidFolder("Assets/Units/Buildings"))
                AssetDatabase.CreateFolder("Assets/Units", "Buildings");

            // Create Solar Panel Config
            BuildingConfigSO solarConfig = CreateInstance<BuildingConfigSO>();
            SerializedObject soSolar = new SerializedObject(solarConfig);
            soSolar.FindProperty("powerGeneration").floatValue = 50f;
            soSolar.FindProperty("housingCapacity").intValue = 0;
            soSolar.ApplyModifiedProperties();
            AssetDatabase.CreateAsset(solarConfig, "Assets/Units/Buildings/SolarPanelConfig.asset");

            // Create Habitat Config
            BuildingConfigSO habitatConfig = CreateInstance<BuildingConfigSO>();
            SerializedObject soHabitat = new SerializedObject(habitatConfig);
            soHabitat.FindProperty("powerUpkeep").floatValue = 10f;
            soHabitat.FindProperty("oxygenUpkeep").floatValue = 5f;
            soHabitat.FindProperty("housingCapacity").intValue = 10;
            soHabitat.ApplyModifiedProperties();
            AssetDatabase.CreateAsset(habitatConfig, "Assets/Units/Buildings/HabitatConfig.asset");

            // Try to find a base prefab to clone
            GameObject basePrefab = null;
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("Oxygen Processor") || path.Contains("Command Post") || path.Contains("Building"))
                {
                    GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go != null && go.GetComponent<BaseBuilding>() != null)
                    {
                        basePrefab = go;
                        break;
                    }
                }
            }

            if (basePrefab != null)
            {
                // Generate Solar Panel
                BuildingSO solarSO = CreateInstance<BuildingSO>();
                SerializedObject soSolarSO = new SerializedObject(solarSO);
                soSolarSO.FindProperty("<Name>k__BackingField").stringValue = "Solar Panel";
                soSolarSO.FindProperty("<Description>k__BackingField").stringValue = "Generates power for the colony.";
                soSolarSO.FindProperty("<Health>k__BackingField").intValue = 300;
                soSolarSO.FindProperty("<BuildTime>k__BackingField").floatValue = 10f;
                soSolarSO.FindProperty("buildingConfig").objectReferenceValue = solarConfig;
                AssetDatabase.CreateAsset(solarSO, "Assets/Units/Buildings/SolarPanel.asset");

                string solarPrefabPath = "Assets/Units/Buildings/SolarPanel.prefab";
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(basePrefab), solarPrefabPath);
                GameObject solarObj = PrefabUtility.LoadPrefabContents(solarPrefabPath);
                
                // Update BuildingSO reference in the prefab if possible
                var b = solarObj.GetComponent<BaseBuilding>();
                if (b != null)
                {
                    SerializedObject serializedB = new SerializedObject(b);
                    SerializedProperty unitSOProp = serializedB.FindProperty("<UnitSO>k__BackingField");
                    if (unitSOProp != null) unitSOProp.objectReferenceValue = solarSO;
                    serializedB.ApplyModifiedProperties();
                }

                solarObj.name = "Solar Panel";
                PrefabUtility.SaveAsPrefabAsset(solarObj, solarPrefabPath);
                PrefabUtility.UnloadPrefabContents(solarObj);

                // Generate Habitat
                BuildingSO habitatSO = CreateInstance<BuildingSO>();
                SerializedObject soHabitatSO = new SerializedObject(habitatSO);
                soHabitatSO.FindProperty("<Name>k__BackingField").stringValue = "Habitat";
                soHabitatSO.FindProperty("<Description>k__BackingField").stringValue = "Houses colonists and consumes power and oxygen.";
                soHabitatSO.FindProperty("<Health>k__BackingField").intValue = 500;
                soHabitatSO.FindProperty("<BuildTime>k__BackingField").floatValue = 15f;
                soHabitatSO.FindProperty("buildingConfig").objectReferenceValue = habitatConfig;
                AssetDatabase.CreateAsset(habitatSO, "Assets/Units/Buildings/Habitat.asset");

                string habitatPrefabPath = "Assets/Units/Buildings/Habitat.prefab";
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(basePrefab), habitatPrefabPath);
                GameObject habitatObj = PrefabUtility.LoadPrefabContents(habitatPrefabPath);

                var hb = habitatObj.GetComponent<BaseBuilding>();
                if (hb != null)
                {
                    SerializedObject serializedB = new SerializedObject(hb);
                    SerializedProperty unitSOProp = serializedB.FindProperty("<UnitSO>k__BackingField");
                    if (unitSOProp != null) unitSOProp.objectReferenceValue = habitatSO;
                    serializedB.ApplyModifiedProperties();
                }

                habitatObj.name = "Habitat";
                PrefabUtility.SaveAsPrefabAsset(habitatObj, habitatPrefabPath);
                PrefabUtility.UnloadPrefabContents(habitatObj);

                Debug.Log("[EconomyBuildingGenerator] Successfully generated Habitat and Solar Panel configurations and prefabs based on " + basePrefab.name);
            }
            else
            {
                Debug.LogWarning("[EconomyBuildingGenerator] Generated Configs but could not find a BaseBuilding prefab to clone. You will need to create the Prefabs and BuildingSOs manually.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
