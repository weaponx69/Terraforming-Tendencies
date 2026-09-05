using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Environment;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using System.Linq;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.VisualScriptingStubs;

namespace GameDevTV.RTS.Commands
{
    /// <summary>
    /// Building placement command. Heavy logic (NavMesh sampling, sector snapping,
    /// Physics.OverlapBox orbital-drop crushing, spiral search) stays in C#.
    /// VS reads <see cref="Building"/> and supply state.
    /// </summary>
    [IncludeInSettings(true)]
    [CreateAssetMenu(fileName = "Build Building", menuName = "Units/Commands/Build Building")]
    public class BuildBuildingCommand : BaseCommand, IUnlockableCommand
    {
        /// <summary>The BuildingSO this command constructs.</summary>
        [Inspectable]
        [field: SerializeField] public BuildingSO Building { get; set; }

        /// <summary>
        /// Returns true if this building is a command-type building (Command Center, Command Post, etc.)
        /// that should auto-place without requiring a worker selection.
        /// </summary>
        public bool IsCommandBuilding =>
            Building != null && Building.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase);

        public Vector3 SnapToNearestSector(Vector3 point)
        {
            // Do NOT snap player-placed command buildings to the sector center.
            // This allows placing them anywhere within the sector, avoiding starting resources.
            return point;
        }

        public override bool CanHandle(CommandContext context)
        {
            // If the commandable itself is a builder and is already building, abort
            if (context.Commandable is IBuildingBuilder b && b.IsBuilding) return false;

            if (context.Hit.collider != null && context.Button == UnityEngine.InputSystem.LowLevel.MouseButton.Right)
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

            // Prevent building in locked sectors
            var sector = GameDevTV.RTS.Environment.SectorManager.Instance?.GetNearestSector(targetPos);
            if (sector != null && sector.IsLocked)
            {
                return false;
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

            // Check if this is the player's very first Command Post
            bool isCommandPost = Building != null && (Building.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase));
            bool isFirstCommandPost = false;
            if (isCommandPost)
            {
                int existingCount = 0;
                var buildings = FindObjectsByType<BaseBuilding>(FindObjectsInactive.Exclude);
                foreach (var b in buildings)
                {
                    if (b != null && b.Owner == context.Owner && b.BuildingSO != null
                        && b.BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase))
                    {
                        // Check if it's a player-placed building (which Unity names with "(Clone)")
                        if (b.name.Contains("Clone", System.StringComparison.OrdinalIgnoreCase))
                        {
                            existingCount++;
                        }
                    }
                }

                // First Command Post if no player-placed Command Post exists yet.
                // GlobalCommander (Universal Command Center) is the editor-placed starting base,
                // not a player Command Post, so we ignore it for this check.
                if (existingCount == 0)
                {
                    isFirstCommandPost = true;
                }
            }

            if (isFirstCommandPost)
            {
                builder = null;
            }
            else
            {
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
            }

            if (builder == null)
            {
                isCommandPost = Building != null && (Building.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase));
                if (!isCommandPost || !isFirstCommandPost)
                {
                    ExplorationManager.NotifyExplorationFailed("A drone is needed.");
                    Debug.LogWarning($"[BuildBuildingCommand] No drone available to build {Building?.Name}.");
                    return;
                }

