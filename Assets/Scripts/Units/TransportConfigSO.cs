using UnityEngine;
using UnityEngine.Serialization;

namespace GameDevTV.RTS.Units
{
    [CreateAssetMenu(fileName = "Transport Config", menuName = "Units/Transport Config", order = 6)]
    public class TransportConfigSO : ScriptableObject
    {
        [Tooltip("The total carry capacity of this unit (if it is a transporter).")]
        [Range(0, 20)]
        [FormerlySerializedAs("<Capacity>k__BackingField")]
        [SerializeField] private int capacity;

        [Tooltip("The physical size of this unit when it needs to be carried by another.")]
        [FormerlySerializedAs("<Size>k__BackingField")]
        [SerializeField] private TransportSize size;

        [Tooltip("Layers where this unit is allowed to unload passengers.")]
        [FormerlySerializedAs("<SafeDropLayers>k__BackingField")]
        [SerializeField] private LayerMask safeDropLayers;

        [Header("Resource Cargo (Crawlers)")]
        [Tooltip("The maximum amount of Iron this unit can transport.")]
        [FormerlySerializedAs("<MaxIron>k__BackingField")]
        [SerializeField] private int maxIron;

        [Tooltip("The maximum amount of Regolith this unit can transport.")]
        [FormerlySerializedAs("<MaxRegolith>k__BackingField")]
        [SerializeField] private int maxRegolith;

        public int Capacity => capacity;
        public TransportSize Size => size;
        public LayerMask SafeDropLayers => safeDropLayers;
        public int MaxIron => maxIron;
        public int MaxRegolith => maxRegolith;

        public int GetTransportCapacityUsage() => size switch
        {
            TransportSize.Small => 1,
            TransportSize.Medium => 2,
            TransportSize.Large => 4,
            _ => int.MaxValue
        };

        public enum TransportSize
        {
            Small,
            Medium,
            Large,
            Untransportable
        }
    }
}