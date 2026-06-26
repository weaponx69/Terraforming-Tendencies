using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Units;
using UnityEngine;
using Unity.VisualScripting;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Hides a GatherableSupply until its resource type is discovered.
    /// DiscoverySystem checks and EventBus raises stay in C#.
    /// </summary>
    [IncludeInSettings(true)]
    [RequireComponent(typeof(GatherableSupply))]
    public class HiddenResource : MonoBehaviour
    {
        /// <summary>True once this resource has been revealed to the player.</summary>
        [Inspectable]
        public bool IsDiscovered { get; private set; } = false;

        /// <summary>
        /// The resource type name (e.g., "Iron", "Regolith", "Minerals", "Gas").
        /// Set during scatter by PlanetGenerator. Used by DiscoverySystem for type-based reveal.
        /// </summary>
        [Inspectable]
        public string ResourceTypeName { get; set; } = "";

        private void Start()
        {
            // Rocks now act as the physical surface terrain, so we no longer make them invisible.
            // Fog of War will naturally hide them from the player's view if they are out of range.
        }

        /// <summary>
        /// Standard discover — checks DiscoverySystem to see if this resource type has been revealed.
        /// Callable from a Flow Graph when a probe scan completes.
        /// </summary>
        [Inspectable]
        public void Discover()
        {
            if (IsDiscovered) return;

            if (!string.IsNullOrEmpty(ResourceTypeName) && !DiscoverySystem.IsTypeDiscovered(ResourceTypeName))
            {
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
        /// Callable from a Flow Graph for starting-sector resources.
        /// </summary>
        [Inspectable]
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
