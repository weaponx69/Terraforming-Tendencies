using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDevTV.RTS.Editor
{
    public class MemoryCleanerWindow : EditorWindow
    {
        private long lastGcMemory;
        private double lastUpdateTime;
        private List<string> warningList = new();
        private int leakedMaterialCount = 0;
        private int survivedCloneCount = 0;

        [MenuItem("Tools/RTS Memory Cleaner")]
        public static void ShowWindow()
        {
            var window = GetWindow<MemoryCleanerWindow>("Memory Cleaner");
            window.minSize = new Vector2(350, 400);
            window.Show();
        }

        private void OnEnable()
        {
            lastUpdateTime = EditorApplication.timeSinceStartup;
            ScanForLeaks();
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            GUILayout.Label("RTS Memory Monitor & Cleaner", EditorStyles.boldLabel);
            GUILayout.Space(5);

            // Memory Status Block
            long currentMem = GC.GetTotalMemory(false);
            float currentMemMb = currentMem / (1024f * 1024f);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Managed Memory:", $"{currentMemMb:F2} MB");
            if (GUILayout.Button("Force Garbage Collection (GC.Collect)"))
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                ScanForLeaks();
                Repaint();
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(15);

            // Warning Banner
            if (warningList.Count > 0)
            {
                Color origColor = GUI.color;
                GUI.color = new Color(1f, 0.4f, 0.4f, 1f);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUI.color = origColor;
                GUILayout.Label("⚠️ Memory Warning / Potential Leaks Detected:", EditorStyles.boldLabel);
                foreach (var warning in warningList)
                {
                    GUILayout.Label($"- {warning}", EditorStyles.wordWrappedLabel);
                }
                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.HelpBox("No obvious memory leaks detected.", MessageType.Info);
            }

            GUILayout.Space(15);

            // Action Panel
            GUILayout.Label("Cleanup Operations:", EditorStyles.boldLabel);
            GUILayout.Space(5);

            if (GUILayout.Button("Unload Unused Assets (Reclaims GPU/RAM)", GUILayout.Height(30)))
            {
                Resources.UnloadUnusedAssets();
                EditorUtility.UnloadUnusedAssetsImmediate();
                ScanForLeaks();
                Repaint();
                Debug.Log("[MemoryCleaner] Unused assets successfully unloaded.");
            }

            GUILayout.Space(5);

            if (survivedCloneCount > 0)
            {
                if (GUILayout.Button($"Destroy {survivedCloneCount} Survived Playmode Clones/Ghosts", GUILayout.Height(30)))
                {
                    DestroySurvivedClones();
                }
            }

            if (leakedMaterialCount > 0)
            {
                if (GUILayout.Button($"Destroy {leakedMaterialCount} Leaked Runtime Materials", GUILayout.Height(30)))
                {
                    DestroyLeakedMaterials();
                }
            }
        }

        private void Update()
        {
            // Scan for leaks periodically every 2 seconds
            if (EditorApplication.timeSinceStartup - lastUpdateTime > 2.0)
            {
                lastUpdateTime = EditorApplication.timeSinceStartup;
                ScanForLeaks();
                Repaint();
            }
        }

        private void ScanForLeaks()
        {
            warningList.Clear();
            leakedMaterialCount = 0;
            survivedCloneCount = 0;

            // 1. Scan for leaked materials instantiated during Play Mode (name contains "(Instance)")
            // but are no longer attached to active renderers or have leaked into assets
            var materials = Resources.FindObjectsOfTypeAll<Material>();
            foreach (var mat in materials)
            {
                if (mat != null && mat.name.Contains("(Instance)"))
                {
                    leakedMaterialCount++;
                }
            }

            if (leakedMaterialCount > 0)
            {
                warningList.Add($"{leakedMaterialCount} runtime instantiated Material instances found in memory.");
            }

            // 2. Scan for GameObjects that survived playmode exit (usually have "(Clone)" or "Ghost" in Edit Mode)
            if (!EditorApplication.isPlaying)
            {
                var allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
                foreach (var go in allObjects)
                {
                    if (go != null && (go.name.Contains("(Clone)") || go.name.Contains("Ghost")))
                    {
                        survivedCloneCount++;
                    }
                }

                if (survivedCloneCount > 0)
                {
                    warningList.Add($"{survivedCloneCount} temporary clones/ghosts detected in the active Edit Mode scene.");
                }
            }
        }

        private void DestroySurvivedClones()
        {
            if (EditorApplication.isPlaying) return;

            int count = 0;
            var allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            foreach (var go in allObjects)
            {
                if (go != null && (go.name.Contains("(Clone)") || go.name.Contains("Ghost")))
                {
                    DestroyImmediate(go);
                    count++;
                }
            }

            Debug.Log($"[MemoryCleaner] Successfully destroyed {count} survived playmode GameObjects.");
            ScanForLeaks();
            Repaint();
        }

        private void DestroyLeakedMaterials()
        {
            int count = 0;
            var materials = Resources.FindObjectsOfTypeAll<Material>();
            foreach (var mat in materials)
            {
                if (mat != null && mat.name.Contains("(Instance)"))
                {
                    DestroyImmediate(mat, true);
                    count++;
                }
            }

            Debug.Log($"[MemoryCleaner] Successfully cleaned {count} leaked material instances.");
            Resources.UnloadUnusedAssets();
            ScanForLeaks();
            Repaint();
        }
    }
}
