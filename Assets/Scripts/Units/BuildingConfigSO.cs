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

        [Header("Upkeep & Generation (Per Second)")]
        [SerializeField] private float biomassUpkeep = 0f;
        [SerializeField] private float powerUpkeep = 0f;
        [SerializeField] private float oxygenUpkeep = 0f;

        [SerializeField] private int biomassGeneration = 0;
        [SerializeField] private float powerGeneration = 0f;

        [Header("Colony Housing")]
        [SerializeField] private int housingCapacity = 0;

        public int QueueSize => queueSize;
        public float LifeSupportRadius => lifeSupportRadius;
        public float BuildTimeMultiplier => buildTimeMultiplier;

        public float BiomassUpkeep => biomassUpkeep;
        public float PowerUpkeep => powerUpkeep;
        public float OxygenUpkeep => oxygenUpkeep;
        public int BiomassGeneration => biomassGeneration;
        public float PowerGeneration => powerGeneration;
        public int HousingCapacity => housingCapacity;
    }
}
