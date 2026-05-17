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

namespace GameDevTV.RTS.Units
{
    public class AIController : MonoBehaviour
    {
        [Header("Owner")]
        [SerializeField] private Owner aiOwner = Owner.AI1;

        [Header("Spawn References")]
        [SerializeField] private GameObject commandPostPrefab;
        [SerializeField] private BuildingSO commandPostSO;
        [SerializeField] private AbstractUnitSO workerUnitSO;
        [SerializeField] private BuildingSO airportSO;

        [Header("Economy Limits")]
        [SerializeField] private int maxWorkers = 6;
        [SerializeField] private int biomassReserve = 0;

        [Header("Timing")]
        [Tooltip("How often (seconds) the AI evaluates its build/assign decisions.")]
        [SerializeField] private float tickRate = 3f;

        [Tooltip("Seconds after scene load before the AI starts acting.")]
        [SerializeField] private float startDelay = 2f;

        // ── Runtime state ──────────────────────────────────────────────────────────
        private BaseBuilding commandPost;
        private BaseBuilding airport;

        private readonly HashSet<Worker> workers = new();

        // Tracks which Workers are currently tasked with building something
        private readonly HashSet<Worker> busyBuilders = new();

        // ── Lifecycle ──────────────────────────────────────────────────────────────
        private void Awake()
        {
            Bus<UnitSpawnEvent>.OnEvent[aiOwner]    += HandleUnitSpawn;
            Bus<UnitDeathEvent>.OnEvent[aiOwner]    += HandleUnitDeath;
            Bus<BuildingSpawnEvent>.OnEvent[aiOwner] += HandleBuildingSpawn;
            Bus<BuildingDeathEvent>.OnEvent[aiOwner] += HandleBuildingDeath;
        }

        private void Start()
        {
            StartCoroutine(DelayedStart());
        }

        private void OnDestroy()
        {
            Bus<UnitSpawnEvent>.OnEvent[aiOwner]    -= HandleUnitSpawn;
            Bus<UnitDeathEvent>.OnEvent[aiOwner]    -= HandleUnitDeath;
            Bus<BuildingSpawnEvent>.OnEvent[aiOwner] -= HandleBuildingSpawn;
            Bus<BuildingDeathEvent>.OnEvent[aiOwner] -= HandleBuildingDeath;
        }

        // ── Boot ───────────────────────────────────────────────────────────────────
        private IEnumerator DelayedStart()
        {
            yield return new WaitForSeconds(startDelay);

            // Step 1: Spawn command post at map center (free, one-time)
            SpawnCommandPost();

            InvokeRepeating(nameof(Tick), tickRate, tickRate);
        }

        // ── Event handlers ─────────────────────────────────────────────────────────
        private void HandleUnitSpawn(UnitSpawnEvent evt)
        {
            if (evt.Unit.Owner != aiOwner) return;

            if (evt.Unit.TryGetComponent(out Worker worker))
            {
                workers.Add(worker);
            }
        }

        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            if (evt.Unit.TryGetComponent(out Worker worker))
            {
                workers.Remove(worker);
                busyBuilders.Remove(worker);
            }
        }

        private void HandleBuildingSpawn(BuildingSpawnEvent evt)
        {
            if (evt.Building == null) return;
            
            if (commandPostSO != null && evt.Building.UnitSO != null && evt.Building.UnitSO.Name == commandPostSO.Name)
            {
                if (evt.Building.Owner == aiOwner)
                {
                    Debug.Log($"[AI] {aiOwner} tracking Command Post: {evt.Building.name}");
                    commandPost = evt.Building;
                }
            }
            else if (airportSO != null && evt.Building.UnitSO != null && evt.Building.UnitSO.Name == airportSO.Name)
            {
                if (evt.Building.Owner == aiOwner)
                {
                    Debug.Log($"[AI] {aiOwner} tracking Airport: {evt.Building.name}");
                    airport = evt.Building;
                }
            }
        }

