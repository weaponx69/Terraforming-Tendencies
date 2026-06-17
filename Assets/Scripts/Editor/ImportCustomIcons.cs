using UnityEngine;
using UnityEditor;
using System.IO;
using GameDevTV.RTS.Commands;

public class ImportCustomIcons : EditorWindow
{
    [MenuItem("Tools/Import All Custom Icons")]
    public static void ImportIcons()
    {
        // 1. Fix the Plug Icon
        FixSpriteImport("Assets/Resources/PlugIcon.png");

        // 2. Import Solar Panel Icon
        string solarSource = "/home/brian/.gemini/antigravity/brain/02f9ce42-b784-44fb-9708-a469bdea6a72/solar_panel_icon_1781708474731.png";
        string solarDest = "Assets/Units/Commands/SolarPanelIconCustom.png";
        
        if (File.Exists(solarSource))
        {
            File.Copy(solarSource, solarDest, true);
            AssetDatabase.ImportAsset(solarDest);
            FixSpriteImport(solarDest);
            
            // Assign to the Solar Panel command
            BuildBuildingCommand solarCmd = AssetDatabase.LoadAssetAtPath<BuildBuildingCommand>("Assets/Units/Commands/Build Solar Panel.asset");
            Sprite solarSprite = AssetDatabase.LoadAssetAtPath<Sprite>(solarDest);
            if (solarCmd != null && solarSprite != null)
            {
                SerializedObject so = new SerializedObject(solarCmd);
                SerializedProperty iconProp = so.FindProperty("<Icon>k__BackingField");
                if (iconProp != null)
                {
                    iconProp.objectReferenceValue = solarSprite;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(solarCmd);
                    Debug.Log("[ImportCustomIcons] Successfully assigned Solar Panel icon!");
                }
            }
        }
        else
        {
            Debug.LogError("[ImportCustomIcons] Could not find the generated solar panel image.");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[ImportCustomIcons] All icons imported and fixed!");
    }

    private static void FixSpriteImport(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }
    }
}
