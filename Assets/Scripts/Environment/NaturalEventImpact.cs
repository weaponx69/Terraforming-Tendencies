using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.VisualScriptingStubs;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Drives a single "natural event" (meteor strike, etc.).
    /// <para>
    /// Heavy logic (Physics.OverlapSphere + HashSet dedup in Impact(),
    /// Vector3.MoveTowards in Update(), warning-marker instantiation)
    /// stays in C#. VS reads <see cref="DamageRadius"/> and
    /// <see cref="HasImpacted"/> for reactive branching.
    /// </para>
    /// </summary>
    [IncludeInSettings(true)]
    public class NaturalEventImpact : MonoBehaviour, IDamageable
    {
        [Header("Impact")]
        [Tooltip("World-space radius of the damage area.")]
        [Inspectable]
        [SerializeField] private float damageRadius = 5f;
        [Tooltip("Damage dealt to every damageable inside the radius.")]
        [Inspectable]
        [SerializeField] private int damageAmount = 25;

        [Header("Destructibility")]
        [SerializeField] private int maxHealth = 50;
        [SerializeField] private int currentHealth = 50;
        [SerializeField] private GameObject destructionEffectPrefab;

        [Header("Falling")]
        [Tooltip("How high above the target the event starts before falling.")]
        [SerializeField] private float fallHeight = 40f;
        [Tooltip("Fall speed in units per second.")]
        [SerializeField] private float fallSpeed = 35f;

        [Header("Effects (optional)")]
        [SerializeField] private GameObject impactEffectPrefab;
        [SerializeField] private GameObject warningMarkerPrefab;

        private Vector3 impactPoint;
        private GameObject warningMarker;
        private bool hasImpacted;

        /// <summary>World-space radius of the damage area.</summary>
        [Inspectable]
        public float DamageRadius => damageRadius;

        /// <summary>Damage dealt per target inside the radius.</summary>
        [Inspectable]
        public int DamageAmount => damageAmount;

        /// <summary>True once the event has struck the ground.</summary>
        [Inspectable]
        public bool HasImpacted => hasImpacted;

        // IDamageable implementation
        public int MaxHealth => maxHealth;
        [Inspectable]
        public int CurrentHealth => currentHealth;
        public Transform Transform => transform;
        public Owner Owner => Owner.Unowned;

        /// <summary>Applies damage; destroys the event if health reaches zero.</summary>
        [Inspectable]
        public void TakeDamage(int damage)
        {
            if (hasImpacted) return;
            currentHealth -= damage;
            if (currentHealth <= 0)
            {
                Die();
            }
        }

        /// <summary>Destroys the event with effects. Callable from a Flow Graph.</summary>
        [Inspectable]
        public void Die()
        {
            if (hasImpacted) return;
            hasImpacted = true; // Prevent impact logic

            if (destructionEffectPrefab != null)
            {
                Instantiate(destructionEffectPrefab, transform.position, Quaternion.identity);
            }
            else if (impactEffectPrefab != null)
            {
                // Fallback to impact effect if no destruction effect is set
                Instantiate(impactEffectPrefab, transform.position, Quaternion.identity);
            }

            if (warningMarker != null) Destroy(warningMarker);
            Destroy(gameObject);
        }

        private void Start()
        {
            // Scale down meteor damage globally as requested (e.g., from 25 to 5)
            damageAmount = Mathf.Max(1, Mathf.RoundToInt(damageAmount * 0.2f));

            // The spawn position is the intended impact point on the ground.
            impactPoint = transform.position;

            // Optional ground telegraph so the player can react before the strike.
            if (warningMarkerPrefab != null)
            {
                warningMarker = Instantiate(
                    warningMarkerPrefab,
                    impactPoint + Vector3.up * 0.05f,
                    Quaternion.Euler(90f, 0f, 0f));
                warningMarker.transform.localScale = new Vector3(damageRadius * 2f, damageRadius * 2f, 1f);
            }

            // Lift the event up so it visibly falls onto the impact point.
            transform.position = impactPoint + Vector3.up * fallHeight;
        }

        private void Update()
        {
            if (hasImpacted) return;

            transform.position = Vector3.MoveTowards(
                transform.position, impactPoint, fallSpeed * Time.deltaTime);

            if ((transform.position - impactPoint).sqrMagnitude <= 0.01f)
            {
                Impact();
            }
        }

        private void Impact()
        {
            hasImpacted = true;

            // Dedupe so multi-collider targets only take damage once.
            HashSet<IDamageable> damaged = new();
            Collider[] hits = Physics.OverlapSphere(impactPoint, damageRadius);
            foreach (Collider hit in hits)
            {
                IDamageable target = hit.GetComponentInParent<IDamageable>();
                if (target != null && damaged.Add(target))
                {
                    target.TakeDamage(damageAmount);
                }
            }

            if (impactEffectPrefab != null)
            {
                Instantiate(impactEffectPrefab, impactPoint, Quaternion.identity);
            }



            if (warningMarker != null) Destroy(warningMarker);
            Destroy(gameObject);
        }

        private float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            Vector3 ap = p - a;
            float t = Vector3.Dot(ap, ab) / Vector3.Dot(ab, ab);
            if (float.IsNaN(t) || float.IsInfinity(t)) return Vector3.Distance(p, a);
            t = Mathf.Clamp01(t);
            Vector3 closestPoint = a + t * ab;
            return Vector3.Distance(p, closestPoint);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
            Vector3 center = Application.isPlaying ? impactPoint : transform.position;
            Gizmos.DrawWireSphere(center, damageRadius);
        }
    }
}
