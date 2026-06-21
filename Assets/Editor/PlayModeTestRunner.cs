using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Unity.AI.Assistant.PlayModeTest
{
    [InitializeOnLoad]
    internal static class PlayModeTestRunner
    {
        private const string StateKey = "PlayModeTest.State";
        private const string ResultKey = "PlayModeTest.Result";
        private const string ScriptPathKey = "PlayModeTest.ScriptPath";
        private const string SentinelLog = "PLAY_MODE_TEST_COMPLETE";

        private static readonly int WaitFrames = SessionState.GetInt("PlayModeTest.WaitFrames", 50); // wait some frames for scene load/init

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
            public string details;
        }

        private static string RunTestLogic()
        {
            // Find component via string/reflection
            System.Type cmType = System.Type.GetType("GameDevTV.RTS.Player.ColonistManager, Assembly-CSharp");
            if (cmType == null)
            {
                return JsonUtility.ToJson(new TestResult
                {
                    success = false,
                    error = "Could not load GameDevTV.RTS.Player.ColonistManager type via reflection",
                    logs = _capturedLogs.ToArray()
                });
            }

            var cm = Object.FindAnyObjectByType(cmType) as MonoBehaviour;
            if (cm == null)
            {
                return JsonUtility.ToJson(new TestResult
                {
                    success = false,
                    error = "ColonistManager instance not found in scene",
                    logs = _capturedLogs.ToArray()
                });
            }

            string details = "ColonistManager found. IsEnabled: " + cm.enabled + "\n";
            
            // Check its fields via reflection to see values
            var nextArrivalTimeField = cmType.GetField("nextArrivalTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var isWarningActiveField = cmType.GetField("isWarningActive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var currentWaveSizeField = cmType.GetField("currentWaveSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (nextArrivalTimeField != null)
                details += "nextArrivalTime: " + nextArrivalTimeField.GetValue(cm) + "\n";
            if (isWarningActiveField != null)
                details += "isWarningActive: " + isWarningActiveField.GetValue(cm) + "\n";
            if (currentWaveSizeField != null)
                details += "currentWaveSize: " + currentWaveSizeField.GetValue(cm) + "\n";

            // Check unlocked buildings via reflection
            System.Type bdmType = System.Type.GetType("GameDevTV.RTS.Player.BlueprintDraftManager, Assembly-CSharp");
            if (bdmType != null)
            {
                var getUnlockedMethod = bdmType.GetMethod("GetUnlockedBuildingNames", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (getUnlockedMethod != null)
                {
                    var unlocked = getUnlockedMethod.Invoke(null, null) as IEnumerable<string>;
                    if (unlocked != null)
                    {
                        details += "Unlocked buildings: " + string.Join(", ", unlocked) + "\n";
                    }
                }
            }

            // Check if housing unlocked
            var methodHasUnlockedHousing = cmType.GetMethod("HasUnlockedHousing", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (methodHasUnlockedHousing != null)
            {
                details += "HasUnlockedHousing(): " + methodHasUnlockedHousing.Invoke(cm, null) + "\n";
            }

            // Check if spaceport built
            var methodHasBuiltSpaceport = cmType.GetMethod("HasBuiltSpaceport", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (methodHasBuiltSpaceport != null)
            {
                details += "HasBuiltSpaceport(): " + methodHasBuiltSpaceport.Invoke(cm, null) + "\n";
            }

            return JsonUtility.ToJson(new TestResult
            {
                success = true,
                logs = _capturedLogs.ToArray(),
                details = details
            });
        }
    }
}