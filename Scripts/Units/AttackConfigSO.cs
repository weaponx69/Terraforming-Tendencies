using UnityEngine;

namespace GameDevTV.RTS.Units
{
    [CreateAssetMenu(fileName = "Attack Config", menuName = "Units/Attack Config", order = 7)]
    public class AttackConfigSO : ScriptableObject
    {
        [field: SerializeField] public float AttackRange { get; private set; } = 1.5f;
        [field: SerializeField] public float AttackDelay { get; private set; } = 1;
        [field: SerializeField] public int Damage { get; private set; } = 5;
        [field: SerializeField] public bool HasProjectileAttacks { get; private set; }
        [field: SerializeField] public bool IsAreaOfEffect { get; private set; }
        [field: SerializeField] public float AreaOfEffectRadius { get; private set; } = 2;
        [field: SerializeField] public int MaxEnemiesHitPerAttack { get; private set; } = 5;
        [field: SerializeField] public LayerMask DamageableLayers { get; private set; }

        public int CalculateAreaOfEffectDamage(Vector3 impactPoint, Vector3 targetPosition)
        {
            if (!IsAreaOfEffect) return 0;

            float distance = Vector3.Distance(impactPoint, targetPosition);

            return Mathf.Clamp(Mathf.CeilToInt(Damage * (1 - distance / AreaOfEffectRadius)), 0, Damage);
        }
    }
}
