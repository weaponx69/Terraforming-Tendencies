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
        [field: SerializeField] public BuildingSO Building { get; set; }

        public Vector3 SnapToNearestSector(Vector3 point)
        {
            if (Building != null && Building.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase))
            {
                var sector = GameDevTV.RTS.Environment.SectorManager.Instance?.GetNearestSector(point);
                if (sector != null)
                {
                    // Try exact center first
                    if (AllRestrictionsPass(sector.Center)) return sector.Center;

                    // If center is blocked (e.g. by another building), try to find a valid spot nearby within the sector radius
                    float maxSearchRadius = GameDevTV.RTS.Environment.PlanetGenerator.Instance.Config.SectorOccupationRadius;
                    
                    // Spiral search for a valid spot
                    for (int ring = 1; ring <= 5; ring++)
                    {
                        float currentRadius = (maxSearchRadius / 5f) * ring;
                        int pointsInRing = ring * 8;
                        for (int i = 0; i < pointsInRing; i++)
                        {
                            float angle = i * (360f / pointsInRing) * Mathf.Deg2Rad;
                            Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * currentRadius;
                            Vector3 candidate = sector.Center + offset;

                            // Adjust to NavMesh height
                            UnityEngine.AI.NavMeshQueryFilter filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = 0, areaMask = UnityEngine.AI.NavMesh.AllAreas };
                            if (UnityEngine.AI.NavMesh.SamplePosition(candidate, out UnityEngine.AI.NavMeshHit navHit, 5f, filter))
                            {
                                candidate = navHit.position;
                            }

                            if (AllRestrictionsPass(candidate)) return candidate;
                        }
                    }

                    return sector.Center; // Fallback
                }
            }
            return point;
        }

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

            // Removed maximum Command Center limit to allow building multiple bases.
            
            // Check horizontal distance
            Vector3 targetPos = SnapToNearestSector(context.Hit.point);
            UnityEngine.AI.NavMeshQueryFilter filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = 0, areaMask = UnityEngine.AI.NavMesh.AllAreas };
            if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out UnityEngine.AI.NavMeshHit navHit, 20f, filter))
            {
                targetPos = navHit.position;
            }

            return HasEnoughSupplies(context) && AllRestrictionsPass(targetPos, context.Owner);
        }

        public override void Handle(CommandContext context)
        {
            IBuildingBuilder builder = context.Commandable as IBuildingBuilder;

            // Snap the placement position to the NavMesh so it spawns on the true ground, not on top of rock colliders
            Vector3 targetPos = SnapToNearestSector(context.Hit.point);
            UnityEngine.AI.NavMeshQueryFilter filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = 0, areaMask = UnityEngine.AI.NavMesh.AllAreas };
            if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out UnityEngine.AI.NavMeshHit navHit, 20f, filter))
            {
                targetPos = navHit.position;
            }

            // If the unit issuing the command isn't a builder (e.g. Command Center or Global Commander), find the nearest idle drone
            if (builder == null)
            {
                float closestDist = float.MaxValue;
                Worker[] workers = FindObjectsByType<Worker>(FindObjectsInactive.Exclude);
                
                foreach (var w in workers)
                {
                    if (w.Owner == context.Owner && !w.IsBuilding)
                    {
                        float dist = Vector3.Distance(w.transform.position, targetPos);
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
                bool isCommandPost = Building != null && (Building.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase));
                if (!isCommandPost)
                {
                    // // Debug.LogWarning("Only Command Centers can be orbital dropped! You must build a worker first.");
                    return;
                }

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

                if (Building.Cost != null)
                {
                    Bus<SupplyEvent>.Raise(context.Owner, new SupplyEvent(context.Owner, -Building.Cost.Minerals, Building.Cost.MineralsSO));
                    Bus<SupplyEvent>.Raise(context.Owner, new SupplyEvent(context.Owner, -Building.Cost.Gas, Building.Cost.GasSO));
                }
                return;
            }

            if (context.Hit.collider != null && context.Hit.collider.TryGetComponent(out BaseBuilding building))
            {
                builder.ResumeBuilding(building);
            }
            else if (HasEnoughSupplies(context))
            {
                bool pass = AllRestrictionsPass(targetPos, context.Owner);
                if (pass)
                {
                    builder.Build(Building, targetPos);
                }
            }
        }

        public override bool AllRestrictionsPass(Vector3 point)
        {
            return AllRestrictionsPass(point, Owner.Player1);
        }

        public bool AllRestrictionsPass(Vector3 point, Owner owner)
        {
            // Evaluate restrictions directly to ignore NavMesh holes!
            // The ground is covered in rocks which have NavMeshObstacles. This creates holes in the NavMesh.
            // If we strictly check IsFullyOnNavMesh, players can never place buildings!
            foreach (BuildingRestrictionSO restriction in Restrictions)
            {
                int hits = restriction.HitDetectionStyle switch
                {
                    BuildingRestrictionSO.OverlapStyle.Sphere => Physics.OverlapSphere(point, restriction.Radius, restriction.LayerMask).Length,
                    BuildingRestrictionSO.OverlapStyle.Box => Physics.OverlapBox(point, restriction.Extents, Quaternion.identity, restriction.LayerMask).Length,
                    _ => 0
                };

                if (hits > 0)
                {
                    // Command posts crush supplies, so ignore those restrictions
                    bool isCommandPost = Building != null && Building.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase);
                    bool isSuppliesRestriction = (restriction.LayerMask.value & LayerMask.GetMask("Supplies")) != 0;
                    
                    if (isCommandPost && isSuppliesRestriction) continue;
                    
                    return false;
                }
            }

            // Enforce worker requirement for standard buildings
            bool isCP = Building != null && Building.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase);
            if (!isCP)
            {
                Worker[] workers = FindObjectsByType<Worker>(FindObjectsInactive.Exclude);
                bool hasWorker = false;
                foreach (var w in workers)
                {
                    if (w.Owner == owner)
                    {
                        hasWorker = true;
                        break;
                    }
                }

                if (!hasWorker) return false;
            }

            return true;
        }

        public override bool IsLocked(CommandContext context)
        {
            if (Building == null) return false;
            if (!BlueprintDraftManager.IsBuildingUnlocked(Building)) return true;
            return !HasEnoughSupplies(context) || (Building.TechTree != null && !Building.TechTree.IsUnlocked(context.Owner, Building));
        }

        public UnlockableSO[] GetUnmetDependencies(Owner owner)
        {
            if (Building.TechTree == null) return new UnlockableSO[0];
            return Building.TechTree.GetUnmetDependencies(owner, Building);
        }

        private bool HasEnoughSupplies(CommandContext context)
        {
            if (Building == null || Building.Cost == null) return true;

            // Materials replaces minerals/gas. Compute materials-equivalent cost.
            int materialsCost = Mathf.FloorToInt(Building.Cost.Minerals * Supplies.MineralsToMaterialsRateStatic
                + Building.Cost.Gas * Supplies.GasToMaterialsRateStatic);
            
            if (Supplies.Materials == null) return false;

            return materialsCost <= Supplies.Materials[context.Owner];
        }
    }
}
