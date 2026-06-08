using GameDevTV.RTS.TechTree;
using UnityEngine;
using UnityEngine.Serialization;

namespace GameDevTV.RTS.Units
{
    public abstract class AbstractUnitSO : UnlockableSO
    {
        [Header("Base Stats")]
        [Tooltip("Maximum health of the unit.")]
        [Range(1, 1000)]
        [FormerlySerializedAs("<Health>k__BackingField")]
        [SerializeField] private int health = 100;

        [Header("Visuals & Assets")]
        [Tooltip("The actual GameObject spawned in the world.")]
        [FormerlySerializedAs("<Prefab>k__BackingField")]
        [SerializeField] private GameObject prefab;

        [Header("Logic Configurations")]
        [Tooltip("List of upgrades that can be applied to this unit.")]
        [FormerlySerializedAs("<Upgrades>k__BackingField")]
        [SerializeField] private UpgradeSO[] upgrades;

        [Tooltip("Configuration for the unit's line-of-sight and fog-of-war reveal radius.")]
        [FormerlySerializedAs("<SightConfig>k__BackingField")]
        [SerializeField] private SightConfigSO sightConfig;

        // Public accessors
        public int Health => health;
        public GameObject Prefab => prefab;
        public UpgradeSO[] Upgrades => upgrades;
        public SightConfigSO SightConfig 
        { 
            get => sightConfig;
            protected set => sightConfig = value; // Protected setter for cloning
        }
    }
}