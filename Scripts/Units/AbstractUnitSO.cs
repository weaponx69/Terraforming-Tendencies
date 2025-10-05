using GameDevTV.RTS.TechTree;
using UnityEngine;

namespace GameDevTV.RTS.Units
{
    public abstract class AbstractUnitSO : UnlockableSO
    {
        [field: SerializeField] public int Health { get; private set; } = 100;
        [field: SerializeField] public GameObject Prefab { get; private set; }
        [field: SerializeField] public UpgradeSO[] Upgrades { get; private set; }
        [field: SerializeField] public SightConfigSO SightConfig { get; protected set; }
    }
}