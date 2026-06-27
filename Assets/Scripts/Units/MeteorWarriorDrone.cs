using UnityEngine;
using GameDevTV.RTS.Environment;
using Unity.VisualScripting;

namespace GameDevTV.RTS.Units
{
    [IncludeInSettings(true)]
    /// <summary>
    /// A specialized military drone that prioritizes destroying environmental threats
    /// like meteors (<see cref="NaturalEventImpact"/>) before attacking other enemies.
    ///
    /// Combat is driven directly in C# rather than through the behavior graph. The
    /// embedded behavior-graph attack sub-graph never receives its live Self/Agent
    /// blackboard bindings (the same defect that broke movement), so its attack action
    /// fails input validation and never fires. Driving the engagement here is reliable
    /// and mirrors the direct-drive movement bypass in AbstractUnit.
    /// </summary>
    public class MeteorWarriorDrone : BaseMilitaryUnit
    {
        [Header("Defense Settings")]
        [Tooltip("If true, the drone will always pick a meteor over a standard enemy unit.")]
        [SerializeField] private bool prioritizeMeteors = true;

        [Header("Ammo & Guard Settings")]
        [Tooltip("Maximum ammo capacity.")]
        [SerializeField] private int maxAmmo = 100;
        [Tooltip("Distance from built location the drone is allowed to wander to chase a target.")]
        [SerializeField] private float guardLeashRange = 30f;
        [Tooltip("Distance from storage center to trigger a reload.")]
        [SerializeField] private float reloadActivationDistance = 4f;

        private DamageableSensor combatSensor;
        private AttackConfigSO attackConfig;
        private IDamageable currentTarget;
        private IDamageable lastFiredTarget;
        private float lastAttackTime = float.NegativeInfinity;

        private int currentAmmo;
        private Vector3 homePosition;
        private Vector3 currentCenterPosition;
        private Vector3 patrolTarget;
        private float lastCenterCheckTime = float.NegativeInfinity;
        private bool isReloading;
        private LineRenderer tracer;
        private float tracerDuration = 0.05f;
        private float tracerHideTime = -1f;

        protected override void Start()
        {
            base.Start();
            combatSensor = GetComponentInChildren<DamageableSensor>();
            attackConfig = unitSO != null ? unitSO.AttackConfig : null;
            
            homePosition = transform.position;
            currentCenterPosition = homePosition;
            patrolTarget = homePosition;
            currentAmmo = maxAmmo;
            SetupTracer();
        }

        private void SetupTracer()
        {
            GameObject tracerObj = new GameObject("MachineGunTracer");
            tracerObj.transform.SetParent(transform);
            tracer = tracerObj.AddComponent<LineRenderer>();
            tracer.startWidth = 0.05f;
            tracer.endWidth = 0.05f;
            tracer.positionCount = 2;
            tracer.enabled = false;
            
            // Standard Unlit/Color shader if possible, fallback to others
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            
            Material mat = new Material(shader);
            mat.color = Color.yellow;
            tracer.material = mat;
        }

