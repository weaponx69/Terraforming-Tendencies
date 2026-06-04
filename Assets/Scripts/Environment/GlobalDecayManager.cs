using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;

namespace GameDevTV.RTS.Environment
{
    public class GlobalDecayManager : MonoBehaviour
    {
        private float decayTickRate = 1f;
        private float baseDecayRate = 2f;
        private float integrityDamageRate = 0.5f;

        private void Start()
        {
            if (PlanetGenerator.Instance != null && PlanetGenerator.Instance.Config != null)
            {
                baseDecayRate = PlanetGenerator.Instance.Config.BaseDecayRate;
                integrityDamageRate = PlanetGenerator.Instance.Config.IntegrityDrainRate;
            }

            StartCoroutine(DecayLoop());
        }

        private IEnumerator DecayLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(decayTickRate);

                LifeSupportNode[] lifeSupportNodes = FindObjectsByType<LifeSupportNode>(FindObjectsInactive.Exclude);
                AbstractCommandable[] allCommandables = FindObjectsByType<AbstractCommandable>(FindObjectsInactive.Exclude);

                for (int i = 0; i < allCommandables.Length; i++)
                {
                    AbstractCommandable target = allCommandables[i];
                    if (target == null) continue;

                    // Skip decay for objects that ARE LifeSupportNodes or are within range of one.
                    if (target.TryGetComponent<LifeSupportNode>(out _))
                        continue;

                    bool isSupported = false;
                    foreach (var node in lifeSupportNodes)
                    {
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

                        // Use baseDecayRate for buildings and integrityDamageRate for units?
// Or just combine them. User wants Integrity to reflect HP.
                        float damageRate = (target is BaseBuilding) ? baseDecayRate : integrityDamageRate;
                        int damage = Mathf.RoundToInt(damageRate * decayTickRate);
                        
                        if (damage > 0)
                        {
                            target.TakeDamage(damage);
                        }
                    }
                }
            }
        }
    }
}
