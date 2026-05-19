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
        /// <summary>Raised once when the game-over condition is confirmed.</summary>
        public static event System.Action OnGameOver;
        public static event System.Action OnVictory;

        // ── State ──────────────────────────────────────────────────────────────────
        private bool gameOverTriggered;
        private bool inGracePeriod;

        // ── Lifecycle ──────────────────────────────────────────────────────────────
        private void OnEnable()
        {
            Supplies.OnBiomassChanged += HandleBiomassChanged;
            Supplies.OnVictory += HandleVictory;
            Bus<SupplyDepletedEvent>.RegisterForAll(HandleSupplyDepleted);
        }

        private void OnDisable()
        {
            Supplies.OnBiomassChanged -= HandleBiomassChanged;
            Supplies.OnVictory -= HandleVictory;
            Bus<SupplyDepletedEvent>.UnregisterForAll(HandleSupplyDepleted);
        }

        private void Start()
        {
            InvokeRepeating(nameof(CheckNoRecovery), checkInterval, checkInterval);
        }

        // ── Event handlers ─────────────────────────────────────────────────────────

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
                TriggerGameOver();
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

        private void TriggerGameOver()
        {
            if (gameOverTriggered) return;
            gameOverTriggered = true;

            CancelInvoke(nameof(CheckNoRecovery));
            StopAllCoroutines();

            OnGameOver?.Invoke();
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
