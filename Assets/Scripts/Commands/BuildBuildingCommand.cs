using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Utilities;
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

        /// <summary>When ≥ 0, this placement came from a hand card — consume that card on success.</summary>
        public int HandIndex { get; set; } = -1;

        /// <summary>
        /// Returns true if this building is a command-type building (Command Center, Command Post, etc.)
        /// that should auto-place without requiring a worker selection.
        /// </summary>
        public bool IsCommandBuilding =>
            Building != null && Building.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase);

        public Vector3 SnapToNearestSector(Vector3 point)
        {
            // Command Posts auto-claim the next free sector (player does not free-place them).
            if (IsCommandBuilding
                && GameDevTV.RTS.Utilities.SectorColonization.TryGetNextCommandPostPlacement(
                    out Vector3 claimPos, out _))
            {
                return claimPos;
            }

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
            if (HandIndex >= 0)
                targetPos = ColonyTileGrid.SnapForPlacement(targetPos, context.Owner, out _);

            UnityEngine.AI.NavMeshQueryFilter filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = 0, areaMask = UnityEngine.AI.NavMesh.AllAreas };
            if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out UnityEngine.AI.NavMeshHit navHit, 20f, filter))
            {
                if (HandIndex >= 0)
                    targetPos = new Vector3(targetPos.x, navHit.position.y, targetPos.z);
                else
                    targetPos = navHit.position;
            }

            // Card plays: Materials waived under Colony Acts; power is optional (not a place gate).
            if (HandIndex >= 0)
            {
                bool colonyActs = ColonyActManager.Instance != null;
                if (!colonyActs && !PowerGridManager.CanPlayBuildingForPower(Building, context.Owner))
                    return false;
                if (!HasEnoughMaterialsForCard(context.Owner))
                    return false;
                if (BuildingSiteRegistry.IsMineBuilding(Building))
                {
                    if (!DiscoverySystem.HasDiscoveredMineDeposit(Building))
                        return false;
                    // Prefer snapping onto the deposit tile under/near the cursor before validating.
                    if (DiscoverySystem.TrySnapToMineDeposit(Building, targetPos, out Vector3 mineSnap, out _))
                        targetPos = mineSnap;
                    if (!DiscoverySystem.IsOnDiscoveredMineDeposit(Building, targetPos))
                        return false;
                }
                if (DiscoverySystem.TryGetRequiredSectorFeature(Building, out _)
                    && !DiscoverySystem.IsOnRequiredSectorFeature(Building, targetPos))
                    return false;
                return AllRestrictionsPass(targetPos, context.Owner, requireWorker: false);
            }

            return HasEnoughSupplies(context) && AllRestrictionsPass(targetPos, context.Owner);
        }

        public override void Handle(CommandContext context)
        {
            IBuildingBuilder builder = context.Commandable as IBuildingBuilder;

            bool isCommandPost = Building != null
                && Building.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase);
            UnityEngine.AI.NavMeshQueryFilter filter = new UnityEngine.AI.NavMeshQueryFilter
            {
                agentTypeID = 0,
                areaMask = UnityEngine.AI.NavMesh.AllAreas
            };

            // Snap the placement position to the NavMesh so it spawns on the true ground, not on top of rock colliders
            Vector3 targetPos = SnapToNearestSector(context.Hit.point);

            // Card tiles snap to the Combolands square grid (join edges with neighbors).
            // Command Posts auto-claim the next free sector pad instead.
            if (HandIndex >= 0)
            {
                if (isCommandPost
                    && GameDevTV.RTS.Utilities.SectorColonization.TryGetNextCommandPostPlacement(
                        out Vector3 claimPos, out _))
                {
                    targetPos = claimPos;
                    if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out UnityEngine.AI.NavMeshHit claimHit, 20f, filter))
                        targetPos = new Vector3(targetPos.x, claimHit.position.y, targetPos.z);
                }
                else
                {
                    targetPos = ColonyTileGrid.SnapForPlacement(targetPos, context.Owner, out _);
                    if (BuildingSiteRegistry.IsMineBuilding(Building)
                        && DiscoverySystem.TrySnapToMineDeposit(Building, context.Hit.point, out Vector3 mineSnap, out _))
                    {
                        targetPos = new Vector3(mineSnap.x, targetPos.y, mineSnap.z);
                    }
                }
            }

            if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out UnityEngine.AI.NavMeshHit navHit, 20f, filter))
            {
                if (HandIndex >= 0)
                    targetPos = new Vector3(targetPos.x, navHit.position.y, targetPos.z);
                else
                    targetPos = navHit.position;
            }

            // Check if this is the player's very first Command Post
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

                if (HandIndex >= 0)
                {
                    bool colonyActs = ColonyActManager.Instance != null;
                    if (!colonyActs && !PowerGridManager.CanPlayBuildingForPower(Building, context.Owner))
                    {
                        string reason = ExplainCardPlacementFailure(targetPos, context.Owner)
                            ?? "Not enough spare power to place this card.";
                        ExplorationManager.NotifyPlacementFailed(reason, targetPos);
                        return;
                    }

                    if (!HasEnoughMaterialsForCard(context.Owner))
                    {
                        string reason = ExplainCardPlacementFailure(targetPos, context.Owner)
                            ?? $"Need materials to place {Building.Name}.";
                        ExplorationManager.NotifyPlacementFailed(reason, targetPos);
                        return;
                    }

                    if (BuildingSiteRegistry.IsMineBuilding(Building))
                    {
                        if (!DiscoverySystem.HasDiscoveredMineDeposit(Building))
                        {
                            DiscoverySystem.TryGetMineResourceType(Building, out string type);
                            ExplorationManager.NotifyPlacementFailed(
                                $"Discover a {type ?? "resource"} deposit before placing this mine.",
                                targetPos);
                            return;
                        }
                        if (!DiscoverySystem.IsOnDiscoveredMineDeposit(Building, targetPos))
                        {
                            ExplorationManager.NotifyPlacementFailed(
                                "Place this mine on a discovered deposit of the matching resource.",
                                targetPos);
                            return;
                        }
                    }

                    if (DiscoverySystem.TryGetRequiredSectorFeature(Building, out var needFeature)
                        && !DiscoverySystem.IsOnRequiredSectorFeature(Building, targetPos))
                    {
                        ExplorationManager.NotifyPlacementFailed(
                            $"Place {Building.Name} in a {needFeature} sector (polar ice / aquifer zones).",
                            targetPos);
                        return;
                    }

                    if (!AllRestrictionsPass(targetPos, context.Owner, requireWorker: false))
                    {
                        string reason = ExplainCardPlacementFailure(targetPos, context.Owner)
                            ?? "Can't place here.";
                        ExplorationManager.NotifyPlacementFailed(reason, targetPos);
                        return;
                    }

                    if (!ReservedSiteBuildUtility.TrySpendMaterials(Building, context.Owner))
                    {
                        int need = ReservedSiteBuildUtility.GetMaterialsCost(Building);
                        ExplorationManager.NotifyPlacementFailed(
                            $"Need {need} Materials to place {Building.Name}.",
                            targetPos);
                        return;
                    }

                    GameObject cardInstance = Instantiate(Building.Prefab, targetPos, Quaternion.identity);
                    if (cardInstance.TryGetComponent(out BaseBuilding cardBuilding))
                    {
                        // Card tiles finish instantly (Combolands-style); week spend + score flush in ConsumeCardAfterBuild.
                        cardBuilding.CompleteInstantCardPlace(context.Owner, Building);
                        // Command Posts must stay replayable across sectors — do not draft-lock them.
                        if (!isCommandPost)
                            BlueprintDraftManager.LockBuilding(Building.Name);
                        if (isCommandPost)
                            PlayerInput.FocusCameraOnWorldPosition(targetPos);
                        if (CardDeckController.Instance != null)
                        {
                            CardDeckController.Instance.ConsumeCardAfterBuild(HandIndex);
                            HandIndex = -1;
                            if (isCommandPost)
                                CardDeckController.Instance.NotifyCommandPostPlaced();
                        }
                    }

                    return;
                }

            if (isFirstCommandPost)
            {
                builder = null;
            }
            else
            {
                // Prefer a drone when one is free; otherwise self-construct.
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
                // No drone: still place with construction animation.
                if (!AllRestrictionsPass(targetPos, context.Owner, requireWorker: false))
                {
                    ExplorationManager.NotifyExplorationFailed("Can't place here.");
                    return;
                }

                if (!HasEnoughSupplies(context) && !isFirstCommandPost)
                {
                    ExplorationManager.NotifyExplorationFailed("Not enough materials.");
                    return;
                }

                GameObject instance = Instantiate(Building.Prefab, targetPos, Quaternion.identity);
                if (instance.TryGetComponent(out BaseBuilding newBuilding))
                {
                    newBuilding.BeginSelfConstruction(context.Owner, Building, Building.PlacementMaterial);
                    BlueprintDraftManager.LockBuilding(Building.Name);
                    if (CardDeckController.Instance != null)
                        CardDeckController.Instance.DrawCard();
                    GameFlowManager.Instance?.PlayerActed();
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

            // Check sector feature requirement for themed buildings (legacy non-card path).
            // Card plays also require standing in the matching feature sector.
            if (HandIndex < 0)
            {
                string bldName = Building.Name;
                var sectorMgr = GameDevTV.RTS.Environment.SectorManager.Instance;
                bool requiresFeature = bldName.Contains("Lava Tube") || bldName.Contains("Subterranean") ||
                                       bldName.Contains("Sector Command") || bldName.Contains("Magnetic Shield") ||
                                       bldName.Contains("Subglacial") || bldName.Contains("Biosphere") ||
                                       bldName.Contains("Aquifer");
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
                        else if (bldName.Contains("Subglacial") || bldName.Contains("Biosphere")
                            || bldName.Contains("Aquifer"))
                            hasFeature = nearestSector.Feature == GameDevTV.RTS.Environment.SectorManager.SectorFeature.WaterDeposit;

                        if (!hasFeature && nearestSector.IsExplored)
                        {
                            return false;
                        }
                    }
                }
            }
            else
            {
                if (!DiscoverySystem.IsBuildingGeologicallyAvailable(Building))
                    return false;
                if (!DiscoverySystem.IsOnRequiredSectorFeature(Building, point))
                    return false;
            }

            return true;
        }

        /// <summary>Human-readable reason a card tile cannot be placed at <paramref name="point"/>.</summary>
        public string ExplainCardPlacementFailure(Vector3 point, Owner owner)
        {
            if (Building == null) return "No building on this card.";

            bool colonyActs = ColonyActManager.Instance != null;
            if (!colonyActs && !PowerGridManager.CanPlayBuildingForPower(Building, owner))
            {
                float gen = PowerGridManager.GetBoardPowerGeneration(owner);
                float used = PowerGridManager.GetBoardPowerUpkeep(owner);
                float need = PowerGridManager.GetBuildingPowerUpkeep(Building);
                float free = gen - used;
                return $"Not enough spare power for {Building.Name} " +
                       $"(needs +{need:0.#}; {gen:0.#} gen / {used:0.#} used / {free:0.#} free). " +
                       "Build more Solar or demolish consumers.";
            }

            int matCost = ReservedSiteBuildUtility.GetMaterialsCost(Building);
            int haveMats = Supplies.Materials != null && Supplies.Materials.TryGetValue(owner, out int m) ? m : 0;
            if (matCost > 0 && haveMats < matCost)
                return $"Need {matCost} Materials (have {haveMats}).";

            if (BuildingSiteRegistry.IsMineBuilding(Building))
            {
                if (!DiscoverySystem.HasDiscoveredMineDeposit(Building))
                {
                    DiscoverySystem.TryGetMineResourceType(Building, out string type);
                    return $"Discover a {type ?? "resource"} deposit first.";
                }
                if (!DiscoverySystem.IsOnDiscoveredMineDeposit(Building, point))
                    return "Place this mine on the matching discovered deposit tile.";
            }

            if (DiscoverySystem.TryGetRequiredSectorFeature(Building, out var feature))
            {
                if (!DiscoverySystem.IsSectorFeatureDiscovered(feature))
                    return $"Discover a {feature} geological feature first (scout / reach that sector).";
                if (!DiscoverySystem.IsOnRequiredSectorFeature(Building, point))
                    return $"Place {Building.Name} in a {feature} sector (polar ice / aquifer zones).";
            }

            if (!AllRestrictionsPass(point, owner, requireWorker: false))
            {
                // Overlap is the usual card-place blocker after power/materials.
                if (Restrictions != null)
                {
                    foreach (BuildingRestrictionSO restriction in Restrictions)
                    {
                        Collider[] colliders = restriction.HitDetectionStyle switch
                        {
                            BuildingRestrictionSO.OverlapStyle.Sphere =>
                                Physics.OverlapSphere(point, restriction.Radius, restriction.LayerMask),
                            BuildingRestrictionSO.OverlapStyle.Box =>
                                Physics.OverlapBox(point, restriction.Extents, Quaternion.identity, restriction.LayerMask),
                            _ => System.Array.Empty<Collider>()
                        };
                        foreach (var col in colliders)
                        {
                            if (col == null) continue;
                            var other = col.GetComponentInParent<BaseBuilding>();
                            if (other != null
                                && other.Progress.State != BuildingProgress.BuildingState.Destroyed)
                            {
                                string otherName = other.ResolvedBuildingSO != null
                                    ? other.ResolvedBuildingSO.Name
                                    : other.name;
                                return $"Blocked — too close to {otherName}. Move to an empty tile.";
                            }
                        }
                    }
                }
                return "Can't place here — tile blocked or invalid ground.";
            }

            return null;
        }

        public override bool IsLocked(CommandContext context)
        {
            if (Building == null) return false;

            // Check if the tech tree is unlocked (card plays bypass — unlock happens on consume).
            if (HandIndex < 0 && !BlueprintDraftManager.IsBuildingUnlocked(Building)) return true;

            // Check if the player has completed a round for Command Center.
            // Exception: allow building when no Command Post exists yet (player starts with nothing)
            if (Building.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase))
            {
                // Repeatable expansion: allow while any sector still lacks a player Command Post.
                if (GameDevTV.RTS.Utilities.SectorColonization.GetNextFreeSectorIndex() < 0)
                    return true;

                // Hand card plays ignore tech-tree lock; Materials still apply until economy pivot.
                if (HandIndex >= 0)
                    return !HasEnoughMaterialsForCard(context.Owner);

                return !HasEnoughSupplies(context)
                    || (Building.TechTree != null && !Building.TechTree.IsUnlocked(context.Owner, Building));
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
            return HasEnoughMaterialsForCard(context.Owner);
        }

        private bool HasEnoughMaterialsForCard(Owner owner)
        {
            if (Building == null) return true;
            int materialsCost = ReservedSiteBuildUtility.GetMaterialsCost(Building);
            if (materialsCost <= 0) return true;
            if (Supplies.Materials == null) return false;
            return Supplies.Materials.TryGetValue(owner, out int have) && have >= materialsCost;
        }
    }
}
