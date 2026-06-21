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

        private static readonly int WaitFrames = SessionState.GetInt("PlayModeTest.WaitFrames", 60); // Wait 1 second (60 frames) to let power stabilize

        private static List<string> _capturedLogs = new List<string>();
        private const int MaxCapturedLogs = 100;

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
            Debug.Log("[Test] Running state inspection of active buildings...");

            // Find all active MonoBehaviours in the scene
            var allMono = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            
            foreach (var mono in allMono)
            {
                if (mono == null) continue;
                string typeName = mono.GetType().Name;
                
                // Let's locate the Command Post or any BaseBuilding
                if (typeName == "BaseBuilding")
                {
                    GameObject buildingGo = mono.gameObject;
                    string name = buildingGo.name;
                    
                    var progressField = mono.GetType().GetProperty("Progress", BindingFlags.Instance | BindingFlags.Public);
                    var stateStr = "Unknown";
                    if (progressField != null)
                    {
                        var progressValue = progressField.GetValue(mono);
                        if (progressValue != null)
                        {
                            var stateField = progressValue.GetType().GetProperty("State", BindingFlags.Instance | BindingFlags.Public);
                            if (stateField != null)
                            {
                                stateStr = stateField.GetValue(progressValue).ToString();
                            }
                        }
                    }

                    var buildingSoProp = mono.GetType().GetProperty("BuildingSO", BindingFlags.Instance | BindingFlags.Public);
                    string buildingName = "None";
                    if (buildingSoProp != null)
                    {
                        var so = buildingSoProp.GetValue(mono);
                        if (so != null)
                        {
                            var nameProp = so.GetType().GetProperty("Name", BindingFlags.Instance | BindingFlags.Public);
                            if (nameProp != null)
                            {
                                buildingName = nameProp.GetValue(so).ToString();
                            }
                        }
                    }

                    Debug.Log("[Test] Building: '" + name + "' | SO Name: '" + buildingName + "' | State: " + stateStr);

                    // Inspect PowerNode
                    var powerNodeComp = buildingGo.GetComponent("PowerNode");
                    if (powerNodeComp != null)
                    {
                        var isGridPoweredProp = powerNodeComp.GetType().GetProperty("IsGridPowered", BindingFlags.Instance | BindingFlags.Public);
                        var isPoweredProp = powerNodeComp.GetType().GetProperty("IsPowered", BindingFlags.Instance | BindingFlags.Public);
                        var connectedNodesField = powerNodeComp.GetType().GetField("ConnectedNodes", BindingFlags.Instance | BindingFlags.Public);

                        bool isGridPowered = isGridPoweredProp != null ? (bool)isGridPoweredProp.GetValue(powerNodeComp) : false;
                        bool isPowered = isPoweredProp != null ? (bool)isPoweredProp.GetValue(powerNodeComp) : false;
                        
                        int connectedCount = 0;
                        if (connectedNodesField != null)
                        {
                            var list = connectedNodesField.GetValue(powerNodeComp) as System.Collections.IList;
                            if (list != null) connectedCount = list.Count;
                        }

                        Debug.Log("[Test]   PowerNode exists! IsGridPowered: " + isGridPowered + " | IsPowered: " + isPowered + " | Connected Count: " + connectedCount);
                    }
                    else
                    {
                        Debug.Log("[Test]   PowerNode does not exist!");
                    }

                    // Inspect UnpoweredIndicator
                    var indicatorComp = buildingGo.GetComponent("UnpoweredIndicator");
                    if (indicatorComp != null)
                    {
                        var visualIndicatorField = indicatorComp.GetType().GetField("visualIndicator", BindingFlags.Instance | BindingFlags.NonPublic);
                        GameObject visualIndicator = visualIndicatorField != null ? visualIndicatorField.GetValue(indicatorComp) as GameObject : null;
                        
                        bool isIndicatorActive = visualIndicator != null && visualIndicator.activeSelf;
                        Debug.Log("[Test]   UnpoweredIndicator exists! Visual GameObject active: " + isIndicatorActive);
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