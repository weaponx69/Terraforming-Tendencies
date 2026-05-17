using System.Collections;
using System.Linq;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.TechTree;
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

        [Header("Timing")]
        [SerializeField] private float tickRate = 3f;
        [SerializeField] private float startDelay = 2f;

        // ── Runtime state ──────────────────────────────────────────────────────
        private BaseBuilding commandPost;
        private readonly System.Collections.Generic.HashSet<MiningDrone> drones = new();

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
        private void TryDiscoverDroneSO()
        {
            if (miningDroneUnitSO != null) return;

#if UNITY_EDITOR
            // In Editor / Play-in-Editor: scan all AbstractUnitSO assets on disk.
            string[] guids = AssetDatabase.FindAssets("t:AbstractUnitSO");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AbstractUnitSO so = AssetDatabase.LoadAssetAtPath<AbstractUnitSO>(path);
                if (so != null && so.Prefab != null && so.Prefab.GetComponent<MiningDrone>() != null)
                {
                    miningDroneUnitSO = so;
                    Debug.Log($"[AI] Auto-discovered drone SO: {so.Name} at {path}");
                    return;
                }
            }
#else
            // In a built player: scan everything currently in memory.
            foreach (AbstractUnitSO so in Resources.FindObjectsOfTypeAll<AbstractUnitSO>())
            {
                if (so.Prefab != null && so.Prefab.GetComponent<MiningDrone>() != null)
                {
                    miningDroneUnitSO = so;
                    return;
                }
            }
#endif
            Debug.LogWarning("[AI] Could not auto-discover a MiningDrone SO. Assign it manually on the AIController.");
        }

        // ── Boot ──────────────────────────────────────────────────────────────
        private IEnumerator DelayedStart()
        {
            yield return new WaitForSeconds(startDelay);

            Debug.Log($"[AI] {aiOwner} starting. commandPostPrefab={(commandPostPrefab != null ? commandPostPrefab.name : "NULL")}, commandPostSO={(commandPostSO != null ? commandPostSO.Name : "NULL")}, miningDroneUnitSO={(miningDroneUnitSO != null ? miningDroneUnitSO.Name : "NULL")}, maxDrones={maxDrones}");

            SpawnCommandPost();
            InvokeRepeating(nameof(Tick), tickRate, tickRate);
        }

        // ── Event handlers ─────────────────────────────────────────────────────
        private void HandleUnitSpawn(UnitSpawnEvent evt)
        {
            if (evt.Unit.Owner != aiOwner) return;

            if (evt.Unit.TryGetComponent(out MiningDrone drone))
            {
                drones.Add(drone);
                Debug.Log($"[AI] {aiOwner} drone spawned ({drones.Count}/{maxDrones}). commandPost={(commandPost != null ? commandPost.name : "null")}");
                if (commandPost != null)
                    drone.StartMining(commandPost.gameObject);
                else
                    Debug.LogWarning($"[AI] Drone spawned but commandPost is null — will wire on next tick.");
            }
        }

        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            if (evt.Unit.TryGetComponent(out MiningDrone drone))
                drones.Remove(drone);
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
                WireExistingDrones();
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
            // Recover commandPost if the event was missed
            if (commandPost == null)
            {
                commandPost = Object.FindObjectsByType<BaseBuilding>(FindObjectsInactive.Include)
                    .FirstOrDefault(b => b.Owner == aiOwner &&
                        (commandPostSO != null
                            ? b.UnitSO?.Name == commandPostSO.Name
                            : commandPostPrefab != null && b.name.StartsWith(commandPostPrefab.name)));

                if (commandPost == null)
                {
                    Debug.Log($"[AI] {aiOwner} Tick: no Command Post found — spawning.");
                    SpawnCommandPost();
                    return;
                }

                WireExistingDrones();
            }

            int activeDrones = drones.Count(d => d != null);
            int available    = Player.Supplies.Biomass.TryGetValue(aiOwner, out int b) ? b : 0;

            Debug.Log($"[AI] {aiOwner} Tick: drones={activeDrones}/{maxDrones}, biomass={available}, droneSOset={(miningDroneUnitSO != null)}, queueSize={commandPost.QueueSize}");

            if (miningDroneUnitSO == null)
            {
                Debug.LogWarning($"[AI] {aiOwner} miningDroneUnitSO is null — retrying discovery.");
                TryDiscoverDroneSO();
                return;
            }

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
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        private void WireExistingDrones()
        {
            if (commandPost == null) return;
            foreach (MiningDrone drone in drones)
            {
                if (drone == null) continue;
                drone.StartMining(commandPost.gameObject);
            }
        }

        private bool IsInQueue(BaseBuilding building, UnlockableSO so)
            => building.SOBeingBuilt == so || building.Queue.Contains(so);

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
            if (inst.TryGetComponent(out AbstractCommandable commandable))
                commandable.Owner = aiOwner;
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
            return afford;
        }
    }
}
