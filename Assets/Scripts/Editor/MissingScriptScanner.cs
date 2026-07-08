using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;

namespace GameDevTV.RTS.Editor
{
    [InitializeOnLoad]
    public class MissingScriptScanner
    {
        static MissingScriptScanner()
        {
            EditorApplication.delayCall += ScanAndWriteReport;
        }

        [MenuItem("Tools/Scan Missing Scripts")]
        public static void ScanAndWriteReport()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== MISSING SCRIPTS REPORT ===");
            sb.AppendLine($"Scan Time: {System.DateTime.Now}");
            
            int count = 0;
            var allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            foreach (var go in allObjects)
            {
                if (go == null) continue;
                var components = go.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] == null)
                    {
                        sb.AppendLine($"- GameObject: '{GetGameObjectPath(go)}' has a missing script at index {i}");
                        count++;
                    }
                }
            }
            sb.AppendLine($"Total missing scripts found: {count}");

            var pm = GameObject.Find("PlanetManager");
            if (pm == null) pm = GameObject.Find("Planet Manager");
            if (pm != null)
            {
                sb.AppendLine("\n=== PLANET MANAGER COMPONENTS ===");
                var comps = pm.GetComponents<Component>();
                foreach (var c in comps)
                {
                    if (c == null)
                    {
                        sb.AppendLine("- Missing component (MonoBehaviour)");
                    }
                    else
                    {
                        sb.AppendLine($"- {c.GetType().FullName}");
                    }
                }
            }
            else
            {
                sb.AppendLine("\nCould not find 'PlanetManager' or 'Planet Manager' GameObject in the active scene!");
            }

            string targetPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp/missing_scripts.txt");
            try
            {
                File.WriteAllText(targetPath, sb.ToString());
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[MissingScriptScanner] Failed to write file: " + e.Message);
            }
        }

        private static string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            while (obj.transform.parent != null)
            {
                obj = obj.transform.parent.gameObject;
                path = obj.name + "/" + path;
            }
            return path;
        }
    }
}
