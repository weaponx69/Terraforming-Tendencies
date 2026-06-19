using UnityEditor;
using System.IO;
using UnityEngine;

[InitializeOnLoad]
public static class AssetMover
{
    static AssetMover()
    {
        string[] filesToDelete = new string[]
        {
            "Assets/Units/Buildings/SolarPanel/SolarPanel.asset",
            "Assets/Units/Buildings/SolarPanel/SolarPanel.asset.meta",
            "Assets/Units/Buildings/Command Post/Command Post.asset",
            "Assets/Units/Buildings/Command Post/Command Post.asset.meta",
            "Assets/Units/Buildings/Habitat/Habitat.asset",
            "Assets/Units/Buildings/Habitat/Habitat.asset.meta",
            "Assets/Units/Buildings/Oxygen Processor/Oxygen Processor.asset",
            "Assets/Units/Buildings/Oxygen Processor/Oxygen Processor.asset.meta"
        };

        bool deletedAny = false;
        foreach (string file in filesToDelete)
        {
            string fullPath = Path.Combine(Application.dataPath, "..", file);
            if (File.Exists(fullPath))
            {
                try
                {
                    File.Delete(fullPath);
                    deletedAny = true;
                }
                catch (System.Exception)
                {
                }
            }
        }

        if (deletedAny)
        {
            AssetDatabase.Refresh();
        }
    }
}
