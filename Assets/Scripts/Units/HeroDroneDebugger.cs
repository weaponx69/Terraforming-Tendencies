using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Attach this to the Hero Drone prefab to diagnose movement snap-back.
    /// It tracks the transform position and the NavMeshAgent's internal position
    /// every frame and draws them as colored trails in the Scene view.
    /// GREEN trail = where the transform actually is.
    /// RED trail   = where the NavMeshAgent THINKS it is internally.
    /// YELLOW sphere = agent.nextPosition each frame.
    /// If the red trail stays at spawn while the green trail moves, the agent is snapping back.
    /// Remove this component when debugging is done.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class HeroDroneDebugger : MonoBehaviour
    {
        [Header("Trail Settings")]
        [SerializeField] private int maxHistory = 120;
        [SerializeField] private float recordInterval = 0.05f;

        private NavMeshAgent agent;
        private HeroDroneController heroCtrl;

        private readonly Queue<Vector3> transformHistory = new Queue<Vector3>();
        private readonly Queue<Vector3> agentHistory = new Queue<Vector3>();

        private float lastRecordTime;

        // State snapshot recorded each interval for on-screen display
        private SnapState lastSnap;

        private struct SnapState
        {
            public Vector3 transformPos;
            public Vector3 agentPos;
            public Vector3 agentNextPos;
            public bool agentOnNavMesh;
            public bool agentUpdatePos;
            public bool agentIsStopped;
            public bool agentEnabled;
            public bool heroDroneIsManual;
            public float positionDivergence;
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            heroCtrl = GetComponent<HeroDroneController>();
        }

        private void Update()
        {
            if (Time.time - lastRecordTime < recordInterval) return;
            lastRecordTime = Time.time;

            Vector3 tPos = transform.position;
            Vector3 aPos = agent != null ? agent.transform.position : tPos;

            // agent.transform.position reflects the agent's internal navmesh position
            // only when updatePosition = true. We need to read the internal position differently.
            // The closest we can get without reflection is agent.nextPosition.
            Vector3 nextPos = agent != null ? agent.nextPosition : tPos;

            RecordPoint(transformHistory, tPos);
            RecordPoint(agentHistory, nextPos);

            lastSnap = new SnapState
            {
                transformPos = tPos,
                agentPos = aPos,
                agentNextPos = nextPos,
                agentOnNavMesh = agent != null && agent.isOnNavMesh,
                agentUpdatePos = agent != null && agent.updatePosition,
                agentIsStopped = agent != null && agent.isStopped,
                agentEnabled = agent != null && agent.enabled,
                heroDroneIsManual = heroCtrl != null && heroCtrl.IsBeingManuallyControlled,
                positionDivergence = Vector3.Distance(tPos, nextPos)
            };
        }

        private void RecordPoint(Queue<Vector3> queue, Vector3 point)
        {
            queue.Enqueue(point);
            while (queue.Count > maxHistory)
                queue.Dequeue();
        }

        private void OnDrawGizmos()
        {
            DrawTrail(transformHistory, Color.green, 0.15f);
            DrawTrail(agentHistory, Color.red, 0.1f);

            // Yellow sphere at agent.nextPosition — this is what the NavMesh agent
            // will try to snap the transform TO if updatePosition ever re-enables.
            if (agent != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(agent.nextPosition, 0.4f);
            }
        }

        private void DrawTrail(Queue<Vector3> history, Color color, float radius)
        {
            if (history.Count == 0) return;

            Gizmos.color = color;
            Vector3 prev = Vector3.zero;
            bool first = true;
            foreach (Vector3 pt in history)
            {
                Gizmos.DrawWireSphere(pt, radius);
                if (!first) Gizmos.DrawLine(prev, pt);
                prev = pt;
                first = false;
            }
        }

        private void OnGUI()
        {
            // Display a small diagnostic overlay in the game view.
            GUILayout.BeginArea(new Rect(10, 10, 420, 200));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"=== Hero Drone Debugger ===");
            GUILayout.Label($"Transform: {lastSnap.transformPos:F2}");
            GUILayout.Label($"Agent.nextPosition: {lastSnap.agentNextPos:F2}");
            GUILayout.Label($"Position Divergence: {lastSnap.positionDivergence:F3} units");
            GUILayout.Label($"Agent Enabled: {lastSnap.agentEnabled}   On NavMesh: {lastSnap.agentOnNavMesh}");
            GUILayout.Label($"Agent UpdatePos: {lastSnap.agentUpdatePos}   IsStopped: {lastSnap.agentIsStopped}");
            GUILayout.Label($"HeroCtrl IsManual: {lastSnap.heroDroneIsManual}");
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
