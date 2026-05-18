using System.Collections;
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
        [SerializeField] private int maxDrones = 4;
        [SerializeField] private int biomassReserve = 0;
        [Tooltip("Biomass granted to the AI at startup, independent of the player's starting biomass.")]
        [SerializeField] private int startingAIBiomass = 500;

        [Header("Timing")]
        [SerializeField] private float tickRate = 3f;
        [SerializeField] private float startDelay = 2f;

        // ── Runtime state ──────────────────────────────────────────────────────
        private BaseBuilding commandPost;
        private readonly System.Collections.Generic.HashSet<Worker> drones = new();

        // ── Lifecycle ──────────────────────────────────────────────────────────
        private void Awake()
        {
            Bus<UnitSpawnEvent>.OnEvent[aiOwner]     += HandleUnitSpawn;
            Bus<UnitDeathEvent>.OnEvent[aiOwner]     += HandleUnitDeath;
            Bus<BuildingSpawnEvent>.OnEvent[aiOwner] += HandleBuildingSpawn;
            Bus<BuildingDeathEvent>.OnEvent[aiOwner] += HandleBuildingDeath;

            TryDiscoverDroneSO();
        }

        private void Start() => StartCoroutine(DelayedStart());

        private void OnDestroy()
        {
            Bus<UnitSpawnEvent>.OnEvent[aiOwner]     -= HandleUnitSpawn;
            Bus<UnitDeathEvent>.OnEvent[aiOwner]     -= HandleUnitDeath;
            Bus<BuildingSpawnEvent>.OnEvent[aiOwner] -= HandleBuildingSpawn;
            Bus<BuildingDeathEvent>.OnEvent[aiOwner] -= HandleBuildingDeath;
        }

        // ── Auto-discovery ─────────────────────────────────────────────────────
        // Known asset path — same one used by SetupAIAutomation.cs
        private const string DRONE_SO_PATH = "Assets/Units/Air Transport/Air Transport.asset";

        private void TryDiscoverDroneSO()
        {
            if (miningDroneUnitSO != null) return;

#if UNITY_EDITOR
            // Primary: load directly from the known project path.
            AbstractUnitSO direct = UnityEditor.AssetDatabase.LoadAssetAtPath<AbstractUnitSO>(DRONE_SO_PATH);
            if (direct != null)
            {
                miningDroneUnitSO = direct;
                Debug.Log($"[AI] Drone SO loaded from known path: {direct.Name}");
                return;
            }

            // Fallback: scan concrete subtype assets (t:AbstractUnitSO fails; use concrete names).
            foreach (string typeName in new[] { "t:UnitSO", "t:BuildingSO" })
            {
                foreach (string guid in UnityEditor.AssetDatabase.FindAssets(typeName))
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    AbstractUnitSO so = UnityEditor.AssetDatabase.LoadAssetAtPath<AbstractUnitSO>(path);
                    if (so != null && so.Prefab != null && so.Prefab.GetComponent<Worker>() != null)
                    {
                        miningDroneUnitSO = so;
                        Debug.Log($"[AI] Drone SO discovered via scan: {so.Name} at {path}");
                        return;
                    }
                }
            }
#else
            // Built player: scan loaded memory.
            foreach (AbstractUnitSO so in Resources.FindObjectsOfTypeAll<AbstractUnitSO>())
            {
                if (so.Prefab != null && so.Prefab.GetComponent<Worker>() != null)
                {
                    miningDroneUnitSO = so;
                    return;
                }
            }
