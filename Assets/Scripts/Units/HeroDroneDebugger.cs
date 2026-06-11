using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Attach to the Hero Drone to diagnose position snap-back.
    /// Logs to the Unity Console whenever a suspicious state change is detected.
    /// Remove this component when debugging is done.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class HeroDroneDebugger : MonoBehaviour
    {
        [Tooltip("How often (in seconds) to log a status snapshot.")]
        [SerializeField] private float logInterval = 0.25f;

        [Tooltip("Only log when position divergence between transform and agent.nextPosition exceeds this threshold.")]
        [SerializeField] private float divergenceAlertThreshold = 0.5f;

        private NavMeshAgent agent;
        private HeroDroneController heroCtrl;

        private float lastLogTime;
        private Vector3 lastTransformPos;
        private bool lastUpdatePos;
        private bool lastOnNavMesh;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            heroCtrl = GetComponent<HeroDroneController>();
            lastTransformPos = transform.position;
        }

        private void LateUpdate()
        {
            if (agent == null) return;

            Vector3 tPos = transform.position;
            Vector3 agentNext = agent.nextPosition;
            float divergence = Vector3.Distance(tPos, agentNext);

            // --- Alert immediately on large divergence (this is the snap indicator) ---
            if (divergence > divergenceAlertThreshold)
            {
                Debug.LogWarning(
                    $"[HeroDrone SNAP ALERT] Divergence={divergence:F3}u | " +
                    $"Transform={tPos:F2} | Agent.nextPos={agentNext:F2} | " +
                    $"OnNavMesh={agent.isOnNavMesh} | UpdatePos={agent.updatePosition} | " +
                    $"IsStopped={agent.isStopped} | HasPath={agent.hasPath} | " +
                    $"IsManual={heroCtrl?.IsBeingManuallyControlled}");
            }

            // --- Alert on state changes that could cause snap ---
            if (agent.updatePosition != lastUpdatePos)
            {
                Debug.Log($"[HeroDrone] agent.updatePosition changed: {lastUpdatePos} -> {agent.updatePosition}");
                lastUpdatePos = agent.updatePosition;
            }

            if (agent.isOnNavMesh != lastOnNavMesh)
            {
                Debug.Log($"[HeroDrone] isOnNavMesh changed: {lastOnNavMesh} -> {agent.isOnNavMesh} | Transform={tPos:F2}");
                lastOnNavMesh = agent.isOnNavMesh;
            }

            // --- Periodic snapshot log ---
            if (Time.time - lastLogTime >= logInterval)
            {
                lastLogTime = Time.time;
                Vector3 moved = tPos - lastTransformPos;
                Debug.Log(
                    $"[HeroDrone STATUS] Transform={tPos:F2} | MovedThisInterval={moved.magnitude:F3}u | " +
                    $"Agent.nextPos={agentNext:F2} | Divergence={divergence:F3}u | " +
                    $"OnNavMesh={agent.isOnNavMesh} | UpdatePos={agent.updatePosition} | " +
                    $"IsStopped={agent.isStopped} | HasPath={agent.hasPath} | " +
                    $"IsManual={heroCtrl?.IsBeingManuallyControlled}");
                lastTransformPos = tPos;
            }
        }
    }
}
