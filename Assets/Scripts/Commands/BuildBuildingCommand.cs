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
            // If the commandable itself is a builder and is already building, abort
            if (context.Commandable is IBuildingBuilder b && b.IsBuilding) return false;

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
            IBuildingBuilder builder = context.Commandable as IBuildingBuilder;

            // If the unit issuing the command isn't a builder (e.g. Command Center), find the nearest idle drone
            if (builder == null)
            {
                float closestDist = float.MaxValue;
                Worker[] workers = FindObjectsOfType<Worker>();
                
                foreach (var w in workers)
                {
                    if (w.Owner == context.Owner && !w.IsBuilding)
                    {
                        float dist = Vector3.Distance(w.transform.position, context.Hit.point);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            builder = w;
                        }
                    }
                }
            }

            if (builder == null)
            {
                // // Debug.LogWarning("No available drones to construct the building!");
                return;
            }

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
