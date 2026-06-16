using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using GameDevTV.RTS.UI.Containers;

namespace GameDevTV.RTS.Editor
{
    public static class GenerationSummaryUIChecker
    {
        [MenuItem("Tools/Diagnostics/Check Generation Summary UI")]
        public static void CheckSummaryUI()
        {
            Debug.Log("[SummaryUI Checker] Starting diagnostic checks...");

            Scene activeScene = SceneManager.GetActiveScene();
            GameObject[] rootObjects = activeScene.GetRootGameObjects();
            List<GenerationSummaryUI> foundComponents = new List<GenerationSummaryUI>();

            foreach (var root in rootObjects)
            {
                var comps = root.GetComponentsInChildren<GenerationSummaryUI>(true);
                foundComponents.AddRange(comps);
            }

            if (foundComponents.Count == 0)
            {
                Debug.LogError("[SummaryUI Checker] Could not find any GenerationSummaryUI component in the active scene!");
                EditorUtility.DisplayDialog(
                    "Diagnostic Failed", 
                    "Could not find any GenerationSummaryUI component in the active scene. Please open the correct scene.", 
                    "OK"
                );
                return;
            }

            int fixedCount = 0;
            string report = "Diagnostic Results:\n\n";

            foreach (var ui in foundComponents)
            {
                GameObject go = ui.gameObject;
                report += $"Found GenerationSummaryUI on GameObject: '{go.name}'\n";

                // Check and fix the GameObject itself
                bool goWasDisabled = !go.activeSelf;
                if (goWasDisabled)
                {
                    report += $"- GameObject '{go.name}' itself is DISABLED. (This prevents OnEnable from running and subscribing to events!)\n";
                    Undo.RecordObject(go, "Enable GenerationSummaryUI GameObject");
                    go.SetActive(true);
                    fixedCount++;
                }
                else
                {
                    report += $"- GameObject '{go.name}' itself is active.\n";
                }

                // Check and fix parent GameObjects
                List<string> disabledParents = new List<string>();
                Transform current = go.transform.parent;
                while (current != null)
                {
                    if (!current.gameObject.activeSelf)
                    {
                        disabledParents.Add(current.name);
                        Undo.RecordObject(current.gameObject, $"Enable Parent GameObject {current.name}");
                        current.gameObject.SetActive(true);
                        fixedCount++;
                    }
                    current = current.parent;
                }

                if (disabledParents.Count > 0)
                {
                    report += $"- Disabled parents found and enabled: {string.Join(", ", disabledParents)}\n";
                }
                else
                {
                    report += "- All parent GameObjects are active.\n";
                }

                // Check 'panel' field using Serialized Properties (Inspector configuration)
                SerializedObject so = new SerializedObject(ui);
                SerializedProperty panelProp = so.FindProperty("panel");
                bool isPanelNull = false;

                if (panelProp != null)
                {
                    if (panelProp.objectReferenceValue == null)
                    {
                        report += "- WARNING: 'panel' field is set to 'None' (Null) in the Inspector!\n";
                        isPanelNull = true;
                    }
                    else
                    {
                        report += $"- 'panel' field is correctly set to '{panelProp.objectReferenceValue.name}' in the Inspector.\n";
                    }
                }
                else
                {
                    report += "- ERROR: 'panel' property could not be found via SerializedObject.\n";
                }

                // Check 'panel' field using Reflection
                var field = typeof(GenerationSummaryUI).GetField("panel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var reflectionValue = field.GetValue(ui) as GameObject;
                    if (reflectionValue == null)
                    {
                        report += "- WARNING (Reflection Check): Private field 'panel' is NULL.\n";
                    }
                    else
                    {
                        report += $"- (Reflection Check): Private field 'panel' is assigned to '{reflectionValue.name}'.\n";
                    }
                }
                else
                {
                    report += "- WARNING: Could not access private 'panel' field via reflection.\n";
                }

                // Highlight/Ping the GameObject in Hierarchy
                Selection.activeGameObject = go;
                EditorGUIUtility.PingObject(go);

                report += "\nActions Taken:\n";
                if (goWasDisabled || disabledParents.Count > 0)
                {
                    report += $"- Enabled the GameObject '{go.name}' and its disabled parents so OnEnable can run and subscribe to events.\n";
                }
                report += $"- Selected and highlighted '{go.name}' in the Hierarchy.\n";
                if (isPanelNull)
                {
                    report += "- [IMPORTANT] Please assign the 'panel' GameObject in the Inspector so the UI can activate properly.\n";
                }
            }

            Debug.Log($"[SummaryUI Checker] Finished diagnostic checks.\n{report}");

            EditorUtility.DisplayDialog(
                "Generation Summary UI Diagnostic",
                report,
                "OK"
            );
        }
    }
}

