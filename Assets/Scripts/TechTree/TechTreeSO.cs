using System;
using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Player;
using UnityEngine;

namespace GameDevTV.RTS.TechTree
{
    [CreateAssetMenu(fileName = "Tech Tree", menuName = "Tech Tree/Tech Tree", order = 1)]
    public class TechTreeSO : ScriptableObject
    {
        [SerializeField] private List<UnlockableSO> allUnlockables = new();
        public IEnumerable<UnlockableSO> AllUnlockables => allUnlockables?.Where(u => u != null).ToList() ?? Enumerable.Empty<UnlockableSO>();

        private Dictionary<Owner, Dictionary<UnlockableSO, Dependency>> techTrees;
        private Dictionary<Owner, HashSet<UnlockableSO>> unlockedDependencies;

        public bool IsUnlocked(Owner owner, UnlockableSO unlockable)
        {
            if (techTrees == null || !techTrees.ContainsKey(owner)) return true;
            return techTrees[owner].TryGetValue(unlockable, out Dependency value) && value.IsUnlocked;
        }

        public bool IsResearched(Owner owner, UnlockableSO unlockable)
        {
            if (unlockedDependencies == null || !unlockedDependencies.ContainsKey(owner)) return false;
            return unlockedDependencies[owner].Contains(unlockable);
        }
    public UnlockableSO[] GetUnmetDependencies(Owner owner, UnlockableSO unlockableSO)
    {
        if (techTrees == null || !techTrees.ContainsKey(owner)) return Array.Empty<UnlockableSO>();
        if (techTrees[owner].TryGetValue(unlockableSO, out Dependency dependency))
        {
            return dependency.GetUnmetDependencies();
        }

        return Array.Empty<UnlockableSO>();
    }

    public bool HasCompletedRound(Owner owner)
    {
        return GenerationManager.Instance != null && GenerationManager.Instance.IsExpansionPhase;
    }

    private void OnEnable()
    {
        try
        {
            BuildTechTrees();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TechTreeSO] Failed to build tech trees: {ex.Message}");
        }

        Bus<BuildingSpawnEvent>.RegisterForAll(HandleBuildingSpawn);
        Bus<UpgradeResearchedEvent>.RegisterForAll(HandleUpgradeResearched);
        Bus<BuildingDeathEvent>.RegisterForAll(HandleBuildingDeath);
        Bus<UnitSpawnEvent>.RegisterForAll(HandleUnitSpawn);
        Bus<UnitDeathEvent>.RegisterForAll(HandleUnitDeath);
    }

    private void HandleUpgradeResearched(UpgradeResearchedEvent evt)
    {
        // // Debug.Log($"Researched {evt.Upgrade.Name} for {evt.Owner}!");
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
        Bus<UnitSpawnEvent>.UnregisterForAll(HandleUnitSpawn);
        Bus<UnitDeathEvent>.UnregisterForAll(HandleUnitDeath);
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

    private void HandleUnitSpawn(UnitSpawnEvent evt)
    {
        foreach (KeyValuePair<UnlockableSO, Dependency> keyValuePair in techTrees[evt.Unit.Owner])
        {
            keyValuePair.Value.UnlockDependency(evt.Unit.UnitSO);
        }
    }

    private void HandleUnitDeath(UnitDeathEvent evt)
    {
        if (evt.Unit == null || evt.Unit.UnitSO == null) return;

        foreach (KeyValuePair<UnlockableSO, Dependency> keyValuePair in techTrees[evt.Unit.Owner])
        {
            keyValuePair.Value.LoseDependency(evt.Unit.UnitSO);
        }
    }

    private void BuildTechTrees()
    {
        techTrees = new Dictionary<Owner, Dictionary<UnlockableSO, Dependency>>();
        unlockedDependencies = new Dictionary<Owner, HashSet<UnlockableSO>>();

        foreach(Owner owner in Enum.GetValues(typeof(Owner)))
        {
            techTrees[owner] = new Dictionary<UnlockableSO, Dependency>();
            unlockedDependencies[owner] = new HashSet<UnlockableSO>();

            foreach(UnlockableSO unlockableSO in allUnlockables)
            {
                if (unlockableSO == null) continue;
                techTrees[owner][unlockableSO] = new Dependency(unlockableSO);
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
}//namespace