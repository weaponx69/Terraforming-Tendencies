using System;
using System.Collections.Generic;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using GameDevTV.RTS.UI;
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
            eligibleSites.AddRange(BuildingSiteRegistry.GetEligibleSites(building, owner));

            // Ensure every eligible pad has an active marker before we highlight / click.
            BuildingSiteRegistry.RefreshAllMarkers();

            if (eligibleSites.Count == 0)
            {
                string reason = BuildingSiteRegistry.IsSolarBuilding(building)
                    ? "No open solar array sites. Unlock a new sector or wait for a cluster to free up."
                    : "No powered building sites. Build a Solar Panel at a cluster first, then pick that cluster.";
                NotifyBuildFeedback(reason);
                callback?.Invoke(false, reason);
                ClearPending();
                return;
            }

            FocusCameraOnEligibleSites();

            // Single candidate: build immediately after framing the pad.
            if (eligibleSites.Count == 1)
            {
                TryCommitSite(eligibleSites[0]);
                return;
            }

            // Instructional only — do not use the red failure banner.
            Debug.Log($"[BuildingSiteSelection] Choose a site for {building.Name} ({eligibleSites.Count} option(s)). Esc to cancel.");
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

            TryCommitSite(site);
            return true;
        }

        /// <summary>Missed all pads — quiet tip in the console; avoid spamming the red banner.</summary>
        public static void NotifyMissedClick()
        {
            if (!IsSelecting || pendingBuilding == null) return;
            Debug.Log($"[BuildingSiteSelection] Click a highlighted pad to build {pendingBuilding.Name}. Esc to cancel.");
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

        private static void TryCommitSite(BuildingSiteSlot site)
        {
            bool built = ReservedSiteBuildUtility.TryBuildAtSite(pendingBuilding, pendingOwner, site, out string reason);
            if (built && pendingCardIndex >= 0 && CardDeckController.Instance != null)
            {
                // Site is now occupied — normal PlayCard CanApply would fail. Consume directly.
                CardDeckController.Instance.ConsumeCardAfterBuild(pendingCardIndex);
            }

            if (built)
            {
                // Clear any prior selection tip / failure flash.
                var runtimeUi = UnityEngine.Object.FindAnyObjectByType<RuntimeUI>(FindObjectsInactive.Include);
                runtimeUi?.HideWarningBanner();
            }
            else
            {
                NotifyBuildFeedback(string.IsNullOrEmpty(reason)
                    ? $"Could not build {pendingBuilding?.Name}."
                    : reason);
            }

            var callback = onComplete;
            Cancel();
            callback?.Invoke(built, reason);
        }

        private static void FocusCameraOnEligibleSites()
        {
            if (eligibleSites.Count == 0) return;

            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var site in eligibleSites)
            {
                if (site == null) continue;
                sum += site.Position;
                count++;
            }

            if (count == 0) return;
            PlayerInput.FocusCameraOnWorldPosition(sum / count);
        }

        private static void NotifyBuildFeedback(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            ExplorationManager.NotifyExplorationFailed(message);
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

            // Clicking the pad ghost mesh (child, no marker on collider) after strip/recreate.
            Transform t = hit.collider.transform;
            while (t != null)
            {
                marker = t.GetComponent<BuildingSiteMarker>();
                if (marker?.Site != null) return marker.Site;
                t = t.parent;
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

            // Nearest eligible pad within click radius (ghost meshes can miss marker collider).
            const float snapRadius = 8f;
            BuildingSiteSlot nearest = null;
            float best = snapRadius * snapRadius;
            Vector3 point = hit.point;
            foreach (var site in eligibleSites)
            {
                if (site == null) continue;
                float d = (site.Position - point).sqrMagnitude;
                // Ignore Y for pad snaps.
                Vector3 flat = site.Position; flat.y = point.y;
                d = (flat - point).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = site;
                }
            }

            return nearest;
        }

        private static void ClearPending()
        {
            pendingBuilding = null;
            onComplete = null;
        }
    }
}
