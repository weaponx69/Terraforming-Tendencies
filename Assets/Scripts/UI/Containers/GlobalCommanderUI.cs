using GameDevTV.RTS.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Containers
{
    public class GlobalCommanderUI : MonoBehaviour, IUIElement<AbstractCommandable>
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI allowanceValueText;
        [SerializeField] private Slider allowanceSlider;

        private void Start()
        {
            if (allowanceSlider != null)
            {
                allowanceSlider.onValueChanged.AddListener(HandleSliderValueChanged);
            }
        }

        public void EnableFor(AbstractCommandable item)
        {
            gameObject.SetActive(true);
            if (titleText != null)
            {
                titleText.SetText("UNIVERSAL COMMAND CENTER");
            }
            UpdateSliderFromController();
        }

        public void Disable()
        {
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (gameObject.activeInHierarchy)
            {
                UpdateSliderFromController();
            }
        }

        private void UpdateSliderFromController()
        {
            if (GreedyAIController.Instance != null)
            {
                float currentAllowance = GreedyAIController.Instance.AISpendingAllowance;
                if (allowanceSlider != null && !Mathf.Approximately(allowanceSlider.value, currentAllowance))
                {
                    allowanceSlider.SetValueWithoutNotify(currentAllowance);
                }

                if (allowanceValueText != null)
                {
                    int percentage = Mathf.RoundToInt(currentAllowance * 100f);
                    // Color code based on spending level
                    string colorHex = percentage > 66 ? "#00FF00" : (percentage > 33 ? "#FFFF00" : "#FF5500");
                    allowanceValueText.SetText($"AI Budget Limit: <color={colorHex}>{percentage}%</color>");
                }
            }
            else
            {
                if (allowanceValueText != null)
                {
                    allowanceValueText.SetText("AI Budget Limit: <color=#888888>N/A</color>");
                }
            }
        }

        private void HandleSliderValueChanged(float value)
        {
            if (GreedyAIController.Instance != null)
            {
                GreedyAIController.Instance.AISpendingAllowance = value;
            }
        }

        private void OnDestroy()
        {
            if (allowanceSlider != null)
            {
                allowanceSlider.onValueChanged.RemoveListener(HandleSliderValueChanged);
            }
        }
    }
}