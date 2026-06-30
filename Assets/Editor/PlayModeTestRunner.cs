using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Environment;

namespace Unity.AI.Assistant.PlayModeTest
{
    [InitializeOnLoad]
    internal static class PlayModeTestRunner
    {
        private const string StateKey = "PlayModeTest.State";
        private const string ResultKey = "PlayModeTest.Result";
        private const string ScriptPathKey = "PlayModeTest.ScriptPath";
        private const string SentinelLog = "PLAY_MODE_TEST_COMPLETE";

        private static List<string> _capturedLogs = new List<string>();

        static PlayModeTestRunner()
        {
            string state = SessionState.GetString(StateKey, "Idle");

            switch (state)
            {
                case "WaitingForCompile":
                    Debug.Log("[PlayModeTest] Bootstrap compiled. Scheduling Play Mode entry.");
                    EditorApplication.delayCall += () =>
                    {
                        SessionState.SetString(StateKey, "EnteringPlayMode");
                        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                        EditorApplication.isPlaying = true;
                    };
                    break;

                case "EnteringPlayMode":
                    EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                    if (EditorApplication.isPlaying)
                    {
                        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                        SessionState.SetString(StateKey, "InPlayMode");
                        EditorApplication.update += StartTestCoroutine;
                    }
                    break;

                case "InPlayMode":
                    if (EditorApplication.isPlaying)
                    {
                        EditorApplication.update += StartTestCoroutine;
                    }
                    break;

                case "Done":
                    Debug.Log(SentinelLog);
                    EditorApplication.delayCall += SelfDestruct;
                    break;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                SessionState.SetString(StateKey, "InPlayMode");
                EditorApplication.update += StartTestCoroutine;
            }
        }

        private static bool _started = false;

        private static void StartTestCoroutine()
        {
            if (_started) return;
            _started = true;
            EditorApplication.update -= StartTestCoroutine;

            // Run test logic via a MonoBehaviour helper to support coroutines/waiting
            var go = new GameObject("TestHelper");
            var helper = go.AddComponent<TestHelperMono>();
            helper.StartCoroutine(helper.RunTest(OnTestComplete));
        }

        private static void OnTestComplete(string resultJson)
        {
            SessionState.SetString(ResultKey, resultJson);
            SessionState.SetString(StateKey, "Done");
            EditorApplication.isPlaying = false;
        }

        private static void SelfDestruct()
        {
            string scriptPath = SessionState.GetString(ScriptPathKey, "");
            if (!string.IsNullOrEmpty(scriptPath) && AssetDatabase.AssetPathExists(scriptPath))
            {
                AssetDatabase.DeleteAsset(scriptPath);
            }
            SessionState.EraseString(StateKey);
            SessionState.EraseString(ScriptPathKey);
        }

        private static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            if (_capturedLogs.Count >= 150) return;
            _capturedLogs.Add("[" + type + "] " + message);
        }

        [System.Serializable]
        private class TestResult
        {
            public bool success;
            public string error;
            public string[] logs;
        }

        internal class TestHelperMono : MonoBehaviour
        {
            public IEnumerator RunTest(System.Action<string> callback)
            {
                Application.logMessageReceived += OnLogMessage;
                Debug.Log("[Test] Waiting 15 frames for initialization...");
                for (int i = 0; i < 15; i++) yield return null;

                Debug.Log("[Test] Placing Command Post...");
                var generator = Object.FindAnyObjectByType<PlanetGenerator>();
                if (generator == null)
                {
                    callback(JsonUtility.ToJson(new TestResult { success = false, error = "PlanetGenerator not found!" }));
                    yield break;
                }

                var worker = Object.FindAnyObjectByType<Worker>();
                AbstractCommandable commandable = worker;
                if (commandable == null)
                {
                    commandable = Object.FindAnyObjectByType<GlobalCommander>();
                }

                if (commandable == null)
                {
                    callback(JsonUtility.ToJson(new TestResult { success = false, error = "No Worker or GlobalCommander found!" }));
                    yield break;
                }

                string buildingPath = "Assets/Resources/Buildings/Command Post/Command Post.asset";
                var buildingSO = AssetDatabase.LoadAssetAtPath<BuildingSO>(buildingPath);
                if (buildingSO == null)
                {
                    callback(JsonUtility.ToJson(new TestResult { success = false, error = "Command Post BuildingSO not found at: " + buildingPath }));
                    yield break;
                }

                var buildCommand = ScriptableObject.CreateInstance<BuildBuildingCommand>();
                var field = typeof(BuildBuildingCommand).GetField("Building", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(buildCommand, buildingSO);
                }

                RaycastHit hit = new RaycastHit();
                hit.point = new Vector3(15f, 0f, 15f);
                CommandContext context = new CommandContext(commandable, hit, 0);

                buildCommand.Handle(context);

                Debug.Log("[Test] Placed building. Monitoring for 3 seconds...");
                for (int step = 0; step < 6; step++)
                {
                    var buildings = Object.FindObjectsByType<BaseBuilding>(FindObjectsInactive.Include);
                    Debug.Log($"[Test] Time={Time.time:F2}s | Active Buildings={buildings.Length}");
                    foreach (var b in buildings)
                    {
                        Debug.Log($"  - Building: {b.name} | HP={b.CurrentHealth}/{b.MaxHealth} | State={b.Progress.State} | IsOperating={b.IsOperating}");
                    }
                    yield return new WaitForSeconds(0.5f);
                }

                Application.logMessageReceived -= OnLogMessage;
                Destroy(gameObject);

                callback(JsonUtility.ToJson(new TestResult
                {
                    success = true,
                    logs = _capturedLogs.ToArray()
                }));
            }
        }
    }
}