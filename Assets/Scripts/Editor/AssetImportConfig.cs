#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class AssetImportConfig
{
    static AssetImportConfig()
    {
        // Limit the number of parallel asset import workers to prevent Out-Of-Memory (OOM) crashes
        // on systems with many CPU cores but limited RAM.
        int maxWorkers = 4;
        
        // EditorUserSettings.desiredImportWorkerCount controls how many background Unity instances
        // are spawned to import assets. By default, Unity may spawn one for every logical CPU core.
        if (EditorUserSettings.desiredImportWorkerCount > maxWorkers || EditorUserSettings.desiredImportWorkerCount == 0)
        {
            EditorUserSettings.desiredImportWorkerCount = maxWorkers;
            Debug.Log($"[AssetImportConfig] Limited Asset Import Worker Count to {maxWorkers} to prevent OOM crashes.");
        }
    }
}
#endif
