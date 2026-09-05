using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Utilities
{
    /// <summary>
    /// After a sector unlocks, reveal its build pads and claim it with a Command Post
    /// so solar/climate sites become playable immediately.
    /// </summary>
    public static class SectorColonization
    {
        /// <summary>
        /// Unlock (if needed) and claim the map sector closest to the player's current
        /// colony front — used when a terraforming round completes.
        /// </summary>
        public static bool TryColonizeClosestSectorNeedingCommandPost(Owner owner = Owner.Player1)
        {
            if (SectorManager.Instance == null) return false;

            Vector3 origin = GetColonizationOrigin();
            int index = SectorManager.Instance.GetClosestSectorNeedingCommandPostIndex(origin);
            if (index < 0) return false;

            var sector = SectorManager.Instance.Sectors[index];
            if (sector == null) return false;

            if (sector.IsLocked)
                return SectorManager.Instance.UnlockAndColonizeSector(index, owner, claimTerraformingFocus: true);

            PrepareNewlyUnlockedSector(sector, owner, index);
            if (sector.IsOccupied)
                SectorManager.Instance.BeginTerraformingOn(sector);
            return sector.IsOccupied;
        }

        /// <summary>
        /// World position of a sector's Command Post (building, pad, or sector center fallback).
        /// </summary>
        public static bool TryGetCommandPostFocusPosition(int sectorIndex, out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            if (SectorManager.Instance == null) return false;
            if (sectorIndex < 0 || sectorIndex >= SectorManager.Instance.Sectors.Count) return false;
            return TryGetCommandPostFocusPosition(SectorManager.Instance.Sectors[sectorIndex], out worldPosition);
        }

        public static bool TryGetCommandPostFocusPosition(SectorManager.Sector sector, out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            if (sector == null) return false;

            if (sector.OccupyingBuilding != null)
            {
                worldPosition = sector.OccupyingBuilding.transform.position;
                return true;
            }

            if (sector.BuildingSites != null)
            {
                foreach (var site in sector.BuildingSites)
                {
                    if (site == null || site.Kind != BuildingSiteKind.CommandPost) continue;
                    if (site.OccupyingBuilding != null)
                    {
                        worldPosition = site.OccupyingBuilding.transform.position;
                        return true;
                    }

                    worldPosition = site.Position;
                    return true;
                }
            }

            worldPosition = sector.Center;
            return true;
        }

        private static Vector3 GetColonizationOrigin()
        {
            var sm = SectorManager.Instance;
            if (sm?.ActiveSector != null) return sm.ActiveSector.Center;

            if (sm?.Sectors != null)
            {
                foreach (var sector in sm.Sectors)
                {
                    if (sector != null && sector.IsOccupied)
                        return sector.Center;
                }
            }

            return Vector3.zero;
        }

        /// <summary>Index of the sector that would be colonized next, or -1 if none.</summary>
        public static int GetClosestSectorNeedingCommandPostIndex()
        {
            if (SectorManager.Instance == null) return -1;
            return SectorManager.Instance.GetClosestSectorNeedingCommandPostIndex(GetColonizationOrigin());
        }

        public static bool SectorHasCommandPost(SectorManager.Sector sector)
        {
            if (sector == null) return false;
            if (sector.IsOccupied && sector.OccupyingBuilding != null) return true;

            if (sector.BuildingSites != null)
            {
                foreach (var site in sector.BuildingSites)
                {
                    if (site == null || site.Kind != BuildingSiteKind.CommandPost || !site.IsOccupied) continue;
                    if (site.OccupyingBuilding != null
                        && site.OccupyingBuilding.Progress.State == BuildingProgress.BuildingState.Completed)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Reveal fog over reserved pads and auto-place a Command Post on the sector's
        /// CP pad (waives materials — the exploration card already paid to open the sector).
        /// </summary>
        public static void PrepareNewlyUnlockedSector(int sectorIndex, Owner owner = Owner.Player1)
        {
            if (SectorManager.Instance == null) return;
            if (sectorIndex < 0 || sectorIndex >= SectorManager.Instance.Sectors.Count) return;

            PrepareNewlyUnlockedSector(SectorManager.Instance.Sectors[sectorIndex], owner, sectorIndex);
        }

        public static void PrepareNewlyUnlockedSector(SectorManager.Sector sector, Owner owner = Owner.Player1, int sectorIndex = -1)
        {
            if (sector == null || sector.IsLocked) return;

            RevealSectorBuildSites(sector);
            bool placed = TryAutoPlaceCommandPost(sector, owner, out string failureReason);
            BuildingSiteRegistry.RefreshAllMarkers();
            CardDeckController.Instance?.RefreshHand();

            Debug.Log(placed
                ? $"[SectorColonization] Sector {sectorIndex} claimed with Command Post; pads revealed."
                : $"[SectorColonization] Sector {sectorIndex} pads revealed; Command Post not placed ({failureReason ?? "unknown"}).");
        }

        public static void RevealSectorBuildSites(SectorManager.Sector sector)
        {
            if (sector == null || HexGridManager.Instance == null) return;

            float radius = HexGridManager.Instance.StartingAreaRevealRadius;
            HexGridManager.Instance.RevealHexesAroundPosition(sector.Center, Mathf.Max(radius * 1.75f, 22f));

            if (sector.BuildingSites == null) return;
            foreach (var site in sector.BuildingSites)
            {
                if (site == null) continue;
                HexGridManager.Instance.RevealHexesAroundPosition(site.Position, Mathf.Max(radius * 0.6f, 10f));
            }
        }

        public static bool TryAutoPlaceCommandPost(SectorManager.Sector sector, Owner owner, out string failureReason)
        {
            failureReason = null;
            if (sector?.BuildingSites == null)
            {
                failureReason = "Sector has no building sites.";
                return false;
            }

            BuildingSiteSlot cpSite = null;
            foreach (var site in sector.BuildingSites)
            {
                if (site != null && site.Kind == BuildingSiteKind.CommandPost && !site.IsOccupied)
                {
                    cpSite = site;
                    break;
                }
            }

            if (cpSite == null)
            {
                failureReason = "No open Command Post pad on sector.";
                return false;
            }

            BuildingSO commandPost = BlueprintDraftManager.GetBuildingSOByName("Command Post");
            if (commandPost == null)
            {
                failureReason = "Command Post BuildingSO missing.";
                Debug.LogWarning("[SectorColonization] Command Post BuildingSO missing — cannot auto-claim sector.");
                return false;
            }

            BlueprintDraftManager.UnlockBuilding("Command Post");
            bool placed = ReservedSiteBuildUtility.TryBuildAtSite(commandPost, owner, cpSite, out failureReason, waiveCost: true);
            if (placed)
            {
                sector.IsOccupied = true;
                if (cpSite.OccupyingBuilding != null)
                    sector.OccupyingBuilding = cpSite.OccupyingBuilding;
            }

            return placed;
        }

        /// <summary>True when an unlocked, unoccupied sector still needs a Command Post claim.</summary>
        public static bool HasUnclaimedUnlockedSector()
        {
            if (SectorManager.Instance?.Sectors == null) return false;
            foreach (var sector in SectorManager.Instance.Sectors)
            {
                if (sector == null || sector.IsLocked || sector.IsOccupied) continue;
                if (sector.BuildingSites == null) continue;
                foreach (var site in sector.BuildingSites)
                {
                    if (site != null && site.Kind == BuildingSiteKind.CommandPost && !site.IsOccupied)
                        return true;
                }
            }

            return false;
        }
    }
}
