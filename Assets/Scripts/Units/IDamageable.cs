using UnityEngine;

namespace GameDevTV.RTS.Units
{
    public interface IDamageable
    {
        public int MaxHealth { get; }
        public int CurrentHealth { get; }
        public Transform Transform { get; }
        public Owner Owner { get; }

        public void TakeDamage(int damage);
        public void Die();
    }
}
