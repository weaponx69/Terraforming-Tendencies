using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Components
{
    [RequireComponent(typeof(Button))]
    public class UIUnitButton : MonoBehaviour, IUIElement<ITransportable, UnityAction>
    {
        [SerializeField] private Image icon;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            Disable();
        }
        
        public void EnableFor(ITransportable item, UnityAction callback)
        {
            button.onClick.RemoveAllListeners();
            gameObject.SetActive(true);

            icon.sprite = item.Icon;
            button.onClick.AddListener(callback);
        }

        public void Disable()
        {
            button.onClick.RemoveAllListeners();
            gameObject.SetActive(false);
        }
    }
}