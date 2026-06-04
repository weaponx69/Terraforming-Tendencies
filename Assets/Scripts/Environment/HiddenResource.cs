using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    [RequireComponent(typeof(GatherableSupply))]
    public class HiddenResource : MonoBehaviour
    {
        public bool IsDiscovered { get; private set; } = false;

        private void Start()
        {
            // Rocks now act as the physical surface terrain, so we no longer make them invisible.
            // Fog of War will naturally hide them from the player's view if they are out of range.
        }

        public void Discover()
        {
            if (IsDiscovered) return;
            
            IsDiscovered = true;
            Bus<ResourceDiscoveredEvent>.Raise(Owner.Unowned, new ResourceDiscoveredEvent(this));
            // // Debug.Log($"Rock surface charted at {transform.position}!");
        }
    }
}
