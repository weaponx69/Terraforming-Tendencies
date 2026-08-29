using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;


namespace GameDevTV.RTS.Environment
{
    public class GlobalDecayManager : MonoBehaviour
    {
        [Header("Override Options")]
        [SerializeField, Tooltip("If true, the rates set here in the inspector will be used instead of those from the Planet Config.")]
        private bool overridePlanetConfig = false;

        [Header("Global Scaling")]
        [SerializeField, Range(0f, 5f), Tooltip("Multiplier applied to all decay rates after they are resolved (from PlanetConfig or inspector). 1.0 = normal, 0.25 = quarter speed.")]
        private float decayRateMultiplier = 1.0f;

        [Header("Decay Rates")]
        [SerializeField, Tooltip("How often decay ticks occur (in seconds).")]
        private float decayTickRate = 1.0f;

        [SerializeField, Tooltip("Damage dealt per second to buildings not protected by a Life Support node.")]
        private float baseDecayRate = 2f;

        [SerializeField, Tooltip("Damage dealt per second to units not protected by a Life Support node.")]
        private float integrityDamageRate = 0.5f;

        // Accumulators for fractional damage — allows dealing <1 damage per tick
        // without the Mathf.Max(1, ...) floor that made inspector tuning impossible.
        private float buildingDamageAccumulator;
        private float unitDamageAccumulator;

        private void Start()
            {
                if (!overridePlanetConfig && PlanetGenerator.Instance != null && PlanetGenerator.Instance.Config != null)
                {
                    baseDecayRate = PlanetGenerator.Instance.Config.BaseDecayRate;
                    integrityDamageRate = PlanetGenerator.Instance.Config.IntegrityDrainRate;
                }
    
                // Apply global multiplier to final rates
                baseDecayRate *= decayRateMultiplier;
                integrityDamageRate *= decayRateMultiplier;

            StartCoroutine(DecayLoop());
        }

        private IEnumerator DecayLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(decayTickRate);

                // Accumulate fractional damage from DPS × tick interval.
                // This allows tuning decayTickRate and damage rates independently without
                // the Mathf.Max(1, ...) floor that previously made inspector values ineffective.
                buildingDamageAccumulator += baseDecayRate * decayTickRate;
                unitDamageAccumulator += integrityDamageRate * decayTickRate;

                var lifeSupportNodes = LifeSupportNode.ActiveNodes;
                var allCommandables = AbstractCommandable.ActiveCommandables;
                int decayedCount = 0;

                for (int i = allCommandables.Count - 1; i >= 0; i--)
                {
                    AbstractCommandable target = allCommandables[i];
                    if (target == null) continue;

                    // Skip decay for objects that ARE LifeSupportNodes or are within range of one.
                    if (target.TryGetComponent<LifeSupportNode>(out _))
                    {
                        if (target.TryGetComponent<BaseBuilding>(out var targetBuilding) && !targetBuilding.IsOperating)
                        {
                            // Unpowered/non-operating life support buildings do not skip decay
                        }
                        else
                        {
                            continue;
                        }
                    }

                    bool isSupported = false;
                    foreach (var node in lifeSupportNodes)
                    {
                        if (node == null) continue;
                        if (node.TryGetComponent<BaseBuilding>(out var nodeBuilding) && !nodeBuilding.IsOperating)
                            continue;

                        if (Vector3.Distance(target.transform.position, node.transform.position) <= node.Radius)
                        {
                            isSupported = true;
                            break;
                        }
                    }

                    if (!isSupported)
                    {
                        // Skip decay for ghost buildings (paused state) or buildings under construction
                        if (target is BaseBuilding building && (building.Progress.State == BuildingProgress.BuildingState.Paused || building.Progress.State == BuildingProgress.BuildingState.Building))
                            continue;

                        // Use baseDecayRate for buildings and integrityDamageRate for units (DPS values).
                        // Accumulate fractional damage so we can deal <1 damage/tick without clamping.
                        int damage = (target is BaseBuilding)
                            ? Mathf.FloorToInt(buildingDamageAccumulator)
                            : Mathf.FloorToInt(unitDamageAccumulator);

                        if (damage > 0)
                        {
                            target.TakeDamage(damage);
                            decayedCount++;
                        }
                    }
                }

                // Keep fractional remainders for the next tick
                buildingDamageAccumulator -= Mathf.Floor(buildingDamageAccumulator);
                unitDamageAccumulator -= Mathf.Floor(unitDamageAccumulator);

                // Recalculate colony integrity from actual commandable health and push to the UI bar.
                Owner monitoredOwner = GameOverManager.MonitoredOwner;
                float integrity = Supplies.CalculateIntegrity(monitoredOwner);
                Supplies.UpdateIntegrity(monitoredOwner, integrity);
            }
        }
    }
}
