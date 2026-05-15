using UnityEditor;
using UnityEngine;
using System.IO;
using GameDevTV.RTS.Environment.Procedural;

namespace GameDevTV.RTS.Environment.Editor
{
    public class ProceduralAssetGeneratorWindow : EditorWindow
    {
        public enum AssetType { Rock, Crystal }
        
        [Header("Generation Settings")]
        public AssetType type = AssetType.Rock;
        public string assetName = "ProceduralAsset";
        public int seed = 12345;

        [Header("Rock Settings")]
        public int rockSubdivisions = 3;
        public float rockRadius = 1f;
        public float rockNoiseScale = 2f;
        public float rockNoiseStrength = 0.3f;
        public bool flattenBottom = true;

        [Header("Crystal Settings")]
        public int crystalSides = 6;
        public float crystalHeight = 2f;
        public float crystalRadius = 0.5f;

        [MenuItem("Tools/Procedural Asset Generator")]
        public static void ShowWindow()
        {
            GetWindow<ProceduralAssetGeneratorWindow>("Procedural Assets");
        }

        private void OnGUI()
        {
            GUILayout.Label("Procedural Land Feature Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            type = (AssetType)EditorGUILayout.EnumPopup("Asset Type", type);
            assetName = EditorGUILayout.TextField("Base Name", assetName);
            
            GUILayout.BeginHorizontal();
            seed = EditorGUILayout.IntField("Seed", seed);
            if (GUILayout.Button("Random", GUILayout.Width(60)))
            {
                seed = Random.Range(0, 100000);
            }
            GUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (type == AssetType.Rock)
            {
                GUILayout.Label("Rock Parameters", EditorStyles.boldLabel);
                rockSubdivisions = EditorGUILayout.IntSlider("Subdivisions", rockSubdivisions, 1, 6);
                rockRadius = EditorGUILayout.Slider("Base Radius", rockRadius, 0.1f, 10f);
                rockNoiseScale = EditorGUILayout.Slider("Noise Scale", rockNoiseScale, 0.1f, 10f);
                rockNoiseStrength = EditorGUILayout.Slider("Noise Strength", rockNoiseStrength, 0f, 5f);
                flattenBottom = EditorGUILayout.Toggle("Flatten Bottom", flattenBottom);
            }
            else if (type == AssetType.Crystal)
            {
                GUILayout.Label("Crystal Parameters", EditorStyles.boldLabel);
                crystalSides = EditorGUILayout.IntSlider("Sides", crystalSides, 3, 12);
                crystalHeight = EditorGUILayout.Slider("Height", crystalHeight, 0.5f, 10f);
                crystalRadius = EditorGUILayout.Slider("Radius", crystalRadius, 0.1f, 5f);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Generate and Save Asset", GUILayout.Height(40)))
            {
                GenerateAsset();
            }
        }

        private void GenerateAsset()
        {
            string meshFolder = "Assets/ProceduralAssets/Meshes";
            string prefabFolder = "Assets/ProceduralAssets/Prefabs";

            if (!AssetDatabase.IsValidFolder("Assets/ProceduralAssets"))
                AssetDatabase.CreateFolder("Assets", "ProceduralAssets");
            if (!AssetDatabase.IsValidFolder(meshFolder))
                AssetDatabase.CreateFolder("Assets/ProceduralAssets", "Meshes");
            if (!AssetDatabase.IsValidFolder(prefabFolder))
                AssetDatabase.CreateFolder("Assets/ProceduralAssets", "Prefabs");

            Mesh generatedMesh = null;

            if (type == AssetType.Rock)
            {
                generatedMesh = ProceduralMeshUtils.GenerateIcoSphere(rockSubdivisions, rockRadius);
                ProceduralMeshUtils.ApplyNoise(generatedMesh, rockNoiseScale, rockNoiseStrength, seed, flattenBottom);
            }
            else if (type == AssetType.Crystal)
            {
                generatedMesh = ProceduralMeshUtils.GenerateCrystal(crystalSides, crystalHeight, crystalRadius);
            }

            if (generatedMesh == null) return;

            string fileName = $"{assetName}_{type}_{seed}";
            string meshPath = $"{meshFolder}/{fileName}.asset";

            // Save Mesh
            AssetDatabase.CreateAsset(generatedMesh, meshPath);
            AssetDatabase.SaveAssets();

            // Create Prefab
            GameObject tempObj = new GameObject(fileName);
            MeshFilter mf = tempObj.AddComponent<MeshFilter>();
            mf.sharedMesh = generatedMesh;

            MeshRenderer mr = tempObj.AddComponent<MeshRenderer>();
            Material defaultMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
            
            // Try to find URP Lit, if we want to use the default URP mat
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader != null)
            {
                Material urpMat = new Material(urpShader);
                urpMat.SetFloat("_Smoothness", type == AssetType.Crystal ? 0.8f : 0.1f);
                
                string matFolder = "Assets/ProceduralAssets/Materials";
                if (!AssetDatabase.IsValidFolder(matFolder))
                    AssetDatabase.CreateFolder("Assets/ProceduralAssets", "Materials");
                    
                string matPath = $"{matFolder}/{type}Mat.mat";
                Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                
                if (existingMat == null)
                {
                    AssetDatabase.CreateAsset(urpMat, matPath);
                    existingMat = urpMat;
                }
                mr.sharedMaterial = existingMat;
            }
            else
            {
                mr.sharedMaterial = defaultMat;
            }

            MeshCollider mc = tempObj.AddComponent<MeshCollider>();
            mc.sharedMesh = generatedMesh;

            string prefabPath = $"{prefabFolder}/{fileName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(tempObj, prefabPath);

            DestroyImmediate(tempObj);

            Debug.Log($"Procedural Asset created at {prefabPath}");
            
            // Ping the prefab in the project window
            GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            EditorGUIUtility.PingObject(savedPrefab);
        }
    }
}
