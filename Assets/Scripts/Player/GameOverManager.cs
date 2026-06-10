using System.Collections;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// Monitors the losing condition and fires the static OnGameOver event.
    ///
    /// Losing condition (either triggers Game Over):
    ///   A) Biomass for the target owner reaches 0 AND there is no remaining
    ///      way to earn more (no GatherableSupply with resources left AND no
    ///      active mining units on the map).
    ///   B) All GatherableSupply nodes are exhausted AND biomass is 0.
    /// </summary>
    public class GameOverManager : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────────
        [Header("Ownership")]
        [Tooltip("The owner whose Biomass is checked for the losing condition.")]
        [SerializeField] private Owner monitoredOwner = Owner.Player1;

        [Tooltip("AI owner whose mining units count toward 'recovery still possible'.")]
        [SerializeField] private Owner aiOwner = Owner.AI1;

        [Header("Timing")]
        #pragma warning disable 0414
                [Tooltip("Seconds between checks for the 'no recovery possible' condition.")]
                [SerializeField] private float checkInterval = 3f;
        #pragma warning restore 0414

        // ── Public event ───────────────────────────────────────────────────────────
        public enum GameOverReason { LifeSupport, Resources, MachineryFailure }

        public static event System.Action<GameOverReason> OnGameOver;
        public static event System.Action OnVictory;

        public static Owner MonitoredOwner { get; private set; } = Owner.Player1;

        // ── State ──────────────────────────────────────────────────────────────────
        private bool gameOverTriggered;
        private bool isPlanetGenerated;

        // ── Lifecycle ──────────────────────────────────────────────────────────────
        private void Awake()
        {
            MonitoredOwner = monitoredOwner;
        }

        private void OnEnable()
        {
            Supplies.OnBiomassChanged += HandleBiomassChanged;
            Supplies.OnIntegrityChanged += HandleIntegrityChanged;
            Bus<SupplyDepletedEvent>.RegisterForAll(HandleSupplyDepleted);
            PlanetGenerator.OnPlanetGenerated += HandlePlanetGenerated;
        }

        private void OnDisable()
        {
            Supplies.OnBiomassChanged -= HandleBiomassChanged;
            Supplies.OnIntegrityChanged -= HandleIntegrityChanged;
            Bus<SupplyDepletedEvent>.UnregisterForAll(HandleSupplyDepleted);
            PlanetGenerator.OnPlanetGenerated -= HandlePlanetGenerated;
        }

        private void HandlePlanetGenerated()
        {
            isPlanetGenerated = true;
            Debug.Log("[GameOverManager] Planet generation detected. Monitoring for loss conditions.");
            
            CancelInvoke(nameof(CheckNoRecovery));
            InvokeRepeating(nameof(CheckNoRecovery), 30f, checkInterval);
        }

        private void HandleIntegrityChanged(Owner owner, float value)
        {
            if (gameOverTriggered || !isPlanetGenerated) return;
            if (owner != monitoredOwner) return;
            if (value > 0f) return;
            if (Time.timeSinceLevelLoad < 30f) return;

            TriggerGameOver(GameOverReason.LifeSupport);
        }

        private void Update()
        {
            if (gameOverTriggered || !isPlanetGenerated) return;
            
            // Wait for colony to bootstrap
            if (Time.timeSinceLevelLoad < 30f) return;

            // 1. Authoritative Win Check
            bool oxygenComplete = Supplies.Oxygen != null
                && Supplies.Oxygen.TryGetValue(monitoredOwner, out float oxygen)
                && oxygen >= 99.9f;

            bool sectorsComplete = SectorManager.Instance != null && SectorManager.Instance.AreAllSectorsOccupied();

            if (oxygenComplete && sectorsComplete)
            {
                TriggerVictory();
                return;
            }

            // 2. Authoritative immediate check: Biomass at 0 is a hard loss.
            if (Supplies.Biomass != null && Supplies.Biomass.TryGetValue(monitoredOwner, out int b) && b <= 0)
            {
                Debug.Log($"[GameOverManager] Hard biomass check triggered. Biomass: {b}");
                TriggerGameOver(GameOverReason.Resources);
                return;
            }

            // 3. Check recovery potential BEFORE triggering life support failure.
            // If the player has 400 biomass, they can orbital drop a new Command Center even if everything else is gone.
            int biomass = 0;
            if (Supplies.Biomass != null)
            {
                biomass = Supplies.Biomass.TryGetValue(monitoredOwner, out int currentBiomass) ? currentBiomass : 0;
            }
bool canRebuild = (AnyMiningUnitsAlive() || biomass >= 400) && AnySupplyNodesRemain();

            // Loss Condition 1: Colony life support coverage collapsed.
            if (!AnyLifeSupportNodesRemain(monitoredOwner) && !canRebuild)
            {
                Debug.Log("[GameOverManager] No life support nodes remain and cannot rebuild. Colony collapsed.");
                TriggerGameOver(GameOverReason.LifeSupport);
                return;
            }

            // Loss Condition 2: Colony integrity depleted to 0.
            if (Supplies.Integrity != null
                && Supplies.Integrity.TryGetValue(monitoredOwner, out float integrity)
                && integrity <= 0f
                && !canRebuild)
            {
                Debug.Log("[GameOverManager] Integrity depleted to 0 and cannot rebuild.");
                TriggerGameOver(GameOverReason.LifeSupport);
            }
        }

        private bool AnyLifeSupportNodesRemain(Owner owner)
        {
            var nodes = Object.FindObjectsByType<LifeSupportNode>(FindObjectsInactive.Exclude);
            foreach (var node in nodes)
            {
                if (node.TryGetComponent<BaseBuilding>(out var b) && b.Owner == owner)
                {
                    if (b.Progress.State == BuildingProgress.BuildingState.Completed) return true;
                }
            }
            return false;
        }

        private void HandleVictory()
        {
            if (gameOverTriggered) return;
            TriggerVictory();
        }

        private void HandleBiomassChanged(Owner owner, int newValue)
        {
            if (gameOverTriggered || !isPlanetGenerated) return;
            if (owner != monitoredOwner) return;

            // Immediate loss if biomass hits 0, but only after bootstrapping.
            if (newValue <= 0 && Time.timeSinceLevelLoad > 30f)
            {
                TriggerGameOver(GameOverReason.Resources);
            }
        }

        private void HandleSupplyDepleted(SupplyDepletedEvent evt)
        {
            if (gameOverTriggered || !isPlanetGenerated) return;
            CheckNoRecovery();
        }

        private void CheckNoRecovery()
        {
            if (gameOverTriggered || !isPlanetGenerated) return;

            if (Supplies.Biomass == null)
            {
                Debug.LogWarning("[GameOverManager] Supplies.Biomass is null during recovery check.");
                return;
            }

            int biomass = Supplies.Biomass.TryGetValue(monitoredOwner, out int b) ? b : 0;
bool supplyNodesExist = AnySupplyNodesRemain();
            bool miningUnitsExist = AnyMiningUnitsAlive();

            // Logic Fix: Even if minerals exist on the map, if you have 0 workers and cannot afford to build a base (400 biomass), you lose.
            bool recoveryPossible = supplyNodesExist && (miningUnitsExist || biomass >= 400);

            if (!recoveryPossible)
            {
                Debug.Log($"[GameOverManager] Recovery check failed. Nodes Exist: {supplyNodesExist}, Drones Exist: {miningUnitsExist}, Biomass: {biomass}");
                TriggerGameOver(GameOverReason.Resources);
            }
        }

        private static bool AnySupplyNodesRemain()
        {
            GatherableSupply[] all = Object.FindObjectsByType<GatherableSupply>(FindObjectsInactive.Exclude);
            foreach (GatherableSupply gs in all)
            {
                if (gs != null && gs.Amount > 0) return true;
            }
            return false;
        }

        private bool AnyMiningUnitsAlive()
        {
            Worker[] workers = Object.FindObjectsByType<Worker>(FindObjectsInactive.Exclude);
            foreach (Worker w in workers)
            {
                if (w != null && (w.Owner == monitoredOwner || w.Owner == aiOwner)) return true;
            }

            MiningDrone[] drones = Object.FindObjectsByType<MiningDrone>(FindObjectsInactive.Exclude);
            foreach (MiningDrone d in drones)
            {
                if (d != null && d.TryGetComponent<AbstractUnit>(out var u)
                    && (u.Owner == monitoredOwner || u.Owner == aiOwner))
                    return true;
            }

            BaseBuilding[] buildings = Object.FindObjectsByType<BaseBuilding>(FindObjectsInactive.Exclude);
            foreach (var b in buildings)
            {
                if (b != null && (b.Owner == monitoredOwner || b.Owner == aiOwner))
                {
                    if (b.QueueSize > 0) return true; 
                }
            }
            return false;
        }

        public void TriggerGameOver(GameOverReason reason)
        {
            if (gameOverTriggered) return;
            gameOverTriggered = true;
            Debug.Log($"[GameOverManager] TriggerGameOver called. Reason: {reason}. Initializing shutdown sequence...");
            
            CancelInvoke(nameof(CheckNoRecovery));
            
            // Note: We don't stop ALL coroutines here to avoid breaking UI transitions 
            // that might be running on this object.
            
            if (OnGameOver != null)
            {
                try
                {
                    Debug.Log("[GameOverManager] Invoking OnGameOver event listeners...");
                    OnGameOver.Invoke(reason);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[GameOverManager] Exception in OnGameOver listener: {e}");
                }
            }
            else
            {
                Debug.LogError("[GameOverManager] CRITICAL: No listeners for OnGameOver event! The game over screen will not appear.");
            }
        }

        private void TriggerVictory()
        {
            if (gameOverTriggered) return;
            gameOverTriggered = true;
            Debug.Log("[GameOverManager] Victory triggered! All sectors occupied and oxygen complete.");
            CancelInvoke(nameof(CheckNoRecovery));
            StopAllCoroutines();
            OnVictory?.Invoke();
        }
    }
}
