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
    /// Lets the player pick which reserved pad to build on after playing a building card.
    /// Q / E cycle camera focus across planet-wide eligible pads.
    /// </summary>
    public static class BuildingSiteSelectionController
    {
        public static bool IsSelecting => pendingBuilding != null;
        public static int AcceptClicksAfterFrame => acceptClicksAfterFrame;
        public static BuildingSiteSlot FocusedSite =>
            focusedIndex >= 0 && focusedIndex < eligibleSites.Count ? eligibleSites[focusedIndex] : null;

        private static BuildingSO pendingBuilding;
        private static Owner pendingOwner;
        private static int pendingCardIndex = -1;
        private static int acceptClicksAfterFrame = -1;
        private static bool markersPending;
        private static int focusedIndex = -1;
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
            focusedIndex = -1;

            eligibleSites.Clear();
            // Whole planet: ignore fog / former sector locks while picking a pad for a card.
            eligibleSites.AddRange(BuildingSiteRegistry.GetEligibleSites(building, owner, visibleToPlayerOnly: false));
            SortSitesForCycling(eligibleSites);

            // Ensure every eligible pad has an active marker before we highlight / click.
            BuildingSiteRegistry.RefreshAllMarkers();

            if (eligibleSites.Count == 0)
            {
                string reason = BuildingSiteRegistry.IsSolarBuilding(building)
                    ? "No open solar array sites left on the planet."
                    : "No powered building sites. Build a Solar Panel at a cluster first, then pick that cluster.";
                NotifyBuildFeedback(reason);
                callback?.Invoke(false, reason);
                ClearPending();
                return;
            }

            focusedIndex = FindNearestSiteIndex(PlayerInput.GetCameraFocusPosition());
            FocusCameraOnFocusedSite();

            // Single candidate: build immediately after framing the pad.
            if (eligibleSites.Count == 1)
            {
                TryCommitSite(eligibleSites[0]);
                return;
            }

            // Instructional only — do not use the red failure banner.
            Debug.Log($"[BuildingSiteSelection] Choose a site for {building.Name} ({eligibleSites.Count} option(s)). Q/E cycle pads, click to place, Esc cancel.");
        }

        /// <summary>Cycle eligible pads planet-wide. Negative = previous (Q), positive = next (E).</summary>
        public static void CycleFocus(int direction)
        {
            if (!IsSelecting || eligibleSites.Count == 0 || direction == 0) return;

            if (focusedIndex < 0) focusedIndex = 0;
            focusedIndex = (focusedIndex + direction) % eligibleSites.Count;
            if (focusedIndex < 0) focusedIndex += eligibleSites.Count;

            FocusCameraOnFocusedSite();
            RefreshFocusHighlights();
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
            RefreshFocusHighlights();
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

            focusedIndex = eligibleSites.IndexOf(site);
            TryCommitSite(site);
            return true;
        }

        /// <summary>Missed all pads — quiet tip in the console; avoid spamming the red banner.</summary>
        public static void NotifyMissedClick()
        {
            if (!IsSelecting || pendingBuilding == null) return;
            Debug.Log($"[BuildingSiteSelection] Q/E to browse pads, click to build {pendingBuilding.Name}. Esc to cancel.");
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
            focusedIndex = -1;
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

        private static void SortSitesForCycling(List<BuildingSiteSlot> sites)
        {
            sites.Sort((a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                int cmp = a.Position.x.CompareTo(b.Position.x);
                if (cmp != 0) return cmp;
                return a.Position.z.CompareTo(b.Position.z);
            });
        }

        private static int FindNearestSiteIndex(Vector3 cameraPos)
        {
            cameraPos.y = 0f;
            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < eligibleSites.Count; i++)
            {
                var site = eligibleSites[i];
                if (site == null) continue;
                Vector3 flat = site.Position;
                flat.y = 0f;
                float dist = (flat - cameraPos).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = i;
                }
            }
            return best;
        }

        private static void FocusCameraOnFocusedSite()
        {
            var site = FocusedSite;
            if (site == null) return;
            PlayerInput.FocusCameraOnWorldPosition(site.Position);
        }

        private static void RefreshFocusHighlights()
        {
            for (int i = 0; i < eligibleSites.Count; i++)
            {
                var site = eligibleSites[i];
                if (site?.MarkerGO == null) continue;
                var marker = site.MarkerGO.GetComponent<BuildingSiteMarker>();
                if (marker == null) continue;
                // All eligible pads stay clickable; only the focused pad gets the bright highlight.
                marker.SetHighlight(i == focusedIndex);
            }
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
            if (building != null)
            {
                foreach (var site in eligibleSites)
                {
                    if (site?.Cluster?.SolarBuilding == building && site.Kind == BuildingSiteKind.PairedBuilding)
                    {
                        return site;
                    }
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
                Vector3 flat = site.Position;
                flat.y = point.y;
                float d = (flat - point).sqrMagnitude;
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
