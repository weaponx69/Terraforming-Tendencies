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

            var sm = SectorManager.Instance;
            if (sm != null && BaseBuilding.ActiveBuildings != null)
            {
                foreach (var building in BaseBuilding.ActiveBuildings)
                {
                    if (building == null || building.Owner != Owner.Player1) continue;
                    if (building.BuildingSO == null
                        || building.BuildingSO.Name.IndexOf("Command", System.StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    if (building.name.IndexOf("Ghost", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    if (building.Progress.State == BuildingProgress.BuildingState.Destroyed)
                        continue;
                    if (sm.GetNearestSector(building.transform.position) == sector)
                        return true;
                }
            }

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

            // Colony Acts: player plays Command Post cards to claim sectors — no free auto-CP.
            bool skipAutoCp = ColonyActManager.Instance != null && ColonyActManager.Instance.IsRunActive;
            bool placed = false;
            string failureReason = null;
            if (!skipAutoCp)
                placed = TryAutoPlaceCommandPost(sector, owner, out failureReason);

            BuildingSiteRegistry.RefreshAllMarkers();
            CardDeckController.Instance?.RefreshHand();

            if (skipAutoCp)
            {
                Debug.Log($"[SectorColonization] Sector {sectorIndex} pads revealed (play Command Post card to claim).");
                return;
            }

            Debug.Log(placed
                ? $"[SectorColonization] Sector {sectorIndex} claimed with Command Post; pads revealed."
                : $"[SectorColonization] Sector {sectorIndex} pads revealed; Command Post not placed ({failureReason ?? "unknown"}).");
        }

        /// <summary>First sector index without a player Command Post, or -1.</summary>
        public static int GetNextFreeSectorIndex()
        {
            var sm = SectorManager.Instance;
            if (sm?.Sectors == null) return -1;
            for (int i = 0; i < sm.Sectors.Count; i++)
            {
                if (sm.Sectors[i] == null) continue;
                if (!SectorHasCommandPost(sm.Sectors[i]))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// World position for the next Command Post claim (CP pad or sector center).
        /// Unlocks/reveals the sector if needed without auto-placing a free CP.
        /// </summary>
        public static bool TryGetNextCommandPostPlacement(out Vector3 worldPosition, out int sectorIndex)
        {
            worldPosition = Vector3.zero;
            sectorIndex = GetNextFreeSectorIndex();
            if (sectorIndex < 0) return false;

            var sm = SectorManager.Instance;
            var sector = sm.Sectors[sectorIndex];
            if (sector == null) return false;

            if (sector.IsLocked)
            {
                sector.IsLocked = false;
                sector.IsExplored = true;
                sector.IsDiscovered = true;
                DiscoverySystem.RevealFeaturesForSector(sector);
                RevealSectorBuildSites(sector);
                BuildingSiteRegistry.RefreshAllMarkers();
                SectorManager.Instance?.NotifySectorUnlocked();
            }
            else
            {
                RevealSectorBuildSites(sector);
            }

            return TryGetCommandPostFocusPosition(sector, out worldPosition);
        }

        /// <summary>Display name for sector index (feature or Sector N).</summary>
        public static string GetSectorDisplayName(int sectorIndex)
        {
            var sm = SectorManager.Instance;
            if (sm?.Sectors == null || sectorIndex < 0 || sectorIndex >= sm.Sectors.Count)
                return $"Sector {sectorIndex + 1}";

            var sector = sm.Sectors[sectorIndex];
            if (sector != null && sector.Feature != SectorManager.SectorFeature.None)
                return $"Sector {sectorIndex + 1} · {sector.Feature}";

            bool claimed = SectorHasCommandPost(sector);
            return claimed ? $"Sector {sectorIndex + 1}" : $"Sector {sectorIndex + 1} · Unclaimed";
        }

        /// <summary>Focus camera on a sector (CP if present, else center).</summary>
        public static void FocusCameraOnSector(int sectorIndex)
        {
            if (!TryGetCommandPostFocusPosition(sectorIndex, out Vector3 pos))
            {
                var sm = SectorManager.Instance;
                if (sm == null || sectorIndex < 0 || sectorIndex >= sm.Sectors.Count) return;
                pos = sm.Sectors[sectorIndex].Center;
            }
            PlayerInput.FocusCameraOnWorldPosition(pos);
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
