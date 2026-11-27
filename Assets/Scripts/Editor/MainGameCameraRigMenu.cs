using System;
using System.Linq;
using System.Reflection;
using GameDevTV.RTS.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.EditorTools
{
    public static class MainGameCameraRigMenu
    {
        const string CinemachineBrain = "Unity.Cinemachine.CinemachineBrain";
        const string CinemachineCamera = "Unity.Cinemachine.CinemachineCamera";

        [MenuItem("Terraforming/Repair MainGame Camera Rig (PlayerInput + Cinemachine)")]
        public static void RepairMainGameCameraRig()
        {
            var cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                EditorUtility.DisplayDialog("Repair camera rig", "No Camera in the open scene.", "OK");
                return;
            }

            if (!cam.CompareTag("MainCamera"))
                cam.tag = "MainCamera";

            if (cam.GetComponent<PlayerInput>() == null)
                Undo.AddComponent<PlayerInput>(cam.gameObject);

            EnsureComponentByFullName(cam.gameObject, CinemachineBrain);

            var vcamGo = GameObject.Find("MainGame Virtual Camera");
            if (vcamGo == null)
            {
                vcamGo = new GameObject("MainGame Virtual Camera");
                Undo.RegisterCreatedObjectUndo(vcamGo, "Create MainGame Virtual Camera");
                if (cam.transform.parent != null)
                    Undo.SetTransformParent(vcamGo.transform, cam.transform.parent, "Parent vcam");
            }

            EnsureComponentByFullName(vcamGo, CinemachineCamera);
            EnsureComponentByFullName(vcamGo, "Unity.Cinemachine.CinemachineFollow");
            EnsureComponentByFullName(vcamGo, "Unity.Cinemachine.CinemachineHardLookAt");

            RemoveComponentByFullName(cam.gameObject, CinemachineCamera);
            RemoveComponentByFullName(cam.gameObject, "Unity.Cinemachine.CinemachineFollow");
            RemoveComponentByFullName(cam.gameObject, "Unity.Cinemachine.CinemachineHardLookAt");

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog(
                "Camera rig repaired",
                "Added PlayerInput / Cinemachine on Main Camera.\n\nSave MainGame.unity (Ctrl+S) so Play skips runtime repair.",
                "OK");
        }

        static void EnsureComponentByFullName(GameObject host, string fullName)
        {
            if (host == null || string.IsNullOrEmpty(fullName)) return;

            foreach (var c in host.GetComponents<Component>())
            {
                if (c != null && c.GetType().FullName == fullName)
                    return;
            }

            Type type = Type.GetType($"{fullName}, Unity.Cinemachine");
            if (type == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type found = null;
                    try
                    {
                        found = asm.GetType(fullName, throwOnError: false);
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        found = ex.Types?.FirstOrDefault(t => t != null && t.FullName == fullName);
                    }

                    if (found != null)
                    {
                        type = found;
                        break;
                    }
                }
            }

            if (type == null)
            {
                Debug.LogWarning($"[MainGameCameraRigMenu] Could not resolve {fullName} — install Cinemachine or add it manually.");
                return;
            }

            Undo.AddComponent(host, type);
        }

        static void RemoveComponentByFullName(GameObject host, string fullName)
        {
            if (host == null) return;
            foreach (var c in host.GetComponents<Component>())
            {
                if (c == null || c.GetType().FullName != fullName) continue;
                Undo.DestroyObjectImmediate(c);
            }
        }
    }
}