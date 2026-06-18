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

        [Header("Specialized Logic Configurations")]
        [Tooltip("Configuration for the unit's movement speed.")]
        [FormerlySerializedAs("<MovementConfig>k__BackingField")]
        [SerializeField] private MovementConfigSO movementConfig;

        [Tooltip("Configuration for the unit's gathering rates.")]
        [FormerlySerializedAs("<GatherConfig>k__BackingField")]
        [SerializeField] private GatherConfigSO gatherConfig;

        [Tooltip("Configuration for the unit's building and repair rates.")]
        [FormerlySerializedAs("<BuilderConfig>k__BackingField")]
        [SerializeField] private BuilderConfigSO builderConfig;

        [Tooltip("Configuration for Probe scanning and analysis.")]
        [FormerlySerializedAs("<ProbeConfig>k__BackingField")]
        [SerializeField] private ProbeConfigSO probeConfig;

        [Tooltip("Configuration for Buildings (queue, life support, etc).")]
        [FormerlySerializedAs("<BuildingConfig>k__BackingField")]
        [SerializeField] private BuildingConfigSO buildingConfig;

        // Public accessors
        public int Health { get => health; set => health = value; }
        public GameObject Prefab { get => prefab; set => prefab = value; }
        public UpgradeSO[] Upgrades { get => upgrades; set => upgrades = value; }
        
        public SightConfigSO SightConfig 
        { 
            get => sightConfig;
            set => sightConfig = value;
        }

        public MovementConfigSO MovementConfig
        {
            get => movementConfig;
            set => movementConfig = value;
        }

        public GatherConfigSO GatherConfig
        {
            get => gatherConfig;
            set => gatherConfig = value;
        }

        public BuilderConfigSO BuilderConfig
        {
            get => builderConfig;
            set => builderConfig = value;
        }

        public ProbeConfigSO ProbeConfig
        {
            get => probeConfig;
            set => probeConfig = value;
        }

        public BuildingConfigSO BuildingConfig
        {
            get => buildingConfig;
            set => buildingConfig = value;
        }
    }
}