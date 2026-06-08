using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.UI.Containers;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Commands;

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
        private static readonly float TestTimeout = SessionState.GetFloat("PlayModeTest.TestTimeout", 15.0f);

        private static List<string> _capturedLogs = new List<string>();
        private const int MaxCapturedLogs = 100;

        static PlayModeTestRunner()
        {
            string state = SessionState.GetString(StateKey, "Idle");
            switch (state)
            {
                case "Idle": break;
                case "WaitingForCompile":
                    EditorApplication.delayCall += () =>
                    {
                        SessionState.SetString(StateKey, "EnteringPlayMode");
                        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                        EditorApplication.isPlaying = true;
                    };
                    break;
                case "EnteringPlayMode":
                    if (EditorApplication.isPlaying)
                    {
                        SessionState.SetString(StateKey, "InPlayMode");
                        EditorApplication.update += WaitFramesThenRun;
                    }
                    break;
                case "InPlayMode":
                    if (EditorApplication.isPlaying) EditorApplication.update += WaitFramesThenRun;
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
                SessionState.SetString(StateKey, "InPlayMode");
                EditorApplication.update += WaitFramesThenRun;
            }
        }

        private static int _frameCount = 0;
        private static bool _setupDone = false;
        private static bool _testDone = false;
        private static double _testStartTime = 0;

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
                try { Setup(); }
                catch (System.Exception e) { FinishTest(true, e.Message + "\n" + e.StackTrace); }
                return;
            }

            float elapsed = (float)(EditorApplication.timeSinceStartup - _testStartTime);
            bool timedOut = elapsed >= TestTimeout;
            try
            {
                bool complete = Tick(elapsed);
                if (complete || timedOut) FinishTest(timedOut && !complete, timedOut ? "Test timed out" : null);
            }
            catch (System.Exception e) { FinishTest(true, e.Message + "\n" + e.StackTrace); }
        }

        private static void FinishTest(bool isError, string errorMessage)
        {
            _testDone = true;
            EditorApplication.update -= WaitFramesThenRun;
            Application.logMessageReceived -= OnLogMessage;
            string resultJson = GetResult();
            if (isError && errorMessage != null)
                resultJson = JsonUtility.ToJson(new TestResult { success = false, error = errorMessage, logs = _capturedLogs.ToArray() });
            SessionState.SetString(ResultKey, resultJson);
            SessionState.SetString(StateKey, "Done");
            EditorApplication.isPlaying = false;
        }

        private static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            if (_capturedLogs.Count >= MaxCapturedLogs) return;
            _capturedLogs.Add("[" + type + "] " + message);
        }

        private static void SelfDestruct()
        {
            string scriptPath = SessionState.GetString(ScriptPathKey, "");
            if (!string.IsNullOrEmpty(scriptPath) && AssetDatabase.AssetPathExists(scriptPath)) AssetDatabase.DeleteAsset(scriptPath);
            SessionState.EraseString(StateKey);
            SessionState.EraseString(ScriptPathKey);
        }

        [System.Serializable]
        private class TestResult
        {
            public bool success;
            public string error;
            public string[] logs;
            public bool barracksEnabled;
            public bool singleUIActive;
            public bool buildUIActive;
            public int queueSize;
            public string soBeingBuilt;
        }

        private static BaseBuilding _barracks;
        private static SingleUnitSelectedUI _singleUI;
        private static BuildingBuildingUI _buildUI;
        private static BuildUnitCommand _buildCommand;

        private static void Setup()
        {
            _barracks = Object.FindObjectsByType<BaseBuilding>(FindObjectsSortMode.None)
                .FirstOrDefault(b => b.BuildingSO != null && b.BuildingSO.Name.Contains("Barracks"));
            
            if (_barracks == null)
            {
                BuildingSO barracksSO = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/Units/Buildings/Barracks/Barracks.asset");
                GameObject inst = Object.Instantiate(barracksSO.Prefab, Vector3.zero, Quaternion.identity);
                _barracks = inst.GetComponent<BaseBuilding>();
                _barracks.Owner = Owner.Player1;
                _barracks.InitializeIfNeeded();
                _barracks.CompleteConstruction();
                _barracks.enabled = true;
            }

            _singleUI = Object.FindAnyObjectByType<SingleUnitSelectedUI>(FindObjectsInactive.Include);
            _buildUI = Object.FindAnyObjectByType<BuildingBuildingUI>(FindObjectsInactive.Include);
            
            _buildCommand = AssetDatabase.LoadAssetAtPath<BuildUnitCommand>("Assets/Units/Buildings/Commands/Build Rifleman.asset");

            Debug.Log("[Test] Setup: Barracks=" + (_barracks != null) + ", SingleUI=" + (_singleUI != null) + ", BuildUI=" + (_buildUI != null) + ", Command=" + (_buildCommand != null));
            
            // Force selection via Event Bus
            Bus<UnitSelectedEvent>.Raise(Owner.Player1, new UnitSelectedEvent(_barracks));
        }

        private static int _step = 0;

        private static bool Tick(float elapsed)
        {
            if (_step == 0 && elapsed > 1.0f)
            {
                Debug.Log("[Test] Step 0: Selection verification.");
                Debug.Log("[Test] SingleUI Active: " + (_singleUI != null && _singleUI.gameObject.activeInHierarchy));
                Debug.Log("[Test] BuildUI Active: " + (_buildUI != null && _buildUI.gameObject.activeInHierarchy));
                
                // Trigger command through context to simulate real handling
                CommandContext context = new CommandContext(Owner.Player1, _barracks, new RaycastHit(), 0);
                if (_buildCommand != null && _buildCommand.CanHandle(context))
                {
                    _buildCommand.Handle(context);
                    Debug.Log("[Test] Command handled.");
                }
                else
                {
                    Debug.LogError("[Test] Command Cannot Handle or is null! Command=" + (_buildCommand != null));
                }
                
                _step = 1;
                return false;
            }
            
            if (_step == 1 && elapsed > 2.0f)
            {
                Debug.Log("[Test] Step 1: Post-command verification.");
                return true;
            }
            
            return false;
        }

        private static string GetResult()
        {
            return JsonUtility.ToJson(new TestResult
            {
                success = true,
                barracksEnabled = _barracks != null && _barracks.enabled,
                singleUIActive = _singleUI != null && _singleUI.gameObject.activeInHierarchy,
                buildUIActive = _buildUI != null && _buildUI.gameObject.activeInHierarchy,
                queueSize = _barracks != null ? _barracks.QueueSize : -1,
                soBeingBuilt = _barracks != null && _barracks.SOBeingBuilt != null ? _barracks.SOBeingBuilt.Name : "null",
                logs = _capturedLogs.ToArray()
            });
        }
    }
}
