using UnityEngine;
using UnityEngine.Serialization;

namespace GameDevTV.RTS.Units
{
    [CreateAssetMenu(fileName = "Attack Config", menuName = "Units/Attack Config", order = 7)]
    public class AttackConfigSO : ScriptableObject
    {
        [Header("General Combat")]
        [Tooltip("Maximum distance from which the unit can engage a target.")]
        [Range(0, 100)]
        [FormerlySerializedAs("<AttackRange>k__BackingField")]
        [SerializeField] private float attackRange = 1.5f;

        [Tooltip("Time in seconds between consecutive attacks.")]
        [Range(0.01f, 5.0f)]
        [FormerlySerializedAs("<AttackDelay>k__BackingField")]
        [SerializeField] private float attackDelay = 1.0f;

        [Tooltip("Raw damage dealt per hit or projectile impact.")]
        [Range(0, 250)]
        [FormerlySerializedAs("<Damage>k__BackingField")]
        [SerializeField] private int damage = 5;

        [Header("Projectiles")]
        [Tooltip("If true, the unit spawns projectiles rather than applying damage instantly.")]
        [FormerlySerializedAs("<HasProjectileAttacks>k__BackingField")]
        [SerializeField] private bool hasProjectileAttacks;

        [Header("Area of Effect (AOE)")]
        [Tooltip("If true, damage is applied in a radius around the impact point.")]
        [FormerlySerializedAs("<IsAreaOfEffect>k__BackingField")]
        [SerializeField] private bool isAreaOfEffect;

        [Tooltip("The radius of the explosion if IsAreaOfEffect is true.")]
        [Range(0, 20)]
        [FormerlySerializedAs("<AreaOfEffectRadius>k__BackingField")]
        [SerializeField] private float areaOfEffectRadius = 2.0f;

        [Tooltip("The maximum number of targets that can be hit by a single AOE blast.")]
        [Range(1, 20)]
        [FormerlySerializedAs("<MaxEnemiesHitPerAttack>k__BackingField")]
        [SerializeField] private int maxEnemiesHitPerAttack = 5;

        [Header("Targeting")]
        [Tooltip("Layers that this unit's attacks can interact with and damage.")]
        [FormerlySerializedAs("<DamageableLayers>k__BackingField")]
        [SerializeField] private LayerMask damageableLayers;

        // Public accessors for the direct-drive combat systems
        public float AttackRange => attackRange;
        public float AttackDelay => attackDelay;
        public int Damage => damage;
        public bool HasProjectileAttacks => hasProjectileAttacks;
        public bool IsAreaOfEffect => isAreaOfEffect;
        public float AreaOfEffectRadius => areaOfEffectRadius;
        public int MaxEnemiesHitPerAttack => maxEnemiesHitPerAttack;
        public LayerMask DamageableLayers => damageableLayers;

        public int CalculateAreaOfEffectDamage(Vector3 impactPoint, Vector3 targetPosition)
        {
            if (!isAreaOfEffect) return 0;

            float distance = Vector3.Distance(impactPoint, targetPosition);
            return Mathf.Clamp(Mathf.CeilToInt(damage * (1 - distance / areaOfEffectRadius)), 0, damage);
        }
    }
}
