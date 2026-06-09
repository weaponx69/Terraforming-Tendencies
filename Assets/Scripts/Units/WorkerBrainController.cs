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
    /// Manages the Gather → Return loop and Build loop directly via NavMeshAgent
    /// coroutines, independent of the BehaviorGraphAgent.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Worker))]
    public class WorkerBrainController : MonoBehaviour
    {
        public enum State { Idle, MovingToSupply, Gathering, MovingToBase, MovingToBuild, Building, MovingToRepair, Repairing, BuildingPipeline }

        public State CurrentState { get; private set; } = State.Idle;

        private NavMeshAgent agent;
        private Worker worker;
        private GatherSuppliesEventChannel eventChannel;
        private Transform homeBase;

        private GatherableSupply targetSupply;
        private BaseBuilding targetBuilding;
        public EnergyPipelineManager CurrentPipeline { get; private set; }
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

        /// <summary>
        /// Navigate to <paramref name="targetLocation"/>, then incrementally construct
        /// <paramref name="building"/> over <paramref name="buildingSO"/>.BuildTime seconds.
        /// Runs entirely in C# coroutines, bypassing the behavior-tree which cannot
        /// handle Mining Drones (no Animator component).
        /// </summary>
        public void StartBuild(BaseBuilding building, BuildingSO buildingSO, Vector3 targetLocation)
        {
            Restart(BuildLoop(building, buildingSO, targetLocation));
        }

        public void StartRepair(AbstractCommandable target)
        {
            Restart(RepairLoop(target));
        }

        public void StartPipelineBuild(EnergyPipelineManager pipelineManager)
        {
            CurrentPipeline = pipelineManager;
            Restart(PipelineBuildLoop());
        }

        public void Halt()
        {
            StopRunning();
            if (targetSupply != null)
            {
                targetSupply.AbortGather();
                targetSupply = null;
            }
            CurrentPipeline = null;
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

        private IEnumerator PipelineBuildLoop()
        {
            while (CurrentPipeline != null && CurrentPipeline.HasPendingSegments())
            {
                Vector3 targetPos = CurrentPipeline.GetNextSegmentPosition();
                
                CurrentState = State.MovingToBuild;
                if (agent.isOnNavMesh)
                {
                    agent.stoppingDistance = 1.0f;
                    agent.SetDestination(targetPos);
                }

                // Wait until we reach the target (using distance check)
                while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
                {
                    if (CurrentPipeline == null || !CurrentPipeline.HasPendingSegments() || CurrentPipeline.IsPaused)
                    {
                        break;
                    }
                    yield return null;
                }

                if (CurrentPipeline == null || !CurrentPipeline.HasPendingSegments() || CurrentPipeline.IsPaused)
                    break;

                CurrentState = State.BuildingPipeline;

                // Stop moving while building
                if (agent.isOnNavMesh) agent.ResetPath();
                
                // Simulate build time for one segment
                yield return new WaitForSeconds(1.5f); 

                if (CurrentPipeline != null && CurrentPipeline.CanAffordNextSegment())
                {
                    bool built = CurrentPipeline.BuildNextSegment();
                    if (!built) break; // Something went wrong or we ran out of biomass
                }
                else
                {
                    break; // Out of biomass, abort and let GreedyAI re-assign later
                }

                yield return null;
            }

            CurrentState = State.Idle;
            runningCoroutine = null;
            worker.Stop();
        }

        private IEnumerator GatherLoop()
        {
            while (targetSupply != null && targetSupply.Amount > 0)
            {
                // ── State: MovingToSupply ──────────────────────────────
                CurrentState = State.MovingToSupply;

                if (agent.isOnNavMesh)
                    agent.SetDestination(targetSupply.transform.position);

                yield return WaitUntilNear(targetSupply.transform, agent.stoppingDistance + 0.5f, timeout: 30f);

                if (targetSupply == null || targetSupply.Amount <= 0) break;

                agent.ResetPath();

                // ── State: Gathering ──────────────────────────────────
                CurrentState = State.Gathering;

                if (!targetSupply.BeginGather())
                {
                    yield return new WaitForSeconds(1.5f);
                    continue;
                }

                float gatherTime = targetSupply.Supply != null ? targetSupply.Supply.BaseGatherTime : 1.5f;
                yield return new WaitForSeconds(gatherTime);

                if (targetSupply == null) break;

                int gathered = targetSupply.EndGather();

                if (eventChannel != null && gathered > 0)
                    eventChannel.SendEventMessage(gameObject, gathered, targetSupply?.Supply);

                // ── State: Return to base ──────────────────────────────
                if (homeBase != null && agent.isOnNavMesh)
                {
                    CurrentState = State.MovingToBase;
                    agent.SetDestination(homeBase.position);
                    yield return WaitUntilNear(homeBase, 3f, timeout: 30f);
                    if (agent.isOnNavMesh) agent.ResetPath();
                }

                yield return null;
            }

            CurrentState = State.Idle;
            runningCoroutine = null;
            worker.Stop();
        }

        private IEnumerator RepairLoop(AbstractCommandable target)
        {
            while (target != null && target.CurrentHealth < target.MaxHealth)
            {
                CurrentState = State.MovingToRepair;
                if (agent.isOnNavMesh)
                    agent.SetDestination(target.transform.position);

                yield return WaitUntilNear(target.transform, agent.stoppingDistance + 1.2f, timeout: 30f);

                if (target == null || target.CurrentHealth >= target.MaxHealth) break;

                CurrentState = State.Repairing;
                if (agent.isOnNavMesh) agent.ResetPath();

                float repairTimer = 0f;
                while (target != null && target.CurrentHealth < target.MaxHealth)
                {
                    repairTimer += Time.deltaTime;
                    if (repairTimer >= 0.5f) // Repair tick rate
                    {
                        target.Heal(10); // Heal per tick (20 health per second)
                        repairTimer = 0f;
                    }

                    if (Vector3.Distance(transform.position, target.transform.position) > agent.stoppingDistance + 2f)
                    {
                        break; 
                    }

                    yield return null;
                }
            }

            CurrentState = State.Idle;
            runningCoroutine = null;
            worker.Stop();
        }

        private IEnumerator BuildLoop(BaseBuilding building, BuildingSO buildingSO, Vector3 targetLocation)
        {
            // ── Navigate to build site ────────────────────────────────
            CurrentState = State.MovingToBuild;

            if (agent.isOnNavMesh)
                agent.SetDestination(targetLocation);

            float arrivalDistance = agent.stoppingDistance + 0.5f;
            yield return WaitUntilNear(targetLocation, arrivalDistance, timeout: 60f);

            if (building == null)
            {
                CurrentState = State.Idle;
                runningCoroutine = null;
                worker.Stop();
                yield break;
            }

            // Only construct if the drone actually reached the site. If it could not
            // path there within the timeout, leave the ghost in its Paused state so it
            // can be resumed later — never auto-complete a building without a drone present.
            float adx = transform.position.x - targetLocation.x;
            float adz = transform.position.z - targetLocation.z;
            bool arrived = (adx * adx + adz * adz) <= arrivalDistance * arrivalDistance;
            if (!arrived)
            {
                CurrentState = State.Idle;
                runningCoroutine = null;
                worker.Stop();
                yield break;
            }

            agent.ResetPath();

            // ── Start construction ────────────────────────────────────
            CurrentState = State.Building;
            building.StartBuilding(worker);

            // Rise-from-ground animation
            Renderer buildingRenderer = building.MainRenderer;
            Vector3 endPosition = building.transform.position;
            Vector3 startPosition = endPosition;
            if (buildingRenderer != null)
            {
                startPosition = endPosition - Vector3.up * buildingRenderer.bounds.size.y;
                buildingRenderer.transform.position = startPosition;
            }

            float startTime = building.Progress.StartTime;
            float targetHealth = 0f;

            while (building != null)
            {
                float normalizedTime = (Time.time - startTime) / buildingSO.BuildTime;

                targetHealth += Time.deltaTime * (buildingSO.Health / buildingSO.BuildTime);
                if (targetHealth >= 1)
                {
                    int healAmount = Mathf.FloorToInt(targetHealth);
                    building.Heal(healAmount);
                    targetHealth -= healAmount;
                }

                if (buildingRenderer != null)
                    buildingRenderer.transform.position = Vector3.Lerp(startPosition, endPosition, normalizedTime);

                if (normalizedTime >= 1f) break;
                yield return null;
            }

            // ── Complete construction ─────────────────────────────────
            if (building != null)
            {
                building.enabled = true;
                building.CompleteConstruction();
            }

            CurrentState = State.Idle;
            runningCoroutine = null;
            worker.Stop();
        }

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

        private IEnumerator WaitUntilNear(Vector3 targetPos, float stopDist, float timeout)
        {
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                float dx = transform.position.x - targetPos.x;
                float dz = transform.position.z - targetPos.z;
                if (dx * dx + dz * dz <= stopDist * stopDist) yield break;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}
