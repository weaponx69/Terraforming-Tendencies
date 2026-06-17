using UnityEngine;
using UnityEngine.Serialization;

namespace GameDevTV.RTS.Units
{
    [CreateAssetMenu(fileName = "Gather Config", menuName = "Units/Gather Config", order = 9)]
    public class GatherConfigSO : ScriptableObject
    {
        [Tooltip("Multiplier applied to the gathering time. Higher means faster gathering. (e.g. 1.5 means 50% faster)")]
        [Range(0.1f, 10f)]
        [FormerlySerializedAs("<GatherRateMultiplier>k__BackingField")]
        [SerializeField] private float gatherRateMultiplier = 1f;

        public float GatherRateMultiplier => gatherRateMultiplier;
    }
}