#endif
            Debug.LogWarning($"[AI] Could not find drone SO at '{DRONE_SO_PATH}'. Assign miningDroneUnitSO manually on the AIController Inspector.");
        }

        // ── Boot ──────────────────────────────────────────────────────────────
        private IEnumerator DelayedStart()
        {
            yield return new WaitForSeconds(startDelay);

            // Grant the AI its own starting biomass pool, independent of the player's.
            GrantStartingBiomass();

            Debug.Log($"[AI] {aiOwner} starting. commandPostPrefab={(commandPostPrefab != null ? commandPostPrefab.name : "NULL")}, commandPostSO={(commandPostSO != null ? commandPostSO.Name : "NULL")}, miningDroneUnitSO={(miningDroneUnitSO != null ? miningDroneUnitSO.Name : "NULL")}, maxDrones={maxDrones}, startingBiomass={startingAIBiomass}");

            SpawnCommandPost();
            InvokeRepeating(nameof(Tick), tickRate, tickRate);
        }

        /// <summary>
        /// Grants the AI its own starting biomass by writing directly to the Supplies.Biomass
        /// dictionary. Bypasses the SupplyEvent system entirely — no SO discovery needed.
        /// </summary>
        private void GrantStartingBiomass()
        {
            if (startingAIBiomass <= 0) return;
            if (Player.Supplies.Biomass == null)
            {
                Debug.LogWarning("[AI] Supplies.Biomass dictionary is null — Supplies may not have initialized yet.");
                return;
            }

            int current = Player.Supplies.Biomass.TryGetValue(aiOwner, out int b) ? b : 0;
            int total   = current + startingAIBiomass;
            Player.Supplies.Biomass[aiOwner] = total;
            Player.Supplies.RaiseBiomassChanged(aiOwner, total);
            Debug.Log($"[AI] {aiOwner} biomass set to {total} (was {current}, granted {startingAIBiomass}).");
        }

        // ── Event handlers ─────────────────────────────────────────────────────
        private void HandleUnitSpawn(UnitSpawnEvent evt)
        {
            if (evt.Unit.Owner != aiOwner) return;

            // AbstractUnit inherits UnitSO from AbstractCommandable — access it directly.
            if (miningDroneUnitSO == null || evt.Unit.UnitSO?.Name != miningDroneUnitSO.Name) return;

            if (evt.Unit is Worker worker)
            {
                // Set the stopping distance to a larger value to prevent the drone
                // from getting physically blocked by resource and Command Post colliders!
                if (worker.TryGetComponent(out NavMeshAgent navAgent))
                {
                    navAgent.stoppingDistance = 0.5f;

                    // Warp onto NavMesh if not already on it
                    if (!navAgent.isOnNavMesh)
                    {
                        if (NavMesh.SamplePosition(worker.transform.position, out NavMeshHit hit, 25f, NavMesh.AllAreas))
                        {
                            navAgent.Warp(hit.position);
                            Debug.Log($"[AI] Warped spawned drone {worker.name} onto NavMesh at {hit.position}");
                        }
                        else
                        {
                            Debug.LogWarning($"[AI] Failed to sample NavMesh position for drone {worker.name} at {worker.transform.position}");
                        }
                    }
                }

                drones.Add(worker);
                Debug.Log($"[AI] {aiOwner} drone tracked ({drones.Count}/{maxDrones}).");

                StartCoroutine(DeferredGatherAssignment(worker));
            }
        }

        private System.Collections.IEnumerator DeferredGatherAssignment(Worker worker)
        {
            yield return null; // Wait for one frame to let BehaviorGraphAgent initialize
            if (worker == null) yield break;

            GatherableSupply supply = FindNearestAvailableSupply(worker.transform.position);
            if (supply != null)
            {
                worker.Gather(supply);
                Debug.Log($"[AI] Initialized drone {worker.name} gather task: {supply.name} (deferred)");
            }
            else
            {
                Debug.LogWarning($"[AI] No resources found for new drone {worker.name} to gather.");
            }
        }

        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            if (evt.Unit is Worker worker)
                drones.Remove(worker);
        }

        private void HandleBuildingSpawn(BuildingSpawnEvent evt)
        {
            if (evt.Building == null || evt.Building.Owner != aiOwner) return;

            // Match on commandPostSO name if wired, otherwise fall back to checking
            // whether the prefab type has commandPostPrefab as the source.
            bool isCommandPost = commandPostSO != null
                ? evt.Building.UnitSO?.Name == commandPostSO.Name
                : commandPostPrefab != null && evt.Building.name.StartsWith(commandPostPrefab.name);

            if (isCommandPost)
            {
                commandPost = evt.Building;
                Debug.Log($"[AI] {aiOwner} Command Post tracked: {evt.Building.name}");
            }
        }

        private void HandleBuildingDeath(BuildingDeathEvent evt)
        {
            if (evt.Building == commandPost)
            {
                Debug.Log($"[AI] {aiOwner} Command Post destroyed.");
                commandPost = null;
            }
        }

        // ── Main tick ──────────────────────────────────────────────────────────
        private void Tick()
        {
            // Recover commandPost if the event was missed or if it's destroyed
            if (commandPost == null || commandPost.Progress.State == BuildingProgress.BuildingState.Destroyed)
            {
                commandPost = Object.FindObjectsByType<BaseBuilding>(FindObjectsInactive.Include)
                    .FirstOrDefault(b => b.Owner == aiOwner &&
                        b.Progress.State != BuildingProgress.BuildingState.Destroyed &&
                        (commandPostSO != null
                            ? b.UnitSO?.Name == commandPostSO.Name
                            : commandPostPrefab != null && b.name.StartsWith(commandPostPrefab.name)));

                if (commandPost == null)
                {
                    Debug.Log($"[AI] {aiOwner} Tick: no active Command Post found — spawning.");
                    SpawnCommandPost();
                    return;
                }
            }

            int activeDrones = drones.Count(d => d != null);
            int available    = Player.Supplies.Biomass.TryGetValue(aiOwner, out int b) ? b : 0;

            Debug.Log($"[AI] {aiOwner} Tick: drones={activeDrones}/{maxDrones}, biomass={available}, droneSOset={(miningDroneUnitSO != null)}, queueSize={commandPost.QueueSize}");

            // ── Diagnostic Logging for active drones ───────────────────────────
            foreach (Worker drone in drones.ToList())
            {
                if (drone == null) continue;

                string cmdState = "Unknown";
                if (drone.TryGetComponent(out BehaviorGraphAgent ga) && ga.GetVariable("Command", out BlackboardVariable<UnitCommands> cmd))
                {
                    cmdState = cmd.Value.ToString();
                }

                bool onMesh = false;
                string naDetails = "No NavMeshAgent";
                if (drone.TryGetComponent(out NavMeshAgent na))
                {
                    onMesh = na.isOnNavMesh;
                    naDetails = $"enabled={na.enabled}, isStopped={na.isStopped}, hasPath={na.hasPath}, pathStatus={na.pathStatus}, dest={na.destination}, velocity={na.velocity}, speed={na.speed}, remainingDist={na.remainingDistance}";
                }

                Debug.Log($"[AI Diagnostic] Drone={drone.name}, Command={cmdState}, isOnNavMesh={onMesh}, position={drone.transform.position}, {naDetails}");
            }

            if (miningDroneUnitSO == null)
            {
                Debug.LogWarning($"[AI] {aiOwner} miningDroneUnitSO is null — retrying discovery.");
                TryDiscoverDroneSO();
                return;
            }

            // 1. Spawning check
            if (activeDrones < maxDrones && commandPost.QueueSize < 5)
            {
                bool affordable = CanAfford(miningDroneUnitSO);
                bool inQueue    = IsInQueue(commandPost, miningDroneUnitSO);
                Debug.Log($"[AI] {aiOwner} drone check: affordable={affordable}, inQueue={inQueue}");

                if (affordable && !inQueue)
                {
                    Debug.Log($"[AI] {aiOwner} queuing {miningDroneUnitSO.Name} ({activeDrones}/{maxDrones})");
                    commandPost.BuildUnlockable(miningDroneUnitSO);
                }
            }

            // 2. Idle drone reassignment
            foreach (Worker drone in drones.ToList())
            {
                if (drone == null) continue;

                // Ensure stopping distance and NavMesh snapping
                if (drone.TryGetComponent(out NavMeshAgent navAgent))
                {
                    navAgent.stoppingDistance = 0.5f;
                    if (!navAgent.isOnNavMesh)
                    {
                        if (NavMesh.SamplePosition(drone.transform.position, out NavMeshHit hit, 25f, NavMesh.AllAreas))
                        {
                            navAgent.Warp(hit.position);
                            Debug.Log($"[AI] Warped idle drone {drone.name} onto NavMesh at {hit.position}");
                        }
                    }
                }

                if (drone.IsIdle)
                {
                    GatherableSupply supply = FindNearestAvailableSupply(drone.transform.position);
                    if (supply != null)
                    {
                        drone.Gather(supply);
                        Debug.Log($"[AI] Reassigned idle drone {drone.name} to gather {supply.name}.");
                    }
                }
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        private GatherableSupply FindNearestAvailableSupply(Vector3 position)
        {
            float closestDistance = float.MaxValue;
            GatherableSupply closestSupply = null;

            GatherableSupply[] supplies = Object.FindObjectsByType<GatherableSupply>(FindObjectsInactive.Exclude);
            foreach (GatherableSupply supply in supplies)
            {
                if (supply == null || supply.Amount <= 0) continue;

                float dist = Vector3.Distance(position, supply.transform.position);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    closestSupply = supply;
                }
            }

            return closestSupply;
        }

        private bool IsInQueue(BaseBuilding building, UnlockableSO so)
            // Guard SOBeingBuilt with QueueSize > 0: BaseBuilding never clears SOBeingBuilt
            // after a build finishes, so the stale reference would block re-queuing forever.
            => (building.QueueSize > 0 && building.SOBeingBuilt == so) || building.Queue.Contains(so);

        private void SpawnCommandPost()
        {
            if (commandPostPrefab == null || commandPost != null) return;

            Vector3 center = Vector3.zero;
            if (PlanetGenerator.Instance?.Config != null)
            {
                float w = PlanetGenerator.Instance.Config.MapWidth  * PlanetGenerator.Instance.CellSize;
                float h = PlanetGenerator.Instance.Config.MapHeight * PlanetGenerator.Instance.CellSize;
                center = new Vector3(w / 2f, 0f, h / 2f);
            }

            Debug.Log($"[AI] {aiOwner} spawning Command Post at {center}");
            GameObject inst = Instantiate(commandPostPrefab, center, Quaternion.identity);
            if (inst.TryGetComponent(out BaseBuilding building))
            {
                building.Owner = aiOwner;
                building.CompleteConstruction();
                commandPost = building;
            }
            else if (inst.TryGetComponent(out AbstractCommandable commandable))
            {
                commandable.Owner = aiOwner;
            }
            }

        private bool CanAfford(UnlockableSO unlockable)
        {
            if (unlockable?.Cost == null) return true;
            int cost = Mathf.FloorToInt(
                unlockable.Cost.Minerals * Player.Supplies.MineralsToBiomassRateStatic
              + unlockable.Cost.Gas      * Player.Supplies.GasToBiomassRateStatic
            );
            int available = Player.Supplies.Biomass.TryGetValue(aiOwner, out int b) ? b : 0;
            bool afford   = cost + biomassReserve <= available;
            if (!afford)
                Debug.Log($"[AI] Cannot afford {unlockable.Name}: costs {unlockable.Cost.Minerals}min+{unlockable.Cost.Gas}gas = {cost} biomass, have {available} (reserve={biomassReserve})");
            return afford;
        }
    }
}
