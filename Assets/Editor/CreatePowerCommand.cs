using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class CreatePowerCommand
{
    static CreatePowerCommand()
    {
        EditorApplication.delayCall += DoIt;
    }

    static void DoIt()
    {
        if (SessionState.GetBool("CreatedPowerCommand", false)) return;

        string dirPath = "Assets/Units/Commands";
        string path = dirPath + "/Connect Power Command.asset";
        
        if (!AssetDatabase.IsValidFolder(dirPath))
        {
            System.IO.Directory.CreateDirectory(dirPath);
            AssetDatabase.Refresh();
        }

        GameDevTV.RTS.Commands.ConnectPowerCommand cmd = AssetDatabase.LoadAssetAtPath<GameDevTV.RTS.Commands.ConnectPowerCommand>(path);
        
        if (cmd == null)
        {
            cmd = ScriptableObject.CreateInstance<GameDevTV.RTS.Commands.ConnectPowerCommand>();
            
            var nameField = typeof(GameDevTV.RTS.Commands.BaseCommand).GetField("<Name>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (nameField != null) nameField.SetValue(cmd, "Connect Power");

            var iconField = typeof(GameDevTV.RTS.Commands.BaseCommand).GetField("<Icon>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (iconField != null) iconField.SetValue(cmd, UnityEngine.Resources.Load<UnityEngine.Sprite>("PlugIcon"));
            
            AssetDatabase.CreateAsset(cmd, path);
            AssetDatabase.SaveAssets();
            Debug.Log("[Antigravity] Successfully generated Connect Power Command.asset");
        }

        string[] possiblePaths = {
            "Assets/Units/Buildings/SolarPanel/SolarPanel.prefab",
            "Assets/Units/Buildings/Solar Panel/Solar Panel.prefab",
            "Assets/Units/Buildings/SolarPanel/Solar Panel.prefab"
        };

        foreach (string solarPath in possiblePaths)
        {
            GameObject solarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(solarPath);
            if (solarPrefab != null)
            {
                var bb = solarPrefab.GetComponent<GameDevTV.RTS.Units.BaseBuilding>();
                if (bb != null)
                {
                    bool hasCommand = false;
                    foreach(var c in bb.AvailableCommands)
                    {
                        if (c is GameDevTV.RTS.Commands.ConnectPowerCommand) hasCommand = true;
                    }

                    if (!hasCommand)
                    {
                        var serializedObj = new SerializedObject(bb);
                        var prop = serializedObj.FindProperty("<AvailableCommands>k__BackingField");
                        if (prop != null)
                        {
                            int index = prop.arraySize;
                            prop.InsertArrayElementAtIndex(index);
                            prop.GetArrayElementAtIndex(index).objectReferenceValue = cmd;
                            serializedObj.ApplyModifiedProperties();
                            
                            PrefabUtility.SavePrefabAsset(solarPrefab);
                            Debug.Log($"[Antigravity] Successfully added Connect Power to {solarPath}!");
                        }
                    }
                }
                break;
            }
        }
        
        SessionState.SetBool("CreatedPowerCommand", true);
    }
}
