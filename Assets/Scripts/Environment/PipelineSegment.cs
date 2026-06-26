using UnityEngine;
using GameDevTV.RTS.Units;
using Unity.VisualScripting;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// A single segment of the energy pipeline. Carries health and damage
    /// callbacks; heavy collider setup and trigger logic stays in C#.
    /// </summary>
    [IncludeInSettings(true)]
    public class PipelineSegment : MonoBehaviour, IDamageable
    {
        private EnergyPipelineManager manager;
        private int segmentIndex;

        /// <summary>The EnergyPipelineManager that owns this segment.</summary>
        [Inspectable]
        public EnergyPipelineManager Manager => manager;

        [Inspectable] public int MaxHealth => 10;
        [Inspectable] public int CurrentHealth => 10;
        public Transform Transform => transform;
        public Owner Owner => Owner.Player1;

        /// <summary>
        /// Initializes this segment with its owning manager and index.
        /// Callable from a Flow Graph during pipeline construction.
        /// </summary>
        [Inspectable]
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

        /// <summary>Applies damage; destroys the segment if health reaches zero.</summary>
        [Inspectable]
        public void TakeDamage(int damage)
        {
            DieFromDamage();
        }

        /// <summary>Cancels the parent pipeline expansion.</summary>
        [Inspectable]
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
