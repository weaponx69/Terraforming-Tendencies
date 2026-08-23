using System.Collections.Generic;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Utilities;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;
using TMPro;
using GameDevTV.RTS.VisualScriptingStubs;

namespace GameDevTV.RTS.Units
{
    [IncludeInSettings(true)]
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(NavMeshAgent), typeof(BehaviorGraphAgent))]
    public abstract class AbstractUnit : AbstractCommandable, IMoveable, IAttacker
    {
        public float AgentRadius => Agent.radius;
        [field: SerializeField] public ParticleSystem AttackingParticleSystem { get; private set; }
        [SerializeField] private DamageableSensor DamageableSensor;
        [SerializeField] private Shader statusShader;

        public NavMeshAgent Agent { get; private set; }
        public Sprite Icon => UnitSO.Icon;
        public bool IsIdle => GetCurrentCommand() == UnitCommands.Stop;
        protected BehaviorGraphAgent graphAgent;
protected UnitSO unitSO;
        protected UnitCommands currentCommand = UnitCommands.Stop;

        private static int nextUnitId = 1;
        public int UnitID { get; private set; }

        private float lastStatusUpdateTime = 0f;
        private const float STATUS_UPDATE_INTERVAL = 0.2f;

        // Animation is driven directly from C# rather than through the behavior graph,
        // whose SetAnimatorBoolAction nodes bind to a sub-graph-local "Self" that never
        // receives the live unit (logging "No Animator set.").
        protected Animator unitAnimator;
        private HashSet<string> animatorParameters;

        protected override void Awake()
        {
            base.Awake();

            UnitID = nextUnitId++;

            Agent = GetComponent<NavMeshAgent>();
            graphAgent = GetComponent<BehaviorGraphAgent>();

            // Force enable the agent to ensure it can reach the NavMesh
            if (Agent != null)
            {
                Agent.enabled = true;
            }

            unitSO = UnitSO as UnitSO;

            SetCurrentCommand(UnitCommands.Stop);

            // Ensure every unit has an Animator component
            if (GetComponentInChildren<Animator>(true) == null)
            {
                gameObject.AddComponent<Animator>();
            }

            // Cache the Animator and the parameters its controller actually exposes,
            // so the C# animation driver only touches parameters that exist.
            CacheAnimator();

            // Initialization is handled by Unity Behavior package in Unity 6
            ReapplyCoreBlackboardVariables();
        }

        private void CacheAnimator()
        {
            unitAnimator = GetComponentInChildren<Animator>(true);
            animatorParameters = new HashSet<string>();
            if (unitAnimator != null && unitAnimator.runtimeAnimatorController != null)
            {
                foreach (AnimatorControllerParameter p in unitAnimator.parameters)
                {
                    animatorParameters.Add(p.name);
                }
            }
        }

        protected void SetAnimBool(string parameter, bool value)
        {
            if (unitAnimator == null || animatorParameters == null) return;
            if (!animatorParameters.Contains(parameter)) return;
            unitAnimator.SetBool(parameter, value);
        }

        protected void SetAnimFloat(string parameter, float value)
        {
            if (unitAnimator == null || animatorParameters == null) return;
            if (!animatorParameters.Contains(parameter)) return;
            unitAnimator.SetFloat(parameter, value);
        }

        /// <summary>
        /// Drives the unit's Animator from real state every frame. Base sets locomotion
        /// speed and clears all state bools; subclasses call base then set the bools that
        /// apply to them (e.g. Worker -> IsGathering/IsBuilding, combat -> IsAttacking).
        /// </summary>
        protected virtual void UpdateAnimation()
        {
            if (unitAnimator == null) return;

            float speed = (Agent != null && Agent.isActiveAndEnabled && Agent.isOnNavMesh)
                ? Agent.velocity.magnitude
                : 0f;
            SetAnimFloat("Speed", speed);

            // Defaults; subclasses override to set the ones that apply.
            SetAnimBool("IsGathering", false);
            SetAnimBool("IsBuilding", false);
            SetAnimBool("IsAttacking", false);
        }

        protected virtual void ReapplyCoreBlackboardVariables()
        {
            if (graphAgent != null && graphAgent.isActiveAndEnabled)
            {
                try
                {
                    // Primary variables
                    graphAgent.SetVariableValue(BlackboardConstants.SELF, gameObject);
                    graphAgent.SetVariableValue(BlackboardConstants.UNIT, this);
                    graphAgent.SetVariableValue(BlackboardConstants.COMMAND, currentCommand);
                    
                    // Naming convention fallbacks
                    graphAgent.SetVariableValue("Self", gameObject);
                    graphAgent.SetVariableValue("self", gameObject);
                    graphAgent.SetVariableValue("Unit", this);
                    graphAgent.SetVariableValue("unit", this);
                    
                    // Navigation Agent fallbacks
                    graphAgent.SetVariableValue("Agent", gameObject);
                    graphAgent.SetVariableValue("agent", gameObject);

                    // Animator fallbacks
                    Animator animator = GetComponentInChildren<Animator>(true);
                    if (animator != null)
                    {
                        graphAgent.SetVariableValue("Animator", animator);
                        graphAgent.SetVariableValue("animator", animator);
                    }

                    // Attack configuration
                    if (unitSO != null && unitSO.AttackConfig != null)
                    {
                        graphAgent.SetVariableValue(BlackboardConstants.ATTACK_CONFIG, unitSO.AttackConfig);
                        graphAgent.SetVariableValue("AttackConfig", unitSO.AttackConfig);
                        graphAgent.SetVariableValue("attackConfig", unitSO.AttackConfig);
                    }
                }
                catch (System.Exception ex)
                {
                    // Catch-all to prevent initialization failure from breaking the unit
                    Debug.LogWarning($"[AbstractUnit] Exception during ReapplyCoreBlackboardVariables on {gameObject.name}: {ex.Message}");
                }
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            // Re-ensure blackboard variables are set whenever the unit is enabled.
            // This catches race conditions during spawning.
            ReapplyCoreBlackboardVariables();
        }

        protected override void Start()
        {
            if (graphAgent == null) graphAgent = GetComponent<BehaviorGraphAgent>();
            
            if (UnitSO == null)
{
                Debug.LogError($"[AbstractUnit] UnitSO is NULL on GameObject '{gameObject.name}'! This will cause crashes. Destroying unit.", gameObject);
                Destroy(gameObject);
                return;
            }

            CurrentHealth = UnitSO.Health;
            MaxHealth = UnitSO.Health;

            // The BehaviorGraphAgent.Init() clones the graph but leaves each module's
            // m_Blackboard.m_Variables empty — variables are in m_Source (RuntimeBlackboardAsset).
            // We call GenerateInstanceData via reflection to populate the live blackboard.
            ReapplyCoreBlackboardVariables();

            base.Start();
            Bus<UnitSpawnEvent>.Raise(Owner, new UnitSpawnEvent(this));

            // Cache the HeroDroneController once so we don't GetComponent every Update frame.
            heroDroneController = GetComponent<HeroDroneController>();

            if (DamageableSensor != null)
            {
                DamageableSensor.OnUnitEnter += HandleUnitEnter;
                DamageableSensor.OnUnitExit += HandleUnitExit;
                DamageableSensor.Owner = Owner;

                // Use a larger detection range for drones to see meteors earlier.
                // Combat drones have "Drone" in their name or use MeteorWarriorDrone script.
                float detectionRange = unitSO.AttackConfig != null ? unitSO.AttackConfig.AttackRange : 15f;
                bool isCombatDrone = this is MeteorWarriorDrone || name.Contains("Drone");
                
                if (isCombatDrone)
                {
                    detectionRange = Mathf.Max(detectionRange, 60f); 
                }
                
                DamageableSensor.SetupFromRange(detectionRange);
            }

            foreach(UpgradeSO upgrade in unitSO.Upgrades)
            {
                if (unitSO.TechTree != null && unitSO.TechTree.IsResearched(Owner, upgrade))
                {
                    upgrade.Apply(unitSO);
                }
            }

            if (unitSO.MovementConfig != null && Agent != null)
            {
                Agent.speed = unitSO.MovementConfig.Speed;
            }
}

        private float lastNavMeshSampleTime = 0f;
        private const float NAVMESH_SAMPLE_INTERVAL = 0.5f;
        private bool hasFirstFrameRepair = false;
        private HeroDroneController heroDroneController;

        // Direct-drive movement. The embedded behavior-graph "Move" sub-graph binds its
        // movement/stop actions to a sub-graph-local "Self"/"Agent" variable that never
        // receives the live value from the parent blackboard, so SetDestination is never
        // called by the graph. For explicit Move commands we drive the NavMeshAgent
        // directly here, which is reliable and verified on the NavMesh.
        private bool hasDirectMoveTarget;
        private Vector3 directMoveTarget;
        public bool agentShouldBeDisabled = false;

        protected virtual void Update()
        {
            // Initialization check for blackboard
            if (graphAgent != null && graphAgent.isActiveAndEnabled)
            {
                if (!hasFirstFrameRepair)
                {
                    if (graphAgent.SetVariableValue(BlackboardConstants.SELF, gameObject))
                    {
                        hasFirstFrameRepair = true;
                        ReapplyCoreBlackboardVariables();
                        graphAgent.Restart();
                    }
                }
            }

            if (Agent != null && Agent.isActiveAndEnabled)
            {
                // If this unit has a HeroDroneController, that script fully owns the
                // transform position — never let the warp guard override it, regardless
                // of whether the player is actively pressing a key right now.
                bool isHeroDrone = heroDroneController != null;

                if (!Agent.isOnNavMesh && !isHeroDrone)
                {
                    if (Time.time - lastNavMeshSampleTime >= NAVMESH_SAMPLE_INTERVAL)
                    {
                        lastNavMeshSampleTime = Time.time;
                        // Use a broad sample range to find the ground navmesh
                        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 25f, new NavMeshQueryFilter { agentTypeID = Agent.agentTypeID, areaMask = NavMesh.AllAreas }))
                        {
                            Agent.Warp(hit.position);
                        }
                    }
                }
            }
            else if (Agent != null && !Agent.enabled && !agentShouldBeDisabled)
            {
                // Safely check if we are near the NavMesh before blindly enabling to prevent crashes
                // if we were spawned inside a building or NavMeshObstacle.
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, new NavMeshQueryFilter { agentTypeID = Agent.agentTypeID, areaMask = NavMesh.AllAreas }))
                {
                    Agent.enabled = true;
                }
            }

            // Maintain direct-drive movement for explicit Move commands.
            if (hasDirectMoveTarget && Agent != null && Agent.isActiveAndEnabled && Agent.isOnNavMesh)
            {
                if (!Agent.pathPending)
                {
                    if (Agent.hasPath && Agent.remainingDistance <= Mathf.Max(Agent.stoppingDistance, 0.5f))
                    {
                        hasDirectMoveTarget = false; // arrived
                    }
                    else if (!Agent.hasPath)
                    {
                        // Path was lost or not yet assigned — (re)assert the destination.
                        Agent.isStopped = false;
                        Agent.SetDestination(directMoveTarget);
                    }
                }
            }

            // Drive animation from real state (replaces the broken graph animator nodes).
            UpdateAnimation();

            if (this is Worker)
            {
                if (Time.time - lastStatusUpdateTime >= STATUS_UPDATE_INTERVAL)
                {
                    lastStatusUpdateTime = Time.time;
                    UpdateStatusIndicator();
                }
            }
        }

        private void UpdateStatusIndicator()
        {
            Color statusColor = Color.red; 
            string reason = "STUCK / NO PATH";

            if (Agent == null)
            {
                reason = "NO AGENT";
            }
            else if (!Agent.isActiveAndEnabled)
            {
                reason = "AGENT DISABLED";
            }
            else if (!Agent.isOnNavMesh)
            {
                reason = "OFF NAVMESH";
            }
            else if (graphAgent == null)
            {
                reason = "NO BEHAVIOR";
            }
            else if (!TryGetCurrentCommand(out UnitCommands cmd))
            {
                reason = "NO COMMAND";
            }
            else
            {
                if (cmd == UnitCommands.Stop)
                {
                    statusColor = Color.cyan; // Idle/Healthy
                    reason = "IDLE";
                }
                else if (cmd == UnitCommands.BuildBuilding)
                {
                    statusColor = Color.green; // Building
                    reason = "BUILDING";
                }
                else if (this is Worker w && w.IsActivelyWorking)
                {
                    // Map BrainController state to indicator colour
                    switch (w.BrainState)
                    {
                        case WorkerBrainController.State.Gathering:
                            statusColor = Color.yellow;
                            reason = "GATHERING";
                            break;
                        case WorkerBrainController.State.MovingToBase:
                            statusColor = Color.green;
                            reason = "RETURNING";
                            break;
                        default: // MovingToSupply
                            statusColor = Color.green;
                            reason = "ACTIVE";
                            break;
                    }
                }
                else if (Agent.pathPending || (Agent.hasPath && (Agent.pathStatus == NavMeshPathStatus.PathComplete || Agent.pathStatus == NavMeshPathStatus.PathPartial)))
                {
                    statusColor = Color.green; // Active/Go
                    reason = "ACTIVE";
                }

            }

            SetStatusColor(statusColor, reason);
        }

        private object GetBlackboardObject()
        {
            if (graphAgent == null) return null;
            
            // Try BlackboardReference first (Standard for Unity 6 Behavior)
            try {
                var bbRefProp = graphAgent.GetType().GetProperty("BlackboardReference", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (bbRefProp != null)
                {
                    object bbRef = bbRefProp.GetValue(graphAgent);
                    if (bbRef != null)
                    {
                        var bbProp = bbRef.GetType().GetProperty("Blackboard", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (bbProp != null) return bbProp.GetValue(bbRef);
                    }
                }
            } catch {}

            // Fallbacks for older versions or internal fields
            var props = new[] { "Blackboard", "m_Blackboard", "RuntimeBlackboard" };
            foreach (var p in props)
            {
                try {
                    var prop = graphAgent.GetType().GetProperty(p, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (prop != null) return prop.GetValue(graphAgent);

                    var field = graphAgent.GetType().GetField(p, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null) return field.GetValue(graphAgent);
                } catch {}
            }
            return null;
        }

        public bool TryGetCurrentCommand(out UnitCommands cmd)
        {
            cmd = UnitCommands.Stop;
            if (graphAgent == null) return false;

            // Primary: typed generic GetVariable — fires correctly after m_IsInitialised = true
            try
            {
                if (graphAgent.GetVariable(BlackboardConstants.COMMAND, out BlackboardVariable<UnitCommands> cmdVar))
                {
                    cmd = cmdVar.Value;
                    return true;
                }
            } catch {}

            // Fallback: non-generic, converts via ObjectValue
            try
            {
                if (graphAgent.GetVariable(BlackboardConstants.COMMAND, out BlackboardVariable bbVar) && bbVar?.ObjectValue != null)
                {
                    cmd = (UnitCommands)System.Convert.ToInt32(bbVar.ObjectValue);
                    return true;
                }
            } catch {}

            return false;
        }

        public UnitCommands GetCurrentCommand()
        {
            if (TryGetCurrentCommand(out UnitCommands cmd))
            {
                return cmd;
            }
            return UnitCommands.Stop;
        }

        public void SetCurrentCommand(UnitCommands cmd)
        {
            currentCommand = cmd;
            if (graphAgent == null) return;

            // Primary: typed SetVariableValue
            bool setSuccess = graphAgent.SetVariableValue(BlackboardConstants.COMMAND, cmd);

            if (!setSuccess)
            {
                // Fallback: set ObjectValue directly on the raw variable.
                try
                {
                    if (graphAgent.GetVariable(BlackboardConstants.COMMAND, out BlackboardVariable bbVar) && bbVar != null)
                    {
                        bbVar.ObjectValue = cmd;
                        setSuccess = true;
                    }
                }
                catch {}
            }

            if (setSuccess)
            {
                try
                {
                    graphAgent.Restart();
                    ReapplyCoreBlackboardVariables();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[Command Queue] Failed to restart behavior graph: {ex.Message}");
                }
            }
        }

        private GameObject statusIndicator;
        private Material indicatorMaterial;
        private Color lastIndicatorColor;

        public void SetStatusColor(Color color, string reason = "")
        {
            if (statusIndicator == null)
            {
                // Small sphere dot — replace the flat cube
                statusIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                statusIndicator.name = "StatusIndicator";
                statusIndicator.layer = 0;

                if (Application.isPlaying)
                    Destroy(statusIndicator.GetComponent<Collider>());
                else
                    DestroyImmediate(statusIndicator.GetComponent<Collider>());

                statusIndicator.transform.SetParent(transform);
                // Sit just above the drone body (drones hover at ~2 units)
                statusIndicator.transform.localPosition = new Vector3(0f, 1.8f, 0f);
                statusIndicator.transform.localScale    = new Vector3(0.35f, 0.35f, 0.35f);

                Renderer r = statusIndicator.GetComponent<Renderer>();

                Shader shader = statusShader;
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Sprites/Default");

                indicatorMaterial = new Material(shader);
                indicatorMaterial.renderQueue = 3100;
                r.sharedMaterial = indicatorMaterial;
            }

            if (indicatorMaterial != null && color != lastIndicatorColor)
            {
                lastIndicatorColor = color;
                Color displayColor = color;
                if (color == Color.red)    displayColor = new Color(1f,   0.1f, 0.1f, 1f);
                else if (color == Color.green)  displayColor = new Color(0.1f, 1f,   0.1f, 1f);
                else if (color == Color.yellow) displayColor = new Color(1f,   0.85f, 0f,  1f);
                else if (color == Color.cyan)   displayColor = new Color(0.1f, 0.8f, 1f,  1f);

                if (indicatorMaterial.HasProperty("_BaseColor")) indicatorMaterial.SetColor("_BaseColor", displayColor);
                indicatorMaterial.color = displayColor;

                statusIndicator.SetActive(true);
            }
        }

        public void MoveTo(Vector3 position)
        {
            if (graphAgent != null && graphAgent.isActiveAndEnabled)
            {
                try {
                    graphAgent.SetVariableValue(BlackboardConstants.TARGET_LOCATION, position);
                    graphAgent.SetVariableValue<GameObject>(BlackboardConstants.TARGET_GAME_OBJECT, null);
                } catch (System.Exception ex) {
                    Debug.LogWarning($"[AbstractUnit] MoveTo failed to set blackboard variables on {gameObject.name}: {ex.Message}");
                }
            }
            SetCurrentCommand(UnitCommands.Move);
            DriveAgentTo(position);
        }

        public void MoveTo(Transform transform)
        {
            if (graphAgent != null)
            {
                try {
                    graphAgent.SetVariableValue(BlackboardConstants.TARGET_GAME_OBJECT, transform.gameObject);
                } catch (System.Exception ex) {
                    Debug.LogWarning($"[AbstractUnit] MoveTo (Transform) failed to set blackboard variables on {gameObject.name}: {ex.Message}");
                }
            }
            SetCurrentCommand(UnitCommands.Move);
            DriveAgentTo(transform.position);
        }

        public virtual void Stop()
        {
            ClearDirectMove();
            if (Agent != null && Agent.isActiveAndEnabled && Agent.isOnNavMesh)
            {
                Agent.ResetPath();
            }
            SetCommandOverrides(null);
            SetCurrentCommand(UnitCommands.Stop);
        }

        /// <summary>
        /// Directly drives the NavMeshAgent toward a world position. Used for explicit
        /// Move commands because the embedded behavior-graph move sub-graph never receives
        /// its Agent binding and therefore never calls SetDestination.
        /// </summary>
        protected void DriveAgentTo(Vector3 worldPosition)
        {
            if (Agent == null) return;

            Vector3 dest = worldPosition;
            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = Agent.agentTypeID,
                areaMask = NavMesh.AllAreas
            };
            if (NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, 25f, filter))
            {
                dest = hit.position;
            }

            directMoveTarget = dest;
            hasDirectMoveTarget = true;

            if (Agent.isActiveAndEnabled && Agent.isOnNavMesh)
            {
                Agent.isStopped = false;
                Agent.SetDestination(dest);
            }
        }

        protected void ClearDirectMove()
        {
            hasDirectMoveTarget = false;
        }

        public void Attack(IDamageable damageable)
        {
            if (graphAgent != null && graphAgent.isActiveAndEnabled)
            {
                try {
                    graphAgent.SetVariableValue(BlackboardConstants.TARGET_GAME_OBJECT, damageable.Transform.gameObject);
                } catch (System.Exception ex) {
                    Debug.LogWarning($"[AbstractUnit] Attack failed to set blackboard variables on {gameObject.name}: {ex.Message}");
                }
            }
            SetCurrentCommand(UnitCommands.Attack);
        }

        public void Attack(Vector3 location)
        {
            if (graphAgent != null && graphAgent.isActiveAndEnabled)
            {
                try {
                    graphAgent.SetVariableValue<GameObject>(BlackboardConstants.TARGET_GAME_OBJECT, null);
                    graphAgent.SetVariableValue(BlackboardConstants.TARGET_LOCATION, location);
                } catch (System.Exception ex) {
                    Debug.LogWarning($"[AbstractUnit] Attack (Vector3) failed to set blackboard variables on {gameObject.name}: {ex.Message}");
                }
            }
            SetCurrentCommand(UnitCommands.Attack);
        }

        private void HandleUnitEnter(IDamageable damageable)
        {
            List<GameObject> nearbyEnemies = SetNearbyEnemiesOnBlackboard();

            if (graphAgent.GetVariable(BlackboardConstants.TARGET_GAME_OBJECT, out BlackboardVariable<GameObject> targetVariable)
                && targetVariable.Value == null && nearbyEnemies.Count > 0)
            {
                graphAgent.SetVariableValue(BlackboardConstants.TARGET_GAME_OBJECT, nearbyEnemies[0]);
            }
        }

        private void HandleUnitExit(IDamageable damageable)
        {
            List<GameObject> nearbyEnemies = SetNearbyEnemiesOnBlackboard();

            // Safety check: casting to Object to detect destroyed Unity objects
            bool isTargetValid = damageable != null && (damageable is Object obj && obj != null) && damageable.Transform != null;
            
            if (!isTargetValid || 
                !graphAgent.GetVariable(BlackboardConstants.TARGET_GAME_OBJECT, out BlackboardVariable<GameObject> targetVariable)
                || damageable.Transform.gameObject != targetVariable.Value) return;

            if (nearbyEnemies.Count > 0)
            {
                graphAgent.SetVariableValue(BlackboardConstants.TARGET_GAME_OBJECT, nearbyEnemies[0]);
            }
            else
            {
                graphAgent.SetVariableValue<GameObject>(BlackboardConstants.TARGET_GAME_OBJECT, null);
                if (damageable.Transform != null)
                {
                    graphAgent.SetVariableValue(BlackboardConstants.TARGET_LOCATION, damageable.Transform.position);
                }
            }
        }

        private List<GameObject> SetNearbyEnemiesOnBlackboard()
        {
            List<GameObject> nearbyEnemies = new List<GameObject>();

            // If this unit is being destroyed, accessing 'transform' below would throw.
            if (this == null || DamageableSensor == null) return nearbyEnemies;

            foreach (var d in DamageableSensor.Damageables)
            {
                // Safety check for destroyed objects implementing IDamageable
                if (d != null && (d is Object obj && obj != null) && d.Transform != null)
                {
                    nearbyEnemies.Add(d.Transform.gameObject);
                }
            }

            nearbyEnemies.Sort(new ClosestGameObjectComparer(transform.position));

            if (graphAgent != null)
            {
                graphAgent.SetVariableValue(BlackboardConstants.NEARBY_ENEMIES, nearbyEnemies);
            }

            return nearbyEnemies;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Bus<UnitDeathEvent>.Raise(Owner, new UnitDeathEvent(this));
        }
    }
}
