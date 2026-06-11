using GameDevTV.RTS.Units;
using TMPro;
using UnityEngine;

namespace GameDevTV.RTS.UI.Containers
{
    public class SingleUnitSelectedUI : MonoBehaviour, IUIElement<AbstractCommandable>
    {
        [SerializeField] private TextMeshProUGUI unitName;
        
        public void EnableFor(AbstractCommandable item)
        {
            gameObject.SetActive(true);
            unitName.SetText(ResolveDisplayName(item));

            bool isCrawler = item is FoundryCrawler;
            foreach (Transform child in transform)
            {
                string childName = child.name.ToLower();
                if (childName.Contains("damage") || childName.Contains("armor"))
                {
                    child.gameObject.SetActive(!isCrawler);
                }
            }
        }

        // Buildings such as Command Posts are renamed with a unique suffix (e.g. "Command Post #2")
        // when they complete construction. Prefer that unique GameObject name so the player can tell
        // multiple identical buildings apart. Fall back to the UnitSO name, and finally to the
        // GameObject name (e.g. the Universal Command Center / GlobalCommander has no UnitSO).
        private string ResolveDisplayName(AbstractCommandable item)
        {
            if (item.name.Contains("#"))
            {
                return item.name;
            }

            return item.UnitSO != null ? item.UnitSO.Name : item.name;
        }

        public void Disable()
        {
            gameObject.SetActive(false);
        }
    }
}