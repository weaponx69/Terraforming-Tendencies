using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using System.Linq;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;

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

            // Enforce a maximum of 2 Command Centers per player
            if (Building.name.Contains("Command Post") || Building.name.Contains("Command Center"))
            {
                int commandPostCount = BaseBuilding.ActiveBuildings.Count(b => b.Owner == context.Owner && b.UnitSO == Building);
                if (commandPostCount >= 2) return false;
            }

            Vector3 targetPos = context.Hit.point;
            UnityEngine.AI.NavMeshQueryFilter filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = 0, areaMask = UnityEngine.AI.NavMesh.AllAreas };
            if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out UnityEngine.AI.NavMeshHit navHit, 20f, filter))
            {
                targetPos = navHit.position;
            }

            return HasEnoughSupplies(context) && AllRestrictionsPass(targetPos);
        }

        public override void Handle(CommandContext context)
        {
            IBuildingBuilder builder = context.Commandable as IBuildingBuilder;

            // If the unit issuing the command isn't a builder (e.g. Command Center), find the nearest idle drone
            if (builder == null)
            {
                float closestDist = float.MaxValue;
                Worker[] workers = FindObjectsByType<Worker>(FindObjectsSortMode.None);
                
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

            // Snap the placement position to the NavMesh so it spawns on the true ground, not on top of rock colliders
            Vector3 targetPos = context.Hit.point;
            UnityEngine.AI.NavMeshQueryFilter filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = 0, areaMask = UnityEngine.AI.NavMesh.AllAreas };
            if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out UnityEngine.AI.NavMeshHit navHit, 20f, filter))
            {
                targetPos = navHit.position;
            }

            if (builder == null)
            {
                // Instant-build fallback from orbit when player has NO workers at all
                GameObject instance = Instantiate(Building.Prefab, targetPos, Quaternion.identity);
                if (instance.TryGetComponent(out BaseBuilding newBuilding))
                {
                    newBuilding.enabled = true;
                    newBuilding.Owner = context.Owner;
                    newBuilding.CompleteConstruction();
                }

                // Crush any rocks/supplies underneath the orbital drop!
                Collider ghostHitbox = Building.Prefab.GetComponent<Collider>();
                if (ghostHitbox != null)
                {
                    Collider[] crushed = Physics.OverlapBox(
                        targetPos + ghostHitbox.bounds.center - Building.Prefab.transform.position,
                        ghostHitbox.bounds.extents,
                        Quaternion.identity,
                        LayerMask.GetMask("Supplies")
                    );
                    foreach (var rock in crushed)
                    {
                        Destroy(rock.gameObject);
                    }
                }

                Bus<SupplyEvent>.Raise(context.Owner, new SupplyEvent(context.Owner, -Building.Cost.Minerals, Building.Cost.MineralsSO));
                Bus<SupplyEvent>.Raise(context.Owner, new SupplyEvent(context.Owner, -Building.Cost.Gas, Building.Cost.GasSO));
                return;
            }

            if (context.Hit.collider != null && context.Hit.collider.TryGetComponent(out BaseBuilding building))
            {
                builder.ResumeBuilding(building);
            }
            else if (HasEnoughSupplies(context) && AllRestrictionsPass(targetPos))
            {
                builder.Build(Building, targetPos);
            }
        }

        public override bool AllRestrictionsPass(Vector3 point)
        {
            // First check base restrictions
            bool passes = base.AllRestrictionsPass(point);
            if (passes) return true;

            // If it failed, check if it's due to overlapping rocks during an Orbital Drop
            Worker[] workers = FindObjectsByType<Worker>(FindObjectsSortMode.None);
            bool hasWorker = false;
            foreach (var w in workers)
            {
                if (w.Owner == Owner.Player1 || w.Owner == Owner.Player2)
                {
                    hasWorker = true;
                    break;
                }
            }

            if (!hasWorker)
            {
                // It's an orbital drop! We want to ignore overlaps with rocks ("Supplies" layer).
                // Re-evaluate restrictions but ignore hits on the Supplies layer.
                foreach (BuildingRestrictionSO restriction in Restrictions)
                {
                    int hits = restriction.HitDetectionStyle switch
                    {
                        BuildingRestrictionSO.OverlapStyle.Sphere => Physics.OverlapSphere(point, restriction.Radius, restriction.LayerMask & ~LayerMask.GetMask("Supplies")).Length,
                        BuildingRestrictionSO.OverlapStyle.Box => Physics.OverlapBox(point, restriction.Extents, Quaternion.identity, restriction.LayerMask & ~LayerMask.GetMask("Supplies")).Length,
                        _ => 0
                    };

                    if (hits > 0) return false;

                    if (restriction.MustBeFullyOnNavmesh)
                    {
                        UnityEngine.AI.NavMeshQueryFilter queryFilter = new()
                        {
                            areaMask = UnityEngine.AI.NavMesh.AllAreas,
                            agentTypeID = restriction.NavMeshAgentTypeId
                        };

                        bool isOnNavMesh = UnityEngine.AI.NavMesh.SamplePosition(point + new Vector3(restriction.Extents.x, 0, restriction.Extents.z), out _, restriction.NavMeshTolerance, queryFilter)
                                        && UnityEngine.AI.NavMesh.SamplePosition(point + new Vector3(restriction.Extents.x, 0, -restriction.Extents.z), out _, restriction.NavMeshTolerance, queryFilter)
                                        && UnityEngine.AI.NavMesh.SamplePosition(point + new Vector3(-restriction.Extents.x, 0, -restriction.Extents.z), out _, restriction.NavMeshTolerance, queryFilter)
                                        && UnityEngine.AI.NavMesh.SamplePosition(point + new Vector3(-restriction.Extents.x, 0, restriction.Extents.z), out _, restriction.NavMeshTolerance, queryFilter);

                        if (!isOnNavMesh) return false;
                    }
                }
                // If we get here, the ONLY reason it failed originally was because it hit the Supplies layer.
                // Since this is an orbital drop, we allow it!
                return true;
            }

            return false;
        }

        public override bool IsLocked(CommandContext context) =>
            !HasEnoughSupplies(context) || (Building.TechTree != null && !Building.TechTree.IsUnlocked(context.Owner, Building));

        public UnlockableSO[] GetUnmetDependencies(Owner owner)
        {
            if (Building.TechTree == null) return new UnlockableSO[0];
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