        protected override void Update()
        {
            base.Update();

            if (attackConfig == null) return;
            if (combatSensor == null)
            {
                combatSensor = GetComponentInChildren<DamageableSensor>();
                if (combatSensor == null) return;
            }

            if (tracer != null && tracer.enabled && Time.time > tracerHideTime)
            {
                tracer.enabled = false;
            }

            // Respect explicit player move orders
            if (IsManuallyTravelling())
            {
                currentTarget = null;
                isReloading = false;
                return;
            }

            // Periodically find the nearest command center
            if (Time.time >= lastCenterCheckTime + 2.0f)
            {
                lastCenterCheckTime = Time.time;
                BaseBuilding center = FindNearestCommandCenter();
                if (center != null)
                {
                    currentCenterPosition = center.transform.position;
                }
                else
                {
                    currentCenterPosition = homePosition;
                }
            }

            // Reload logic
            if (currentAmmo <= 0 || isReloading)
            {
                HandleReloading();
                return;
            }

            currentTarget = AcquireTarget();
            
            if (currentTarget == null)
            {
                // No target: Patrol around the command center
                float distToPatrol = Vector3.Distance(transform.position, patrolTarget);
                float distToCenter = Vector3.Distance(patrolTarget, currentCenterPosition);
                
                // If we arrived at patrol target, or if the patrol target is too far from current command center
                if (distToPatrol <= 2f || distToCenter > 20f)
                {
                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    float radius = Random.Range(8f, 15f);
                    patrolTarget = currentCenterPosition + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                }

                DriveAgentTo(patrolTarget);
                return;
            }

            Transform targetTf = currentTarget.Transform;
            Vector3 targetPos = targetTf.position;
            Vector3 selfPos = transform.position;
            
            // Check horizontal distance
            float horizontalDistance = Vector2.Distance(
                new Vector2(selfPos.x, selfPos.z),
                new Vector2(targetPos.x, targetPos.z));

            if (horizontalDistance > attackConfig.AttackRange * 0.9f)
            {
                // Chase only if target is within leash of the command center
                float targetDistToCenter = Vector3.Distance(targetPos, currentCenterPosition);
                if (targetDistToCenter <= guardLeashRange)
                {
                    // Move towards the target but stop at the edge of attack range.
                    // This keeps drones from flying directly into meteor impact zones.
                    Vector3 directionToDrone = (selfPos - targetPos).normalized;
                    directionToDrone.y = 0f;
                    if (directionToDrone == Vector3.zero) directionToDrone = Vector3.forward;

                    Vector3 stopPos = targetPos + directionToDrone * (attackConfig.AttackRange * 0.8f);
                    stopPos.y = selfPos.y; 
                    DriveAgentTo(stopPos);
                }
                else
                {
                    // Target too far from command center: clear target and return to patrol
                    currentTarget = null;
                }
            }
            else
            {
                // In range: hold position and fire
                ClearDirectMove();
                if (Agent != null && Agent.isActiveAndEnabled && Agent.isOnNavMesh)
                {
                    Agent.isStopped = true;
                }
                FaceTarget(targetPos);
                TryFire();
            }
        }

        private void HandleReloading()
        {
            if (!isReloading)
            {
                Debug.Log($"[MeteorWarriorDrone] {gameObject.name} out of ammo! Finding storage center.");
                isReloading = true;
            }

            BaseBuilding nearestStorage = FindNearestStorage();
            if (nearestStorage == null)
            {
                // Nowhere to reload, just stay put or return home
                if (Vector3.Distance(transform.position, homePosition) > 2f)
                    DriveAgentTo(homePosition);
                return;
            }

            float distToStorage = Vector3.Distance(transform.position, nearestStorage.transform.position);
            if (distToStorage > reloadActivationDistance)
            {
                DriveAgentTo(nearestStorage.transform.position);
            }
            else
            {
                // At storage: reload
                Debug.Log($"[MeteorWarriorDrone] {gameObject.name} reloaded at {nearestStorage.name}.");
                currentAmmo = maxAmmo;
                isReloading = false;
                ClearDirectMove();
                if (Agent != null && Agent.isActiveAndEnabled && Agent.isOnNavMesh)
                {
                    Agent.isStopped = true;
                }
            }
        }

        private BaseBuilding FindNearestStorage()
        {
            BaseBuilding best = null;
            float minDistance = float.MaxValue;

            foreach (var building in BaseBuilding.ActiveBuildings)
            {
                if (building == null || building.Owner != Owner) continue;
                
                // Check BuildingSO name or fallback to UnitSO name
                string buildingName = "";
                if (building.BuildingSO != null) buildingName = building.BuildingSO.Name;
                else if (building.UnitSO != null) buildingName = building.UnitSO.Name;

                if (buildingName.Contains("Supply Hut"))
                {
                    float dist = Vector3.Distance(transform.position, building.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        best = building;
                    }
                }
            }
            return best;
        }

