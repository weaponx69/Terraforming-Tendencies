using UnityEditor;
using UnityEngine;

namespace GameDevTV.RTS.EditorScripts
{
    [InitializeOnLoad]
    public class FixTMPShaders
    {
        static FixTMPShaders()
        {
            EditorApplication.delayCall += Fix;
        }

        [MenuItem("Tools/Fix TMP Shaders")]
        public static void Fix()
        {
            Shader tmpShader = Shader.Find("TextMeshPro/Distance Field");
            if (tmpShader == null) return;

            bool fixedAny = false;

            string[] guids = AssetDatabase.FindAssets("t:Material");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("TextMesh Pro") || path.Contains("Fonts & Materials") || path.Contains("SDF") || path.Contains("Dogfish"))
                {
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat != null && mat.shader.name == "Custom/URP_CurvedWorld")
                    {
                        mat.shader = tmpShader;
                        EditorUtility.SetDirty(mat);
                        fixedAny = true;
                        Debug.Log($"Fixed shader on TMP material: {mat.name}");
                    }
                }
            }
            
            string[] fontGuids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            foreach (string guid in fontGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TMPro.TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(path);
                if (font != null && font.material != null)
                {
                    if (font.material.shader.name == "Custom/URP_CurvedWorld")
                    {
                        font.material.shader = tmpShader;
                        EditorUtility.SetDirty(font.material);
                        fixedAny = true;
                        Debug.Log($"Fixed shader on TMP FontAsset: {font.name}");
                    }
                }
            }
            
            if (fixedAny)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("Successfully restored TextMeshPro shaders!");
            }
        }
    }
}
