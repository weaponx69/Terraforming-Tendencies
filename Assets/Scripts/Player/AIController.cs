using System.Collections;
using System.Linq;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.TechTree;
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

        [Tooltip("The Air Transport SO. Leave blank — auto-discovered at runtime from whichever AbstractUnitSO prefab has a MiningDrone component.")]
        [SerializeField] private AbstractUnitSO miningDroneUnitSO;

        [Header("Economy Limits")]
        [SerializeField] private int maxDrones = 4;
        [SerializeField] private int biomassReserve = 0;

        [Header("Timing")]
        [Tooltip("How often (seconds) the AI evaluates its build decisions.")]
        [SerializeField] private float tickRate = 3f;

        [Tooltip("Seconds after scene load before the AI starts acting.")]
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

            // Self-wire: find the drone SO from any loaded AbstractUnitSO whose prefab
            // carries a MiningDrone component, so no Inspector wiring is needed.
            if (miningDroneUnitSO == null)
            {
                foreach (AbstractUnitSO so in Resources.FindObjectsOfTypeAll<AbstractUnitSO>())
                {
                    if (so.Prefab != null && so.Prefab.GetComponent<MiningDrone>() != null)
                    {
                        miningDroneUnitSO = so;
                        break;
                    }
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
            yield return new WaitForSeconds(startDelay);
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
                // Start mining immediately if the command post is already known;
                // otherwise WireExistingDrones() will pick it up next tick.
                if (commandPost != null)
                    drone.StartMining(commandPost.gameObject);
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

            if (commandPostSO != null && evt.Building.UnitSO?.Name == commandPostSO.Name)
                commandPost = evt.Building;
        }

        private void HandleBuildingDeath(BuildingDeathEvent evt)
        {
            if (evt.Building == commandPost)
                commandPost = null;
        }

        // ── Main tick ──────────────────────────────────────────────────────────
        private void Tick()
        {
            // Recover commandPost reference if lost
            if (commandPost == null)
            {
                commandPost = Object.FindObjectsByType<BaseBuilding>(FindObjectsInactive.Include)
                    .FirstOrDefault(b => b.Owner == aiOwner && b.UnitSO?.Name == commandPostSO.Name);

                if (commandPost == null) { SpawnCommandPost(); return; }

                // Wire any drones that spawned while commandPost was null
                WireExistingDrones();
            }

            int activeDrones = drones.Count(d => d != null);

            // Queue drones at the Command Post up to the cap
            if (activeDrones < maxDrones && commandPost.QueueSize < 5 && miningDroneUnitSO != null)
            {
                if (CanAfford(miningDroneUnitSO) && !IsInQueue(commandPost, miningDroneUnitSO))
                {
                    Debug.Log($"[AI] {aiOwner} queuing {miningDroneUnitSO.Name} ({activeDrones}/{maxDrones})");
                    commandPost.BuildUnlockable(miningDroneUnitSO);
                }
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        private void WireExistingDrones()
        {
            foreach (MiningDrone drone in drones)
            {
                if (drone == null) continue;
                // StartMining is a no-op when already running (isRunning guard inside MiningDrone)
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
            return cost + biomassReserve <= available;
        }
    }
}
