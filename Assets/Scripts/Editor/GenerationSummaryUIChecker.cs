#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using GameDevTV.RTS.UI.Containers;

public static class GenerationSummaryUIChecker
{
    [MenuItem("Terraforming/Check Generation Summary UI")]
    public static void CheckUI()
    {
        GenerationSummaryUI ui = Object.FindAnyObjectByType<GenerationSummaryUI>(FindObjectsInactive.Include);
        
        if (ui == null)
        {
            Debug.LogError("[Check UI] GenerationSummaryUI component is completely MISSING from the scene! You need to drag the prefab or add the script to a Canvas.");
            return;
        }

        GameObject root = ui.gameObject;
        
        if (!root.activeInHierarchy)
        {
            Debug.LogWarning($"[Check UI] Found GenerationSummaryUI on '{root.name}', but it is DISABLED in the hierarchy! Checking parents...");
            
            Transform current = root.transform;
            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                {
                    Debug.LogWarning($"[Check UI] -> Parent '{current.name}' is disabled! Enabling it now...");
                    current.gameObject.SetActive(true);
                    EditorUtility.SetDirty(current.gameObject);
                }
                current = current.parent;
            }
        }
        else
        {
            Debug.Log($"[Check UI] GenerationSummaryUI on '{root.name}' is active in hierarchy. Good!");
        }

        // Use reflection to check if the panel is assigned
        var panelField = typeof(GenerationSummaryUI).GetField("panel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (panelField != null)
        {
            GameObject panel = (GameObject)panelField.GetValue(ui);
            if (panel == null)
            {
                Debug.LogError($"[Check UI] The 'panel' reference on GenerationSummaryUI is NULL. You need to assign the child panel in the Inspector!");
            }
            else
            {
                Debug.Log($"[Check UI] Panel is successfully assigned to '{panel.name}'.");
            }
        }
        
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        
        Debug.Log("[Check UI] Check complete! The object has been highlighted in your Hierarchy.");
    }
}
#endif
