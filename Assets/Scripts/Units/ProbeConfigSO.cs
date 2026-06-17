using UnityEngine;
using UnityEngine.Serialization;

namespace GameDevTV.RTS.Units
{
    [CreateAssetMenu(fileName = "Probe Config", menuName = "Units/Probe Config", order = 11)]
    public class ProbeConfigSO : ScriptableObject
    {
        [Tooltip("Multiplier applied to anomaly analysis time. Higher means faster analysis.")]
        [Range(0.1f, 10f)]
        [FormerlySerializedAs("<AnalysisTimeMultiplier>k__BackingField")]
        [SerializeField] private float analysisTimeMultiplier = 1f;

        public float AnalysisTimeMultiplier => analysisTimeMultiplier;
    }
}
