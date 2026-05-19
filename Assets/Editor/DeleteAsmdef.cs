using UnityEditor;
using System.IO;
using UnityEngine;

[InitializeOnLoad]
public class DeleteAsmdef
{
    static DeleteAsmdef()
    {
        EditorApplication.delayCall += DeleteFile;
    }

    private static void DeleteFile()
    {
        string path = "Assets/Scripts/Tests/Tests.asmdef";
        if (File.Exists(path))
        {
            AssetDatabase.DeleteAsset(path);
            Debug.Log("[DeleteAsmdef] Deleted Tests.asmdef to avoid compilation errors.");
        }
    }
}
