using UnityEngine;
using UnityEngine.Serialization;

namespace GameDevTV.RTS.Units
{
    [CreateAssetMenu(fileName = "Sight Config", menuName = "Units/Sight Config", order = 8)]
    public class SightConfigSO : ScriptableObject
    {
        [Tooltip("The radius of the area this unit reveals in the fog of war.")]
        [Range(1, 100)]
        [FormerlySerializedAs("<SightRadius>k__BackingField")]
        [SerializeField] private float sightRadius = 5;

        public float SightRadius => sightRadius;
    }
}