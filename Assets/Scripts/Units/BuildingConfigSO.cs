using UnityEngine;
using UnityEngine.Serialization;

namespace GameDevTV.RTS.Units
{
    [CreateAssetMenu(fileName = "Building Config", menuName = "Units/Building Config", order = 12)]
    public class BuildingConfigSO : ScriptableObject
    {
        [Tooltip("Queue size for Command Post production.")]
        [Range(1, 10)]
        [FormerlySerializedAs("<QueueSize>k__BackingField")]
        [SerializeField] private int queueSize = 1;

        [Tooltip("Life support radius extension provided by bio-domes.")]
        [Range(0f, 100f)]
        [FormerlySerializedAs("<LifeSupportRadius>k__BackingField")]
        [SerializeField] private float lifeSupportRadius = 10f;

        [Tooltip("Build time reduction multiplier.")]
        [Range(0.1f, 10f)]
        [FormerlySerializedAs("<BuildTimeMultiplier>k__BackingField")]
        [SerializeField] private float buildTimeMultiplier = 1f;

        public int QueueSize => queueSize;
        public float LifeSupportRadius => lifeSupportRadius;
        public float BuildTimeMultiplier => buildTimeMultiplier;
    }
}