                // Instant orbital drop for the very first Command Post only (no drones yet).
                GameObject instance = Instantiate(Building.Prefab, targetPos, Quaternion.identity);
                if (instance.TryGetComponent(out BaseBuilding newBuilding))
                {
                    newBuilding.enabled = true;
                    newBuilding.Owner = context.Owner;
                    newBuilding.CompleteConstruction();
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

                    // Consume blueprint immediately on placement
                    BlueprintDraftManager.LockBuilding(Building.Name);
                    if (CardDeckController.Instance != null)
                    {
                        CardDeckController.Instance.DrawCard();
                    }

                    // Trigger PlayerActed event
                    if (GameFlowManager.Instance != null)
                    {
                        GameFlowManager.Instance.PlayerActed();
                    }
                }
                else
                {
                    Debug.LogWarning($"[BuildBuildingCommand] Silent failure: AllRestrictionsPass failed at {targetPos} for building {Building.Name}");
                }
            }
            else
            {
                Debug.LogWarning($"[BuildBuildingCommand] Silent failure: Insufficient resources to build {Building.Name}");
            }
        }

        public override bool AllRestrictionsPass(Vector3 point)
        {
            return AllRestrictionsPass(point, Owner.Player1);
        }

        public bool AllRestrictionsPass(Vector3 point, Owner owner, bool requireWorker = true)
        {
            // If this is a Command Post, prevent placing multiple Command Posts in the same sector.
            // Ignore GlobalCommander (editor-placed starting base) — only count player-built "(Clone)" buildings.
            bool isCommandBldg = Building != null && Building.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase);
            if (isCommandBldg)
            {
                var sectorManager = GameDevTV.RTS.Environment.SectorManager.Instance;
                var sector = sectorManager?.GetNearestSector(point);
                if (sector != null)
                {
                    // Check if any player-built Command Post is already in this sector (completed or under construction)
                    var buildings = FindObjectsByType<BaseBuilding>(FindObjectsInactive.Include);
                    foreach (var b in buildings)
                    {
                        if (b != null && b.Owner == owner && b.BuildingSO != null
                            && b.BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase)
                            && !b.name.Contains("Ghost", System.StringComparison.OrdinalIgnoreCase)
                            && b.name.Contains("Clone", System.StringComparison.OrdinalIgnoreCase))
                        {
                            if (b.Progress.State != BuildingProgress.BuildingState.Destroyed
                                && sectorManager.GetNearestSector(b.transform.position) == sector)
                            {
                                return false;
                            }
                        }
                    }
                }
            }

            // Evaluate restrictions directly to ignore NavMesh holes!
            // The ground is covered in rocks which have NavMeshObstacles. This creates holes in the NavMesh.
            // If we strictly check IsFullyOnNavMesh, players can never place buildings!
            if (Restrictions != null)
            {
                bool isCommandBldgRestriction = Building != null && Building.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase);

                foreach (BuildingRestrictionSO restriction in Restrictions)
                {
                    Collider[] colliders = restriction.HitDetectionStyle switch
                    {
                        BuildingRestrictionSO.OverlapStyle.Sphere => Physics.OverlapSphere(point, restriction.Radius, restriction.LayerMask),
                        BuildingRestrictionSO.OverlapStyle.Box => Physics.OverlapBox(point, restriction.Extents, Quaternion.identity, restriction.LayerMask),
                        _ => System.Array.Empty<Collider>()
                    };

                    int activeHits = 0;
                    foreach (var col in colliders)
                    {
                        if (col == null) continue;

                        var commandable = col.GetComponentInParent<AbstractCommandable>();
                        if (commandable != null)
                        {
                            // If placing a Command Post, ignore any editor pre-placed buildings (e.g. Universal Command Center / UCC starting base)
                            if (isCommandBldgRestriction && !commandable.name.Contains("Clone", System.StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            if (commandable is BaseBuilding bld && bld.Progress.State == BuildingProgress.BuildingState.Destroyed)
                            {
                                continue;
                            }
                        }

                        activeHits++;
                    }

                    if (activeHits > 0)
                    {
                        // Command posts crush supplies, so ignore those restrictions
                        bool isSuppliesRestriction = (restriction.LayerMask.value & LayerMask.GetMask("Supplies")) != 0;
                        if (isCommandBldgRestriction && isSuppliesRestriction) continue;
                        
                        return false;
                    }
                }
            }

            // Enforce worker requirement for standard buildings (skipped for reserved-site card builds).
            bool isCP = Building != null && Building.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase);
            if (requireWorker && !isCP)
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

            // Check sector feature requirement for themed buildings
            string bldName = Building.Name;
            var sectorMgr = GameDevTV.RTS.Environment.SectorManager.Instance;
            bool requiresFeature = bldName.Contains("Lava Tube") || bldName.Contains("Subterranean") ||
                                   bldName.Contains("Sector Command") || bldName.Contains("Magnetic Shield") ||
                                   bldName.Contains("Subglacial") || bldName.Contains("Biosphere");
            if (requiresFeature && sectorMgr != null)
            {
                var nearestSector = sectorMgr.GetNearestSector(new Vector3(point.x, 0, point.z));
                if (nearestSector != null)
                {
                    bool hasFeature = false;
                    if (bldName.Contains("Lava Tube") || bldName.Contains("Subterranean"))
                        hasFeature = nearestSector.Feature == GameDevTV.RTS.Environment.SectorManager.SectorFeature.LavaTube;
                    else if (bldName.Contains("Sector Command") || bldName.Contains("Magnetic Shield"))
                        hasFeature = nearestSector.Feature == GameDevTV.RTS.Environment.SectorManager.SectorFeature.FaultLine;
                    else if (bldName.Contains("Subglacial") || bldName.Contains("Biosphere"))
                        hasFeature = nearestSector.Feature == GameDevTV.RTS.Environment.SectorManager.SectorFeature.WaterDeposit;

                    if (!hasFeature && nearestSector.IsExplored)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public override bool IsLocked(CommandContext context)
        {
            if (Building == null) return false;

            // Check if the tech tree is unlocked.
            if (!BlueprintDraftManager.IsBuildingUnlocked(Building)) return true;

            // Check if the player has completed a round for Command Center.
            // Exception: allow building when no Command Post exists yet (player starts with nothing)
            if (Building.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase))
            {
                // Allow the first player-placed Command Post anytime if none exist in the world yet.
                // This prevents softlocking on campaigns/start where the starting base is not yet active.
                bool hasExistingCommandPost = false;
                if (BaseBuilding.ActiveBuildings != null)
                {
                    foreach (var b in BaseBuilding.ActiveBuildings)
                    {
                        if (b != null && b.Owner == context.Owner && b.BuildingSO != null
                            && b.BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase)
                            && !b.name.Contains("Ghost", System.StringComparison.OrdinalIgnoreCase))
                        {
                            // Filter for player-placed runtime buildings (whose GameObject names contain "(Clone)")
                            if (b.name.Contains("Clone", System.StringComparison.OrdinalIgnoreCase))
                            {
                                hasExistingCommandPost = true;
                                break;
                            }
                        }
                    }
                }
                
                if (!hasExistingCommandPost)
                {
                    // If no player Command Post exists, they can always place it (it is unlocked)
                    return !HasEnoughSupplies(context) || (Building.TechTree != null && !Building.TechTree.IsUnlocked(context.Owner, Building));
                }

                // If they already have a player Command Post, allow another when an
                // unlocked sector still needs claiming (exploration opened it). Expansion
                // phase also allows this. Do not lock mid-run after Orbital Scan / Survey.
                if (GenerationManager.Instance != null && !GenerationManager.Instance.IsExpansionPhase
                    && !GameDevTV.RTS.Utilities.SectorColonization.HasUnclaimedUnlockedSector())
                {
                    return true;
                }

                // During expansion / colonization, check if there's an unoccupied sector.
                var sectorMgr = GameDevTV.RTS.Environment.SectorManager.Instance;
                if (sectorMgr != null && sectorMgr.Sectors.Count > 0)
                {
                    bool hasUnoccupiedSector = false;
                    foreach (var sector in sectorMgr.Sectors)
                    {
                        if (!sector.IsOccupied && !sector.IsLocked)
                        {
                            hasUnoccupiedSector = true;
                            break;
                        }
                    }
                    if (!hasUnoccupiedSector) return true; // Lock if no unoccupied sectors available
                }
            }
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
