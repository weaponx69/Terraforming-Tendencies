using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using System.Linq;
using GameDevTV.RTS.Units;
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

        private static readonly int WaitFrames = SessionState.GetInt("PlayModeTest.WaitFrames", 5);
        private static readonly float TestTimeout = 15.0f;

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
        private static bool _setupDone = false;
        private static bool _testDone = false;
        private static double _testStartTime = 0;

        private static SectorManager.Sector _targetSector;
        private static Vector3 _targetPosition;

        private static void WaitFramesThenRun()
        {
            _frameCount++;
            if (_frameCount < WaitFrames) return;

            if (_testDone) return;

            if (!_setupDone)
            {
                _setupDone = true;
                Application.logMessageReceived += OnLogMessage;
                _testStartTime = EditorApplication.timeSinceStartup;
                try
                {
                    Setup();
                }
                catch (System.Exception e)
                {
                    Debug.LogError("[Test] Setup exception: " + e);
                    FinishTest(true, e.Message);
                    return;
                }
                return;
            }

            float elapsed = (float)(EditorApplication.timeSinceStartup - _testStartTime);
            bool timedOut = elapsed >= TestTimeout;

            try
            {
                bool complete = Tick(elapsed);
                if (complete || timedOut)
                {
                    FinishTest(timedOut && !complete, timedOut ? "Test timed out" : null);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Test] Tick exception: " + e);
                FinishTest(true, e.Message);
            }
        }

        private static void Setup()
        {
            Debug.Log("[Test] Setup starting...");
            BaseBuilding.ActiveBuildings.Clear();

            // Supplies
            GameObject suppliesObj = new GameObject("Supplies");
            suppliesObj.AddComponent<Supplies>();
            Supplies.Biomass[Owner.Player1] = 1000;

            // SectorManager
            GameObject sectorManagerObj = new GameObject("SectorManager");
            var sectorManager = sectorManagerObj.AddComponent<SectorManager>();
            
            _targetPosition = new Vector3(20f, 0f, 0f);
            _targetSector = new SectorManager.Sector { Center = _targetPosition, IsOccupied = false };
            sectorManager.Sectors.Add(new SectorManager.Sector { Center = Vector3.zero, IsOccupied = false });
            sectorManager.Sectors.Add(_targetSector);

            // ColonyExpansionManager
            GameObject colonyExpansionManagerObj = new GameObject("ColonyExpansionManager");
            var colonyExpansionManager = colonyExpansionManagerObj.AddComponent<ColonyExpansionManager>();

            var ghostPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Units/Buildings/Command Post/Command Post Ghost Variant.prefab");
            var realPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Units/Buildings/Command Post/Command Post.prefab");
            
            var type = typeof(ColonyExpansionManager);
            type.GetField("ghostPrefab", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(colonyExpansionManager, ghostPrefab);
            type.GetField("realPrefab", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(colonyExpansionManager, realPrefab);

            // Starting Building - Use Prefab instead of manual construction
            GameObject baseBuildingObj = Object.Instantiate(realPrefab, Vector3.zero, Quaternion.identity);
            baseBuildingObj.name = "Initial Command Post";
            var baseBuilding = baseBuildingObj.GetComponent<BaseBuilding>();

            baseBuilding.Owner = Owner.Player1;
            baseBuilding.CompleteConstruction();

            // Start expansion
            Debug.Log("[Test] Starting expansion to " + _targetPosition);
            colonyExpansionManager.StartExpansion(_targetPosition, _targetSector);
        }

        private static bool Tick(float elapsed)
        {
            // Expansion usually takes 5 seconds (growth + boot-up)
            if (elapsed < 7.0f) return false;

            // Check if new building exists
            bool found = BaseBuilding.ActiveBuildings.Any(b => 
                b != null && 
                b.Owner == Owner.Player1 && 
                b.Progress.State == BuildingProgress.BuildingState.Completed &&
                Vector3.Distance(b.transform.position, _targetPosition) < 1.0f);

            if (found)
            {
                Debug.Log("[Test] New Command Post found at expansion site!");
                return true;
            }

            return false;
        }

        private static void FinishTest(bool isError, string errorMessage)
        {
            _testDone = true;
            EditorApplication.update -= WaitFramesThenRun;
            Application.logMessageReceived -= OnLogMessage;

            var result = new TestResult
            {
                success = !isError,
                error = errorMessage,
                logs = _capturedLogs.ToArray()
            };

            SessionState.SetString(ResultKey, JsonUtility.ToJson(result));
            SessionState.SetString(StateKey, "Done");
            EditorApplication.isPlaying = false;
        }

        private static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            if (_capturedLogs.Count >= MaxCapturedLogs) return;
            string log = "[" + type + "] " + message;
            if (type == LogType.Exception || type == LogType.Error)
            {
                log += "\nStack: " + stackTrace;
            }
            _capturedLogs.Add(log);
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

        [System.Serializable]
        private class TestResult
        {
            public bool success;
            public string error;
            public string[] logs;
        }
    }
}
