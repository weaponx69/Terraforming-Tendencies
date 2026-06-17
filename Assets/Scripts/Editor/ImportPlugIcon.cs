using UnityEngine;
using UnityEditor;
using System.IO;

public class ImportPlugIcon : EditorWindow
{
    [MenuItem("Tools/Import Plug Icon")]
    public static void ImportIcon()
    {
        string sourcePath = "/home/brian/.gemini/antigravity/brain/02f9ce42-b784-44fb-9708-a469bdea6a72/wall_plug_icon_1781707057279.png";
        
        // Ensure Resources folder exists
        if (!Directory.Exists("Assets/Resources"))
        {
            Directory.CreateDirectory("Assets/Resources");
        }
        
        string destPath = "Assets/Resources/PlugIcon.png";
        
        if (File.Exists(sourcePath))
        {
            File.Copy(sourcePath, destPath, true);
            AssetDatabase.ImportAsset(destPath);
            
            TextureImporter importer = AssetImporter.GetAtPath(destPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.SaveAndReimport();
                Debug.Log("[ImportPlugIcon] Successfully imported PlugIcon.png as a Sprite!");
            }
        }
        else
        {
            Debug.LogError($"[ImportPlugIcon] Could not find the generated image at {sourcePath}");
        }
    }
}
