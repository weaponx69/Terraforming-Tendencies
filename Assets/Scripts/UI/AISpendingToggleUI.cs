using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.UI
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class AISpendingToggleUI : MonoBehaviour, IPointerClickHandler
    {
        private TextMeshProUGUI labelText;

        private void Awake()
        {
            labelText = GetComponent<TextMeshProUGUI>();
        }

        private void Start()
        {
            UpdateVisuals();
        }

        private void Update()
        {
            // Hotkey 'K' toggles AI Spending
            if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
            {
                ToggleSpending();
            }

            UpdateVisuals();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            ToggleSpending();
        }

        private void ToggleSpending()
        {
            if (GreedyAIController.Instance != null)
            {
                GreedyAIController.Instance.AutoSpendEnabled = !GreedyAIController.Instance.AutoSpendEnabled;
                UpdateVisuals();
            }
        }

        private void UpdateVisuals()
        {
            if (GreedyAIController.Instance == null)
            {
                labelText.SetText("<b>AI Auto-Spend:</b> <color=#888888>N/A</color>");
                return;
            }

            if (GreedyAIController.Instance.AutoSpendEnabled)
            {
                labelText.SetText("<b>AI Auto-Spend:</b> <color=#00FF00>ON</color> <size=14>(Click/K)</size>");
            }
            else
            {
                labelText.SetText("<b>AI Auto-Spend:</b> <color=#FF0000>OFF</color> <size=14>(Click/K)</size>");
            }
        }
    }
}