using System.Collections.Generic;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Utilities;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;
using TMPro;

namespace GameDevTV.RTS.Units
{
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
            
            // Initialization is handled by Unity Behavior package in Unity 6
            ReapplyCoreBlackboardVariables();
        }

        protected virtual void ReapplyCoreBlackboardVariables()
        {
            if (graphAgent != null && graphAgent.isActiveAndEnabled)
            {
                try
                {
                    graphAgent.SetVariableValue(BlackboardConstants.SELF, gameObject);
                    graphAgent.SetVariableValue(BlackboardConstants.UNIT, this);
                    graphAgent.SetVariableValue(BlackboardConstants.COMMAND, currentCommand);
                    graphAgent.SetVariableValue("Agent", gameObject);

                    Animator animator = GetComponentInChildren<Animator>();
                    graphAgent.SetVariableValue("Animator", animator);

                    if (unitSO != null && unitSO.AttackConfig != null)
                    {
                        graphAgent.SetVariableValue(BlackboardConstants.ATTACK_CONFIG, unitSO.AttackConfig);
                    }
                }
                catch (System.Exception)
                {
                    // Initialization might happen later
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
                if (unitSO.TechTree.IsResearched(Owner, upgrade))
                {
                    upgrade.Apply(unitSO);
                }
            }
        }

        private float lastNavMeshSampleTime = 0f;
private const float NAVMESH_SAMPLE_INTERVAL = 0.5f;
        private bool hasFirstFrameRepair = false;

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
                if (!Agent.isOnNavMesh)
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
            else if (Agent != null && !Agent.enabled)
            {
                Agent.enabled = true;
            }

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
            graphAgent.SetVariableValue(BlackboardConstants.TARGET_LOCATION, position);
            graphAgent.SetVariableValue<GameObject>(BlackboardConstants.TARGET_GAME_OBJECT, null);
            SetCurrentCommand(UnitCommands.Move);
        }

        public void MoveTo(Transform transform)
        {
            graphAgent.SetVariableValue(BlackboardConstants.TARGET_GAME_OBJECT, transform.gameObject);
            SetCurrentCommand(UnitCommands.Move);
        }

        public virtual void Stop()
        {
            SetCommandOverrides(null);
            SetCurrentCommand(UnitCommands.Stop);
        }

        public void Attack(IDamageable damageable)
        {
            graphAgent.SetVariableValue(BlackboardConstants.TARGET_GAME_OBJECT, damageable.Transform.gameObject);
            SetCurrentCommand(UnitCommands.Attack);
        }

        public void Attack(Vector3 location)
        {
            graphAgent.SetVariableValue<GameObject>(BlackboardConstants.TARGET_GAME_OBJECT, null);
            graphAgent.SetVariableValue(BlackboardConstants.TARGET_LOCATION, location);
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

            if (damageable == null || damageable.Transform == null || 
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
            foreach (var d in DamageableSensor.Damageables)
            {
                if (d != null && d.Transform != null)
                {
                    nearbyEnemies.Add(d.Transform.gameObject);
                }
            }

            nearbyEnemies.Sort(new ClosestGameObjectComparer(transform.position));

            graphAgent.SetVariableValue(BlackboardConstants.NEARBY_ENEMIES, nearbyEnemies);

            return nearbyEnemies;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Bus<UnitDeathEvent>.Raise(Owner, new UnitDeathEvent(this));
        }
    }
}
