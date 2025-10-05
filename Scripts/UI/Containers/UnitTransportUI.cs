using System.Collections.Generic;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.Units;
using TMPro;
using UnityEngine;

namespace GameDevTV.RTS.UI.Containers
{
    public class UnitTransportUI : MonoBehaviour, IUIElement<ITransporter>
    {
        [SerializeField] private UIUnitButton[] loadedUnitButtons;
        [SerializeField] private TextMeshProUGUI capacityText;

        private ITransporter transporter;

        private const string CAPACITY_TEXT = "{0} / {1}";

        public void EnableFor(ITransporter item)
        {
            gameObject.SetActive(true);
            transporter = item;
            capacityText.SetText(string.Format(CAPACITY_TEXT, item.UsedCapacity, item.Capacity));

            List<ITransportable> loadedUnits = item.GetLoadedUnits();
            for(int i = 0 ; i < loadedUnitButtons.Length; i++)
            {
                if (i < loadedUnits.Count)
                {
                    int index = i;
                    loadedUnitButtons[i].EnableFor(loadedUnits[i], () => HandleClick(loadedUnits[index], index));
                }
                else
                {
                    loadedUnitButtons[i].Disable();
                }
            }
        }

        private void HandleClick(ITransportable transportable, int index)
        {
            if (transporter.Unload(transportable))
            {
                loadedUnitButtons[index].Disable();
            }
        }

        public void Disable()
        {
            gameObject.SetActive(false);
            foreach(UIUnitButton button in loadedUnitButtons)
            {
                button.Disable();
            }
        }
    }
}