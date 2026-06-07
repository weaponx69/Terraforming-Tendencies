using System.Collections.Generic;
using UnityEngine;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Utilities;
using Unity.Behavior;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// A specialized military drone that prioritizes destroying environmental threats
    /// like meteors (<see cref="NaturalEventImpact"/>) before attacking other enemies.
    /// </summary>
    public class MeteorWarriorDrone : BaseMilitaryUnit
    {
        [Header("Defense Settings")]
        [Tooltip("If true, the drone will always pick a meteor over a standard enemy unit.")]
        [SerializeField] private bool prioritizeMeteors = true;

        protected override void Update()
        {
            base.Update();
            
            // Periodically refresh target prioritization if we are in attack mode
            if (prioritizeMeteors && GetCurrentCommand() == UnitCommands.Attack)
            {
                ReprioritizeTargets();
            }
        }

        private void ReprioritizeTargets()
        {
            if (graphAgent == null) return;

            // Get the list of nearby enemies from the sensor
            var sensor = GetComponentInChildren<DamageableSensor>();
            if (sensor == null || sensor.Damageables.Count == 0) return;

            List<IDamageable> targets = sensor.Damageables;
            
            // Sort: NaturalEventImpact (Meteors) first, then by distance
            targets.Sort((a, b) =>
            {
                bool aIsMeteor = a.Transform.GetComponent<NaturalEventImpact>() != null;
                bool bIsMeteor = b.Transform.GetComponent<NaturalEventImpact>() != null;

                if (aIsMeteor && !bIsMeteor) return -1;
                if (!aIsMeteor && bIsMeteor) return 1;

                float distA = Vector3.Distance(transform.position, a.Transform.position);
                float distB = Vector3.Distance(transform.position, b.Transform.position);
                return distA.CompareTo(distB);
            });

            // Update the blackboard with the prioritized list
            List<GameObject> sortedEnemies = targets.ConvertAll(t => t.Transform.gameObject);
            graphAgent.SetVariableValue(BlackboardConstants.NEARBY_ENEMIES, sortedEnemies);

            // If we don't have a target or our current target isn't a meteor but one is available, switch.
            if (graphAgent.GetVariable(BlackboardConstants.TARGET_GAME_OBJECT, out BlackboardVariable<GameObject> targetVar))
            {
                GameObject currentTarget = targetVar.Value;
                bool targetIsMeteor = currentTarget != null && currentTarget.GetComponent<NaturalEventImpact>() != null;

                if (!targetIsMeteor && targets[0].Transform.GetComponent<NaturalEventImpact>() != null)
                {
                    graphAgent.SetVariableValue(BlackboardConstants.TARGET_GAME_OBJECT, targets[0].Transform.gameObject);
                }
            }
        }
    }
}
