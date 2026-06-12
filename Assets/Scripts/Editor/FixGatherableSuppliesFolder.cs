using UnityEditor;
using UnityEngine;

namespace GameDevTV.RTS.EditorScripts
{
    [InitializeOnLoad]
    public class FixGatherableSuppliesFolder
    {
        static FixGatherableSuppliesFolder()
        {
            // Move Gatherable Supplies to Resources so Resources.Load can find Iron and Regolith
            if (AssetDatabase.IsValidFolder("Assets/Gatherable Supplies"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                {
                    AssetDatabase.CreateFolder("Assets", "Resources");
                }

                string newPath = AssetDatabase.GenerateUniqueAssetPath("Assets/Resources/Gatherable Supplies");
                string error = AssetDatabase.MoveAsset("Assets/Gatherable Supplies", newPath);
                
                if (string.IsNullOrEmpty(error))
                {
                    Debug.Log($"[Auto-Fix] Successfully moved Gatherable Supplies to {newPath}");
                }
                else
                {
                    Debug.LogError($"[Auto-Fix] Failed to move Gatherable Supplies: {error}");
                }
            }
        }
    }
}
