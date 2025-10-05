using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.Commands
{
    [CreateAssetMenu(fileName = "Build Building", menuName = "Units/Commands/Build Building")]
    public class BuildBuildingCommand : BaseCommand, IUnlockableCommand
    {
        [field: SerializeField] public BuildingSO Building { get; private set; }

        public override bool CanHandle(CommandContext context)
        {
            if (context.Commandable is not IBuildingBuilder buildingBuilder || buildingBuilder.IsBuilding) return false;

            if (context.Hit.collider != null && context.Button == MouseButton.Right)
            {
                return context.Hit.collider.TryGetComponent(out BaseBuilding building)
                    && Building == building.BuildingSO
                       && (building.Progress.State == BuildingProgress.BuildingState.Paused
                           || building.Progress.State == BuildingProgress.BuildingState.Destroyed
                       );
            }

            return HasEnoughSupplies(context) && AllRestrictionsPass(context.Hit.point);
        }

        public override void Handle(CommandContext context)
        {
            IBuildingBuilder builder = (IBuildingBuilder)context.Commandable;

            if (context.Hit.collider != null && context.Hit.collider.TryGetComponent(out BaseBuilding building))
            {
                builder.ResumeBuilding(building);
            }
            else if (HasEnoughSupplies(context) && AllRestrictionsPass(context.Hit.point))
            {
                builder.Build(Building, context.Hit.point);
            }
        }

        public override bool IsLocked(CommandContext context) =>
            !HasEnoughSupplies(context) || !Building.TechTree.IsUnlocked(context.Owner, Building);

        public UnlockableSO[] GetUnmetDependencies(Owner owner)
        {
            return Building.TechTree.GetUnmetDependencies(owner, Building);
        }

        private bool HasEnoughSupplies(CommandContext context)
        {
            // Biomass replaces minerals/gas. Compute biomass-equivalent cost.
            int biomassCost = Mathf.FloorToInt(Building.Cost.Minerals * Supplies.MineralsToBiomassRateStatic
                + Building.Cost.Gas * Supplies.GasToBiomassRateStatic);
            return biomassCost <= Supplies.Biomass[context.Owner];
        }
    }
}
