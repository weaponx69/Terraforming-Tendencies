using UnityEngine;

namespace GameDevTV.RTS.Units
{
    [CreateAssetMenu(fileName = "Transport Config", menuName = "Units/Transport Config", order = 6)]
    public class TransportConfigSO : ScriptableObject
    {
        [field: SerializeField] public int Capacity { get; private set; }
        [field: SerializeField] public TransportSize Size { get; private set; }
        [field: SerializeField] public LayerMask SafeDropLayers { get; private set; }

        public int GetTransportCapacityUsage() => Size switch
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