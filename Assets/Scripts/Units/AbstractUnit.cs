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
    [RequireComponent(typeof(NavMeshAgent), typeof(BehaviorGraphAgent))]
    public abstract class AbstractUnit : AbstractCommandable, IMoveable, IAttacker
    {
        public float AgentRadius => Agent.radius;
        [field: SerializeField] public ParticleSystem AttackingParticleSystem { get; private set; }
        [SerializeField] private DamageableSensor DamageableSensor;
        [SerializeField] private Shader statusShader;

        public NavMeshAgent Agent { get; private set; }
        public Sprite Icon => UnitSO.Icon;
        protected BehaviorGraphAgent graphAgent;
        protected UnitSO unitSO;

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

            unitSO = UnitSO as UnitSO;

            // IMPORTANT: Removed manual graphAgent.Init() to prevent "Clone(Clone)" issues in Unity 6.
            // The BehaviorGraphAgent handles its own initialization.
            
            SetCurrentCommand(UnitCommands.Stop);
            ReapplyCoreBlackboardVariables();
        }

        protected virtual void ReapplyCoreBlackboardVariables()
        {
            if (graphAgent != null)
            {
                try
                {
                    graphAgent.SetVariableValue(BlackboardConstants.SELF, gameObject);
                    graphAgent.SetVariableValue(BlackboardConstants.UNIT, this);
                    if (unitSO != null && unitSO.AttackConfig != null)
                    {
                        graphAgent.SetVariableValue(BlackboardConstants.ATTACK_CONFIG, unitSO.AttackConfig);
                    }
                }
                catch { }
            }
        }

        protected override void Start()
        {
            base.Start();
            CurrentHealth = UnitSO.Health;
            MaxHealth = UnitSO.Health;
            Bus<UnitSpawnEvent>.Raise(Owner, new UnitSpawnEvent(this));

            // The BehaviorGraphAgent.Init() clones the graph but leaves each module's
            // m_Blackboard.m_Variables empty — variables are in m_Source (RuntimeBlackboardAsset).
            // We call GenerateInstanceData via reflection to populate the live blackboard.
            RepairBlackboards();

            if (DamageableSensor != null)
            {
                DamageableSensor.OnUnitEnter += HandleUnitEnter;
                DamageableSensor.OnUnitExit += HandleUnitExit;
                DamageableSensor.Owner = Owner;
                DamageableSensor.SetupFrom(unitSO.AttackConfig);
            }

            foreach(UpgradeSO upgrade in unitSO.Upgrades)
            {
                if (unitSO.TechTree.IsResearched(Owner, upgrade))
                {
                    upgrade.Apply(unitSO);
                }
            }
        }

        private void RepairBlackboards()
        {
            if (graphAgent == null) return;

            try
            {
                var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var graphField = graphAgent.GetType().GetField("m_Graph", bf);
                if (graphField == null) return;

                var graph = graphField.GetValue(graphAgent) as Unity.Behavior.BehaviorGraph;
                if (graph == null) return;

                // Access BehaviorGraph.Graphs (internal List<BehaviorGraphModule>)
                var graphsField = graph.GetType().GetField("Graphs",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public);
                if (graphsField == null) return;

                var modules = graphsField.GetValue(graph) as System.Collections.IList;
                if (modules == null) return;

                // BlackboardReference, Blackboard are public — look them up once
                var mSourceField = typeof(Unity.Behavior.BlackboardReference).GetField("m_Source", bf);
                var mBlackboardField = typeof(Unity.Behavior.BlackboardReference).GetField("m_Blackboard", bf);
                var generateMethod = typeof(Unity.Behavior.Blackboard).GetMethod("GenerateInstanceData", bf);

                if (mSourceField == null || mBlackboardField == null || generateMethod == null) return;

                for (int i = 0; i < modules.Count; i++)
                {
                    var module = modules[i];
                    if (module == null) continue;

                    // BehaviorGraphModule is internal — get its type from the instance
                    var bbRefField = module.GetType().GetField("BlackboardReference",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (bbRefField == null) continue;

                    var bbRef = bbRefField.GetValue(module);
                    if (bbRef == null) continue;

                    var source = mSourceField.GetValue(bbRef);
                    if (source == null) continue;

                    var blackboard = mBlackboardField.GetValue(bbRef) as Unity.Behavior.Blackboard;
                    if (blackboard == null) continue;

                    // Only repair the main blackboard of the agent, do not touch subgraphs!
                    var mainBB = GetBlackboardObject() as Unity.Behavior.Blackboard;
                    if (blackboard != mainBB) continue;

                    // Only repopulate if m_Variables is empty
                    if (blackboard.Variables.Count == 0)
                    {
                        var sourceBB = source.GetType().GetProperty("Blackboard",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                            ?.GetValue(source) as Unity.Behavior.Blackboard;
                        if (sourceBB != null && sourceBB.Variables.Count > 0)
                        {
                            generateMethod.Invoke(blackboard, new object[] { sourceBB, source });
                        }
                    }
                }
            }
            catch {}
        }

        private float lastNavMeshSampleTime = 0f;
        private const float NAVMESH_SAMPLE_INTERVAL = 0.5f;

        protected virtual void Update()
        {
            if (Agent != null && Agent.isActiveAndEnabled && !Agent.isOnNavMesh)
            {
                if (Time.time - lastNavMeshSampleTime >= NAVMESH_SAMPLE_INTERVAL)
                {
                    lastNavMeshSampleTime = Time.time;
                    NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = Agent.agentTypeID, areaMask = NavMesh.AllAreas };
                    if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 15f, filter))
                    {
                        Agent.Warp(hit.position);
                    }
                }
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

            if (!graphAgent.GetVariable(BlackboardConstants.TARGET_GAME_OBJECT, out BlackboardVariable<GameObject> targetVariable)
                || damageable.Transform.gameObject != targetVariable.Value) return;

            if (nearbyEnemies.Count > 0)
            {
                graphAgent.SetVariableValue(BlackboardConstants.TARGET_GAME_OBJECT, nearbyEnemies[0]);
            }
            else
            {
                graphAgent.SetVariableValue<GameObject>(BlackboardConstants.TARGET_GAME_OBJECT, null);
                graphAgent.SetVariableValue(BlackboardConstants.TARGET_LOCATION, damageable.Transform.position);
            }
        }

        private List<GameObject> SetNearbyEnemiesOnBlackboard()
        {
            List<GameObject> nearbyEnemies = DamageableSensor.Damageables
                            .ConvertAll(damageable => damageable.Transform.gameObject);
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
