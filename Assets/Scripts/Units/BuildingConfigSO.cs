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

        public int QueueSize { get => queueSize; set => queueSize = value; }
        public float LifeSupportRadius { get => lifeSupportRadius; set => lifeSupportRadius = value; }
        public float BuildTimeMultiplier { get => buildTimeMultiplier; set => buildTimeMultiplier = value; }

        public float BiomassUpkeep { get => biomassUpkeep; set => biomassUpkeep = value; }
        public float PowerUpkeep { get => powerUpkeep; set => powerUpkeep = value; }
        public float OxygenUpkeep { get => oxygenUpkeep; set => oxygenUpkeep = value; }
        public int BiomassGeneration { get => biomassGeneration; set => biomassGeneration = value; }
        public float PowerGeneration { get => powerGeneration; set => powerGeneration = value; }
        public int HousingCapacity { get => housingCapacity; set => housingCapacity = value; }
    }
}