        private void TryFire()
        {
            if (currentAmmo <= 0) return;

            bool newTarget = !ReferenceEquals(currentTarget, lastFiredTarget);
            if (!newTarget && Time.time < lastAttackTime + attackConfig.AttackDelay) return;

            lastAttackTime = Time.time;
            lastFiredTarget = currentTarget;

            // Machine Gun Effect (Tracer)
            if (tracer != null)
            {
                tracer.enabled = true;
                tracer.SetPosition(0, transform.position + Vector3.up * 1.5f); // Fire from body height
                tracer.SetPosition(1, currentTarget.Transform.position);
                tracerHideTime = Time.time + tracerDuration;
            }

            if (AttackingParticleSystem != null)
            {
                AttackingParticleSystem.Play();
            }

            currentTarget.TakeDamage(attackConfig.Damage);
            currentAmmo--;
        }

        protected override void UpdateAnimation()
        {
            base.UpdateAnimation();
            // Attacking when we hold a valid, living target.
            bool attacking = currentTarget != null
                && !(currentTarget is Object o && o == null)
                && currentTarget.Transform != null
                && currentTarget.CurrentHealth > 0;
            SetAnimBool("IsAttacking", attacking);
            
            if (isReloading)
            {
                SetStatusColor(Color.magenta, "RELOADING");
            }
        }

        /// <summary>
        /// True only while the unit is mid-way through executing an explicit player Move order.
        /// </summary>
        private bool IsManuallyTravelling()
        {
            if (GetCurrentCommand() != UnitCommands.Move) return false;
            return Agent != null && Agent.isActiveAndEnabled && Agent.isOnNavMesh
                   && Agent.hasPath && !Agent.pathPending
                   && Agent.remainingDistance > Agent.stoppingDistance + 0.1f;
        }

        /// <summary>
        /// Selects the best valid target from the sensor, prioritizing meteors then proximity.
        /// </summary>
        private IDamageable AcquireTarget()
        {
            var list = combatSensor.Damageables;
            if (list == null || list.Count == 0) return null;

            IDamageable best = null;
            bool bestIsMeteor = false;
            float bestDistance = float.MaxValue;

            foreach (var d in list)
            {
                // Skip nulls and destroyed Unity objects.
                if (d == null || (d is Object o && o == null) || d.Transform == null) continue;
                if (d.CurrentHealth <= 0) continue;

                // Skip if target is outside leash range of command center
                float targetDistToCenter = Vector3.Distance(d.Transform.position, currentCenterPosition);
                if (targetDistToCenter > guardLeashRange) continue;

                bool isMeteor = d.Transform.GetComponent<NaturalEventImpact>() != null;
                float distance = Vector3.Distance(transform.position, d.Transform.position);

                if (best == null)
                {
                    best = d;
                    bestIsMeteor = isMeteor;
                    bestDistance = distance;
                    continue;
                }

                if (prioritizeMeteors)
                {
                    if (isMeteor && !bestIsMeteor)
                    {
                        best = d;
                        bestIsMeteor = true;
                        bestDistance = distance;
                        continue;
                    }
                    if (!isMeteor && bestIsMeteor) continue;
                }

                if (distance < bestDistance)
                {
                    best = d;
                    bestIsMeteor = isMeteor;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private BaseBuilding FindNearestCommandCenter()
        {
            BaseBuilding best = null;
            float minDistance = float.MaxValue;
            foreach (var building in BaseBuilding.ActiveBuildings)
            {
                if (building == null || building.Owner != Owner) continue;
                
                string name = "";
                if (building.BuildingSO != null) name = building.BuildingSO.Name;
                else if (building.UnitSO != null) name = building.UnitSO.Name;

                if (name.Contains("Command Post") || name.Contains("Command Center"))
                {
                    float dist = Vector3.Distance(transform.position, building.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        best = building;
                    }
                }
            }
            return best;
        }

        private void FaceTarget(Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            Quaternion look = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.Euler(
                transform.rotation.eulerAngles.x,
                look.eulerAngles.y,
                transform.rotation.eulerAngles.z);
        }
    }
}
