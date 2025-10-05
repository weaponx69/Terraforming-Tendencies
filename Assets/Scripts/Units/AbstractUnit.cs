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

        protected override void Awake()
        {
            base.Awake();

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
