using System.Collections.Generic;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using UnityEditor;
using UnityEngine;
using System.IO;

namespace GameDevTV.RTS.Editor
{
    public class UpgradeGenerator
    {
        [MenuItem("Tools/Generate 100 Droid Upgrades")]
        public static void GenerateUpgrades()
        {
            string folderPath = "Assets/Units/Upgrades/DroidUpgrades";
            if (!AssetDatabase.IsValidFolder("Assets/Units/Upgrades"))
                AssetDatabase.CreateFolder("Assets/Units", "Upgrades");
            if (!AssetDatabase.IsValidFolder(folderPath))
                AssetDatabase.CreateFolder("Assets/Units/Upgrades", "DroidUpgrades");

            TechTreeSO techTree = AssetDatabase.LoadAssetAtPath<TechTreeSO>("Assets/Tech Trees/Human Tech Tree.asset");
            if (techTree == null) techTree = AssetDatabase.LoadAssetAtPath<TechTreeSO>("Assets/Tech Trees/Colony Tech Tree.asset");

            List<UnlockableSO> allUnlockables = new List<UnlockableSO>();

            // 1. Mining Drone
            string[] miningNames = {
                "Thrusters Mk I", "Cargo Bins Mk I", "Drill Bits Mk I",
                "Thrusters Mk II", "Cargo Bins Mk II", "Drill Bits Mk II",
                "Thrusters Mk III", "Cargo Bins Mk III", "Drill Bits Mk III",
                "Thrusters Mk IV", "Cargo Bins Mk IV", "Drill Bits Mk IV",
                "Thrusters Mk V", "Cargo Bins Mk V", "Drill Bits Mk V",
                "Advanced Thrusters", "Advanced Cargo Bins", "Elite Drill Bits",
                "Elite Thrusters", "Omega Cargo Bins"
            };
            string[] miningProps = {
                "MovementConfig/Speed", "TransportConfig/Capacity", "GatherConfig/GatherRateMultiplier"
            };
            GenerateSet("Mining Drone", miningNames, miningProps, folderPath, allUnlockables, "Assets/Resources/Units/MiningDrone.asset");

            // 2. Construction Drone
            string[] constNames = {
                "Thrusters Mk I", "Alloy Plating Mk I", "Welding Torch Mk I",
                "Thrusters Mk II", "Alloy Plating Mk II", "Welding Torch Mk II",
                "Thrusters Mk III", "Alloy Plating Mk III", "Welding Torch Mk III",
                "Thrusters Mk IV", "Alloy Plating Mk IV", "Welding Torch Mk IV",
                "Thrusters Mk V", "Alloy Plating Mk V", "Welding Torch Mk V",
                "Advanced Thrusters", "Advanced Alloy Plating", "Elite Welding Torch",
                "Elite Thrusters", "Omega Alloy Plating"
            };
            string[] constProps = {
                "MovementConfig/Speed", "Health", "BuilderConfig/BuildSpeedMultiplier"
            };
            GenerateSet("Construction Drone", constNames, constProps, folderPath, allUnlockables, "Assets/Resources/Units/ConstructionDrone.asset");

            // 3. Probe
            string[] probeNames = {
                "Thrusters Mk I", "Optics Mk I", "Processor Mk I",
                "Thrusters Mk II", "Optics Mk II", "Processor Mk II",
                "Thrusters Mk III", "Optics Mk III", "Processor Mk III",
                "Thrusters Mk IV", "Optics Mk IV", "Processor Mk IV",
                "Thrusters Mk V", "Optics Mk V", "Processor Mk V",
                "Advanced Thrusters", "Advanced Optics", "Elite Processor",
                "Elite Thrusters", "Omega Optics"
            };
            string[] probeProps = {
                "MovementConfig/Speed", "SightConfig/SightRadius", "ProbeConfig/AnalysisTimeMultiplier"
            };
            GenerateSet("Probe", probeNames, probeProps, folderPath, allUnlockables, "Assets/Resources/Units/Probe.asset");

            // 4. Foundry Crawler
            string[] crawlerNames = {
                "Treads Mk I", "Iron Hopper Mk I", "Regolith Hopper Mk I",
                "Treads Mk II", "Iron Hopper Mk II", "Regolith Hopper Mk II",
                "Treads Mk III", "Iron Hopper Mk III", "Regolith Hopper Mk III",
                "Treads Mk IV", "Iron Hopper Mk IV", "Regolith Hopper Mk IV",
                "Treads Mk V", "Iron Hopper Mk V", "Regolith Hopper Mk V",
                "Advanced Treads", "Advanced Iron Hopper", "Elite Regolith Hopper",
                "Elite Treads", "Omega Iron Hopper"
            };
            string[] crawlerProps = {
                "MovementConfig/Speed", "TransportConfig/MaxIron", "TransportConfig/MaxRegolith"
            };
            GenerateSet("Foundry Crawler", crawlerNames, crawlerProps, folderPath, allUnlockables, "Assets/Resources/Units/FoundryCrawler.asset");

            // 5. Command Post
            string[] cpNames = {
                "Scaffolding Mk I", "Bio-Dome Mk I", "AI Schedulers Mk I",
                "Scaffolding Mk II", "Bio-Dome Mk II", "AI Schedulers Mk II",
                "Scaffolding Mk III", "Bio-Dome Mk III", "AI Schedulers Mk III",
                "Scaffolding Mk IV", "Bio-Dome Mk IV", "AI Schedulers Mk IV",
                "Scaffolding Mk V", "Bio-Dome Mk V", "AI Schedulers Mk V",
                "Advanced Scaffolding", "Advanced Bio-Dome", "Elite AI Schedulers",
                "Elite Scaffolding", "Omega Bio-Dome"
            };
            string[] cpProps = {
                "BuildingConfig/BuildTimeMultiplier", "BuildingConfig/LifeSupportRadius", "BuildingConfig/QueueSize"
            };
            GenerateSet("Command Post", cpNames, cpProps, folderPath, allUnlockables, "Assets/Units/Buildings/Command Post/Command Post.asset");

            if (techTree != null)
            {
                SerializedObject serializedObject = new SerializedObject(techTree);
                SerializedProperty unlockablesProp = serializedObject.FindProperty("allUnlockables");
                
                // Overwrite the existing Tech Tree so the UI automatically sees it!
                SerializedObject so = new SerializedObject(techTree);
                SerializedProperty newUnlockables = so.FindProperty("allUnlockables");
                newUnlockables.ClearArray();
                for (int i = 0; i < allUnlockables.Count; i++)
                {
                    newUnlockables.InsertArrayElementAtIndex(i);
                    newUnlockables.GetArrayElementAtIndex(i).objectReferenceValue = allUnlockables[i];
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(techTree);
                Debug.Log($"Overwrote existing Tech Tree with {allUnlockables.Count} upgrades!");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Generated 100 Upgrades!");
        }

        private static void GenerateSet(string prefix, string[] names, string[] props, string folderPath, List<UnlockableSO> allUnlockables, string unitAssetPath)
        {
            AbstractUnitSO targetUnit = AssetDatabase.LoadAssetAtPath<AbstractUnitSO>(unitAssetPath);
            List<UpgradeSO> unitUpgrades = new List<UpgradeSO>();

            for (int i = 0; i < names.Length; i++)
            {
                string propPath = props[i % 3];
                string assetName = $"{prefix} - {names[i]}";
                string assetPath = $"{folderPath}/{assetName}.asset";

                UpgradeSO upgrade = null;

                bool isFloat = propPath.Contains("Multiplier") || propPath.Contains("Speed") || propPath.Contains("Radius");
                
                if (isFloat)
                {
                    AdditiveFloatModifierSO floatMod = ScriptableObject.CreateInstance<AdditiveFloatModifierSO>();
                    SerializedObject so = new SerializedObject(floatMod);
                    so.FindProperty("<PropertyPath>k__BackingField").stringValue = propPath;
                    
                    // Values
                    float amount = 1f;
                    if (propPath.Contains("Speed") || propPath.Contains("Radius")) amount = 2f;
                    if (propPath.Contains("Multiplier")) amount = 0.25f;
                    
                    so.FindProperty("<Amount>k__BackingField").floatValue = amount;
                    
                    so.FindProperty("<Title>k__BackingField").stringValue = names[i];
                    so.FindProperty("<Description>k__BackingField").stringValue = $"Improves {propPath.Split('/')[1]} by {amount}";
                    so.ApplyModifiedProperties();
                    upgrade = floatMod;
                }
                else
                {
                    AdditiveIntModifierSO intMod = ScriptableObject.CreateInstance<AdditiveIntModifierSO>();
                    SerializedObject so = new SerializedObject(intMod);
                    so.FindProperty("<PropertyPath>k__BackingField").stringValue = propPath;
                    
                    int amount = 1;
                    if (propPath.Contains("Health")) amount = 50;
                    if (propPath.Contains("MaxIron") || propPath.Contains("MaxRegolith") || propPath.Contains("Capacity")) amount = 5;
                    
                    so.FindProperty("<Amount>k__BackingField").intValue = amount;
                    so.FindProperty("<Title>k__BackingField").stringValue = names[i];
                    so.FindProperty("<Description>k__BackingField").stringValue = $"Increases {propPath.Split('/')[1]} by {amount}";
                    so.ApplyModifiedProperties();
                    upgrade = intMod;
                }

                AssetDatabase.CreateAsset(upgrade, assetPath);
                allUnlockables.Add(upgrade);
                unitUpgrades.Add(upgrade);
            }

            if (targetUnit != null)
            {
                SerializedObject unitSO = new SerializedObject(targetUnit);
                SerializedProperty upgradesProp = unitSO.FindProperty("upgrades");
                if (upgradesProp == null) upgradesProp = unitSO.FindProperty("<Upgrades>k__BackingField"); // Try backing field

                if (upgradesProp != null)
                {
                    upgradesProp.ClearArray();
                    for (int i = 0; i < unitUpgrades.Count; i++)
                    {
                        upgradesProp.InsertArrayElementAtIndex(i);
                        upgradesProp.GetArrayElementAtIndex(i).objectReferenceValue = unitUpgrades[i];
                    }
                    unitSO.ApplyModifiedProperties();
                    EditorUtility.SetDirty(targetUnit);
                    Debug.Log($"Assigned 20 upgrades to {prefix}");
                }
            }
            else
            {
                Debug.LogWarning($"Could not find unit asset at {unitAssetPath} to assign upgrades to.");
            }
        }
    }
}
