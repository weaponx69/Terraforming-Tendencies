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
    /// <summary>
    /// Automated controller for Owner.AI1.
    ///
    /// Priority build order (each step only fires if affordable):
    ///   1. Spawn Command Post at map center (free, one time).
    ///   2. Build enough Workers to keep gathering / building.
    ///   3. Build supporting infrastructure buildings (Airport, etc.).
    ///   4. Queue Mining Drone (Air Transport) units from the Airport.
    ///   5. Assign idle Mining Drones to gather resources.
    ///
    /// All spending is gated behind CanAfford() so the controller never goes
    /// into negative Biomass.
    /// </summary>
    public class AIController : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────────
        [Header("Owner")]
        [Tooltip("Which AI player this controller acts as.")]
        [SerializeField] private Owner aiOwner = Owner.AI1;

        [Header("Spawn References")]
        [Tooltip("The Command Post prefab — instantiated for free at map center on start.")]
        [SerializeField] private GameObject commandPostPrefab;

        [Tooltip("BuildingSO for the Command Post (needed to calculate costs and unlock deps).")]
        [SerializeField] private BuildingSO commandPostSO;

        [Tooltip("Worker UnitSO — queued from the Command Post.")]
        [SerializeField] private AbstractUnitSO workerUnitSO;

        [Tooltip("Airport BuildingSO — Workers build this before Mining Drones are available.")]
        [SerializeField] private BuildingSO airportSO;

        [Tooltip("Mining Drone (Air Transport) UnitSO — queued from the Airport.")]
        [SerializeField] private AbstractUnitSO miningDroneUnitSO;

        [Header("Economy Limits")]
        [Tooltip("Maximum concurrent Workers the AI will maintain.")]
        [SerializeField] private int maxWorkers = 3;

        [Tooltip("Maximum concurrent Mining Drones the AI will maintain.")]
        [SerializeField] private int maxMiningDrones = 4;

        [Tooltip("Biomass reserve to keep in hand before spending (safety buffer).")]
        [SerializeField] private int biomassReserve = 20;

        [Header("Timing")]
        [Tooltip("How often (seconds) the AI evaluates its build/assign decisions.")]
        [SerializeField] private float tickRate = 3f;

        [Tooltip("Seconds after scene load before the AI starts acting.")]
        [SerializeField] private float startDelay = 2f;

        // ── Runtime state ──────────────────────────────────────────────────────────
        private BaseBuilding commandPost;
        private BaseBuilding airport;

        private readonly HashSet<Worker>      workers      = new();
        private readonly HashSet<MiningDrone> miningDrones = new();

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
            else if (miningDroneUnitSO != null && evt.Unit.UnitSO != null
                     && evt.Unit.UnitSO.Equals(miningDroneUnitSO))
            {
                // Add MiningDrone component dynamically (keeps prefab clean)
                if (!evt.Unit.TryGetComponent(out MiningDrone existingDrone))
                {
                    existingDrone = evt.Unit.gameObject.AddComponent<MiningDrone>();
                }

                // Stop the BehaviorGraph so it doesn't fight the drone's NavMeshAgent control
                if (evt.Unit.TryGetComponent(out BehaviorGraphAgent graph))
                {
                    graph.SetVariableValue("Command", UnitCommands.Stop);
                }

                miningDrones.Add(existingDrone);

                if (commandPost != null)
                {
                    existingDrone.StartMining(commandPost.gameObject);
                }
            }
        }

        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            if (evt.Unit.TryGetComponent(out Worker worker))
            {
                workers.Remove(worker);
                busyBuilders.Remove(worker);
            }
            else if (evt.Unit.TryGetComponent(out MiningDrone drone))
            {
                miningDrones.Remove(drone);
            }
        }

        private void HandleBuildingSpawn(BuildingSpawnEvent evt)
        {
            if (evt.Building == null) return;

            if (commandPostSO != null && evt.Building.UnitSO.Equals(commandPostSO))
            {
                commandPost = evt.Building;
                // Notify any drones that may have spawned before the post finished
                foreach (MiningDrone drone in miningDrones)
                {
                    drone.SetCommandPost(commandPost.gameObject);
                }
            }
            else if (airportSO != null && evt.Building.UnitSO.Equals(airportSO))
            {
                airport = evt.Building;
            }
        }

        private void HandleBuildingDeath(BuildingDeathEvent evt)
        {
            if (evt.Building == commandPost)  commandPost = null;
            if (evt.Building == airport)      airport     = null;
        }

        // ── Main tick ──────────────────────────────────────────────────────────────
        private void Tick()
        {
            // ── Priority 1: Rebuild command post if destroyed ──────────────────
            if (commandPost == null)
            {
                SpawnCommandPost();
                return;
            }

            // ── Priority 2: Keep workers stocked ──────────────────────────────
            int activeWorkers = workers.Count(w => w != null);
            if (activeWorkers < maxWorkers && commandPost.QueueSize < BaseBuilding_MaxQueueSize())
            {
                if (workerUnitSO != null && CanAfford(workerUnitSO))
                {
                    commandPost.BuildUnlockable(workerUnitSO);
                    return;
                }
            }

            // ── Priority 3: Build Airport (if not yet built and affordable) ────
            if (airport == null && airportSO != null)
            {
                Worker idleBuilder = GetIdleWorker();
                if (idleBuilder != null && CanAfford(airportSO))
                {
                    Vector3 buildPos = FindBuildLocation(commandPost.transform.position, 20f);
                    if (buildPos != Vector3.zero)
                    {
                        idleBuilder.Build(airportSO, buildPos);
                        busyBuilders.Add(idleBuilder);
                        return;
                    }
                }
            }

            // ── Priority 4: Queue Mining Drones from Airport ───────────────────
            if (airport != null && miningDroneUnitSO != null)
            {
                int activeDrones = miningDrones.Count(d => d != null);
                if (activeDrones < maxMiningDrones && airport.QueueSize < BaseBuilding_MaxQueueSize())
                {
                    if (CanAfford(miningDroneUnitSO))
                    {
                        airport.BuildUnlockable(miningDroneUnitSO);
                        return;
                    }
                }
            }

            // ── Priority 5: Assign idle drones to mining ──────────────────────
            AssignIdleDronesToMine();
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

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

        private void AssignIdleDronesToMine()
        {
            if (commandPost == null) return;

            foreach (MiningDrone drone in miningDrones)
            {
                if (drone == null) continue;
                if (drone.State == MiningDrone.DroneState.Idle)
                {
                    drone.StartMining(commandPost.gameObject);
                }
            }
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
