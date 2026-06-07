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

            // Target Selection Logic
            if (graphAgent.GetVariable(BlackboardConstants.TARGET_GAME_OBJECT, out BlackboardVariable<GameObject> targetVar))
            {
                GameObject currentTarget = targetVar.Value;
                
                // If we have no target, pick the highest priority one (which will be a meteor if any exist)
                if (currentTarget == null)
                {
                    graphAgent.SetVariableValue(BlackboardConstants.TARGET_GAME_OBJECT, sortedEnemies[0]);
                    return;
                }

                // If our current target is NOT a meteor, check if there is a meteor we should switch to
                bool currentIsMeteor = currentTarget.GetComponent<NaturalEventImpact>() != null;
                if (!currentIsMeteor)
                {
                    bool bestIsMeteor = sortedEnemies[0].GetComponent<NaturalEventImpact>() != null;
                    if (bestIsMeteor)
                    {
                        graphAgent.SetVariableValue(BlackboardConstants.TARGET_GAME_OBJECT, sortedEnemies[0]);
                    }
                }
            }
}
    }
}
