using System.Collections.Generic;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Utilities;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

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

            graphAgent.SetVariableValue("Command", UnitCommands.Stop);
            graphAgent.SetVariableValue("AttackConfig", unitSO.AttackConfig);
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

            UpdateStatusIndicator();
        }

        private void UpdateStatusIndicator()
        {
            Color statusColor = Color.red; // Default to "No-Go"

            if (Agent != null && Agent.isActiveAndEnabled && Agent.isOnNavMesh)
            {
                if (graphAgent != null && graphAgent.GetVariable("Command", out BlackboardVariable<UnitCommands> cmd))
                {
                    if (cmd.Value == UnitCommands.Stop)
                    {
                        statusColor = Color.cyan; // Idle/Healthy
                    }
                    else if (Agent.pathPending || (Agent.hasPath && Agent.pathStatus == NavMeshPathStatus.PathComplete))
                    {
                        statusColor = Color.green; // Active/Go
                    }
                }
            }

            SetStatusColor(statusColor);
        }

        private GameObject statusIndicator;
        private Material indicatorMaterial;

        public void SetStatusColor(Color color)
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
        }

        public void MoveTo(Vector3 position)
        {
            graphAgent.SetVariableValue("TargetLocation", position);
            graphAgent.SetVariableValue<GameObject>("TargetGameObject", null);
            graphAgent.SetVariableValue("Command", UnitCommands.Move);
        }

        public void MoveTo(Transform transform)
        {
            graphAgent.SetVariableValue("TargetGameObject", transform.gameObject);
            graphAgent.SetVariableValue("Command", UnitCommands.Move);
        }

        public void Stop()
        {
            SetCommandOverrides(null);
            graphAgent.SetVariableValue("Command", UnitCommands.Stop);
        }

        public void Attack(IDamageable damageable)
        {
            graphAgent.SetVariableValue("TargetGameObject", damageable.Transform.gameObject);
            graphAgent.SetVariableValue("Command", UnitCommands.Attack);
        }

        public void Attack(Vector3 location)
        {
            graphAgent.SetVariableValue<GameObject>("TargetGameObject", null);
            graphAgent.SetVariableValue("TargetLocation", location);
            graphAgent.SetVariableValue("Command", UnitCommands.Attack);
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
