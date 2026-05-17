using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Attach to an Air Transport prefab (alongside AbstractUnit + NavMeshAgent) to give it
    /// a fully autonomous mining loop:
    ///   Search → Move to resource → Gather → Return to Command Post → repeat.
    ///
    /// Started externally by AIController.StartMining() once the unit spawns.
    /// </summary>
    [RequireComponent(typeof(AbstractUnit))]
    public class MiningDrone : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Tooltip("How far (world units) the drone scans for a GatherableSupply each pass.")]
        [SerializeField] private float searchRadius = 60f;

        [Tooltip("NavMesh stopping distance when approaching a resource or the command post.")]
        [SerializeField] private float stoppingDistance = 1.5f;

        [Tooltip("Seconds to wait before retrying when no resource is found nearby.")]
        [SerializeField] private float searchRetryDelay = 3f;

        // ── State ──────────────────────────────────────────────────────────────
        public enum DroneState { Idle, Searching, MovingToResource, Gathering, ReturningToBase }
        public DroneState State { get; private set; } = DroneState.Idle;

        private AbstractUnit unit;
        private NavMeshAgent agent;
        private GatherableSupply currentTarget;
        private GameObject commandPost;
        private bool isRunning;

        // ── Lifecycle ──────────────────────────────────────────────────────────
        private void Awake()
        {
            unit  = GetComponent<AbstractUnit>();
            agent = GetComponent<NavMeshAgent>();
        }

        private void OnDestroy() => StopAllCoroutines();

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Assign a command post and begin the autonomous mining loop. Safe to call multiple times (no-op if already running).</summary>
        public void StartMining(GameObject post)
        {
            commandPost = post;

            // Stop the behavior graph from fighting this C# script's NavMeshAgent commands
            if (TryGetComponent(out Unity.Behavior.BehaviorGraphAgent graphAgent))
            {
                graphAgent.enabled = false;
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.ResetPath();
                }
                Debug.Log($"[AI Drone] Disabled BehaviorGraphAgent on {name} to hand over control to C#.");
            }

            if (isRunning) return;
            isRunning = true;
            StartCoroutine(MiningLoop());
        }

        /// <summary>Update the command post reference (e.g. after it is rebuilt).</summary>
        public void SetCommandPost(GameObject post) => commandPost = post;

        // ── Core loop ──────────────────────────────────────────────────────────
        private System.Collections.IEnumerator MiningLoop()
        {
            while (isRunning && unit != null)
            {
                // 1. Search for a free resource
                State         = DroneState.Searching;
                currentTarget = FindNearestAvailableSupply();

                if (currentTarget == null)
                {
                    yield return new WaitForSeconds(searchRetryDelay);
                    continue;
                }

                // 2. Travel to the resource
                State = DroneState.MovingToResource;
                agent.stoppingDistance = stoppingDistance;

                Vector3 dest = currentTarget.transform.position;
                if (currentTarget.TryGetComponent(out Collider col))
                    dest = col.ClosestPoint(transform.position);

                // If spawned off-mesh, warp onto closest NavMesh surface
                if (!agent.isOnNavMesh)
                {
                    if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
                    {
                        agent.Warp(hit.position);
                    }
                }

                if (agent.isOnNavMesh)
                {
                    agent.SetDestination(dest);
                }
                else
                {
                    Debug.LogWarning($"[AI Drone] {name} is not on NavMesh! Cannot navigate to resource.");
                }

                while (!HasArrived() && currentTarget != null)
                {
                    if (currentTarget == null || currentTarget.Amount <= 0)
                    {
                        currentTarget = null;
                        break;
                    }
                    yield return null;
                }

                if (currentTarget == null) continue;

                // 3. Claim and gather
                if (!currentTarget.BeginGather())
                {
                    currentTarget = null;
                    continue;
                }

                State = DroneState.Gathering;
                float gatherEnd = Time.time + (currentTarget.Supply != null ? currentTarget.Supply.BaseGatherTime : 1.5f);

                while (Time.time < gatherEnd)
                {
                    if (currentTarget == null) break;
                    yield return null;
                }

                if (currentTarget == null) continue;

                int collected  = currentTarget.EndGather();
                SupplySO supply = currentTarget.Supply;
                currentTarget  = null;

                if (supply == null || collected <= 0) continue;

                // 4. Return to command post and deposit
                State = DroneState.ReturningToBase;
                if (commandPost != null)
                {
                    agent.stoppingDistance = stoppingDistance;

                    if (!agent.isOnNavMesh)
                    {
                        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
                        {
                            agent.Warp(hit.position);
                        }
                    }

                    if (agent.isOnNavMesh)
                    {
                        agent.SetDestination(commandPost.transform.position);
                    }
                    else
                    {
                        Debug.LogWarning($"[AI Drone] {name} is not on NavMesh! Cannot navigate to Command Post.");
                    }

                    while (!HasArrived())
                    {
                        if (commandPost == null) break;
                        yield return null;
                    }
                }

                Bus<SupplyEvent>.Raise(unit.Owner, new SupplyEvent(unit.Owner, collected, supply));
            }

            State = DroneState.Idle;
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        private bool HasArrived()
        {
            if (agent == null || !agent.isOnNavMesh) return true;
            if (agent.pathPending) return false;
            return agent.remainingDistance <= agent.stoppingDistance + 0.1f;
        }

        private GatherableSupply FindNearestAvailableSupply()
        {
            GatherableSupply[] all = FindObjectsByType<GatherableSupply>(FindObjectsInactive.Exclude);
            GatherableSupply best  = null;
            float bestDist         = float.MaxValue;

            foreach (GatherableSupply gs in all)
            {
                if (gs.IsBusy || gs.Amount <= 0) continue;

                float dist = Vector3.Distance(transform.position, gs.transform.position);
                if (dist > searchRadius || dist >= bestDist) continue;

                bestDist = dist;
                best     = gs;
            }

            return best;
        }
    }
}
