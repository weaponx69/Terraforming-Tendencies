using UnityEngine;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Environment
{
    public class PipelineSegment : MonoBehaviour, IDamageable
    {
        private EnergyPipelineManager manager;
        private int segmentIndex;

        public EnergyPipelineManager Manager => manager;

        public int MaxHealth => 10;
        public int CurrentHealth => 10;
        public Transform Transform => transform;
        public Owner Owner => Owner.Player1;

        public void Initialize(EnergyPipelineManager mgr, int index)
        {
            manager = mgr;
            segmentIndex = index;

            Collider col = GetComponent<Collider>();
            if (col == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
            }
            else
            {
                col.isTrigger = true;
                col.enabled = true;
            }
        }

        public void TakeDamage(int damage)
        {
            DieFromDamage();
        }

        public void Die()
        {
            if (manager != null)
            {
                manager.CancelExpansion();
            }
        }

        private void DieFromDamage()
        {
            if (manager != null)
            {
                manager.HandleSegmentDestroyed(segmentIndex);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<NaturalEventImpact>() != null)
            {
                DieFromDamage();
            }
        }
    }
}
