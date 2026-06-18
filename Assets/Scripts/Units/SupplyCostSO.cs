using GameDevTV.RTS.Environment;
using UnityEngine;

namespace GameDevTV.RTS.Units
{
    [CreateAssetMenu(fileName = "Supply Cost", menuName = "Supply Cost", order = 5)]
    public class SupplyCostSO : ScriptableObject
    {
        [field: SerializeField] public int Minerals { get; set; } = 50;
        [field: SerializeField] public SupplySO MineralsSO { get; set; }
        [field: SerializeField] public int Gas { get; set; } = 0;
        [field: SerializeField] public SupplySO GasSO { get; set; }
    }
}