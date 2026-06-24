using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

namespace Unity.AI.Assistant.PlayModeTest
{
    [InitializeOnLoad]
    internal static class PlayModeTestRunner
    {
        private const string StateKey = "PlayModeTest.State";
        private const string ResultKey = "PlayModeTest.Result";
        private const string ScriptPathKey = "PlayModeTest.ScriptPath";
        private const string SentinelLog = "PLAY_MODE_TEST_COMPLETE";

        private static readonly int WaitFrames = SessionState.GetInt("PlayModeTest.WaitFrames", 10);

        private static List<string> _capturedLogs = new List<string>();
        private const int MaxCapturedLogs = 50;

        static PlayModeTestRunner()
        {
            string state = SessionState.GetString(StateKey, "Idle");

            switch (state)
            {
                case "Idle":
                    break;

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
                        EditorApplication.update += WaitFramesThenRun;
                    }
                    break;

                case "InPlayMode":
                    if (EditorApplication.isPlaying)
                    {
                        EditorApplication.update += WaitFramesThenRun;
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
                EditorApplication.update += WaitFramesThenRun;
            }
        }

        private static int _frameCount = 0;
        private static bool _hasRun = false;

        private static void WaitFramesThenRun()
        {
            _frameCount++;
            if (_frameCount < WaitFrames) return;

            if (_hasRun) return;
            _hasRun = true;
            EditorApplication.update -= WaitFramesThenRun;

            Application.logMessageReceived += OnLogMessage;
            string resultJson;
            try
            {
                resultJson = RunTestLogic();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[PlayModeTest] Test threw exception: " + e);
                resultJson = JsonUtility.ToJson(new TestResult
                {
                    success = false,
                    error = e.Message,
                    logs = _capturedLogs.ToArray()
                });
            }
            finally
            {
                Application.logMessageReceived -= OnLogMessage;
            }

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
            if (_capturedLogs.Count >= MaxCapturedLogs) return;
            _capturedLogs.Add("[" + type + "] " + message);
        }

        [System.Serializable]
        private class TestResult
        {
            public bool success;
            public string error;
            public string[] logs;
        }

        private static string RunTestLogic()
        {
            Debug.Log("[Test] Running Worker commands inspection test...");
            
            // Log Unlocked Buildings at start of play
            var draftManagerType = System.Type.GetType("GameDevTV.RTS.Player.BlueprintDraftManager, MainGame");
            if (draftManagerType != null)
            {
                var getUnlockedMethod = draftManagerType.GetMethod("GetUnlockedBuildingNames", BindingFlags.Public | BindingFlags.Static);
                if (getUnlockedMethod != null)
                {
                    var unlocked = (HashSet<string>)getUnlockedMethod.Invoke(null, null);
                    Debug.Log("[Test] Unlocked buildings in BlueprintDraftManager: " + string.Join(", ", unlocked));
                }
            }

            // Find Worker gameobjects
            GameObject[] workers = GameObject.FindGameObjectsWithTag("Worker");
            if (workers == null || workers.Length == 0)
            {
                // Fallback search by name/components
                var allGos = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                var workerList = new List<GameObject>();
                foreach (var go in allGos)
                {
                    if (go.name.ToLower().Contains("worker") || go.name.ToLower().Contains("drone") || go.GetComponent("Worker") != null)
                    {
                        workerList.Add(go);
                    }
                }
                workers = workerList.ToArray();
            }

            Debug.Log("[Test] Found " + workers.Length + " worker/drone objects.");
            foreach (var w in workers)
            {
                Debug.Log("[Test] Worker GO: " + w.name);
                var workerComp = w.GetComponent("Worker");
                if (workerComp != null)
                {
                    var availableCmdsProp = workerComp.GetType().GetProperty("AvailableCommands", BindingFlags.Public | BindingFlags.Instance);
                    if (availableCmdsProp != null)
                    {
                        var cmds = (object[])availableCmdsProp.GetValue(workerComp);
                        Debug.Log("[Test] - Number of available commands: " + (cmds != null ? cmds.Length : 0));
                        if (cmds != null)
                        {
                            foreach (var cmd in cmds)
                            {
                                if (cmd != null)
                                {
                                    var nameProp = cmd.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                                    string cmdName = nameProp != null ? (string)nameProp.GetValue(cmd) : "Unnamed";
                                    
                                    var buildingProp = cmd.GetType().GetProperty("Building", BindingFlags.Public | BindingFlags.Instance);
                                    string bldStr = "";
                                    if (buildingProp != null)
                                    {
                                        var bld = buildingProp.GetValue(cmd);
                                        if (bld != null)
                                        {
                                            var bldNameProp = bld.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                                            bldStr = " (Building: " + (bldNameProp != null ? bldNameProp.GetValue(bld) : "Unnamed") + ")";
                                        }
                                    }

                                    Debug.Log("[Test]   -> Command: " + cmdName + bldStr + " [Type: " + cmd.GetType().Name + "]");
                                }
                            }
                        }
                    }
                }
            }

            return JsonUtility.ToJson(new TestResult
            {
                success = true,
                logs = _capturedLogs.ToArray()
            });
        }
    }
}