        private void HandleBuildingDeath(BuildingDeathEvent evt)
        {
            if (evt.Building == commandPost)
            {
                Debug.Log($"[AI] {aiOwner} Command Post destroyed!");
                commandPost = null;
            }
            if (evt.Building == airport)
            {
                Debug.Log($"[AI] {aiOwner} Airport destroyed!");
                airport = null;
            }
        }

        // ── Main tick ──────────────────────────────────────────────────────────────
        private void Tick()
        {
            int activeWorkers = workers.Count(w => w != null);

            Debug.Log($"[AI] {aiOwner} Tick: Population={activeWorkers}/{maxWorkers}, commandPost={(commandPost != null ? "Found" : "Missing")}");
            
            if (commandPost == null)
            {
                BaseBuilding[] allBuildings = Object.FindObjectsByType<BaseBuilding>(FindObjectsInactive.Include);
                Debug.Log($"[AI] {aiOwner} searching for Command Post in {allBuildings.Length} buildings.");
                foreach (var b in allBuildings)
                {
                    Debug.Log($"  - {b.name}: Owner={b.Owner}, UnitSO={b.UnitSO?.Name}");
                    if (b.Owner == aiOwner && b.UnitSO != null && b.UnitSO.Name == commandPostSO.Name)
                    {
                        Debug.Log($"[AI] {aiOwner} found existing Command Post: {b.name}");
                        commandPost = b;
                        break;
                    }
                }

                if (commandPost == null)
                {
                    Debug.Log($"[AI] {aiOwner} spawning new Command Post");
                    SpawnCommandPost();
                    return;
                }
            }

            if (activeWorkers < maxWorkers && commandPost.QueueSize < BaseBuilding_MaxQueueSize())
            {
                if (workerUnitSO != null)
                {
                    bool canAfford = CanAfford(workerUnitSO);
                    bool inQueue = IsUnitInQueue(commandPost, workerUnitSO);
                    
                    if (canAfford && !inQueue)
                    {
                        Debug.Log($"[AI] {aiOwner} queuing {workerUnitSO.Name} at {commandPost.name}");
                        commandPost.BuildUnlockable(workerUnitSO);
                        return;
                    }
                    else if (!canAfford)
                    {
                        Debug.Log($"[AI] {aiOwner} cannot afford {workerUnitSO.Name} (Biomass check failed).");
                    }
}
            }

            // ── Priority 3: Build Airport ──────────────────────────────────────
            if (airport == null && airportSO != null)
            {
                Worker idleBuilder = GetIdleWorker();
                if (idleBuilder != null && CanAfford(airportSO))
                {
                    Vector3 buildPos = FindBuildLocation(commandPost.transform.position, 20f);
                    if (buildPos != Vector3.zero)
                    {
                        Debug.Log($"[AI] {aiOwner} building Airport");
                        idleBuilder.Build(airportSO, buildPos);
                        busyBuilders.Add(idleBuilder);
                        return;
                    }
                }
            }

            // ── Priority 4: Assign idle workers (Drones) to gather ─────────────
            AssignIdleWorkersToGather();
        }

        private bool IsUnitInQueue(BaseBuilding building, UnlockableSO unitSO)
        {
            if (building == null) return false;
            if (building.SOBeingBuilt == unitSO) return true;
            return building.Queue.Any(u => u == unitSO);
        }

        private void StopIdleWorkers()
        {
            foreach (Worker worker in workers)
            {
                if (worker == null || worker.IsBuilding || busyBuilders.Contains(worker)) continue;
                
                if (worker.TryGetComponent(out BehaviorGraphAgent graph))
                {
                    if (graph.GetVariable("Command", out BlackboardVariable<UnitCommands> cmdVar))
                    {
                        if (cmdVar.Value != UnitCommands.Stop) 
                        {
                            worker.Stop();
                        }
                    }
                }
            }
        }

            // ── Helpers ────────────────────────────────────────────────────────────────

