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

        [Tooltip("Air Transport SO. Auto-discovered at runtime if left blank.")]
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
        private bool isSpawning = false;

        // ── Lifecycle ──────────────────────────────────────────────────────────
        private void Awake()
        {
            Bus<UnitSpawnEvent>.OnEvent[aiOwner]     += HandleUnitSpawn;
            Bus<UnitDeathEvent>.OnEvent[aiOwner]     += HandleUnitDeath;
            Bus<BuildingSpawnEvent>.OnEvent[aiOwner] += HandleBuildingSpawn;
            Bus<BuildingDeathEvent>.OnEvent[aiOwner] += HandleBuildingDeath;
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
            Debug.Log($"[AI] {aiOwner} starting DelayedStart. delay={startDelay}");
            yield return new WaitForSeconds(startDelay);
            GrantStartingBiomass();
            Debug.Log($"[AI] {aiOwner} biomass granted. startingBiomass={startingAIBiomass}");
            
            // Only spawn if nothing exists
            if (GetBuildingsInScene().Count == 0)
            {
                Debug.Log($"[AI] No buildings found, spawning initial Command Post.");
                SpawnCommandPost();
            }
            
            Debug.Log($"[AI] Starting Tick repetition every {tickRate}s.");
            InvokeRepeating(nameof(Tick), tickRate, tickRate);
        }

        private void GrantStartingBiomass()
        {
            if (startingAIBiomass <= 0) return;
            if (Player.Supplies.Biomass == null) return;

            int current = Player.Supplies.Biomass.TryGetValue(aiOwner, out int biomass) ? biomass : 0;
            int total   = current + startingAIBiomass;
            Player.Supplies.Biomass[aiOwner] = total;
            Player.Supplies.RaiseBiomassChanged(aiOwner, total);
        }

        // ── Event handlers ─────────────────────────────────────────────────────
        private void HandleUnitSpawn(UnitSpawnEvent evt)
        {
            if (evt.Unit.Owner != aiOwner) return;
            if (miningDroneUnitSO == null || evt.Unit.UnitSO?.Name != miningDroneUnitSO.Name) return;

            if (evt.Unit is Worker worker)
            {
                if (worker.TryGetComponent(out NavMeshAgent navAgent) && navAgent.isActiveAndEnabled)
                {
                    navAgent.stoppingDistance = 0.5f;
                    float baseSpeed = navAgent.speed;
                    navAgent.speed = baseSpeed * Random.Range(0.9f, 1.1f);
                    navAgent.avoidancePriority = Random.Range(30, 71);
                    navAgent.acceleration *= Random.Range(0.8f, 1.2f);

                    if (!navAgent.isOnNavMesh)
                    {
                        if (NavMesh.SamplePosition(worker.transform.position, out NavMeshHit hit, 25f, NavMesh.AllAreas))
                        {
                            navAgent.Warp(hit.position);
                        }
                    }
                }

                // Assign to closest node
                AINode node = activeNodes.OrderBy(n => Vector3.Distance(worker.transform.position, n.CommandPost.transform.position)).FirstOrDefault();
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
            if (node == null) yield break;

            // Try to find a resource that isn't globally assigned yet
            HashSet<GatherableSupply> excluded = new HashSet<GatherableSupply>(assignedTargets.Values);
            GatherableSupply supply = FindNearestAvailableSupplyInNode(worker.transform.position, node, excluded, worker.Agent.agentTypeID);
            
            if (supply != null)
            {
                assignedTargets[worker] = supply;
                worker.Gather(supply);
            }
        }

        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            if (evt.Unit is Worker worker)
            {
                foreach (var node in activeNodes) node.Drones.Remove(worker);
                assignedTargets.Remove(worker);
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
                Debug.Log($"[AI] {aiOwner} Node created around: {evt.Building.name} with {node.ResourcesInRange.Count} resources.");
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
                .Where(s => s != null && s.Amount > 0 && s.GetComponent<GhostRock>() == null)
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
            if (removed > 0) Debug.Log($"[AI] {aiOwner} removed {removed} nodes. Remaining: {activeNodes.Count}");

            // Add nodes for buildings that aren't tracked yet
            foreach (var b in sceneBuildings)
            {
                if (!activeNodes.Any(n => n.CommandPost == b))
                {
                    AINode node = new AINode { CommandPost = b };
                    RefreshNodeResources(node);
                    activeNodes.Add(node);
                    Debug.Log($"[AI] {aiOwner} discovered existing building, added node: {b.name}");
                }
            }

            if (activeNodes.Count == 0)
            {
                if (!isSpawning) SpawnCommandPost();
                return;
            }

            int availableBiomass = Player.Supplies.Biomass.TryGetValue(aiOwner, out int biomass) ? biomass : 0;
            bool allNodesMaxed = true;

            foreach (var node in activeNodes.ToList())
            {
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
                    if (node.CommandPost.QueueSize < 5 && CanAfford(miningDroneUnitSO) && !IsInQueue(node.CommandPost, miningDroneUnitSO))
                    {
                        node.CommandPost.BuildUnlockable(miningDroneUnitSO);
                    }
                }

                ProcessNodeDrones(node);
                DispatchIdleDronesInNode(node);
                }

                UpdateOxygenLevel();

                if (allNodesMaxed && activeNodes.Count < 20 && !isSpawning) 
                {
                TryExpand();
                }
                }

                private void UpdateOxygenLevel()
                {
                // Each node contributes 5% to the habitability
                int oxygenPercent = activeNodes.Count * 5;
                Player.Supplies.UpdateOxygen(aiOwner, oxygenPercent);
                }

                private void ProcessNodeDrones(AINode node)
                {
                foreach (Worker drone in node.Drones.ToList())
                {
                if (drone == null) continue;
                if (drone.TryGetComponent(out BehaviorGraphAgent ga) && ga.GetVariable("Command", out BlackboardVariable<UnitCommands> cmd))
                {
                    if (drone.TryGetComponent(out NavMeshAgent na) && na.isOnNavMesh)
                    {
                        if (cmd.Value == UnitCommands.ReturnSupplies) na.stoppingDistance = 2.5f;
                        else if (cmd.Value == UnitCommands.Gather) na.stoppingDistance = 1.5f;

                        if (cmd.Value != UnitCommands.Stop && na.velocity.sqrMagnitude < 0.01f && na.remainingDistance > na.stoppingDistance + 0.2f)
                            drone.Stop();

                        if (cmd.Value == UnitCommands.ReturnSupplies && na.remainingDistance <= na.stoppingDistance + 0.1f)
                        {
                            if (drone.HasSupplies) drone.ClearSupplies();
                            drone.Stop();
                        }
                    }
                }
                }
                }

        private void DispatchIdleDronesInNode(AINode node)
        {
            foreach (Worker drone in node.Drones.ToList())
            {
                if (drone == null) continue;

                // 1. Check if the drone already has a sticky assignment
                if (assignedTargets.TryGetValue(drone, out var currentTarget))
                {
                    // If target is gone or depleted, clear the assignment
                    if (currentTarget == null || currentTarget.Amount <= 0)
                    {
                        assignedTargets.Remove(drone);
                    }
                    else if (IsDroneEligibleForAssignment(drone))
                    {
                        // Drone is idle but still owns a valid resource. Go back to it!
                        drone.Gather(currentTarget);
                    }
                }
                
                // 2. If it still doesn't have an assignment and is eligible, find it a unique one
                if (!assignedTargets.ContainsKey(drone) && IsDroneEligibleForAssignment(drone))
                {
                    // Filter resources to find one that isn't globally assigned to ANY drone
                    HashSet<GatherableSupply> globallyTargeted = new HashSet<GatherableSupply>(assignedTargets.Values);
                    GatherableSupply supply = FindNearestAvailableSupplyInNode(drone.transform.position, node, globallyTargeted, drone.Agent.agentTypeID);
                    
                    if (supply != null)
                    {
                        assignedTargets[drone] = supply;
                        drone.Gather(supply);
                        Debug.Log($"[AI] Node {node.CommandPost.name} sticky dispatch: {drone.name} -> {supply.name}.");
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
                NavMeshPath path = new NavMeshPath();
                if (NavMesh.CalculatePath(position, item.Supply.transform.position, filter, path))
                    if (path.status == NavMeshPathStatus.PathComplete) return item.Supply;
            }
            return null;
        }

        private void TryExpand()
        {
            var allSupplies = GatherableSupply.ActiveSupplies
                .Where(s => s != null && s.Amount > 0 && s.GetComponent<GhostRock>() == null).ToList();

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
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 20f, NavMesh.AllAreas)) position = hit.position;
            
            Debug.Log($"[AI] Spawning expansion Command Post at {position}");
            GameObject inst = Instantiate(commandPostPrefab, position, Quaternion.identity);
            
            if (inst.TryGetComponent(out BaseBuilding building))
            {
                building.enabled = true;
                building.Owner = aiOwner;
                building.CompleteConstruction();
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
                        s.BuildNavMesh(); // Synchronous fallback if uninitialized
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
            return cost + biomassReserve <= available;
        }

        private bool IsDroneEligibleForAssignment(Worker drone)
        {
            if (drone == null) return false;
            return drone.IsIdle && !drone.HasSupplies;
        }
    }
}