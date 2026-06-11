using GameDevTV.RTS.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Containers
{
    public class UnitIconUI : MonoBehaviour, IUIElement<AbstractCommandable>
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI healthText;

        private AbstractCommandable commandable;

        private const string HEALTH_TEXT_FORMAT = "{0} / {1}";

        public void EnableFor(AbstractCommandable commandable)
        {
            if (commandable == null) return;
            this.commandable = commandable;
            gameObject.SetActive(true);
            healthText.SetText(string.Format(HEALTH_TEXT_FORMAT, commandable.CurrentHealth, commandable.MaxHealth));
            if (commandable.UnitSO != null)
            {
                icon.sprite = commandable.UnitSO.Icon;
            }

            bool isCrawler = commandable is FoundryCrawler;
            foreach (Transform child in transform)
            {
                string childName = child.name.ToLower();
                if (childName.Contains("damage") || childName.Contains("armor"))
                {
                    child.gameObject.SetActive(!isCrawler);
                }
            }

            commandable.OnHealthUpdated -= OnHealthUpdated;
            commandable.OnHealthUpdated += OnHealthUpdated;
        }

        public void Disable()
        {
            gameObject.SetActive(false);
            if (commandable != null)
            {
                commandable.OnHealthUpdated -= OnHealthUpdated;
                commandable = null;
            }
        }

        private void OnHealthUpdated(AbstractCommandable _, int lastHealth, int currentHealth)
        {
            healthText.SetText(string.Format(HEALTH_TEXT_FORMAT, currentHealth, commandable.MaxHealth));
        }
    }
}