            private void AssignIdleWorkersToGather()
{
            if (commandPost == null) return;

            foreach (Worker worker in workers)
            {
                if (worker == null) continue;
                if (worker.IsBuilding || busyBuilders.Contains(worker)) continue;

                // Check blackboard to see if it's already doing something
                if (worker.TryGetComponent(out BehaviorGraphAgent graph))
                {
                    if (graph.GetVariable("Command", out BlackboardVariable<UnitCommands> cmdVar))
                    {
                        if (cmdVar.Value != UnitCommands.Stop) continue;
                    }
                }

                // If truly idle, find something to do
                if (worker.HasSupplies)
                {
                    worker.ReturnSupplies(commandPost.gameObject);
                }
                else
                {
                    GatherableSupply closest = FindClosestSupply(worker.transform.position);
                    if (closest != null)
                    {
                        worker.Gather(closest);
                    }
                }
            }
            }

            private GatherableSupply FindClosestSupply(Vector3 position)
            {
            GatherableSupply[] allSupplies = Object.FindObjectsByType<GatherableSupply>(FindObjectsInactive.Exclude);
            GatherableSupply closest = null;
            float minDist = float.MaxValue;
            foreach (var supply in allSupplies)
            {
                if (supply == null || supply.Amount <= 0) continue;

                // Ignore ghosts (which are visual only and have no colliders)
                if (supply.TryGetComponent(out GhostRock _)) continue;

                float dist = Vector3.Distance(position, supply.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = supply;
                }
            }
            return closest;
            }

            private void SpawnCommandPost()
        {
            if (commandPostPrefab == null) return;
            if (commandPost != null)       return;   // already exists

            // Find map center from PlanetGenerator
            Vector3 center = Vector3.zero;
            if (PlanetGenerator.Instance != null && PlanetGenerator.Instance.Config != null)
            {
                float w = PlanetGenerator.Instance.Config.MapWidth  * PlanetGenerator.Instance.CellSize;
                float h = PlanetGenerator.Instance.Config.MapHeight * PlanetGenerator.Instance.CellSize;
                center = new Vector3(w / 2f, 0f, h / 2f);
            }

            GameObject instance = Instantiate(commandPostPrefab, center, Quaternion.identity);

            // Set ownership before BaseBuilding.Start() fires
            if (instance.TryGetComponent(out AbstractCommandable commandable))
            {
                commandable.Owner = aiOwner;
            }

            // BaseBuilding.Start() raises BuildingSpawnEvent which populates commandPost via HandleBuildingSpawn
        }

        /// <summary>True when the AI can spend the Biomass equivalent of the given unlockable's cost.</summary>
        private bool CanAfford(UnlockableSO unlockable)
        {
            if (unlockable?.Cost == null) return true;

            int biomassCost = Mathf.FloorToInt(
                unlockable.Cost.Minerals * Player.Supplies.MineralsToBiomassRateStatic
              + unlockable.Cost.Gas      * Player.Supplies.GasToBiomassRateStatic
            );

            int available = Player.Supplies.Biomass.TryGetValue(aiOwner, out int b) ? b : 0;
            return biomassCost + biomassReserve <= available;
        }

        private Worker GetIdleWorker()
        {
            foreach (Worker w in workers)
            {
                if (w != null && !w.IsBuilding && !busyBuilders.Contains(w))
                    return w;
            }
            return null;
        }

/// <summary>
        /// Finds a flat NavMesh-valid build position near a given origin.
        /// Tries random offsets within the given radius.
        /// </summary>
        private Vector3 FindBuildLocation(Vector3 origin, float radius)
        {
            for (int i = 0; i < 20; i++)
            {
                Vector2 rnd    = Random.insideUnitCircle * radius;
                Vector3 candidate = origin + new Vector3(rnd.x, 0f, rnd.y);

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    return hit.position;
                }
            }
            return Vector3.zero;
        }

        /// <summary>Reflective-free max queue size from the BaseBuilding constant (hardcoded to match).</summary>
        private static int BaseBuilding_MaxQueueSize() => 5;
    }
}
