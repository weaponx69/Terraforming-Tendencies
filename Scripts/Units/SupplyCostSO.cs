using GameDevTV.RTS.Environment;
using UnityEngine;

namespace GameDevTV.RTS.Units
{
    [CreateAssetMenu(fileName = "Supply Cost", menuName = "Supply Cost", order = 5)]
    public class SupplyCostSO : ScriptableObject
    {
        [field: SerializeField] public int Minerals { get; private set; } = 50;
        [field: SerializeField] public SupplySO MineralsSO { get; private set; }
        [field: SerializeField] public int Gas { get; private set; } = 0;
        [field: SerializeField] public SupplySO GasSO { get; private set; }
    }
}