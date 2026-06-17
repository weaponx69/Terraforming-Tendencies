using UnityEngine;
using UnityEngine.Serialization;

namespace GameDevTV.RTS.Units
{
    [CreateAssetMenu(fileName = "Builder Config", menuName = "Units/Builder Config", order = 10)]
    public class BuilderConfigSO : ScriptableObject
    {
        [Tooltip("Multiplier applied to construction speed. Higher means faster building. (e.g. 2.0 means twice as fast)")]
        [Range(0.1f, 10f)]
        [FormerlySerializedAs("<BuildSpeedMultiplier>k__BackingField")]
        [SerializeField] private float buildSpeedMultiplier = 1f;

        public float BuildSpeedMultiplier => buildSpeedMultiplier;
    }
}
