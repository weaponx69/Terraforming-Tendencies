using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using GameDevTV.RTS.Units;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace Unity.AI.Assistant.PlayModeTest
{
    [InitializeOnLoad]
    internal static class PlayModeTestRunner
    {
        private const string StateKey = "PlayModeTest.State";
        private const string ResultKey = "PlayModeTest.Result";
        private const string ScriptPathKey = "PlayModeTest.ScriptPath";
        private const string SentinelLog = "PLAY_MODE_TEST_COMPLETE"; // ri2

        private static readonly int WaitFrames = SessionState.GetInt("PlayModeTest.WaitFrames", 10);
        private static readonly float TestTimeout = SessionState.GetFloat("PlayModeTest.TestTimeout", 30.0f);

        private static List<string> _capturedLogs = new List<string>();
        private const int MaxCapturedLogs = 80;

        static PlayModeTestRunner()
        {
            string state = SessionState.GetString(StateKey, "Idle");
            switch (state)
            {
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
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
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
                return;
            }

            float elapsed = (float)(EditorApplication.timeSinceStartup - _testStartTime);
            bool timedOut = elapsed >= TestTimeout;
            try
            {
                bool complete = Tick(elapsed);
                if (complete || timedOut)
                    FinishTest(timedOut && !complete, timedOut ? "Timed out" : null);
            }
            catch (System.Exception e) { FinishTest(true, "Tick ex: " + e.Message); }
        }

        private static void FinishTest(bool isError, string errorMessage)
        {
            _testDone = true;
            EditorApplication.update -= WaitFramesThenRun;
            Application.logMessageReceived -= OnLogMessage;
            ReleaseW();

            string resultJson = GetResult();
            var res = JsonUtility.FromJson<TestResult>(resultJson);
            if (isError) { res.success = false; res.error = (res.error ?? "") + " | " + errorMessage; }
            resultJson = JsonUtility.ToJson(res);

            SessionState.SetString(ResultKey, resultJson);
            SessionState.SetString(StateKey, "Done");
            EditorApplication.isPlaying = false;
        }

        private static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            if (_capturedLogs.Count >= MaxCapturedLogs) return;
            if (message.Contains("[Test]")) _capturedLogs.Add("[" + type + "] " + message);
        }

        private static void SelfDestruct()
        {
            string scriptPath = SessionState.GetString(ScriptPathKey, "");
            if (!string.IsNullOrEmpty(scriptPath) && AssetDatabase.AssetPathExists(scriptPath))
                AssetDatabase.DeleteAsset(scriptPath);
            SessionState.EraseString(StateKey);
            SessionState.EraseString(ScriptPathKey);
        }

        [System.Serializable]
        private class TestResult
        {
            public bool success;
            public string error;
            public string[] logs;
            public bool found;
            public float xzDistance;
            public bool inputSystemAvailable;
        }

        private static HeroDroneController hero;
        private static Vector3 startPos;
        private static bool holding;
        private static float holdStart;

        private static void HoldW()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = InputSystem.GetDevice<Keyboard>();
            if (kb == null) return;
            using (StateEvent.From(kb, out var ptr))
            {
                kb[Key.W].WriteValueIntoEvent(1f, ptr);
                InputSystem.QueueEvent(ptr);
            }
#endif
        }

        private static void ReleaseW()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = InputSystem.GetDevice<Keyboard>();
            if (kb == null) return;
            using (StateEvent.From(kb, out var ptr))
            {
                kb[Key.W].WriteValueIntoEvent(0f, ptr);
                InputSystem.QueueEvent(ptr);
            }
#endif
        }

        private static bool Tick(float elapsed)
        {
            if (hero == null)
            {
                hero = Object.FindAnyObjectByType<HeroDroneController>(FindObjectsInactive.Include);
                if (hero == null) return false;
                Debug.Log("[Test] Hero found at " + hero.transform.position);
            }

            if (!holding)
            {
                holding = true;
                startPos = hero.transform.position;
                holdStart = elapsed;
            }

            // Keep W pressed each frame so PlayerInput.HandleHeroControl drives the drone.
            HoldW();
            return (elapsed - holdStart) >= 2.0f;
        }

        private static string GetResult()
        {
            var result = new TestResult { success = false, logs = _capturedLogs.ToArray() };
#if ENABLE_INPUT_SYSTEM
            result.inputSystemAvailable = true;
#endif
            result.found = hero != null;
            if (hero == null) { result.error = "Hero Drone never spawned."; return JsonUtility.ToJson(result); }

            Vector3 a = startPos; a.y = 0;
            Vector3 b = hero.transform.position; b.y = 0;
            result.xzDistance = Vector3.Distance(a, b);

            result.success = result.xzDistance > 1.0f;
            result.error = "xz=" + result.xzDistance + " inputSystem=" + result.inputSystemAvailable;
            if (result.success) Debug.Log("[Test] PASS real-input xz=" + result.xzDistance);
            return JsonUtility.ToJson(result);
        }
    }
}
