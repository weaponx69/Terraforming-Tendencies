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

        /// <summary>
        /// The resource type name (e.g., "Iron", "Regolith", "Minerals", "Gas").
        /// Set during scatter by PlanetGenerator. Used by DiscoverySystem for type-based reveal.
        /// </summary>
        public string ResourceTypeName { get; set; } = "";

        private void Start()
        {
            // Rocks now act as the physical surface terrain, so we no longer make them invisible.
            // Fog of War will naturally hide them from the player's view if they are out of range.
        }

        /// <summary>
        /// Standard discover — checks DiscoverySystem to see if this resource type has been revealed.
        /// If the type hasn't been discovered yet, the resource stays hidden.
        /// </summary>
        public void Discover()
        {
            if (IsDiscovered) return;

            // Check if this resource type has been discovered by the player
            if (!string.IsNullOrEmpty(ResourceTypeName) && !DiscoverySystem.IsTypeDiscovered(ResourceTypeName))
            {
                // Type not yet discovered — stay hidden
                return;
            }
            
            IsDiscovered = true;
            if (TryGetComponent<GatherableSupply>(out var supply))
            {
                supply.ToggleColliders(true);
                supply.SetVisible(true);
            }
            Bus<ResourceDiscoveredEvent>.Raise(Owner.Unowned, new ResourceDiscoveredEvent(this));
        }

        /// <summary>
        /// Force-discover this resource, bypassing the DiscoverySystem type check.
        /// Used for starting sector resources and for newly revealed types.
        /// </summary>
        public void ForceDiscover()
        {
            if (IsDiscovered) return;
            
            IsDiscovered = true;
            if (TryGetComponent<GatherableSupply>(out var supply))
            {
                supply.ToggleColliders(true);
                supply.SetVisible(true);
            }
            Bus<ResourceDiscoveredEvent>.Raise(Owner.Unowned, new ResourceDiscoveredEvent(this));
        }
    }
}
