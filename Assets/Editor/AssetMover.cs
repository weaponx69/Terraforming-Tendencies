using UnityEngine;
using UnityEditor;
using System.IO;

[InitializeOnLoad]
public class AssetMover
{
    static AssetMover()
    {
        EditorApplication.delayCall += MoveAssets;
    }

    private static void MoveAssets()
    {
        bool didMove = false;

        string resourcesFolder = "Assets/Resources";
        if (!AssetDatabase.IsValidFolder(resourcesFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        string suppliesFolder = "Assets/Resources/Gatherable Supplies";
        if (!AssetDatabase.IsValidFolder(suppliesFolder))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Gatherable Supplies");
        }

        didMove |= TryMoveAsset("Assets/Gatherable Supplies/Minerals.asset", "Assets/Resources/Gatherable Supplies/Minerals.asset");
        didMove |= TryMoveAsset("Assets/Gatherable Supplies/Gas.asset", "Assets/Resources/Gatherable Supplies/Gas.asset");

        // The drone is likely in Assets/Units/Player1/MiningDrone.asset but we should search for it if it's not there.
        string dronePath = "Assets/Units/Player1/MiningDrone.asset";
        if (!File.Exists(dronePath))
        {
            string[] guids = AssetDatabase.FindAssets("MiningDrone t:AbstractUnitSO");
            if (guids.Length > 0)
            {
                dronePath = AssetDatabase.GUIDToAssetPath(guids[0]);
            }
        }

        if (File.Exists(dronePath) && !dronePath.Contains("/Resources/"))
        {
            string droneResourcesFolder = "Assets/Resources/Units";
            if (!AssetDatabase.IsValidFolder(droneResourcesFolder))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Units");
            }
            didMove |= TryMoveAsset(dronePath, "Assets/Resources/Units/" + Path.GetFileName(dronePath));
        }

        if (didMove)
        {
            Debug.Log("[AssetMover] Automatically moved assets to Resources folder so they can be loaded dynamically!");
            AssetDatabase.Refresh();
        }
    }

    private static bool TryMoveAsset(string oldPath, string newPath)
    {
        if (File.Exists(oldPath) && !File.Exists(newPath))
        {
            string result = AssetDatabase.MoveAsset(oldPath, newPath);
            if (string.IsNullOrEmpty(result))
            {
                return true;
            }
        }
        return false;
    }
}
