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
    ///
    /// "No remaining mining capability" means:
    ///   - Zero GatherableSupply objects still have Amount > 0
    ///   - Zero Worker or MiningDrone units are alive for the AI owner
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
        [Tooltip("Seconds between checks for the 'no recovery possible' condition.")]
        [SerializeField] private float checkInterval = 3f;

        [Tooltip("Grace period (seconds) after biomass first hits 0 before triggering game over. "
               + "Prevents instant loss from a momentary dip.")]
        [SerializeField] private float gracePeriod = 5f;

        // ── Public event ───────────────────────────────────────────────────────────
        /// <summary>Why the run ended in failure, so the UI can show an appropriate message.</summary>
        public enum GameOverReason
        {
            /// <summary>Colony life support collapsed (integrity depleted to 0).</summary>
            LifeSupport,
            /// <summary>No biomass left and no way to gather more.</summary>
            Resources
        }

        /// <summary>Raised once when the game-over condition is confirmed.</summary>
        public static event System.Action<GameOverReason> OnGameOver;
        public static event System.Action OnVictory;

        public static Owner MonitoredOwner { get; private set; } = Owner.Player1;

        // ── State ──────────────────────────────────────────────────────────────────
        private bool gameOverTriggered;
        private bool inGracePeriod;

        // ── Lifecycle ──────────────────────────────────────────────────────────────
        private void Awake()
        {
            MonitoredOwner = monitoredOwner;
        }

        private void OnEnable()
        {
            Supplies.OnBiomassChanged += HandleBiomassChanged;
            Supplies.OnVictory += HandleVictory;
            Supplies.OnIntegrityDepleted += HandleIntegrityDepleted;
            // Subscribe directly to the change events as the authoritative win/loss detection.
            // (Supplies' own internal relays are unreliable, so we evaluate thresholds here.)
            Supplies.OnIntegrityChanged += HandleIntegrityChanged;
            Supplies.OnOxygenChanged += HandleOxygenChanged;
            Bus<SupplyDepletedEvent>.RegisterForAll(HandleSupplyDepleted);
        }

        private void OnDisable()
        {
            Supplies.OnBiomassChanged -= HandleBiomassChanged;
            Supplies.OnVictory -= HandleVictory;
            Supplies.OnIntegrityDepleted -= HandleIntegrityDepleted;
            Supplies.OnIntegrityChanged -= HandleIntegrityChanged;
            Supplies.OnOxygenChanged -= HandleOxygenChanged;
            Bus<SupplyDepletedEvent>.UnregisterForAll(HandleSupplyDepleted);
        }

        /// <summary>
        /// Authoritative loss check: colony integrity (life support) depleted to 0.
        /// Fires immediately (past the brief startup guard), alongside the biomass loss.
        /// </summary>
        private void HandleIntegrityChanged(Owner owner, float value)
        {
            if (gameOverTriggered) return;
            if (owner != monitoredOwner) return;
            if (value > 0f) return;
            // Avoid an instant loss at startup before the colony has spawned in.
            if (Time.timeSinceLevelLoad < 5f) return;

            TriggerGameOver(GameOverReason.LifeSupport);
        }

        /// <summary>Authoritative win check: oxygen sustainability reached 100%.</summary>
        private void HandleOxygenChanged(Owner owner, float value)
        {
            if (gameOverTriggered) return;
            if (owner != monitoredOwner) return;
            if (value >= 100f)
            {
                TriggerVictory();
            }
        }

        private void Start()
        {
            InvokeRepeating(nameof(CheckNoRecovery), 10f, checkInterval); // Wait 10s for initial spawn
        }

        /// <summary>
        /// Authoritative win/loss polling. Reads the shared Supplies state directly each frame
        /// (rather than relying on event delivery) so the conditions are robust and immediate:
        ///   WIN  — oxygen sustainability reaches 100%.
        ///   LOSS — colony integrity (life support) depletes to 0.
        /// The biomass/no-recovery loss continues to work alongside this via CheckNoRecovery.
        /// </summary>
        private void Update()
        {
            if (gameOverTriggered) return;

            // Win: oxygen sustainability at 100% AND all sectors occupied.
            bool oxygenComplete = Supplies.Oxygen != null
                && Supplies.Oxygen.TryGetValue(monitoredOwner, out float oxygen)
                && oxygen >= 100f;

            bool sectorsComplete = SectorManager.Instance != null && SectorManager.Instance.AreAllSectorsOccupied();

            if (oxygenComplete && sectorsComplete)
            {
                TriggerVictory();
                return;
            }

            // Loss Condition 1: Colony life support coverage collapsed.
// If there are no active LifeSupportNodes, the colony cannot survive.
            // Brief startup guard avoids an instant loss before the starting base is completed.
            if (Time.timeSinceLevelLoad >= 5f)
            {
                if (!AnyLifeSupportNodesRemain(monitoredOwner))
                {
                    TriggerGameOver(GameOverReason.LifeSupport);
                    return;
                }

                // Loss Condition 2: Colony integrity (total HP) depleted to 0.
                if (Supplies.Integrity != null
                    && Supplies.Integrity.TryGetValue(monitoredOwner, out float integrity)
                    && integrity <= 0f)
                {
                    TriggerGameOver(GameOverReason.LifeSupport);
                }
            }
        }

        private bool AnyLifeSupportNodesRemain(Owner owner)
        {
            var nodes = Object.FindObjectsByType<LifeSupportNode>(FindObjectsInactive.Exclude);
            foreach (var node in nodes)
            {
                if (node.TryGetComponent<BaseBuilding>(out var b) && b.Owner == owner)
                {
                    // Only count completed buildings; ghosts/under-construction buildings 
                    // haven't activated their life support yet.
                    if (b.Progress.State == BuildingProgress.BuildingState.Completed)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // ── Event handlers ─────────────────────────────────────────────────────────

        private void HandleIntegrityDepleted()
        {
            if (gameOverTriggered) return;
            
            // Check if we actually have units before failing. 
            // If integrity is 0 because of no units, don't fail immediately at start.
            if (Time.timeSinceLevelLoad < 5f) return;
            
            // Integrity hitting 0 means colony life support has collapsed. Fire immediately.
            TriggerGameOver(GameOverReason.LifeSupport);
        }

        private void HandleVictory()
        {
            if (gameOverTriggered) return;
            TriggerVictory();
        }

        /// <summary>Every time Player1 biomass changes, check if it just hit 0.</summary>
        private void HandleBiomassChanged(Owner owner, int newValue)
        {
            if (gameOverTriggered) return;
            if (owner != monitoredOwner) return;

            if (newValue <= 0 && !inGracePeriod)
            {
                StartCoroutine(GracePeriodCoroutine());
            }
        }

        /// <summary>Every time a supply node is exhausted, run an immediate full check.</summary>
        private void HandleSupplyDepleted(SupplyDepletedEvent evt)
        {
            if (gameOverTriggered) return;
            CheckNoRecovery();
        }

        // ── Condition checks ───────────────────────────────────────────────────────

        /// <summary>
        /// Full check: is it impossible to ever earn more Biomass?
        /// Triggers game over when:
        ///   biomass == 0  AND  no supply nodes remain  AND  no mining units alive.
        /// </summary>
        private void CheckNoRecovery()
        {
            if (gameOverTriggered) return;

            int biomass = Supplies.Biomass.TryGetValue(monitoredOwner, out int b) ? b : 0;
            if (biomass > 0) return;   // still have resources — no problem yet

            bool supplyNodesExist = AnySupplyNodesRemain();
            bool miningUnitsExist = AnyMiningUnitsAlive();

            if (!supplyNodesExist && !miningUnitsExist)
            {
                TriggerGameOver(GameOverReason.Resources);
            }
        }

        private IEnumerator GracePeriodCoroutine()
        {
            inGracePeriod = true;
            yield return new WaitForSeconds(gracePeriod);
            inGracePeriod = false;

            // After grace, do a full check
            CheckNoRecovery();
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        private static bool AnySupplyNodesRemain()
        {
            GatherableSupply[] all = FindObjectsByType<GatherableSupply>(FindObjectsInactive.Exclude);
            foreach (GatherableSupply gs in all)
            {
                if (gs.Amount > 0) return true;
            }
            return false;
        }

        private bool AnyMiningUnitsAlive()
        {
            // Workers belonging to either owner
            Worker[] workers = FindObjectsByType<Worker>(FindObjectsInactive.Exclude);
            foreach (Worker w in workers)
            {
                if (w.Owner == monitoredOwner || w.Owner == aiOwner) return true;
            }

            // MiningDrones (added at runtime to Air Transport units)
            MiningDrone[] drones = FindObjectsByType<MiningDrone>(FindObjectsInactive.Exclude);
            foreach (MiningDrone d in drones)
            {
                if (d.GetComponent<AbstractUnit>() is AbstractUnit u
                    && (u.Owner == monitoredOwner || u.Owner == aiOwner))
                    return true;
            }

            return false;
        }

        private void TriggerGameOver(GameOverReason reason)
        {
            if (gameOverTriggered) return;
            gameOverTriggered = true;

            CancelInvoke(nameof(CheckNoRecovery));
            StopAllCoroutines();

            OnGameOver?.Invoke(reason);
        }

        private void TriggerVictory()
        {
            if (gameOverTriggered) return;
            gameOverTriggered = true;

            CancelInvoke(nameof(CheckNoRecovery));
            StopAllCoroutines();

            OnVictory?.Invoke();
        }
        }
        }
