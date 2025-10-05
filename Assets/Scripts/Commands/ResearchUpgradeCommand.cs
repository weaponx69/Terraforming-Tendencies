using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Commands
{
    [CreateAssetMenu(fileName = "Research Upgrade", menuName = "Tech Tree/Research Upgrade Command", order = 140)]
    public class ResearchUpgradeCommand : BaseCommand
    {
        [field: SerializeField] public UpgradeSO Upgrade { get; private set; }

        private Dictionary<Owner, BaseBuilding.QueueUpdatedEvent> updateQueue = new();

        public override bool CanHandle(CommandContext context)
        {
            return context.Commandable is BaseBuilding;
        }

        public override void Handle(CommandContext context)
        {
            BaseBuilding building = context.Commandable as BaseBuilding;

            if (HasEnoughSupplies(context))
            {
                building.BuildUnlockable(Upgrade);

                if (updateQueue.TryAdd(context.Owner, GetQueueUpdatedFunction(context.Owner, building)))
                {
                    building.OnQueueUpdated += updateQueue[context.Owner];
                }
            }
        }

        private BaseBuilding.QueueUpdatedEvent GetQueueUpdatedFunction(Owner owner, BaseBuilding building)
        {
            return (unlockables) => HandleQueueUpdated(owner, building, unlockables);
        }

        private void HandleQueueUpdated(Owner owner, BaseBuilding building, UnlockableSO[] unitsInQueue)
        {
            Debug.Log($"Handle Queue Updated in {Name}");
            if (!unitsInQueue.Contains(Upgrade))
            {
                building.OnQueueUpdated -= updateQueue[owner];
                updateQueue.Remove(owner);
            }
        }

        public override bool IsLocked(CommandContext context)
        {
            bool isLocked = !HasEnoughSupplies(context) || !Upgrade.TechTree.IsUnlocked(context.Owner, Upgrade);

            if (!isLocked && Upgrade.IsOneTimeUnlock && context.Commandable != null
                && context.Commandable is BaseBuilding)
            {
                isLocked = updateQueue.ContainsKey(context.Owner);
            }

            return isLocked;
        }

        public override bool IsAvailable(CommandContext context)
        {
            if (Upgrade.IsOneTimeUnlock && Upgrade.TechTree.IsResearched(context.Owner, Upgrade))
            {
                return false;
            }

            return Upgrade.TechTree.IsUnlocked(context.Owner, Upgrade);
        }

        private bool HasEnoughSupplies(CommandContext context)
        {
            int biomassCost = Mathf.FloorToInt(Upgrade.Cost.Minerals * Supplies.MineralsToBiomassRateStatic
                + Upgrade.Cost.Gas * Supplies.GasToBiomassRateStatic);
            return biomassCost <= Supplies.Biomass[context.Owner];
        }
    }
}
