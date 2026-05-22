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
        public NavMeshAgent Agent { get; private set; }
        public Sprite Icon => UnitSO.Icon;
        protected BehaviorGraphAgent graphAgent;
        protected UnitSO unitSO;

        private static int nextUnitId = 1;
        public int UnitID { get; private set; }

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
            if (graphAgent != null && unitSO != null && unitSO.AttackConfig != null)
            {
                try
                {
                    graphAgent.SetVariableValue("AttackConfig", unitSO.AttackConfig);
                }
                catch {}
            }
        }

        protected override void Start()
        {
            base.Start();
            CurrentHealth = UnitSO.Health;
            MaxHealth = UnitSO.Health;
            Bus<UnitSpawnEvent>.Raise(Owner, new UnitSpawnEvent(this));

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

        protected virtual void Update()
        {
            if (Agent != null && Agent.isActiveAndEnabled && !Agent.isOnNavMesh)
            {
                NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = Agent.agentTypeID, areaMask = NavMesh.AllAreas };
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 15f, filter))
                {
                    Agent.Warp(hit.position);
                }
            }

            if (this is Worker)
            {
                UpdateStatusIndicator();
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
                else if (Agent.pathPending || (Agent.hasPath && (Agent.pathStatus == NavMeshPathStatus.PathComplete || Agent.pathStatus == NavMeshPathStatus.PathPartial)))
                {
                    statusColor = Color.green; // Active/Go
                    reason = "ACTIVE";
                }
            }

            if (statusColor == Color.red)
            {
                Debug.Log($"[Status] {name} (ID: {UnitID}) is RED. Reason: {reason} | NameSO: {unitSO?.Name ?? "null"}");
                
                // Print extensive diagnostics for drones/workers when RED
                if (unitSO != null && (unitSO.Name.Contains("Drone") || unitSO.Name.Contains("Worker")))
                {
                    LogDetailedBlackboardStatus();
                }
            }

            SetStatusColor(statusColor, reason);
        }

        private void LogDetailedBlackboardStatus()
        {
            try
            {
                if (graphAgent == null)
                {
                    Debug.Log($"[Blackboard Diagnostic] Drone #{UnitID} | graphAgent IS NULL");
                    return;
                }

                // Check initialization state — root cause suspect for all NOT FOUND
                bool isInit = false, isStarted = false;
                try
                {
                    var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                    var f1 = graphAgent.GetType().GetField("m_IsInitialised", bf);
                    var f2 = graphAgent.GetType().GetField("m_IsStarted", bf);
                    if (f1 != null) isInit = (bool)f1.GetValue(graphAgent);
                    if (f2 != null) isStarted = (bool)f2.GetValue(graphAgent);
                }
                catch {}

                Debug.Log($"[Blackboard Diagnostic] Drone #{UnitID} | m_IsInitialised={isInit} | m_IsStarted={isStarted}");
                if (!isInit) return;

                string[] vars = { "Command", "Self", "Unit", "Supply", "TargetGameObject", "TargetLocation" };
                foreach (var vName in vars)
                {
                    try {
                        if (graphAgent.GetVariable(vName, out Unity.Behavior.BlackboardVariable bbVar))
                        {
                            object val = bbVar?.ObjectValue;
                            Debug.Log($"[Blackboard Diagnostic] Drone #{UnitID} | '{vName}' = {val ?? "null"} (Type: {val?.GetType().Name ?? "null"})");
                        }
                        else
                        {
                            Debug.Log($"[Blackboard Diagnostic] Drone #{UnitID} | '{vName}' NOT FOUND (initialized=true)");
                        }
                    } catch {}
                }
            }
            catch (System.Exception ex)
            {
                Debug.Log($"[Blackboard Diagnostic] Crash: {ex.Message}");
            }
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
                if (graphAgent.GetVariable("Command", out BlackboardVariable<UnitCommands> cmdVar))
                {
                    cmd = cmdVar.Value;
                    return true;
                }
            } catch {}

            // Fallback: non-generic, converts via ObjectValue
            try
            {
                if (graphAgent.GetVariable("Command", out BlackboardVariable bbVar) && bbVar?.ObjectValue != null)
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

            // Primary: typed SetVariableValue — sets BlackboardVariable<UnitCommands>.Value and
            // fires OnValueChanged, which the BT SwitchComposite listens to for branch re-evaluation.
            if (graphAgent.SetVariableValue("Command", cmd))
                return;

            // Fallback: set ObjectValue directly on the raw variable.
            // This skips OnValueChanged, so the BT may not react immediately,
            // but is a safety net if the typed path fails.
            try
            {
                if (graphAgent.GetVariable("Command", out BlackboardVariable bbVar) && bbVar != null)
                    bbVar.ObjectValue = cmd;
            }
            catch {}
        }

        private GameObject statusIndicator;
        private Material indicatorMaterial;
        private TextMeshPro statusText;

        public void SetStatusColor(Color color, string reason = "")
        {
            if (statusIndicator == null)
            {
                // Create the indicator as a large flat cube for high visibility
                statusIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
                statusIndicator.name = "StatusIndicator";
                statusIndicator.layer = 0; // Default layer

                if (Application.isPlaying)
                {
                    Destroy(statusIndicator.GetComponent<Collider>());
                }
                else
                {
                    DestroyImmediate(statusIndicator.GetComponent<Collider>());
                }

                statusIndicator.transform.SetParent(transform);
                // Position it clearly above the drone height (which is at 2.0)
                statusIndicator.transform.localPosition = new Vector3(0f, 4.5f, 0f);
                statusIndicator.transform.localScale = new Vector3(4.0f, 0.4f, 4.0f); 
                
                Renderer r = statusIndicator.GetComponent<Renderer>();
                
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                
                indicatorMaterial = new Material(shader);
                indicatorMaterial.renderQueue = 3100; // Render on top
                r.sharedMaterial = indicatorMaterial;

                // Create text child
                GameObject textObj = new GameObject("StatusText");
                textObj.transform.SetParent(statusIndicator.transform);
                textObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Face up
                textObj.transform.localPosition = new Vector3(0f, 0.51f, 0f); // Just above cube surface
                
                statusText = textObj.AddComponent<TextMeshPro>();
                statusText.fontSize = 5f;
                statusText.alignment = TextAlignmentOptions.Center;
                statusText.color = Color.black;
                statusText.textWrappingMode = TextWrappingModes.NoWrap;
            }

            if (indicatorMaterial != null)
            {
                // Use bright neon colors
                Color displayColor = color;
                if (color == Color.red) displayColor = new Color(1f, 0.1f, 0.1f, 1f);
                else if (color == Color.green) displayColor = new Color(0.1f, 1f, 0.1f, 1f);
                else if (color == Color.cyan) displayColor = new Color(0.1f, 0.8f, 1f, 1f);

                if (indicatorMaterial.HasProperty("_BaseColor")) indicatorMaterial.SetColor("_BaseColor", displayColor);
                indicatorMaterial.color = displayColor;
                
                statusIndicator.SetActive(true);
            }

            if (statusText != null)
            {
                statusText.text = reason;
                // Only show text if status is red, as requested
                statusText.gameObject.SetActive(color == Color.red);
            }
        }

        public void MoveTo(Vector3 position)
        {
            graphAgent.SetVariableValue("TargetLocation", position);
            graphAgent.SetVariableValue<GameObject>("TargetGameObject", null);
            SetCurrentCommand(UnitCommands.Move);
        }

        public void MoveTo(Transform transform)
        {
            graphAgent.SetVariableValue("TargetGameObject", transform.gameObject);
            SetCurrentCommand(UnitCommands.Move);
        }

        public void Stop()
        {
            SetCommandOverrides(null);
            SetCurrentCommand(UnitCommands.Stop);
        }

        public void Attack(IDamageable damageable)
        {
            graphAgent.SetVariableValue("TargetGameObject", damageable.Transform.gameObject);
            SetCurrentCommand(UnitCommands.Attack);
        }

        public void Attack(Vector3 location)
        {
            graphAgent.SetVariableValue<GameObject>("TargetGameObject", null);
            graphAgent.SetVariableValue("TargetLocation", location);
            SetCurrentCommand(UnitCommands.Attack);
        }

        private void HandleUnitEnter(IDamageable damageable)
        {
            List<GameObject> nearbyEnemies = SetNearbyEnemiesOnBlackboard();

            if (graphAgent.GetVariable("TargetGameObject", out BlackboardVariable<GameObject> targetVariable)
                && targetVariable.Value == null && nearbyEnemies.Count > 0)
            {
                graphAgent.SetVariableValue("TargetGameObject", nearbyEnemies[0]);
            }
        }

        private void HandleUnitExit(IDamageable damageable)
        {
            List<GameObject> nearbyEnemies = SetNearbyEnemiesOnBlackboard();

            if (!graphAgent.GetVariable("TargetGameObject", out BlackboardVariable<GameObject> targetVariable)
                || damageable.Transform.gameObject != targetVariable.Value) return;

            if (nearbyEnemies.Count > 0)
            {
                graphAgent.SetVariableValue("TargetGameObject", nearbyEnemies[0]);
            }
            else
            {
                graphAgent.SetVariableValue<GameObject>("TargetGameObject", null);
                graphAgent.SetVariableValue("TargetLocation", damageable.Transform.position);
            }
        }

        private List<GameObject> SetNearbyEnemiesOnBlackboard()
        {
            List<GameObject> nearbyEnemies = DamageableSensor.Damageables
                            .ConvertAll(damageable => damageable.Transform.gameObject);
            nearbyEnemies.Sort(new ClosestGameObjectComparer(transform.position));

            graphAgent.SetVariableValue("NearbyEnemies", nearbyEnemies);

            return nearbyEnemies;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Bus<UnitDeathEvent>.Raise(Owner, new UnitDeathEvent(this));
        }
    }
}
