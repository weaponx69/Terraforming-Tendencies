using System;
using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.TechTree
{
    [CreateAssetMenu(fileName = "Tech Tree", menuName = "Tech Tree/Tech Tree", order = 1)]
    public class TechTreeSO : ScriptableObject
    {
        [SerializeField] private List<UnlockableSO> allUnlockables = new();
        public IEnumerable<UnlockableSO> AllUnlockables => allUnlockables.ToList();

        private Dictionary<Owner, Dictionary<UnlockableSO, Dependency>> techTrees;
        private Dictionary<Owner, HashSet<UnlockableSO>> unlockedDependencies;

        public bool IsUnlocked(Owner owner, UnlockableSO unlockable) =>
            techTrees[owner].TryGetValue(unlockable, out Dependency value) && value.IsUnlocked;
        public bool IsResearched(Owner owner, UnlockableSO unlockable) =>
            unlockedDependencies[owner].Contains(unlockable);
        public UnlockableSO[] GetUnmetDependencies(Owner owner, UnlockableSO unlockableSO)
        {
            if (techTrees[owner].TryGetValue(unlockableSO, out Dependency dependency))
            {
                return dependency.GetUnmetDependencies();
            }

            return Array.Empty<UnlockableSO>();
        }

        private void OnEnable()
        {
            if (techTrees == null)
            {
                BuildTechTrees();
            }

            Bus<BuildingSpawnEvent>.RegisterForAll(HandleBuildingSpawn);
            Bus<UpgradeResearchedEvent>.RegisterForAll(HandleUpgradeResearched);
            Bus<BuildingDeathEvent>.RegisterForAll(HandleBuildingDeath);
        }

        private void HandleUpgradeResearched(UpgradeResearchedEvent evt)
        {
            Debug.Log($"Researched {evt.Upgrade.Name} for {evt.Owner}!");
            unlockedDependencies[evt.Owner].Add(evt.Upgrade);

            foreach(KeyValuePair<UnlockableSO, Dependency> keyValuePair in techTrees[evt.Owner])
            {
                keyValuePair.Value.UnlockDependency(evt.Upgrade);
            }
        }

        private void OnDisable()
        {
            techTrees = null;
            Bus<BuildingSpawnEvent>.UnregisterForAll(HandleBuildingSpawn);
            Bus<UpgradeResearchedEvent>.UnregisterForAll(HandleUpgradeResearched);
            Bus<BuildingDeathEvent>.UnregisterForAll(HandleBuildingDeath);
        }

        private void HandleBuildingSpawn(BuildingSpawnEvent evt)
        {
            foreach(KeyValuePair<UnlockableSO, Dependency> keyValuePair in techTrees[evt.Owner])
            {
                keyValuePair.Value.UnlockDependency(evt.Building.BuildingSO);
            }
        }

        private void HandleBuildingDeath(BuildingDeathEvent evt)
        {
            foreach (KeyValuePair<UnlockableSO, Dependency> keyValuePair in techTrees[evt.Owner])
            {
                keyValuePair.Value.LoseDependency(evt.Building.BuildingSO);
            }
        }

        private void BuildTechTrees()
        {
            techTrees = new Dictionary<Owner, Dictionary<UnlockableSO, Dependency>>();
            unlockedDependencies = new Dictionary<Owner, HashSet<UnlockableSO>>();

            foreach(Owner owner in Enum.GetValues(typeof(Owner)))
            {
                techTrees.Add(owner, new Dictionary<UnlockableSO, Dependency>());
                unlockedDependencies.Add(owner, new HashSet<UnlockableSO>());

                foreach(UnlockableSO unlockableSO in allUnlockables)
                {
                    techTrees[owner].Add(unlockableSO, new Dependency(unlockableSO));
                }
            }
        }

        private readonly struct Dependency
        {
            public HashSet<UnlockableSO> Dependencies { get; }
            public bool IsUnlocked => Dependencies.Count == metDependencies.Count;
            private readonly Dictionary<UnlockableSO, int> metDependencies;

            public Dependency(UnlockableSO unlockable)
            {
                Dependencies = new HashSet<UnlockableSO>(unlockable.UnlockRequirements);
                metDependencies = new Dictionary<UnlockableSO, int>(Dependencies.Count);
            }

            public UnlockableSO[] GetUnmetDependencies()
            {
                Dictionary<UnlockableSO, int> metDependencies = this.metDependencies;
                return Dependencies.Where(dependency => !metDependencies.ContainsKey(dependency)).ToArray();
            }

            public void UnlockDependency(UnlockableSO dependency)
            {
                if (Dependencies.Contains(dependency) && !metDependencies.TryAdd(dependency, 1))
                {
                    metDependencies[dependency]++;
                }
            }

            public void LoseDependency(UnlockableSO dependency)
            {
                if (dependency.IsOneTimeUnlock || !metDependencies.TryGetValue(dependency, out int count)) return;

                count--;

                if (count > 0)
                {
                    metDependencies[dependency] = count;
                }
                else
                {
                    metDependencies.Remove(dependency);
                }
            }
        }
    }
}