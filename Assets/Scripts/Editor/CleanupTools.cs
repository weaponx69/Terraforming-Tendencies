using UnityEditor;

public class CleanupTools
{
    [MenuItem("Tools/Cleanup Temp Scripts")]
    public static void Cleanup()
    {
        AssetDatabase.DeleteAsset("Assets/Scripts/Editor/FixBuildingCommands.cs");
        AssetDatabase.DeleteAsset("Assets/Scripts/Editor/FixBuildingPrefabsAndIcons.cs");
        AssetDatabase.DeleteAsset("Assets/Scripts/Editor/FixMissingBuildingPrefabs.cs");
        AssetDatabase.DeleteAsset("Assets/Scripts/Editor/UpdateSelectionIndicators.cs");
        AssetDatabase.DeleteAsset("Assets/Scripts/Editor/UpdateBuildingPowerRequirements.cs");
        AssetDatabase.DeleteAsset("Assets/Scripts/Editor/EconomyBuildingGenerator.cs");
        
        // Self-delete
        AssetDatabase.DeleteAsset("Assets/Scripts/Editor/CleanupTools.cs");
        
        UnityEngine.Debug.Log("Cleaned up all temporary Editor tool scripts!");
    }
}
