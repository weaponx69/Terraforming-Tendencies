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
        private List<BaseBuilding> activeBuildings = new List<BaseBuilding>();

        private float decayTickRate = 1f;
        private float baseDecayRate = 2f;

        private void Start()
        {
            if (PlanetGenerator.Instance != null && PlanetGenerator.Instance.Config != null)
            {
                baseDecayRate = PlanetGenerator.Instance.Config.BaseDecayRate;
            }

            Bus<BuildingSpawnEvent>.RegisterForAll(HandleBuildingSpawn);
            Bus<BuildingDeathEvent>.RegisterForAll(HandleBuildingDeath);

            StartCoroutine(DecayLoop());
        }

        private void OnDestroy()
        {
            Bus<BuildingSpawnEvent>.UnregisterForAll(HandleBuildingSpawn);
            Bus<BuildingDeathEvent>.UnregisterForAll(HandleBuildingDeath);
        }

        private void HandleBuildingSpawn(BuildingSpawnEvent evt)
        {
            if (evt.Building != null && !activeBuildings.Contains(evt.Building))
            {
                activeBuildings.Add(evt.Building);
            }
        }

        private void HandleBuildingDeath(BuildingDeathEvent evt)
        {
            if (evt.Building != null)
            {
                activeBuildings.Remove(evt.Building);
            }
        }

        private IEnumerator DecayLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(decayTickRate);

                // All LifeSupportNode components in the scene (including those dynamically added to buildings).
                LifeSupportNode[] lifeSupportNodes = FindObjectsByType<LifeSupportNode>(FindObjectsInactive.Exclude);

                for (int i = activeBuildings.Count - 1; i >= 0; i--)
                {
                    BaseBuilding building = activeBuildings[i];
                    if (building == null)
                    {
                        activeBuildings.RemoveAt(i);
                        continue;
                    }

                    // A building that IS a LifeSupportNode protects itself — skip decay entirely.
                    if (building.TryGetComponent<LifeSupportNode>(out _))
                        continue;

                    bool isSupported = false;
                    foreach (var node in lifeSupportNodes)
                    {
                        if (Vector3.Distance(building.transform.position, node.transform.position) <= node.Radius)
                        {
                            isSupported = true;
                            break;
                        }
                    }

                    if (!isSupported)
                    {
                        int damage = Mathf.RoundToInt(baseDecayRate * decayTickRate);
                        if (damage > 0)
                        {
                            if (building is IDamageable damageable)
                            {
                                damageable.TakeDamage(damage);
                            }
                            else
                            {
                                var d = building.GetComponent<IDamageable>();
                                if (d != null) d.TakeDamage(damage);
                            }
                        }
                    }
                }
            }
        }
    }
}
