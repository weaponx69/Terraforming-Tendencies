using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Attaches to an Air Transport (or any AbstractUnit with a NavMeshAgent) and drives
    /// a fully autonomous mining loop:
    ///   Search → Move to resource → Gather → Return to command post → repeat.
    ///
    /// The component is started externally by AIController once the unit spawns.
    /// It does NOT use the BehaviorGraphAgent so it can coexist with the existing
    /// Air Transport graph (the graph is simply left in Stop state).
    /// </summary>
    [RequireComponent(typeof(AbstractUnit))]
    public class MiningDrone : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────────
        [Tooltip("How far the drone scans for an available GatherableSupply on each search pass.")]
        [SerializeField] private float searchRadius = 60f;

        [Tooltip("NavMesh stopping distance used when approaching a resource or command post.")]
        [SerializeField] private float stoppingDistance = 1.5f;

        [Tooltip("Seconds to wait between search attempts when no resource is found nearby.")]
        [SerializeField] private float searchRetryDelay = 3f;

        // ── State ──────────────────────────────────────────────────────────────────
        public enum DroneState { Idle, Searching, MovingToResource, Gathering, ReturningToBase }
        public DroneState State { get; private set; } = DroneState.Idle;

        private AbstractUnit unit;
        private NavMeshAgent agent;
        private GatherableSupply currentTarget;
        private GameObject commandPost;        // set by AIController
        private bool isRunning;

        // ── Lifecycle ──────────────────────────────────────────────────────────────
        private void Awake()
        {
            unit  = GetComponent<AbstractUnit>();
            agent = GetComponent<NavMeshAgent>();
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        /// <summary>Called by AIController to assign a command post and begin the mining loop.</summary>
        public void StartMining(GameObject post)
        {
            if (isRunning) return;
            commandPost = post;
            isRunning   = true;
            StartCoroutine(MiningLoop());
        }

        /// <summary>Update the command post reference (e.g. if the original one is destroyed).</summary>
        public void SetCommandPost(GameObject post)
        {
            commandPost = post;
        }

        // ── Core loop ──────────────────────────────────────────────────────────────
        private IEnumerator MiningLoop()
        {
            while (isRunning && unit != null)
            {
                // ── 1. Search for a free resource ──────────────────────────────
                State = DroneState.Searching;
                currentTarget = FindNearestAvailableSupply();

                if (currentTarget == null)
                {
                    // Nothing in radius — wait and retry
                    yield return new WaitForSeconds(searchRetryDelay);
                    continue;
                }

                // ── 2. Travel to the resource ──────────────────────────────────
                State = DroneState.MovingToResource;
                agent.stoppingDistance = stoppingDistance;
                
                Vector3 destination = currentTarget.transform.position;
                if (currentTarget.TryGetComponent(out Collider col))
                {
                    destination = col.ClosestPoint(transform.position);
                }
                agent.SetDestination(destination);

                while (!HasArrived() && currentTarget != null)
{
                    // Resource may be destroyed mid-travel; abort and re-search
                    if (currentTarget == null || currentTarget.Amount <= 0)
                    {
                        currentTarget = null;
                        break;
                    }
                    yield return null;
                }

                if (currentTarget == null) continue;

                // ── 3. Gather ──────────────────────────────────────────────────
                if (!currentTarget.BeginGather())
                {
                    // Already claimed by another drone — re-search
                    currentTarget = null;
                    continue;
                }

                State = DroneState.Gathering;
                float gatherEnd = Time.time + (currentTarget.Supply != null ? currentTarget.Supply.BaseGatherTime : 1.5f);

                while (Time.time < gatherEnd)
                {
                    if (currentTarget == null)
                    {
                        break;
                    }
                    yield return null;
                }

                int amountGathered = 0;
                if (currentTarget != null)
                {
                    amountGathered = currentTarget.EndGather();
                    SupplySO supplyType = currentTarget.Supply;

                    // Deposit immediately via the event bus (same as Worker.HandleGatherSupplies)
                    if (supplyType != null && amountGathered > 0)
                    {
                        // ── 4. Return to command post ──────────────────────────
                        State = DroneState.ReturningToBase;
                        if (commandPost != null)
                        {
                            agent.stoppingDistance = stoppingDistance;
                            agent.SetDestination(commandPost.transform.position);

                            while (!HasArrived())
                            {
                                if (commandPost == null) break;
                                yield return null;
                            }
                        }

                        // Raise the supply event so Supplies.cs converts minerals → biomass
                        Bus<SupplyEvent>.Raise(unit.Owner, new SupplyEvent(unit.Owner, amountGathered, supplyType));
                    }
                }

                currentTarget = null;
            }

            State = DroneState.Idle;
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        private bool HasArrived()
        {
            if (agent == null || !agent.isOnNavMesh) return true;
            if (agent.pathPending) return false;
            return agent.remainingDistance <= agent.stoppingDistance + 0.1f;
        }

        private GatherableSupply FindNearestAvailableSupply()
        {
            // Broad-phase: all GatherableSupply objects in the scene
            GatherableSupply[] all = FindObjectsByType<GatherableSupply>(FindObjectsInactive.Exclude);

            GatherableSupply best    = null;
            float            bestDist = float.MaxValue;

            foreach (GatherableSupply gs in all)
            {
                if (gs.IsBusy || gs.Amount <= 0) continue;

                float dist = Vector3.Distance(transform.position, gs.transform.position);
                if (dist > searchRadius) continue;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best     = gs;
                }
            }

            return best;
        }
    }
}
