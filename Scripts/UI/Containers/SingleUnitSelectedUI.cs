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
            unitName.SetText(item.UnitSO.Name);
        }

        public void Disable()
        {
            gameObject.SetActive(false);
        }
    }
}