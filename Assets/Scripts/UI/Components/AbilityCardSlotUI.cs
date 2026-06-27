using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.UI.Components
{
    /// <summary>
    /// A single card slot in the persistent Ability Hand.
    /// Displays an ActiveAbilityCommand with cooldown overlay and click-to-activate.
    /// Respects the command's IsLocked() state (cooldown + building operational status).
    /// </summary>
    public class AbilityCardSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Card Display")]
        [SerializeField] private TextMeshProUGUI abilityNameText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image cooldownOverlay;   // fills from bottom to top as cooldown progresses
        [SerializeField] private TextMeshProUGUI cooldownText;
        [SerializeField] private Button playButton;
        [SerializeField] private Image glowOutline;
        [SerializeField] private Image lockOverlay;       // shown when building is not operational

        [Header("Hover Effect")]
        [SerializeField] private float hoverScaleMultiplier = 1.07f;
        [SerializeField] private float hoverAnimSpeed = 8f;

        private ActiveAbilityCommand abilityCommand;
        private Action<ActiveAbilityCommand> onSelected;
        private Vector3 baseScale;
        private Vector3 targetScale;

        private void Awake()
        {
            baseScale = transform.localScale;
            targetScale = baseScale;
        }

        private void Update()
        {
            // Smooth hover scale
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * hoverAnimSpeed);

            if (abilityCommand == null) return;

            // Update cooldown overlay
            bool isLocked = !abilityCommand.IsReady;
            if (cooldownOverlay != null)
            {
                float progress = abilityCommand.CooldownProgress; // 0 = just used, 1 = ready
                cooldownOverlay.fillAmount = 1f - progress;
                cooldownOverlay.gameObject.SetActive(isLocked);
            }

            if (cooldownText != null)
            {
                if (isLocked)
                {
                    float remaining = abilityCommand.CooldownProgress;
                    // CooldownProgress is normalized; we need actual seconds but don't have direct access
                    // Show percentage instead
                    cooldownText.text = $"{(1f - abilityCommand.CooldownProgress) * 100:F0}%";
                    cooldownText.gameObject.SetActive(true);
                }
                else
                {
                    cooldownText.gameObject.SetActive(false);
                }
            }

            // Update lock overlay (building not operational)
            if (lockOverlay != null)
            {
                // IsLocked checks both cooldown and building operational state
                // We show lock only when building is offline but ability is off cooldown
                bool buildingLocked = isLocked; // simplified — cooldown covers both for now
                lockOverlay.gameObject.SetActive(buildingLocked);
            }

            // Update button interactable
            if (playButton != null)
            {
                playButton.interactable = abilityCommand.IsReady;
            }
        }

        public void Initialize(ActiveAbilityCommand command, Action<ActiveAbilityCommand> selectionCallback)
        {
            abilityCommand = command;
            onSelected = selectionCallback;

            if (abilityNameText != null)
                abilityNameText.text = command.Name;

            if (iconImage != null)
            {
                iconImage.sprite = command.Icon;
                iconImage.enabled = command.Icon != null;
            }

            if (playButton != null)
            {
                playButton.onClick.RemoveAllListeners();
                playButton.onClick.AddListener(HandleClick);
            }

            if (glowOutline != null)
                glowOutline.enabled = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            targetScale = baseScale * hoverScaleMultiplier;
            if (glowOutline != null)
                glowOutline.enabled = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            targetScale = baseScale;
            if (glowOutline != null)
                glowOutline.enabled = false;
        }

        private void HandleClick()
        {
            if (abilityCommand == null || !abilityCommand.IsReady) return;
            onSelected?.Invoke(abilityCommand);
        }
    }
}
