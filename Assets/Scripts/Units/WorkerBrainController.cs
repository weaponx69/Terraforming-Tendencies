using System.Collections;
using GameDevTV.RTS.Behavior;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Utilities;
using UnityEngine;
using UnityEngine.AI;
using GameDevTV.RTS.VisualScriptingStubs;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Self-contained finite state machine for Worker drone behaviour.
    /// All coroutine loops (GatherLoop, BuildLoop, RepairLoop, PipelineBuildLoop)
    /// and NavMeshAgent pathing stay in C#. VS reads <see cref="CurrentState"/> only.
    /// </summary>
    [IncludeInSettings(true)]
    [RequireComponent(typeof(NavMeshAgent), typeof(Worker))]
    public class WorkerBrainController : MonoBehaviour
    {
        public enum State { Idle, MovingToSupply, Gathering, MovingToBase, MovingToBuild, Building, MovingToRepair, Repairing, BuildingPipeline }

        /// <summary>Current FSM state. Readable by Flow Graphs for status HUD branching.</summary>
        [Inspectable]
        public State CurrentState { get; private set; } = State.Idle;

        private NavMeshAgent agent;
        private Worker worker;
        private GatherSuppliesEventChannel eventChannel;
        private Transform homeBase;

        private GatherableSupply targetSupply;
        private BaseBuilding targetBuilding;
        public EnergyPipelineManager CurrentPipeline { get; private set; }
        private Coroutine runningCoroutine;

        [Header("Drone Proximity Repair")]
        [SerializeField] private float proximityRepairRadius = 16f;
        [SerializeField] private float proximityRepairInterval = 0.5f;
        [SerializeField] private int proximityRepairHeal = 12;
        private float proximityRepairTimer;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            worker = GetComponent<Worker>();
        }

        private void Update()
        {
            TickProximityRepair();
        }

        /// <summary>
        /// All worker drones passively repair damaged friendly buildings while nearby
        /// (including during gather/idle trips) so decay does not wipe pads they pass.
        /// </summary>
        private void TickProximityRepair()
        {
            if (worker == null) return;
            if (CurrentState == State.Repairing || CurrentState == State.MovingToRepair) return;

            proximityRepairTimer += Time.deltaTime;
            if (proximityRepairTimer < proximityRepairInterval) return;
            proximityRepairTimer = 0f;

            float radiusSqr = proximityRepairRadius * proximityRepairRadius;
            Vector3 origin = transform.position;

            foreach (BaseBuilding building in BaseBuilding.ActiveBuildings)
            {
                if (building == null) continue;
                if (building.Owner != worker.Owner) continue;
                if (building.Progress.State != BuildingProgress.BuildingState.Completed) continue;
                if (!BuildingSiteSlot.IsValidOccupant(building)) continue;
                if (building.CurrentHealth >= building.MaxHealth) continue;
                if (building is GlobalCommander) continue;
                if (building.GetComponent<DecayStarter>() != null) continue;

                if ((building.transform.position - origin).sqrMagnitude > radiusSqr) continue;

                building.Heal(proximityRepairHeal);
            }
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
            if (agent != null && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = false;
            }
            CurrentState = State.Idle;
        }

        private bool ShouldYieldToManualMove()
        {
            return worker != null && worker.IsDirectMoving;
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
                if (ShouldYieldToManualMove()) yield break;

                // ── State: MovingToSupply ──────────────────────────────
                CurrentState = State.MovingToSupply;

                if (agent.isOnNavMesh)
                    agent.SetDestination(targetSupply.transform.position);

                yield return WaitUntilNear(targetSupply.transform, agent.stoppingDistance + 0.5f, timeout: 30f);

                if (ShouldYieldToManualMove()) yield break;

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
                
                if (worker != null && worker.UnitSO != null && worker.UnitSO.GatherConfig != null)
                {
                    gatherTime /= Mathf.Max(0.1f, worker.UnitSO.GatherConfig.GatherRateMultiplier);
                }
                // Apply passive buff from blueprint cards
                gatherTime /= Mathf.Max(0.1f, BlueprintDraftManager.GatherSpeedMultiplier);
                yield return new WaitForSeconds(gatherTime);

                if (targetSupply == null) break;

                SupplySO gatheredSupplySO = targetSupply.Supply;
                int gathered = targetSupply.EndGather();

                // Combine both names to bulletproof the check (in case the SupplySO is named something weird like "Rocks")
                Transform returnTarget = homeBase;
                if (returnTarget == null && worker != null)
                {
                    float nearestDist = float.MaxValue;
                    BaseBuilding nearestBldg = null;
                    foreach (var bldg in BaseBuilding.ActiveBuildings)
                    {
                        if (bldg != null && bldg.Owner == worker.Owner && bldg.Progress.State == BuildingProgress.BuildingState.Completed)
                        {
                            float dist = Vector3.Distance(transform.position, bldg.transform.position);
                            if (dist < nearestDist)
                            {
                                nearestDist = dist;
                                nearestBldg = bldg;
                            }
                        }
                    }
                    if (nearestBldg != null)
                    {
                        returnTarget = nearestBldg.transform;
                    }
                }

                // ── State: Return to base ──────────────────────────────
                if (returnTarget != null && agent.isOnNavMesh)
                {
                    CurrentState = State.MovingToBase;
                    agent.SetDestination(returnTarget.position);
                    yield return WaitUntilNear(returnTarget, 3f, timeout: 30f);
                    if (agent.isOnNavMesh) agent.ResetPath();
                    
                    // Physically drop off the resources ONLY after arriving
                    if (eventChannel != null && gathered > 0)
                    {
                        eventChannel.SendEventMessage(gameObject, gathered, gatheredSupplySO);
                    }
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
                    float buildSpeedMultiplier = 1f;
                    if (worker != null && worker.UnitSO != null && worker.UnitSO.BuilderConfig != null)
                    {
                        buildSpeedMultiplier = worker.UnitSO.BuilderConfig.BuildSpeedMultiplier;
                    }
                
                    repairTimer += Time.deltaTime * buildSpeedMultiplier;
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
            float elapsedTime = 0f;

            float buildSpeedMultiplier = 1f;
            if (worker != null && worker.UnitSO != null && worker.UnitSO.BuilderConfig != null)
            {
                buildSpeedMultiplier = worker.UnitSO.BuilderConfig.BuildSpeedMultiplier;
            }

            while (building != null)
            {
                elapsedTime += Time.deltaTime * buildSpeedMultiplier;
                float normalizedTime = elapsedTime / buildingSO.BuildTime;

                targetHealth += (Time.deltaTime * buildSpeedMultiplier) * (buildingSO.Health / buildingSO.BuildTime);
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
                // Rise animation moves the mesh renderer; restore it under the root,
                // then pin the whole building to terrain so themed pads never float.
                if (buildingRenderer != null && buildingRenderer.transform != building.transform)
                {
                    buildingRenderer.transform.localPosition = Vector3.zero;
                }

                building.enabled = true;
                building.CompleteConstruction();
                ReservedSiteBuildUtility.GroundBuilding(building);
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

                if (agent != null && agent.isOnNavMesh && !agent.pathPending)
                {
                    if (agent.remainingDistance <= agent.stoppingDistance + 0.5f) yield break;
                }
                else
                {
                    float dx = transform.position.x - target.position.x;
                    float dz = transform.position.z - target.position.z;
                    if (dx * dx + dz * dz <= stopDist * stopDist) yield break;
                }
                
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator WaitUntilNear(Vector3 targetPos, float stopDist, float timeout)
        {
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                if (agent != null && agent.isOnNavMesh && !agent.pathPending)
                {
                    if (agent.remainingDistance <= agent.stoppingDistance + 0.5f) yield break;
                }
                else
                {
                    float dx = transform.position.x - targetPos.x;
                    float dz = transform.position.z - targetPos.z;
                    if (dx * dx + dz * dz <= stopDist * stopDist) yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}
