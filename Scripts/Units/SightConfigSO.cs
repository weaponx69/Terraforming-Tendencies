using UnityEngine;

namespace GameDevTV.RTS.Units
{
    [CreateAssetMenu(fileName = "Sight Config", menuName = "Units/Sight Config", order = 8)]
    public class SightConfigSO : ScriptableObject
    {
        [field: SerializeField] public float SightRadius { get; private set; } = 5;
    }
}