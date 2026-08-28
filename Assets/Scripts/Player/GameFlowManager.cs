using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    public class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoSpawn()
        {
            if (FindAnyObjectByType<GameFlowManager>() != null) return;
            var go = new GameObject("GameFlowManager (auto)");
            go.AddComponent<GameFlowManager>();
            DontDestroyOnLoad(go);
        }

        public float idleTimerDuration = 2.0f;
        private float currentIdleTimer;
        private bool isTimerRunning;
        
        public int currentTurn = 1;

        // Events for Turn Resolution Phases
        public event Action OnTurnUpkeep;
        public event Action OnTurnRecovery;
        public event Action OnTurnIncome;
        public event Action OnTurnThreats;
        public event Action OnTurnDraw;
        public event Action OnTurnEvents;
        public event Action OnTurnMilestones;
        public event Action OnTurnWinLoseCheck;

        // Public event when a turn resolves completely
        public event Action<int> OnTurnResolved;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Start the timer
            ResetIdleTimer();
        }

        private void Update()
        {
            if (isTimerRunning)
            {
                currentIdleTimer -= Time.deltaTime;
                if (currentIdleTimer <= 0)
                {
                    ResolveTurn();
                }
            }
        }

        /// <summary>
        /// Call this method whenever the player performs an action (Deploy, Explore, Repair).
        /// </summary>
        public void PlayerActed()
        {
            ResetIdleTimer();
        }

        private void ResetIdleTimer()
        {
            currentIdleTimer = idleTimerDuration;
            isTimerRunning = true;
        }

        private void ResolveTurn()
        {
            isTimerRunning = false;

            // Sequential Turn Resolution
            
            // 1. Upkeep — Each deployed structure drains energy. Shortfall -> random structures degrade.
            OnTurnUpkeep?.Invoke();
            
            // 2. Recovery — Disabled structures tick down toward reactivation.
            OnTurnRecovery?.Invoke();
            
            // 3. Income — Gain base resources (energy, materials, research) + bonuses from upgrades/deposits.
            OnTurnIncome?.Invoke();
            
            // 4. Threats — Random chance (scales with turn number x planet danger). Damages resources or structures.
            OnTurnThreats?.Invoke();
            
            // 5. Draw — Discard hand, draw fresh hand. If deck empty, shuffle discard.
            OnTurnDraw?.Invoke();
            
            // 6. Events — Every 3rd turn: Discovery Draft. Otherwise: 25% chance of choice event.
            OnTurnEvents?.Invoke();
            
            // 7. Milestones — If terraform progress crosses a threshold, pause and open Upgrade Shop.
            OnTurnMilestones?.Invoke();
            
            // 8. Win/Lose Check — All targets met = victory. Max turns exceeded = defeat.
            OnTurnWinLoseCheck?.Invoke();

            OnTurnResolved?.Invoke(currentTurn);

            currentTurn++;
            
            // Restart the timer for the next turn
            ResetIdleTimer();
        }
    }
}
