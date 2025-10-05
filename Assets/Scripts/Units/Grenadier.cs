using System.Collections;
using Unity.Behavior;
using UnityEngine;

namespace GameDevTV.RTS.Units
{
    public class Grenadier : BaseMilitaryUnit
    {
        [SerializeField] private GameObject grenade;
        [SerializeField] private ParticleSystem explosionParticles;

        private Transform grenadeParent;
        private Vector3 defaultGrenadePosition;
        private Collider[] enemyColliders;

        protected override void Awake()
        {
            base.Awake();

            if (grenade == null || explosionParticles == null)
            {
                Debug.LogError($"Grenadier {name} is missing a grenade or explosion particles! They will not work!");
                return;
            }

            defaultGrenadePosition = grenade.transform.localPosition;
            grenadeParent = grenade.transform.parent;
        }

        protected override void Start()
        {
            base.Start();
            enemyColliders = new Collider[unitSO.AttackConfig.MaxEnemiesHitPerAttack];
        }

        // Animation Event
        public void OnThrowGrenade()
        {
            grenade.transform.SetParent(null);
            Vector3 startPosition = grenade.transform.position;
            Vector3 endPosition = grenade.transform.position + grenade.transform.forward * 3;
            IDamageable damageable = null;

            if (graphAgent.GetVariable("TargetGameObject", out BlackboardVariable<GameObject> targetVariable)
                && targetVariable != null && targetVariable.Value != null)
            {
                endPosition = targetVariable.Value.transform.position + Vector3.up;
                damageable = targetVariable.Value.GetComponent<IDamageable>();
            }
            else if (graphAgent.GetVariable("TargetLocation", out BlackboardVariable<Vector3> targetLocationVariable))
            {
                endPosition = targetLocationVariable;
            }

            StartCoroutine(AnimateGrenadeMovement(startPosition, endPosition, damageable));
        }

        private IEnumerator AnimateGrenadeMovement(Vector3 startPosition, Vector3 endPosition, IDamageable damageable)
        {
            float time = 0;
            const float speed = 2;
            while (time < 1)
            {
                grenade.transform.position = Vector3.Lerp(startPosition, endPosition, time);
                time += Time.deltaTime * speed;
                yield return null;
            }

            explosionParticles.transform.SetParent(null);
            explosionParticles.transform.position = endPosition;
            explosionParticles.Play();

            grenade.transform.SetParent(grenadeParent);
            grenade.transform.localPosition = defaultGrenadePosition;
            
            ApplyDamage(endPosition, damageable);
        }

        private void ApplyDamage(Vector3 endPosition, IDamageable damageable)
        {
            if (damageable != null && damageable.Transform != null)
            {
                damageable?.TakeDamage(unitSO.AttackConfig.Damage);
            }

            if (unitSO.AttackConfig.IsAreaOfEffect)
            {
                int hits = Physics.OverlapSphereNonAlloc(
                    endPosition,
                    unitSO.AttackConfig.AreaOfEffectRadius,
                    enemyColliders,
                    unitSO.AttackConfig.DamageableLayers
                );

                for (int i = 0; i < hits; i++)
                {
                    if (enemyColliders[i].TryGetComponent(out IDamageable nearbyDamageable)
                        && damageable != nearbyDamageable)
                    {
                        nearbyDamageable.TakeDamage(
                            unitSO.AttackConfig.CalculateAreaOfEffectDamage(endPosition, nearbyDamageable.Transform.position)
                        );
                    }
                }
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (grenade != null)
            {
                Destroy(grenade);
            }
            if (explosionParticles != null)
            {
                Destroy(explosionParticles.gameObject);
            }
        }
    }
}
