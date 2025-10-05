using UnityEngine;

namespace GameDevTV.RTS.Units
{
    public interface IAttacker
    {
        public Transform Transform { get; }
        public void Attack(IDamageable damageable);
        public void Attack(Vector3 location);
    }
}