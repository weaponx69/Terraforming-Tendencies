using System;
using System.Collections.Generic;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Lets the player pick which solar cluster to build in after playing a building card.
    /// </summary>
    public static class BuildingSiteSelectionController
    {
        public static bool IsSelecting => pendingBuilding != null;
        public static int AcceptClicksAfterFrame => acceptClicksAfterFrame;

        private static BuildingSO pendingBuilding;
        private static Owner pendingOwner;
        private static int pendingCardIndex = -1;
        private static int acceptClicksAfterFrame = -1;
        private static bool markersPending;
        private static readonly List<BuildingSiteSlot> eligibleSites = new();
        private static Action<bool, string> onComplete;

        public static void Begin(
            BuildingSO building,
            Owner owner,
            int cardIndex,
            Action<bool, string> callback)
        {
            Cancel();

            pendingBuilding = building;
            pendingOwner = owner;
            pendingCardIndex = cardIndex;
            onComplete = callback;
            acceptClicksAfterFrame = Time.frameCount;
            markersPending = true;

            eligibleSites.Clear();
            foreach (var site in BuildingSiteRegistry.GetEligibleSites(building, owner))
            {
                if (BuildingSiteRegistry.IsSiteVisibleToPlayer(site))
                {
                    eligibleSites.Add(site);
                }
            }

            if (eligibleSites.Count == 0)
            {
                string reason = BuildingSiteRegistry.IsSolarBuilding(building)
                    ? "No open solar array sites. Unlock a new sector or wait for a cluster to free up."
                    : "No powered building sites. Build a Solar Panel at a cluster first, then pick that cluster.";
                callback?.Invoke(false, reason);
                ClearPending();
                return;
            }

            Debug.Log($"[BuildingSiteSelection] Choose a site for {building.Name} ({eligibleSites.Count} option(s)).");
        }

        public static void ActivatePendingMarkersIfNeeded()
        {
            if (!markersPending || pendingBuilding == null) return;
            if (Time.frameCount <= acceptClicksAfterFrame) return;

            foreach (var site in eligibleSites)
            {
                if (site?.MarkerGO == null) continue;
                var marker = site.MarkerGO.GetComponent<BuildingSiteMarker>();
                marker?.SetPreviewBuilding(pendingBuilding);
                marker?.SetSelectable(true);
            }

            markersPending = false;
        }

        public static bool TryHandleClick(RaycastHit hit)
        {
            if (!IsSelecting) return false;
            if (Time.frameCount <= acceptClicksAfterFrame) return false;

            BuildingSiteSlot site = ResolveSiteFromHit(hit);
            if (site == null || !eligibleSites.Contains(site))
            {
                return false;
            }

            bool built = ReservedSiteBuildUtility.TryBuildAtSite(pendingBuilding, pendingOwner, site, out string reason);
            if (built && pendingCardIndex >= 0 && CardDeckController.Instance != null)
            {
                CardDeckController.Instance.PlayCard(pendingCardIndex);
            }

            var callback = onComplete;
            Cancel();
            callback?.Invoke(built, reason);
            return true;
        }

        public static void Cancel()
        {
            foreach (var site in eligibleSites)
            {
                if (site?.MarkerGO == null) continue;
                var marker = site.MarkerGO.GetComponent<BuildingSiteMarker>();
                marker?.SetSelectable(false);
                marker?.ClearPreview();
            }

            eligibleSites.Clear();
            markersPending = false;
            acceptClicksAfterFrame = -1;
            pendingCardIndex = -1;
            ClearPending();
            BuildingSiteRegistry.RefreshAllMarkers();
        }

        private static BuildingSiteSlot ResolveSiteFromHit(RaycastHit hit)
        {
            if (hit.collider == null) return null;

            var marker = hit.collider.GetComponent<BuildingSiteMarker>();
            if (marker == null)
            {
                marker = hit.collider.GetComponentInParent<BuildingSiteMarker>();
            }
            if (marker?.Site != null)
            {
                return marker.Site;
            }

            var building = hit.collider.GetComponentInParent<BaseBuilding>();
            if (building == null) return null;

            foreach (var site in eligibleSites)
            {
                if (site?.Cluster?.SolarBuilding == building && site.Kind == BuildingSiteKind.PairedBuilding)
                {
                    return site;
                }
            }

            return null;
        }

        private static void ClearPending()
        {
            pendingBuilding = null;
            onComplete = null;
        }
    }
}
