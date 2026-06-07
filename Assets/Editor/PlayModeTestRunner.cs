using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using GameDevTV.RTS.Units;

namespace Unity.AI.Assistant.PlayModeTest
{
    [InitializeOnLoad]
    internal static class PlayModeTestRunner
    {
        private const string StateKey = "PlayModeTest.State";
        private const string ResultKey = "PlayModeTest.Result";
        private const string ScriptPathKey = "PlayModeTest.ScriptPath";
        private const string SentinelLog = "PLAY_MODE_TEST_COMPLETE";

        private static readonly int WaitFrames = SessionState.GetInt("PlayModeTest.WaitFrames", 180);
        private static readonly float TestTimeout = SessionState.GetFloat("PlayModeTest.TestTimeout", 25.0f);

        private static List<string> _capturedLogs = new List<string>();
        private const int MaxCapturedLogs = 120;

        static PlayModeTestRunner()
        {
            string state = SessionState.GetString(StateKey, "Idle");
            switch (state)
            {
                case "Idle": break;
                case "WaitingForCompile":
                    EditorApplication.delayCall += () => {
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
        private static Vector3 _startPos;
        private static AbstractUnit _unit;
        private static float _distanceMoved = 0;

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
                catch (System.Exception e) { FinishTest(true, "Setup error: " + e.Message); }
                return;
            }

            float elapsed = (float)(EditorApplication.timeSinceStartup - _testStartTime);
            bool timedOut = elapsed >= TestTimeout;
            try {
                if (Tick(elapsed) || timedOut) FinishTest(timedOut, timedOut ? "Timeout" : null);
            }
            catch (System.Exception e) { FinishTest(true, "Tick error: " + e.Message); }
        }

        private static void FinishTest(bool isError, string errorMessage)
        {
            _testDone = true;
            EditorApplication.update -= WaitFramesThenRun;
            Application.logMessageReceived -= OnLogMessage;
            SessionState.SetString(ResultKey, GetResult(isError, errorMessage));
            SessionState.SetString(StateKey, "Done");
            EditorApplication.isPlaying = false;
        }

        private static void OnLogMessage(string m, string s, LogType t)
        {
            if (_capturedLogs.Count < MaxCapturedLogs &&
                (t == LogType.Error || t == LogType.Exception || m.Contains("[MoveDiag]")))
            {
                _capturedLogs.Add("[" + t + "] " + m);
            }
        }

        private static void SelfDestruct()
        {
            string p = SessionState.GetString(ScriptPathKey, "");
            if (!string.IsNullOrEmpty(p) && AssetDatabase.AssetPathExists(p)) AssetDatabase.DeleteAsset(p);
            SessionState.EraseString(StateKey);
            SessionState.EraseString(ScriptPathKey);
        }

        private static void Log(string msg)
        {
            Debug.Log("[MoveDiag] " + msg);
        }

        private static void Setup()
        {
            // Find a real drone in the scene (spawned by GreedyAI), preferring a Worker/Mining Drone
            AbstractUnit[] units = Object.FindObjectsByType<AbstractUnit>(FindObjectsInactive.Exclude);
            Log("Found " + units.Length + " AbstractUnit(s) in scene.");
            foreach (var u in units)
            {
                Log("  Unit: " + u.name + " (SO: " + (u.UnitSO != null ? u.UnitSO.name : "null") + ")");
            }

            if (units.Length == 0)
            {
                // Spawn one if none exist
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Units/Mining Drone/Mining Drone.prefab");
                Vector3 spawn = new Vector3(50, 4, 50);
                if (NavMesh.SamplePosition(spawn, out NavMeshHit shit, 20f, new NavMeshQueryFilter { agentTypeID = -1372625422, areaMask = NavMesh.AllAreas }))
                    spawn = shit.position;
                var go = Object.Instantiate(prefab, spawn, Quaternion.identity);
                go.name = "TestDrone";
                _unit = go.GetComponent<AbstractUnit>();
                Log("Spawned test drone at " + spawn);
            }
            else
            {
                _unit = units[0];
            }

            var agent = _unit.GetComponent<NavMeshAgent>();
            _startPos = _unit.transform.position;

            // Report the FULL state BEFORE moving
            Log("=== PRE-MOVE STATE for '" + _unit.name + "' ===");
            Log("position=" + _startPos);
            if (agent != null)
            {
                Log("agent.enabled=" + agent.enabled);
                Log("agent.isActiveAndEnabled=" + agent.isActiveAndEnabled);
                Log("agent.agentTypeID=" + agent.agentTypeID + " (" + NavMesh.GetSettingsNameFromID(agent.agentTypeID) + ")");
                Log("agent.isOnNavMesh=" + agent.isOnNavMesh);
                Log("agent.baseOffset=" + agent.baseOffset);
                Log("agent.speed=" + agent.speed);
                Log("agent.stoppingDistance=" + agent.stoppingDistance);
                // Can we sample the navmesh near the drone at all?
                bool sampled = NavMesh.SamplePosition(_startPos, out NavMeshHit hit, 25f,
                    new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = NavMesh.AllAreas });
                Log("NavMesh.SamplePosition near drone (range 25) success=" + sampled + (sampled ? (" at " + hit.position) : ""));
            }
            else
            {
                Log("NO NavMeshAgent component!");
            }

            // Issue the move command exactly as a right-click would
            Vector3 target = _startPos + new Vector3(25, 0, 25);
            // Snap target to navmesh for a fair test
            if (agent != null && NavMesh.SamplePosition(target, out NavMeshHit thit, 25f,
                new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = NavMesh.AllAreas }))
            {
                target = thit.position;
            }
            Log("Issuing MoveTo(" + target + ")");
            _unit.MoveTo(target);
            Log("Command after MoveTo = " + _unit.GetCurrentCommand());
        }

