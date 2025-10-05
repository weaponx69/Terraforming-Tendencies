using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    public class Placeholder : MonoBehaviour, IHideable
    {
        public Transform Transform => this == null ? null : transform;
        public bool IsVisible { get; private set; }
        public Owner Owner { get; set; }
        public GameObject ParentObject { get; set; }

        public event IHideable.VisibilityChangeEvent OnVisibilityChanged;

        private void Start()
        {
            Bus<PlaceholderSpawnEvent>.Raise(Owner, new PlaceholderSpawnEvent(this));
        }

        public void SetVisible(bool isVisible)
        {
            if (IsVisible != isVisible)
            {
                OnVisibilityChanged?.Invoke(this, isVisible);
            }

            IsVisible = isVisible;

            if (isVisible && ParentObject == null)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            Bus<PlaceholderDestroyEvent>.Raise(Owner, new PlaceholderDestroyEvent(this));
        }
    }
}