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
            if (sensor == null) return;

            List<IDamageable> targets = sensor.Damageables;
            if (targets == null || targets.Count == 0) return;

            // Remove any null or destroyed entries before sorting
            targets.RemoveAll(t => t == null || t.Transform == null);
            if (targets.Count == 0) return;

            // Sort: NaturalEventImpact (Meteors) first, then by distance
            targets.Sort((a, b) =>
            {
                if (a == null || a.Transform == null) return 1;
                if (b == null || b.Transform == null) return -1;

                bool aIsMeteor = a.Transform.GetComponent<NaturalEventImpact>() != null;
                bool bIsMeteor = b.Transform.GetComponent<NaturalEventImpact>() != null;

                if (aIsMeteor && !bIsMeteor) return -1;
                if (!aIsMeteor && bIsMeteor) return 1;

                float distA = Vector3.Distance(transform.position, a.Transform.position);
                float distB = Vector3.Distance(transform.position, b.Transform.position);
                return distA.CompareTo(distB);
            });

            // Update the blackboard with the prioritized list
            List<GameObject> sortedEnemies = new List<GameObject>();
            foreach (var t in targets)
            {
                if (t != null && t.Transform != null) sortedEnemies.Add(t.Transform.gameObject);
            }

            graphAgent.SetVariableValue(BlackboardConstants.NEARBY_ENEMIES, sortedEnemies);

            // Target Selection Logic
            if (sortedEnemies.Count > 0 && graphAgent.GetVariable(BlackboardConstants.TARGET_GAME_OBJECT, out BlackboardVariable<GameObject> targetVar))
            {
                GameObject currentTarget = targetVar.Value;
                
                // If we have no target, pick the highest priority one (which will be a meteor if any exist)
                if (currentTarget == null)
                {
                    graphAgent.SetVariableValue(BlackboardConstants.TARGET_GAME_OBJECT, sortedEnemies[0]);
                    return;
                }

                // If our current target is NOT a meteor, check if there is a meteor we should switch to
                bool currentIsMeteor = currentTarget != null && currentTarget.GetComponent<NaturalEventImpact>() != null;
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
