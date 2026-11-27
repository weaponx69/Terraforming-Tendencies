using GameDevTV.RTS.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.EditorTools
{
    public static class BakePlanetSurfaceMenu
    {
        const string EasyPlanetPath = "Assets/Resources/Planet 1 - Easy.asset";

        [MenuItem("Terraforming/Bake Planet 1 (Easy) Surface Into Open Scene")]
        public static void BakePlanetOneEasy()
        {
            if (!BakePlanetOneEasyCore(showDialogs: true, out string error))
            {
                if (!string.IsNullOrEmpty(error))
                    EditorUtility.DisplayDialog("Bake planet", error, "OK");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Bake complete",
                    "Planet 1 (Easy) is baked in this scene.\n\n"
                    + "Save the scene (Ctrl+S), then Enter Play — you should see "
                    + "'Baked fast boot' in the Console instead of a full regen.",
                    "OK");
            }
        }

        /// <summary>Unity CLI / agents — no confirmation dialogs.</summary>
        public static bool BakePlanetOneEasyCore(bool showDialogs, out string error)
        {
            error = null;
            var planet = Object.FindAnyObjectByType<PlanetGenerator>();
            if (planet == null)
            {
                error = "No PlanetGenerator in the open scene. Open the main game scene first.";
                return false;
            }

            var config = AssetDatabase.LoadAssetAtPath<PlanetConfig>(EasyPlanetPath);
            if (config == null)
            {
                error = "Missing " + EasyPlanetPath;
                return false;
            }

            if (showDialogs && !EditorUtility.DisplayDialog(
                    "Bake Planet 1 (Easy)",
                    "This regenerates the planet mesh, sector nodes, and ghosts in the OPEN scene, "
                    + "then marks it as the authoritative baked surface for Play Mode.\n\n"
                    + "Save the scene afterward so load stays fast.\n\nContinue?",
                    "Bake",
                    "Cancel"))
            {
                error = "Cancelled.";
                return false;
            }

            Undo.RegisterFullObjectHierarchyUndo(planet.gameObject, "Bake Planet 1 Easy");

            planet.Config = config;
            planet.SceneBakedGeometryRevision = 0;
            planet.GeneratePlanetEditor();

            const int revision = 1;
            planet.SceneBakedGeometryRevision = revision;

            var configSo = new SerializedObject(config);
            configSo.FindProperty("UseAuthoritativeBakedMesh").boolValue = true;
            configSo.FindProperty("BakedGeometryRevision").intValue = revision;
            configSo.ApplyModifiedPropertiesWithoutUndo();

            var planetSo = new SerializedObject(planet);
            planetSo.FindProperty("SceneBakedGeometryRevision").intValue = revision;
            planetSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            return true;
        }
    }
}