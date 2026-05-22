using System.Collections;
using GameDevTV.RTS.Behavior;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Self-contained finite state machine for Worker drone behaviour.
    /// Manages the Gather → (optional Return) loop directly via NavMeshAgent,
    /// independent of the BehaviorGraphAgent which handles animation state only.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Worker))]
    public class WorkerBrainController : MonoBehaviour
    {
        public enum State { Idle, MovingToSupply, Gathering, MovingToBase }

        public State CurrentState { get; private set; } = State.Idle;

        private NavMeshAgent agent;
        private Worker worker;
        private GatherSuppliesEventChannel eventChannel;
        private Transform homeBase;

        private GatherableSupply targetSupply;
        private Coroutine runningCoroutine;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            worker = GetComponent<Worker>();
        }

        // Called by Worker after event channels are loaded.
        public void SetEventChannel(GatherSuppliesEventChannel channel)
        {
            eventChannel = channel;
        }

        /// <summary>Called by AIController when it assigns this drone to a node.</summary>
        public void SetHomeBase(Transform commandPostTransform)
        {
            homeBase = commandPostTransform;
        }

        // --- Public commands ---

        public void StartGather(GatherableSupply supply)
        {
            targetSupply = supply;
            Restart(GatherLoop());
        }

        public void Halt()
        {
            StopRunning();
            if (targetSupply != null)
            {
                targetSupply.AbortGather();
                targetSupply = null;
            }
            if (agent.isOnNavMesh) agent.ResetPath();
            CurrentState = State.Idle;
        }

        // --- Internal ---

        private void Restart(IEnumerator routine)
        {
            StopRunning();
            runningCoroutine = StartCoroutine(routine);
        }

        private void StopRunning()
        {
            if (runningCoroutine != null)
            {
                StopCoroutine(runningCoroutine);
                runningCoroutine = null;
            }
        }

        private IEnumerator GatherLoop()
        {
            while (targetSupply != null && targetSupply.Amount > 0)
            {
                // ── State: MovingToSupply ──────────────────────────────
                CurrentState = State.MovingToSupply;

                if (agent.isOnNavMesh)
                {
                    agent.SetDestination(targetSupply.transform.position);
                }

                yield return WaitUntilNear(targetSupply.transform, agent.stoppingDistance + 0.5f, timeout: 30f);

                if (targetSupply == null || targetSupply.Amount <= 0) break;

                agent.ResetPath();

                // ── State: Gathering ──────────────────────────────────
                CurrentState = State.Gathering;

                if (!targetSupply.BeginGather())
                {
                    // Another drone beat us to it — wait then retry navigation
                    yield return new WaitForSeconds(1.5f);
                    continue;
                }

                float gatherTime = targetSupply.Supply != null
                    ? targetSupply.Supply.BaseGatherTime
                    : 1.5f;

                yield return new WaitForSeconds(gatherTime);

                if (targetSupply == null) break;

                int gathered = targetSupply.EndGather();

                // Credit resources to the player immediately (same as BT GatherSuppliesAction does)
                if (eventChannel != null && gathered > 0)
                {
                    eventChannel.SendEventMessage(gameObject, gathered, targetSupply?.Supply);
                }

                // ── State: Return to base (visual only — resources credited above) ──
                if (homeBase != null && agent.isOnNavMesh)
                {
                    CurrentState = State.MovingToBase;
                    agent.SetDestination(homeBase.position);
                    yield return WaitUntilNear(homeBase, 3f, timeout: 30f);
                    if (agent.isOnNavMesh) agent.ResetPath();
                }

                // One frame so the event processes before we loop
                yield return null;
            }

            // Supply exhausted — go idle so AIController reassigns on next Tick
            CurrentState = State.Idle;
            runningCoroutine = null;
            worker.Stop();
        }

        /// <summary>
        /// Yields until the XZ distance to <paramref name="target"/> is within
        /// <paramref name="stopDist"/>, or <paramref name="timeout"/> seconds elapse.
        /// </summary>
        private IEnumerator WaitUntilNear(Transform target, float stopDist, float timeout)
        {
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                if (target == null) yield break;

                float dx = transform.position.x - target.position.x;
                float dz = transform.position.z - target.position.z;
                if (dx * dx + dz * dz <= stopDist * stopDist) yield break;

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}
