using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.TechTree;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameDevTV.RTS.Units
{
    public class AIController : MonoBehaviour
    {
        [Header("Owner")]
        [SerializeField] private Owner aiOwner = Owner.AI1;

        [Header("Spawn References")]
        [SerializeField] private GameObject commandPostPrefab;
        [SerializeField] private BuildingSO commandPostSO;

        [Tooltip("Air Transport SO. Auto-loaded from Resources/Units/MiningDrone if left blank.")]
        [SerializeField] private AbstractUnitSO miningDroneUnitSO;

        [Header("Economy Limits")]
        [SerializeField] private int biomassReserve = 0;
        [Tooltip("Biomass granted to the AI at startup, independent of the player's starting biomass.")]
        [SerializeField] private int startingAIBiomass = 500;

        [Header("Timing")]
        [SerializeField] private float tickRate = 3f;
        [SerializeField] private float startDelay = 2f;

        [Header("Node Settings")]
        [SerializeField] private float nodeRadius = 35f;
        [SerializeField] private float minNodeSpacing = 80f;

        // ── Runtime state ──────────────────────────────────────────────────────
        private class AINode
        {
            public BaseBuilding CommandPost;
            public readonly List<Worker> Drones = new();
            public readonly HashSet<GatherableSupply> ResourcesInRange = new();
            public int TargetDroneCount => 4;
            }

        private readonly List<AINode> activeNodes = new();
        private readonly System.Collections.Generic.Dictionary<Worker, GatherableSupply> assignedTargets = new();
        private readonly System.Collections.Generic.Dictionary<Worker, UnityEngine.Vector3> lastDronePositions = new();
        private bool isSpawning = false;

        // ── Lifecycle ──────────────────────────────────────────────────────────
        private void Awake()
        {
            Bus<UnitSpawnEvent>.OnEvent[aiOwner]     += HandleUnitSpawn;
            Bus<UnitDeathEvent>.OnEvent[aiOwner]     += HandleUnitDeath;
            Bus<BuildingSpawnEvent>.OnEvent[aiOwner] += HandleBuildingSpawn;
            Bus<BuildingDeathEvent>.OnEvent[aiOwner] += HandleBuildingDeath;

            if (miningDroneUnitSO == null)
            {
                miningDroneUnitSO = Resources.Load<AbstractUnitSO>("Units/MiningDrone");
                if (miningDroneUnitSO == null)
                {
                    miningDroneUnitSO = Resources.Load<AbstractUnitSO>("Units/Air Transport");
                }

                if (miningDroneUnitSO == null)
                {
                    // // // Debug.LogWarning("[AIController] Could not load MiningDrone or Air Transport from Resources! AI will attempt to auto-discover unit type from spawned Workers.");
                }
            }
        }

        private void Start() => StartCoroutine(DelayedStart());

        private void OnDestroy()
        {
            Bus<UnitSpawnEvent>.OnEvent[aiOwner]     -= HandleUnitSpawn;
            Bus<UnitDeathEvent>.OnEvent[aiOwner]     -= HandleUnitDeath;
            Bus<BuildingSpawnEvent>.OnEvent[aiOwner] -= HandleBuildingSpawn;
            Bus<BuildingDeathEvent>.OnEvent[aiOwner] -= HandleBuildingDeath;
        }

        // ── Boot ──────────────────────────────────────────────────────────────
        private IEnumerator DelayedStart()
        {
            // // // Debug.Log($"[AI] {aiOwner} starting DelayedStart. delay={startDelay}");
            yield return new WaitForSeconds(startDelay);
            GrantStartingBiomass();
            // // // Debug.Log($"[AI] {aiOwner} biomass granted. startingBiomass={startingAIBiomass}");
            
            // Only spawn if nothing exists and we aren't the player (player builds their own)
            if (aiOwner != Owner.Player1 && GetBuildingsInScene().Count == 0)
            {
                // // // Debug.Log($"[AI] No buildings found, spawning initial Command Post.");
                SpawnCommandPost();
            }
            
            // // // Debug.Log($"[AI] Starting Tick repetition every {tickRate}s.");
            InvokeRepeating(nameof(Tick), tickRate, tickRate);
        }

        private void GrantStartingBiomass()
        {
            if (Player.Supplies.Biomass == null) return;

            int amountToGrant = aiOwner == Owner.Player1 ? 1000 : startingAIBiomass;
            if (amountToGrant <= 0) return;

            int current = Player.Supplies.Biomass.TryGetValue(aiOwner, out int biomass) ? biomass : 0;
            int total   = current + amountToGrant;
            Player.Supplies.Biomass[aiOwner] = total;
            Player.Supplies.RaiseBiomassChanged(aiOwner, total);
        }

        // ── Event handlers ─────────────────────────────────────────────────────
        private void HandleUnitSpawn(UnitSpawnEvent evt)
        {
            if (evt.Unit.Owner != aiOwner) return;
            
            // Auto-discover miningDroneUnitSO if not set
            if (miningDroneUnitSO == null && evt.Unit is Worker workerDiscover)
            {
                miningDroneUnitSO = evt.Unit.UnitSO;
                // // // Debug.Log($"[AI] Auto-discovered drone unit type: {miningDroneUnitSO.Name}");
            }

            if (miningDroneUnitSO == null || evt.Unit.UnitSO?.Name != miningDroneUnitSO.Name) return;

            if (evt.Unit is Worker worker)
            {
                if (worker.TryGetComponent(out NavMeshAgent navAgent))
                {
                    if (!navAgent.enabled)
                    {
                        navAgent.enabled = true;
                    }
                    navAgent.stoppingDistance = 0.5f;
                    float baseSpeed = navAgent.speed;
                    navAgent.speed = baseSpeed * Random.Range(0.9f, 1.1f);
                    
                    // Flyers should not avoid each other or ground obstacles
                    if (navAgent.agentTypeID != 0) // Air Units
                    {
                        navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                        navAgent.avoidancePriority = 0; 
                    }
                    else
                    {
                        navAgent.avoidancePriority = Random.Range(30, 71);
                    }
                    
                    navAgent.acceleration *= Random.Range(0.8f, 1.2f);

                    if (!navAgent.isOnNavMesh)
                    {
                        NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = navAgent.agentTypeID, areaMask = NavMesh.AllAreas };
                        if (NavMesh.SamplePosition(worker.transform.position, out NavMeshHit hit, 25f, filter))
                        {
                            navAgent.enabled = false;
                            worker.transform.position = hit.position;
                            navAgent.enabled = true;
                            navAgent.Warp(hit.position);
                        }
                    }
                }

                // Assign to closest node
                AINode node = activeNodes
                    .Where(n => n.CommandPost != null)
                    .OrderBy(n => Vector3.Distance(worker.transform.position, n.CommandPost.transform.position))
                    .FirstOrDefault();
                if (node != null)
                {
                    node.Drones.Add(worker);
                }

                StartCoroutine(DeferredGatherAssignment(worker));
            }
        }

        private IEnumerator DeferredGatherAssignment(Worker worker)
        {
            yield return null; 
            if (worker == null) yield break;

            AINode node = activeNodes.FirstOrDefault(n => n.Drones.Contains(worker));
            if (node == null)
            {
                // // // Debug.LogWarning($"[AI] {aiOwner} worker {worker.UnitID} spawned but no node found to assign it to.");
                yield break;
            }

            // Try to find a resource that isn't globally assigned yet
            HashSet<GatherableSupply> excluded = new HashSet<GatherableSupply>(assignedTargets.Values);
            Vector3 queryPos = worker.transform.position;
            if (worker.Agent != null)
            {
                queryPos.y -= worker.Agent.baseOffset;
            }
            GatherableSupply supply = FindNearestAvailableSupplyInNode(queryPos, node, excluded, worker.Agent.agentTypeID);
            
            if (supply != null)
            {
                assignedTargets[worker] = supply;
                // // // Debug.Log($"[AI] {aiOwner} worker {worker.UnitID} initial assignment: {supply.name}");
                worker.GetComponent<WorkerBrainController>()?.SetHomeBase(node.CommandPost?.transform);
                worker.Gather(supply);
            }
            else
            {
                // // // Debug.Log($"[AI] {aiOwner} worker {worker.UnitID} could not find initial resource in range.");
            }
        }

        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            if (evt.Unit is Worker worker)
            {
                foreach (var node in activeNodes) node.Drones.Remove(worker);
                assignedTargets.Remove(worker);
                lastDronePositions.Remove(worker);
            }
        }

        private void HandleBuildingSpawn(BuildingSpawnEvent evt)
        {
            if (evt.Building == null || evt.Building.Owner != aiOwner) return;

            bool isCommandPost = commandPostSO != null
                ? evt.Building.UnitSO?.Name == commandPostSO.Name
                : commandPostPrefab != null && evt.Building.name.StartsWith(commandPostPrefab.name);

            if (isCommandPost)
            {
                // Ensure we don't double-register
                if (activeNodes.Any(n => n.CommandPost == evt.Building)) return;

                AINode node = new AINode { CommandPost = evt.Building };
                RefreshNodeResources(node);
                activeNodes.Add(node);
                // // // Debug.Log($"[AI] {aiOwner} Node created around: {evt.Building.name} with {node.ResourcesInRange.Count} resources.");
            }
        }

        private void HandleBuildingDeath(BuildingDeathEvent evt)
        {
            activeNodes.RemoveAll(n => n.CommandPost == evt.Building);
        }

        private void RefreshNodeResources(AINode node)
        {
            node.ResourcesInRange.Clear();
            Vector3 pos = node.CommandPost.transform.position;
            var supplies = GatherableSupply.ActiveSupplies
                .Where(s => s != null && s.Amount > 0 && (s.transform.parent != null && s.transform.parent.GetComponent<PlanetGenerator>() != null)
                            && (!s.TryGetComponent<HiddenResource>(out var hr) || hr.IsDiscovered))
.Where(s => Vector3.Distance(s.transform.position, pos) <= nodeRadius)
                .OrderBy(s => Vector3.Distance(s.transform.position, pos));

            foreach (var s in supplies) node.ResourcesInRange.Add(s);
        }

        private List<BaseBuilding> GetBuildingsInScene()
        {
             return BaseBuilding.ActiveBuildings
                .Where(b => b != null && b.Owner == aiOwner && b.Progress.State != BuildingProgress.BuildingState.Destroyed)
                .ToList();
        }

        private void Tick()
        {
            // Update node list based on scene objects to handle any missed events or lag
            var sceneBuildings = GetBuildingsInScene();
            
            // Remove nodes whose buildings are gone
            int removed = activeNodes.RemoveAll(n => n.CommandPost == null || !sceneBuildings.Contains(n.CommandPost));
            // if (removed > 0) // // Debug.Log($"[AI] {aiOwner} removed {removed} nodes. Remaining: {activeNodes.Count}");

            // Add nodes for buildings that aren't tracked yet
            foreach (var b in sceneBuildings)
            {
                if (!activeNodes.Any(n => n.CommandPost == b))
                {
                    AINode node = new AINode { CommandPost = b };
                    RefreshNodeResources(node);
                    activeNodes.Add(node);
                    // // // Debug.Log($"[AI] {aiOwner} discovered existing building, added node: {b.name}");
                }
            }

            if (activeNodes.Count == 0)
            {
                if (aiOwner != Owner.Player1 && !isSpawning) SpawnCommandPost();
                return;
            }

            int availableBiomass = Player.Supplies.Biomass.TryGetValue(aiOwner, out int biomass) ? biomass : 0;
            bool allNodesMaxed = true;

            foreach (var node in activeNodes.ToList())
            {
                if (node.CommandPost == null)
                {
                    activeNodes.Remove(node);
                    continue;
                }

                node.ResourcesInRange.RemoveWhere(s => s == null || s.Amount <= 0);

                // Re-fill resources if the cell's pocket has more available within its radius
                if (node.ResourcesInRange.Count < 4)
                {
                    RefreshNodeResources(node);
                }

                int droneCount = node.Drones.Count(d => d != null);
                int targetCount = node.TargetDroneCount;

                if (droneCount < targetCount)
                {
                    allNodesMaxed = false;
                    if (aiOwner != Owner.Player1 && node.CommandPost.QueueSize < 5 && CanAfford(miningDroneUnitSO) && !IsInQueue(node.CommandPost, miningDroneUnitSO))
                    {
                        node.CommandPost.BuildUnlockable(miningDroneUnitSO);
                    }
                }

                ProcessNodeDrones(node);
                DispatchIdleDronesInNode(node);
            }

            if (aiOwner != Owner.Player1)
            {
                TryExpand();

                if (allNodesMaxed && activeNodes.Count < 20 && !isSpawning) 
                {
                    TryExpand();
                }
            }
        }

        private void ProcessNodeDrones(AINode node)
        {
            if (node.Drones.Count > 0)
            {
                // // // Debug.Log($"[AI Debug] Node at {node.CommandPost.transform.position} processing {node.Drones.Count} drones.");
            }

            foreach (Worker drone in node.Drones.ToList())
            {
                if (drone == null)
                {
                    node.Drones.Remove(null);
                    continue;
                }

                UnitCommands droneCmd = drone.GetCurrentCommand();
                if (drone.TryGetComponent(out NavMeshAgent na))
                {
                    if (!na.enabled)
                    {
                        na.enabled = true;
                    }

                    string graphName = "None";
                    if (drone.TryGetComponent(out BehaviorGraphAgent bga) && bga.Graph != null) graphName = bga.Graph.name;

                    // // // Debug.Log($"[AI Debug] Drone #{drone.UnitID} | Graph: {graphName} | Pos: {drone.transform.position} | Command: {droneCmd} | HasSupplies: {drone.HasSupplies} | IsIdle: {drone.IsIdle} | AgentEnabled: {na.enabled} | OnNavMesh: {na.isOnNavMesh}");

                    if (!na.isOnNavMesh && na.isActiveAndEnabled)
                    {
                        // Try to recover agent if it fell off or NavMesh changed under it
                        NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = na.agentTypeID, areaMask = NavMesh.AllAreas };
                        if (NavMesh.SamplePosition(drone.transform.position, out NavMeshHit hit, 10f, filter))
                        {
                            // // // Debug.Log($"[AI Debug] Drone #{drone.UnitID} is not on NavMesh. Warping to {hit.position}.");
                            na.Warp(hit.position);
                        }
                    }

                    if (na.isOnNavMesh)
                    {
                        if (droneCmd == UnitCommands.ReturnSupplies) na.stoppingDistance = 2.5f;
                        else if (droneCmd == UnitCommands.Gather) na.stoppingDistance = 1.5f;

                        // // // Debug.Log($"[AI Debug] Drone #{drone.UnitID} path stats: pathPending={na.pathPending}, hasPath={na.hasPath}, pathStatus={na.pathStatus}, remainingDistance={na.remainingDistance}, stoppingDistance={na.stoppingDistance}");

                        // Stuck detection: position unchanged between Ticks but agent has a path to travel.
                        // Skip if the BrainController is intentionally stationary (Gathering) or returning home.
                        bool brainIsStationary = drone.TryGetComponent(out WorkerBrainController wbc) &&
                            (wbc.CurrentState == WorkerBrainController.State.Gathering ||
                             wbc.CurrentState == WorkerBrainController.State.MovingToBase);

                        if (!brainIsStationary && droneCmd != UnitCommands.Stop && !na.pathPending)
                        {
                            if (lastDronePositions.TryGetValue(drone, out Vector3 lastPos))
                            {
                                float movementDist = Vector3.Distance(drone.transform.position, lastPos);
                                if (movementDist < 0.2f && na.remainingDistance > na.stoppingDistance + 0.5f)
                                {
                                    // // // Debug.Log($"[AI Debug] Drone #{drone.UnitID} stuck detected! (moved {movementDist}m, remaining {na.remainingDistance}m). Stopping drone.");
                                    drone.Stop();
                                }
                            }
                            lastDronePositions[drone] = drone.transform.position;
                        }
                        else
                        {
                            lastDronePositions.Remove(drone);
                        }

                        if (droneCmd == UnitCommands.ReturnSupplies && !drone.HasSupplies)
                        {
                            // // // Debug.Log($"[AI Debug] Drone #{drone.UnitID} finished returning supplies. Stopping drone.");
                            drone.Stop();
                        }
                    }
                }
            }
        }

        private Dictionary<Worker, float> lastCommandTime = new Dictionary<Worker, float>();

        private void DispatchIdleDronesInNode(AINode node)
        {
            foreach (Worker drone in node.Drones.ToList())
            {
                if (drone == null) continue;

                if (assignedTargets.TryGetValue(drone, out var currentTarget))
                {
                    if (currentTarget == null || currentTarget.Amount <= 0)
                    {
                        // // // Debug.Log($"[AI Debug] Drone #{drone.UnitID} target depleted. Clearing.");
                        assignedTargets.Remove(drone);
                        lastCommandTime.Remove(drone);
                    }
                    else
                    {
                        UnitCommands cmd = drone.GetCurrentCommand();
                        // Only re-send if drone is Stop/Move AND we haven't sent it in the last 2 seconds
                        bool needsCommand = cmd == UnitCommands.Stop || cmd == UnitCommands.Move;
                        bool cooldownOver = !lastCommandTime.ContainsKey(drone) || (Time.time - lastCommandTime[drone] > 2f);

                        if (needsCommand && cooldownOver)
                        {
                             // // // Debug.Log($"[AI Debug] Drone #{drone.UnitID} is {cmd}, re-sending Gather command to {currentTarget.name}");
                             drone.Gather(currentTarget);
                             lastCommandTime[drone] = Time.time;
                        }
                        continue; 
                    }
                }
                
                if (!assignedTargets.ContainsKey(drone) && IsDroneEligibleForAssignment(drone))
                {
                    HashSet<GatherableSupply> globallyTargeted = new HashSet<GatherableSupply>(assignedTargets.Values);
                    Vector3 queryPos = drone.transform.position;
                    GatherableSupply supply = FindNearestAvailableSupplyInNode(queryPos, node, globallyTargeted, drone.Agent.agentTypeID);
                    
                    if (supply != null)
                    {
                        assignedTargets[drone] = supply;
                        drone.GetComponent<WorkerBrainController>()?.SetHomeBase(node.CommandPost?.transform);
                        drone.Gather(supply);
                        lastCommandTime[drone] = Time.time;
                        // // // Debug.Log($"[AI] Reassigned drone #{drone.UnitID} -> {supply.name}");
                    }
                }
            }
        }

        private GatherableSupply FindNearestAvailableSupplyInNode(Vector3 position, AINode node, HashSet<GatherableSupply> excluded = null, int agentTypeId = -1)
        {
            var candidates = node.ResourcesInRange
                .Where(s => s != null && s.Amount > 0 && !s.IsBusy)
                .Where(s => excluded == null || !excluded.Contains(s))
                .Select(s => new { Supply = s, DistSq = (s.transform.position - position).sqrMagnitude })
                .OrderBy(x => x.DistSq)
                .Take(20).ToList();

            NavMeshQueryFilter filter = new NavMeshQueryFilter { areaMask = NavMesh.AllAreas };
            if (agentTypeId != -1) filter.agentTypeID = agentTypeId;

            foreach (var item in candidates)
            {
                Vector3 targetPos = item.Supply.transform.position;
                if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 15f, filter))
                {
                    targetPos = hit.position;
                }

                NavMeshPath path = new NavMeshPath();
                bool pathCalculated = NavMesh.CalculatePath(position, targetPos, filter, path);
                // // // Debug.Log($"[AI Debug] Checking path to {item.Supply.name} at {targetPos}. Calculated: {pathCalculated}, Status: {path.status}");
                
                if (pathCalculated)
                {
                    if (path.status == NavMeshPathStatus.PathComplete)
                    {
                        return item.Supply;
                    }
                    else if (path.status == NavMeshPathStatus.PathPartial && path.corners.Length > 0)
                    {
                        // Check 2D distance for air units to ignore vertical gap
                        Vector3 lastCorner = path.corners[path.corners.Length - 1];
                        Vector2 corner2D = new Vector2(lastCorner.x, lastCorner.z);
                        Vector2 target2D = new Vector2(targetPos.x, targetPos.z);
                        
                        if (Vector2.Distance(corner2D, target2D) <= 5f)
                        {
                            return item.Supply;
                        }
                    }
                }
            }
            return null;
        }

        private void TryExpand()
        {
            var allSupplies = GatherableSupply.ActiveSupplies
                .Where(s => s != null && s.Amount > 0 && (s.transform.parent != null && s.transform.parent.GetComponent<PlanetGenerator>() != null)).ToList();

            Vector3 bestPos = Vector3.zero;
            int maxNearby = 0;

            var currentBuildings = GetBuildingsInScene();

            foreach (var s in allSupplies.OrderBy(x => Random.value).Take(50))
            {
                Vector3 candidate = s.transform.position;
                
                // Check distance from all existing buildings in scene to enforce territorial separation
                if (currentBuildings.Any(b => Vector3.Distance(candidate, b.transform.position) < minNodeSpacing)) continue;
                
                int count = allSupplies.Count(other => Vector3.Distance(candidate, other.transform.position) <= nodeRadius);
                if (count >= 4 && count > maxNearby)
                {
                    maxNearby = count;
                    bestPos = candidate;
                }
            }

            if (maxNearby >= 4) SpawnCommandPostAt(bestPos);
            }

        private void SpawnCommandPostAt(Vector3 position)
        {
            if (commandPostPrefab == null || isSpawning) return;
            
            isSpawning = true;
            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 20f, filter)) position = hit.position;
            
            // // // Debug.Log($"[AI] {aiOwner} spawning Command Post at {position}");
            GameObject inst = Instantiate(commandPostPrefab, position, Quaternion.identity);

            if (inst.TryGetComponent(out BaseBuilding building))
            {
                building.enabled = true;
                building.Owner = aiOwner;
                building.CompleteConstruction();
                // // // Debug.Log($"[AI] {aiOwner} Command Post instantiated and construction completed.");
            }
            else
            {
                // Debug.LogError($"[AI] {aiOwner} spawned prefab {commandPostPrefab.name} is missing BaseBuilding component!");
            }
            
            StartCoroutine(RebakeAndUnlockSpawning());
        }

        private IEnumerator RebakeAndUnlockSpawning()
        {
            yield return new WaitForEndOfFrame();
            if (PlanetGenerator.Instance != null)
            {
                var surfaces = PlanetGenerator.Instance.GetComponents<Unity.AI.Navigation.NavMeshSurface>();
                var ops = new System.Collections.Generic.List<AsyncOperation>();
                foreach (var s in surfaces)
                {
                    if (s.navMeshData != null)
                    {
                        ops.Add(s.UpdateNavMesh(s.navMeshData));
                    }
                    else
                    {
                        // // // Debug.LogWarning($"[AI] NavMeshSurface on {s.gameObject.name} has no NavMeshData asset! Synchronous bake skipped to prevent hang.");
                    }
                }
                foreach (var op in ops)
                {
                    while (op != null && !op.isDone)
                    {
                        yield return null;
                    }
                }
            }
            // Hold the lock for half a second to let everything settle
            yield return new WaitForSeconds(0.5f);
            isSpawning = false;
        }

        private bool IsInQueue(BaseBuilding building, UnlockableSO so)
            => (building.QueueSize > 0 && building.SOBeingBuilt == so) || building.Queue.Contains(so);

        private void SpawnCommandPost()
        {
            Vector3 center = Vector3.zero;
            if (PlanetGenerator.Instance?.Config != null)
            {
                float w = PlanetGenerator.Instance.Config.MapWidth  * PlanetGenerator.Instance.CellSize;
                float h = PlanetGenerator.Instance.Config.MapHeight * PlanetGenerator.Instance.CellSize;
                center = new Vector3(w / 2f, 0f, h / 2f);
            }

            // Check if we already have a Command Post at the center to prevent "reappearing" flickering or duplicates
            if (GetBuildingsInScene().Any(b => Vector3.Distance(b.transform.position, center) < 10f))
            {
                return;
            }

            SpawnCommandPostAt(center);
        }

        private bool CanAfford(UnlockableSO unlockable)
        {
            if (unlockable?.Cost == null) return true;
            int cost = Mathf.FloorToInt(unlockable.Cost.Minerals * Player.Supplies.MineralsToBiomassRateStatic + unlockable.Cost.Gas * Player.Supplies.GasToBiomassRateStatic);
            int available = Player.Supplies.Biomass.TryGetValue(aiOwner, out int biomass) ? biomass : 0;

            // Logarithmic spending limit: starts at 50% (half the max) and drops off over time
            float logarithmicFraction = 0.5f - 0.05f * Mathf.Log(Time.time + 1f);
            logarithmicFraction = Mathf.Clamp(logarithmicFraction, 0f, 0.5f);

            int dynamicReserve = Mathf.FloorToInt((1f - logarithmicFraction) * available);
            int finalReserve = Mathf.Max(biomassReserve, dynamicReserve);
            return cost <= (available - finalReserve);
        }

        private bool IsDroneEligibleForAssignment(Worker drone)
        {
            if (drone == null) return false;
            return drone.IsIdle && !drone.HasSupplies;
        }
    }
}