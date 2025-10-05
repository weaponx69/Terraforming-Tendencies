using UnityEngine;

namespace GameDevTV.RTS.Player
{
    public interface IHideable
    {
        public Transform Transform { get; }
        public bool IsVisible { get; }
        public void SetVisible(bool isVisible);

        public delegate void VisibilityChangeEvent(IHideable hideable, bool isVisible);
        public event VisibilityChangeEvent OnVisibilityChanged;
    }
}