        private static bool Tick(float elapsed)
        {
            if (_unit == null) return true;
            var agent = _unit.GetComponent<NavMeshAgent>();
            _distanceMoved = Vector3.Distance(_startPos, _unit.transform.position);

            // Report detailed agent state every ~1.5 seconds
            if (Time.frameCount % 90 == 0 && agent != null)
            {
                Log("t=" + elapsed.ToString("F1")
                    + " cmd=" + _unit.GetCurrentCommand()
                    + " moved=" + _distanceMoved.ToString("F2")
                    + " onNavMesh=" + agent.isOnNavMesh
                    + " hasPath=" + agent.hasPath
                    + " pathPending=" + agent.pathPending
                    + " pathStatus=" + agent.pathStatus
                    + " remaining=" + agent.remainingDistance.ToString("F1")
                    + " vel=" + agent.velocity.magnitude.ToString("F2")
                    + " dest=" + agent.destination);
            }

            return _distanceMoved > 4.0f;
        }

        private static string GetResult(bool isError, string msg)
        {
            var agent = _unit != null ? _unit.GetComponent<NavMeshAgent>() : null;
            Log("=== FINAL: moved=" + _distanceMoved.ToString("F2")
                + " onNavMesh=" + (agent != null ? agent.isOnNavMesh.ToString() : "n/a")
                + " hasPath=" + (agent != null ? agent.hasPath.ToString() : "n/a")
                + " pathStatus=" + (agent != null ? agent.pathStatus.ToString() : "n/a") + " ===");

            var res = new TestResult {
                success = !isError && _distanceMoved > 4.0f,
                error = msg,
                logs = _capturedLogs.ToArray(),
                distanceMoved = _distanceMoved
            };
            return JsonUtility.ToJson(res);
        }

        [System.Serializable]
        private class TestResult {
            public bool success;
            public string error;
            public string[] logs;
            public float distanceMoved;
        }
    }
}
