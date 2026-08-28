using System.Collections;
using UnityEngine;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.UI;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// Wires GameFlowManager turn-resolution events to gameplay systems.
    /// </summary>
    public class GameFlowPhaseController : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoSpawn()
        {
            if (FindAnyObjectByType<GameFlowPhaseController>() != null) return;
            var go = new GameObject("GameFlowPhaseController (auto)");
            go.AddComponent<GameFlowPhaseController>();
            DontDestroyOnLoad(go);
        }

        private void OnEnable()
        {
            StartCoroutine(SubscribeWhenReady());
        }

        private void OnDisable()
        {
            if (GameFlowManager.Instance == null) return;

            var flow = GameFlowManager.Instance;
            flow.OnTurnRecovery -= HandleRecovery;
            flow.OnTurnThreats -= HandleThreats;
            flow.OnTurnDraw -= HandleDraw;
            flow.OnTurnEvents -= HandleEvents;
            flow.OnTurnWinLoseCheck -= HandleWinLoseCheck;
        }

        private IEnumerator SubscribeWhenReady()
        {
            while (GameFlowManager.Instance == null)
            {
                yield return null;
            }

            var flow = GameFlowManager.Instance;
            flow.OnTurnRecovery += HandleRecovery;
            flow.OnTurnThreats += HandleThreats;
            flow.OnTurnDraw += HandleDraw;
            flow.OnTurnEvents += HandleEvents;
            flow.OnTurnWinLoseCheck += HandleWinLoseCheck;
        }

        private void HandleRecovery()
        {
            BuildingUpkeepManager.Instance?.TryTurnRecovery();
        }

        private void HandleThreats()
        {
            var eventManager = FindAnyObjectByType<NaturalEventManager>(FindObjectsInactive.Exclude);
            if (eventManager == null || GameFlowManager.Instance == null) return;
            eventManager.TryTurnThreat(GameFlowManager.Instance.currentTurn);
        }

        private void HandleDraw()
        {
            CardDeckController.Instance?.FillHand();
        }

        private void HandleEvents()
        {
            if (GameFlowManager.Instance == null) return;
            if (GameFlowManager.Instance.currentTurn % 3 != 0) return;

            Debug.Log($"[GameFlowPhaseController] Turn {GameFlowManager.Instance.currentTurn}: narrative event window.");
        }

        private void HandleWinLoseCheck()
        {
            GameOverManager.Instance?.EvaluateTurnConditions();
        }
    }
}
