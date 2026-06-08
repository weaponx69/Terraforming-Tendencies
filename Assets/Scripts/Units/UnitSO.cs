using UnityEngine;
using UnityEngine.Serialization;

namespace GameDevTV.RTS.Units
{
    [CreateAssetMenu(fileName = "Unit", menuName = "Units/Unit")]
    public class UnitSO : AbstractUnitSO
    {
        [Header("Unit Type Configurations")]
        [Tooltip("The combat configuration for this unit (Range, Damage, etc.).")]
        [FormerlySerializedAs("<AttackConfig>k__BackingField")]
        [SerializeField] private AttackConfigSO attackConfig;

        [Tooltip("The transport configuration if this unit can carry other units.")]
        [FormerlySerializedAs("<TransportConfig>k__BackingField")]
        [SerializeField] private TransportConfigSO transportConfig;

        public AttackConfigSO AttackConfig 
        { 
            get => attackConfig;
            private set => attackConfig = value;
        }

        public TransportConfigSO TransportConfig
        {
            get => transportConfig;
            private set => transportConfig = value;
        }

        public override object Clone()
        {
            UnitSO copy = base.Clone() as UnitSO;

            copy.AttackConfig = AttackConfig == null ? null : Instantiate(AttackConfig);
            copy.TransportConfig = TransportConfig == null ? null : Instantiate(TransportConfig);
            copy.SightConfig = SightConfig == null ? null : Instantiate(SightConfig);

            return copy;
        }
    }
}