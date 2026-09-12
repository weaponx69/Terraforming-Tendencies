using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Units;
using UnityEngine;
using GameDevTV.RTS.VisualScriptingStubs;

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
            EnhanceDepositBeacon();
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
            EnhanceDepositBeacon();
            Bus<ResourceDiscoveredEvent>.Raise(Owner.Unowned, new ResourceDiscoveredEvent(this));
        }

        /// <summary>Larger emissive pad + floating type label so mine spots read from camera height.</summary>
        private void EnhanceDepositBeacon()
        {
            transform.localScale = new Vector3(
                Mathf.Max(transform.localScale.x, 1.4f),
                Mathf.Max(transform.localScale.y, 0.15f),
                Mathf.Max(transform.localScale.z, 1.4f));

            if (TryGetComponent<MeshRenderer>(out var mr) && mr.material != null)
            {
                Color c = mr.material.color;
                c.a = 1f;
                mr.material.color = c;
                if (mr.material.HasProperty("_EmissionColor"))
                {
                    mr.material.EnableKeyword("_EMISSION");
                    mr.material.SetColor("_EmissionColor", c * 1.6f);
                }
            }

            if (transform.Find("DepositLabel") != null) return;
            var labelGo = new GameObject("DepositLabel");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = Vector3.up * 2.8f;
            var tmp = labelGo.AddComponent<TMPro.TextMeshPro>();
            tmp.text = string.IsNullOrEmpty(ResourceTypeName) ? "Deposit" : ResourceTypeName;
            tmp.fontSize = 5.5f;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.fontStyle = TMPro.FontStyles.Bold;
            tmp.color = new Color(1f, 0.95f, 0.55f, 1f);
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        }
    }
}
