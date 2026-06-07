using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using GameDevTV.RTS.Units;
using UnityEngine.AI;
using GameDevTV.RTS.Utilities;
using System.Collections.Generic;

namespace GameDevTV.RTS.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Attack Target", story: "[Self] attacks [Target] until it dies.", category: "Action", id: "ec1210263cffd26004732ba1f15cf3c6")]
    public partial class AttackTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Self;
        [SerializeReference] public BlackboardVariable<GameObject> Target;
        [SerializeReference] public BlackboardVariable<AttackConfigSO> AttackConfig;
        [SerializeReference] public BlackboardVariable<List<GameObject>> NearbyEnemies;

        private NavMeshAgent navMeshAgent;
        private AbstractUnit unit;
        private Transform selfTransform;
        private Animator animator;

        private IDamageable targetDamageable;
        private Transform targetTransform;
        private Collider[] enemyColliders;

        private float lastAttackTime;

        protected override Status OnStart()
        {
            if (!HasValidInputs()) return Status.Failure;

            selfTransform = Self.Value.transform;
            navMeshAgent = selfTransform.GetComponent<NavMeshAgent>();
            animator = selfTransform.GetComponent<Animator>();
            unit = selfTransform.GetComponent<AbstractUnit>();

            targetTransform = Target.Value.transform;
            targetDamageable = Target.Value.GetComponent<IDamageable>();
            if (AttackConfig.Value.IsAreaOfEffect)
            {
                enemyColliders = new Collider[AttackConfig.Value.MaxEnemiesHitPerAttack];
            }

            if (!IsInRange())
            {
                navMeshAgent.SetDestination(targetTransform.position);
                navMeshAgent.isStopped = false;
                if (animator != null && HasParameter(animator, AnimationConstants.ATTACK))
                {
                    animator.SetBool(AnimationConstants.ATTACK, false);
                }
            }
            else
            {
                navMeshAgent.isStopped = true;
            }

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (Target.Value == null || targetDamageable.CurrentHealth == 0) return Status.Success;

            if (animator != null && HasParameter(animator, AnimationConstants.SPEED))
            {
                animator.SetFloat(AnimationConstants.SPEED, navMeshAgent.velocity.magnitude);
            }

            if (!IsInRange())
            {
                // Update destination if target moves
                Vector3 targetPos = targetTransform.position;
                // Only update if target has moved significantly from current destination (horizontal distance)
                Vector2 dest2D = new Vector2(navMeshAgent.destination.x, navMeshAgent.destination.z);
                Vector2 target2D = new Vector2(targetPos.x, targetPos.z);

                if (navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
                {
                    if (Vector2.Distance(dest2D, target2D) > 2f)
                    {
                        navMeshAgent.SetDestination(targetPos);
                    }
                    navMeshAgent.isStopped = false;
                }
                
                if (animator != null && HasParameter(animator, AnimationConstants.ATTACK))
                {
                    animator.SetBool(AnimationConstants.ATTACK, false);
                }
                return Status.Running;
            }

            if (navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.isStopped = true;
            }
            LookAtTarget();

            if (animator != null && HasParameter(animator, AnimationConstants.ATTACK))
            {
                animator.SetBool(AnimationConstants.ATTACK, true);
            }

            if (Time.time >= lastAttackTime + AttackConfig.Value.AttackDelay)
            {
                ApplyDamage();
            }

            return Status.Running;
        }

        private bool IsInRange()
        {
            if (Target.Value == null) return false;
            float distSq = (targetTransform.position - selfTransform.position).sqrMagnitude;
            float range = AttackConfig.Value.AttackRange;
            return distSq <= (range * range) + 2.0f; // Add small buffer
        }

        private void LookAtTarget()
{
            Quaternion lookRotation = Quaternion.LookRotation(
                (targetTransform.position - selfTransform.position).normalized,
                Vector3.up
            );
            selfTransform.rotation = Quaternion.Euler(
                selfTransform.rotation.eulerAngles.x,
                lookRotation.eulerAngles.y,
                selfTransform.rotation.eulerAngles.z
            );
        }

        private void ApplyDamage()
        {
            lastAttackTime = Time.time;
            if (unit.AttackingParticleSystem != null)
            {
                unit.AttackingParticleSystem.Play();
            }

            // projectile attacks are handled by the specific subclass of AbstractUnit that shoot projectiles
            if (AttackConfig.Value.HasProjectileAttacks) return;

            targetDamageable.TakeDamage(AttackConfig.Value.Damage);

            if (!AttackConfig.Value.IsAreaOfEffect) return;

            int hits = Physics.OverlapSphereNonAlloc(
                targetTransform.position,
                AttackConfig.Value.AreaOfEffectRadius,
                enemyColliders,
                AttackConfig.Value.DamageableLayers
            );

            for (int i = 0; i < hits; i++)
            {
                if (enemyColliders[i].TryGetComponent(out IDamageable nearbyDamageable)
                    && targetDamageable != nearbyDamageable)
                {
                    nearbyDamageable.TakeDamage(
                        AttackConfig.Value.CalculateAreaOfEffectDamage(
                            targetTransform.position, 
                            nearbyDamageable.Transform.position
                        )
                    );
                }
            }
        }

        protected override void OnEnd()
        {
            if (animator != null && HasParameter(animator, AnimationConstants.ATTACK))
            {
                animator.SetBool(AnimationConstants.ATTACK, false);
            }
            if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.isStopped = false;
            }
        }

        private bool HasParameter(Animator animator, int parameterHash)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.nameHash == parameterHash) return true;
            }
            return false;
        }

        private bool HasValidInputs() => Self.Value != null && Self.Value.TryGetComponent(out NavMeshAgent _)
&& Self.Value.TryGetComponent(out AbstractUnit _)
            && Target.Value != null && Target.Value.TryGetComponent(out IDamageable _)
            && AttackConfig.Value != null && NearbyEnemies.Value != null;
    }

}
