using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using TMPro;
using UnityEngine;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>
    /// Unreal-style fog for Prologue: materials / geology stay "?" until the survey drone
    /// paints over them. No colored pucks / gas meshes until uncovered.
    /// </summary>
    public static class PrologueResourceFog
    {
        const float RevealRadius = 14f;

        /// <summary>Axial hex distance for deposit / sector reveal around a survey center (not full board).</summary>
        const int SurveyRevealHexRadius = 3;

        static readonly List<HiddenResource> CachedHidden = new(128);
        static readonly Dictionary<Vector2Int, List<HiddenResource>> HiddenByCell = new(128);
        static readonly Dictionary<Vector2Int, List<SectorNode>> SectorNodesByCell = new(256);
        static bool spatialIndexBuilt;
        static readonly List<int> IceRevealScratch = new(8);

        /// <summary>Flat-topped axial neighbor steps (matches <see cref="ColonyTileGrid"/>).</summary>
        static readonly Vector2Int[] AxialNeighborOffsets =
        {
            new(+1, 0),
            new(+1, -1),
            new(0, -1),
            new(-1, 0),
            new(-1, +1),
            new(0, +1),
        };

        /// <summary>All axial coords with hex distance ≤ <see cref="SurveyRevealHexRadius"/>.</summary>
        static readonly Vector2Int[] SurveyRevealAxialOffsets = BuildAxialHexDisk(SurveyRevealHexRadius);

        /// <summary>Time-sliced reveal state owned by <see cref="PrologueVisionThrottle"/>.</summary>
        public sealed class ResourceFogRevealJob
        {
            internal readonly List<Vector3> Centers = new(64);
            internal readonly HashSet<Vector2Int> CenterCells = new();
            /// <summary>Survey tiles revealed over time — one localized pass per slice, no full-job reset.</summary>
            internal readonly List<Vector3> PendingNewCenters = new(96);
            internal readonly HashSet<Vector2Int> PendingNewCells = new();
            internal int HiddenIndex;
            internal int SectorIndex;
            internal int NodeIndex;
            internal int ColonyCenterIndex;
            internal byte Phase; // 0 hidden, 1 sectors, 2 colony board
        }

        public const int NewCenterRevealsPerSlice = 5;
        public const int ColonyBoardCentersPerSlice = 5;

        /// <summary>Hide every deposit / feature mesh; show only "?" until survey reveal.</summary>
        public static void ApplyHiddenState()
        {
            if (!PrologueController.IsActive) return;

            CachedHidden.Clear();
            HiddenByCell.Clear();
            SectorNodesByCell.Clear();
            spatialIndexBuilt = false;
            DiscoverySystem.Reset();
            // Reset clears type knowledge; restore from pads / colony so reapply during boot
            // does not refuse mine cards after Survey already uncovered a deposit.
            RestoreKnownTypesAfterFogReset();

            foreach (var hr in Object.FindObjectsByType<HiddenResource>(FindObjectsInactive.Include))
            {
                if (hr == null || hr.IsDiscovered) continue;
                hr.ConcealForPrologue();
                CachedHidden.Add(hr);
            }

            var sm = SectorManager.Instance;
            if (sm?.Sectors == null) return;

            foreach (var sector in sm.Sectors)
            {
                if (sector?.Nodes == null) continue;
                foreach (var node in sector.Nodes)
                {
                    if (node == null) continue;
                    // Keep tiles the Survey Team already uncovered.
                    if (node.isExplored) continue;
                    node.isDiscovered = true; // mystery spot only
                    ApplyUnknownVisual(node);
                }
            }

            // Also hide any orphan gatherable pads not linked through SectorNode.visualGO.
            foreach (var gs in Object.FindObjectsByType<GatherableSupply>(FindObjectsInactive.Include))
            {
                if (gs == null) continue;
                if (gs.TryGetComponent(out HiddenResource hr) && hr.IsDiscovered) continue;
                HideRenderers(gs.gameObject);
                gs.SetVisible(false);
                gs.ToggleColliders(false);
            }

            BuildSpatialIndex();
            Debug.Log("[PrologueResourceFog] Unsearched deposits/features hidden as '?' until survey.");
        }

        public static void RevealNearTile(PrologueController ctrl, int tileIndex)
        {
            if (ctrl == null || !ctrl.TryGetCell(tileIndex, out Vector2Int cell)) return;
            Vector3 world = ColonyTileGrid.CellToWorld(cell, 0f);
            RevealNearWorld(world);
        }

        /// <summary>One pass over hidden pads — not one full-world scan per path tile.</summary>
        public static void RevealAlongSurveyPath(PrologueController ctrl, IList<int> path)
        {
            if (ctrl == null || path == null || path.Count == 0) return;

            var centers = new List<Vector3>(path.Count);
            for (int i = 0; i < path.Count; i++)
            {
                if (!ctrl.TryGetCell(path[i], out Vector2Int cell)) continue;
                centers.Add(ColonyTileGrid.CellToWorld(cell, 0f));
            }

            if (centers.Count == 0) return;
            RevealNearWorlds(centers);
        }

        public static bool AppendSurveyCenters(PrologueController ctrl, ResourceFogRevealJob job, IList<int> path)
        {
            if (ctrl == null || job == null || path == null || path.Count == 0) return false;

            bool added = false;
            for (int i = 0; i < path.Count; i++)
            {
                if (!ctrl.TryGetCell(path[i], out Vector2Int cell)) continue;
                if (!job.PendingNewCells.Add(cell)) continue;
                job.PendingNewCenters.Add(ColonyTileGrid.CellToWorld(cell, 0f));
                added = true;
            }

            return added;
        }

        /// <summary>Confirm burst: deposit / sector fog for tiles without waiting on the slice queue.</summary>
        public static void RevealDepositFogImmediate(PrologueController ctrl, IList<int> tiles)
        {
            if (ctrl == null || tiles == null) return;
            for (int i = 0; i < tiles.Count; i++)
            {
                if (!ctrl.TryGetCell(tiles[i], out Vector2Int cell)) continue;
                Vector3 world = ColonyTileGrid.CellToWorld(cell, 0f);
                RevealNearSingleCenter(ctrl, world);
            }
        }

        /// <summary>Skip tiles already handled on the confirm frame.</summary>
        public static void DropPendingFogTiles(
            ResourceFogRevealJob job,
            PrologueController ctrl,
            IList<int> tiles)
        {
            if (job == null || ctrl == null || tiles == null) return;
            for (int i = 0; i < tiles.Count; i++)
            {
                if (!ctrl.TryGetCell(tiles[i], out Vector2Int cell)) continue;
                job.PendingNewCells.Remove(cell);
            }

            for (int i = job.PendingNewCenters.Count - 1; i >= 0; i--)
            {
                Vector3 w = job.PendingNewCenters[i];
                if (!ctrl.TryGetTileIndex(w, out int tile)) continue;
                for (int t = 0; t < tiles.Count; t++)
                {
                    if (tiles[t] != tile) continue;
                    job.PendingNewCenters.RemoveAt(i);
                    break;
                }
            }
        }

        /// <summary>Returns true when all passes for current centers are complete.</summary>
        public static bool AdvanceRevealJob(
            ResourceFogRevealJob job,
            PrologueController ctrl,
            int hiddenChecksBudget,
            int sectorNodeChecksBudget)
        {
            if (job == null || !PrologueController.IsActive)
                return true;

            if (job.PendingNewCenters.Count > 0)
            {
                int slice = Mathf.Min(NewCenterRevealsPerSlice, job.PendingNewCenters.Count);
                for (int i = 0; i < slice; i++)
                    RevealNearSingleCenter(ctrl, job.PendingNewCenters[i]);
                job.PendingNewCenters.RemoveRange(0, slice);
                if (job.Centers.Count == 0)
                    return job.PendingNewCenters.Count == 0;
            }

            if (job.Centers.Count == 0)
                return job.PendingNewCenters.Count == 0;

            float r2 = RevealRadius * RevealRadius;
            EnsureHiddenCache();

            if (job.Phase == 0)
            {
                int spent = 0;
                while (spent < hiddenChecksBudget && job.HiddenIndex < CachedHidden.Count)
                {
                    var hr = CachedHidden[job.HiddenIndex++];
                    spent++;
                    if (hr == null || hr.IsDiscovered) continue;
                    Vector3 p = hr.transform.position;
                    for (int c = 0; c < job.Centers.Count; c++)
                    {
                        Vector3 w = job.Centers[c];
                        float dx = p.x - w.x;
                        float dz = p.z - w.z;
                        if (dx * dx + dz * dz > r2) continue;
                        hr.ForceDiscover();
                        NotifyPrologueDepositUncovered(hr);
                        break;
                    }
                }

                if (job.HiddenIndex < CachedHidden.Count)
                    return false;
                job.Phase = 1;
                job.SectorIndex = 0;
                job.NodeIndex = 0;
            }

            var sm = SectorManager.Instance;
            if (job.Phase == 1 && sm?.Sectors != null)
            {
                int spent = 0;
                while (spent < sectorNodeChecksBudget && job.SectorIndex < sm.Sectors.Count)
                {
                    var sector = sm.Sectors[job.SectorIndex];
                    if (sector?.Nodes == null)
                    {
                        job.SectorIndex++;
                        job.NodeIndex = 0;
                        continue;
                    }

                    while (spent < sectorNodeChecksBudget && job.NodeIndex < sector.Nodes.Count)
                    {
                        var node = sector.Nodes[job.NodeIndex++];
                        spent++;
                        if (node == null || node.isExplored) continue;
                        float nx = node.position.x;
                        float nz = node.position.z;
                        for (int c = 0; c < job.Centers.Count; c++)
                        {
                            Vector3 w = job.Centers[c];
                            float dx = nx - w.x;
                            float dz = nz - w.z;
                            if (dx * dx + dz * dz > r2) continue;
                            RevealNode(node);
                            break;
                        }
                    }

                    if (job.NodeIndex >= sector.Nodes.Count)
                    {
                        job.SectorIndex++;
                        job.NodeIndex = 0;
                    }
                }

                if (job.SectorIndex < sm.Sectors.Count)
                    return false;
                job.Phase = 2;
                job.ColonyCenterIndex = 0;
            }

            if (job.Phase == 2 && ctrl?.Colony != null)
            {
                int processed = 0;
                while (processed < ColonyBoardCentersPerSlice && job.ColonyCenterIndex < job.Centers.Count)
                {
                    RevealColonyIceRing(ctrl, job.Centers[job.ColonyCenterIndex]);
                    job.ColonyCenterIndex++;
                    processed++;
                }

                if (job.ColonyCenterIndex < job.Centers.Count)
                    return false;
            }

            return true;
        }

        static void RevealNearSingleCenter(PrologueController ctrl, Vector3 world)
        {
            if (ctrl == null) return;
            Vector2Int centerCell = ColonyTileGrid.WorldToCell(world);
            ctrl.TryGetTileIndex(world, out int centerTile);
            RevealDepositsInLocalAxialRing(centerCell);
            RevealColonyIceRingLocalized(ctrl, centerCell, centerTile);
            RevealHexShroudAt(world);
        }

        static void RevealHexShroudAt(Vector3 world)
        {
            var hex = HexGridManager.Instance;
            if (hex != null)
                hex.RevealHex(world);
            else
                PrologueVisionThrottle.QueueHexReveal(world);
        }

        static void RevealColonyIceRing(PrologueController ctrl, Vector3 world)
        {
            if (ctrl?.Colony == null) return;
            Vector2Int centerCell = ColonyTileGrid.WorldToCell(world);
            ctrl.TryGetTileIndex(world, out int centerTile);
            RevealColonyIceRingLocalized(ctrl, centerCell, centerTile);
        }

        /// <summary>Center + 6 axial neighbors only — O(1) tile count, no <see cref="PrologueColony.Dist"/> board scan.</summary>
        static void RevealColonyIceRingLocalized(PrologueController ctrl, Vector2Int centerCell, int centerTile)
        {
            var colony = ctrl.Colony;
            if (colony == null) return;

            int boardCount = colony.Board.Tiles.Count;
            IceRevealScratch.Clear();

            void ConsiderTile(int tileIndex, bool isCenter)
            {
                if (tileIndex < 0 || tileIndex >= boardCount) return;
                var t = colony.Board.At(tileIndex);
                if (t.Revealed) return;
                if (isCenter
                    || t.Feature == PrologueFeature.Ice
                    || t.Feature == PrologueFeature.Glacier)
                {
                    IceRevealScratch.Add(tileIndex);
                }
            }

            if (centerTile >= 0)
                ConsiderTile(centerTile, isCenter: true);
            else if (ctrl.TryGetTileIndex(ColonyTileGrid.CellToWorldOnGround(centerCell), out int resolved))
                ConsiderTile(resolved, isCenter: true);

            for (int i = 0; i < AxialNeighborOffsets.Length; i++)
            {
                Vector2Int neighborCell = centerCell + AxialNeighborOffsets[i];
                if (!ctrl.TryGetTileIndex(ColonyTileGrid.CellToWorldOnGround(neighborCell), out int neighborTile))
                    continue;
                ConsiderTile(neighborTile, isCenter: false);
            }

            if (IceRevealScratch.Count == 0) return;
            colony.RevealTiles(IceRevealScratch);
        }

        static void RevealDepositsInLocalAxialRing(Vector2Int centerCell)
        {
            if (!spatialIndexBuilt) return;

            for (int o = 0; o < SurveyRevealAxialOffsets.Length; o++)
            {
                Vector2Int cell = centerCell + SurveyRevealAxialOffsets[o];
                if (!HiddenByCell.TryGetValue(cell, out var hiddenList)) hiddenList = null;
                if (hiddenList != null)
                {
                    for (int h = hiddenList.Count - 1; h >= 0; h--)
                    {
                        var hr = hiddenList[h];
                        if (hr == null || hr.IsDiscovered) continue;
                        UncoverHiddenResource(hr, cell, hiddenList, h);
                    }
                }

                if (!SectorNodesByCell.TryGetValue(cell, out var nodeList)) continue;
                for (int n = nodeList.Count - 1; n >= 0; n--)
                {
                    var node = nodeList[n];
                    if (node == null || node.isExplored) continue;
                    RevealAndUnregisterNode(node, cell, nodeList, n);
                }
            }
        }

        static void UncoverHiddenResource(
            HiddenResource hr,
            Vector2Int cell,
            List<HiddenResource> bucket,
            int indexInBucket)
        {
            if (hr == null || hr.IsDiscovered) return;
            hr.ForceDiscover();
            NotifyPrologueDepositUncovered(hr);
            bucket.RemoveAt(indexInBucket);
            if (bucket.Count == 0)
                HiddenByCell.Remove(cell);
        }

        static void RevealAndUnregisterNode(
            SectorNode node,
            Vector2Int cell,
            List<SectorNode> bucket,
            int indexInBucket)
        {
            if (node == null || node.isExplored) return;
            RevealNode(node);
            bucket.RemoveAt(indexInBucket);
            if (bucket.Count == 0)
                SectorNodesByCell.Remove(cell);
        }

        static Vector2Int[] BuildAxialHexDisk(int maxHexDistance)
        {
            var list = new List<Vector2Int>((maxHexDistance + 1) * 8);
            for (int dq = -maxHexDistance; dq <= maxHexDistance; dq++)
            {
                for (int dr = -maxHexDistance; dr <= maxHexDistance; dr++)
                {
                    int dist = (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(dq + dr)) / 2;
                    if (dist > maxHexDistance) continue;
                    if (dq == 0 && dr == 0) continue;
                    list.Add(new Vector2Int(dq, dr));
                }
            }

            list.Insert(0, Vector2Int.zero);
            return list.ToArray();
        }

        /// <summary>Prologue boot only — never clears or rebuilds during survey reveal slices.</summary>
        static void BuildSpatialIndex()
        {
            if (spatialIndexBuilt) return;

            for (int i = 0; i < CachedHidden.Count; i++)
            {
                var hr = CachedHidden[i];
                if (hr == null || hr.IsDiscovered) continue;
                Vector2Int cell = ColonyTileGrid.WorldToCell(hr.transform.position);
                if (!HiddenByCell.TryGetValue(cell, out var bucket))
                {
                    bucket = new List<HiddenResource>(2);
                    HiddenByCell[cell] = bucket;
                }
                bucket.Add(hr);
            }

            var sm = SectorManager.Instance;
            if (sm?.Sectors != null)
            {
                for (int s = 0; s < sm.Sectors.Count; s++)
                {
                    var sector = sm.Sectors[s];
                    if (sector?.Nodes == null) continue;
                    for (int n = 0; n < sector.Nodes.Count; n++)
                    {
                        var node = sector.Nodes[n];
                        if (node == null || node.isExplored) continue;
                        Vector2Int cell = ColonyTileGrid.WorldToCell(node.position);
                        if (!SectorNodesByCell.TryGetValue(cell, out var bucket))
                        {
                            bucket = new List<SectorNode>(2);
                            SectorNodesByCell[cell] = bucket;
                        }
                        bucket.Add(node);
                    }
                }
            }

            spatialIndexBuilt = true;
        }

        public static void ClearRevealJob(ResourceFogRevealJob job)
        {
            if (job == null) return;
            job.Centers.Clear();
            job.CenterCells.Clear();
            job.PendingNewCenters.Clear();
            job.PendingNewCells.Clear();
            job.HiddenIndex = 0;
            job.SectorIndex = 0;
            job.NodeIndex = 0;
            job.ColonyCenterIndex = 0;
            job.Phase = 0;
        }

        static void RevealNearWorlds(List<Vector3> centers)
        {
            if (!PrologueController.IsActive || centers == null || centers.Count == 0) return;
            var ctrl = PrologueController.Instance;
            if (ctrl == null) return;

            for (int c = 0; c < centers.Count; c++)
                RevealNearSingleCenter(ctrl, centers[c]);
        }

        public static void ForEachHiddenAtCell(Vector2Int cell, System.Action<HiddenResource> action)
        {
            if (action == null) return;
            EnsureHiddenCache();
            for (int i = 0; i < CachedHidden.Count; i++)
            {
                var hr = CachedHidden[i];
                if (hr == null) continue;
                if (ColonyTileGrid.WorldToCell(hr.transform.position) == cell)
                    action(hr);
            }
        }

        public static void RevealNearWorld(Vector3 world)
        {
            if (!PrologueController.IsActive) return;
            var ctrl = PrologueController.Instance;
            if (ctrl == null) return;
            RevealNearSingleCenter(ctrl, world);
        }

        static void EnsureHiddenCache()
        {
            if (CachedHidden.Count > 0) return;
            var found = Object.FindObjectsByType<HiddenResource>(FindObjectsInactive.Include);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null) CachedHidden.Add(found[i]);
            }
        }

        static void RevealNode(SectorNode node)
        {
            node.isExplored = true;
            node.isDiscovered = false;

            if (node.visualGO != null)
            {
                node.visualGO.SetActive(true);
                ShowRenderers(node.visualGO);
                ApplyTypedDotColor(node);
            }

            if (node.questionMarkGO != null)
            {
                var tmp = node.questionMarkGO.GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    string label = !string.IsNullOrEmpty(node.labelOverride)
                        ? node.labelOverride
                        : node.type.ToString();
                    tmp.text = label;
                    tmp.color = node.type == SectorNode.NodeType.Feature
                        ? new Color(0.55f, 0.95f, 1f)
                        : new Color(1f, 0.95f, 0.55f);
                }
            }

            if (node.visualGO != null
                && node.visualGO.TryGetComponent(out HiddenResource hr))
            {
                if (!hr.IsDiscovered)
                {
                    Vector2Int hrCell = ColonyTileGrid.WorldToCell(hr.transform.position);
                    if (HiddenByCell.TryGetValue(hrCell, out var bucket))
                    {
                        int idx = bucket.IndexOf(hr);
                        if (idx >= 0)
                            UncoverHiddenResource(hr, hrCell, bucket, idx);
                        else
                        {
                            hr.ForceDiscover();
                            NotifyPrologueDepositUncovered(hr);
                        }
                    }
                    else
                    {
                        hr.ForceDiscover();
                        NotifyPrologueDepositUncovered(hr);
                    }
                }
            }

            PrologueRevealGhosts.OnDepositNodeExplored(node);
        }

        static void RestoreKnownTypesAfterFogReset()
        {
            foreach (var hr in Object.FindObjectsByType<HiddenResource>(FindObjectsInactive.Include))
            {
                if (hr == null || !hr.IsDiscovered) continue;
                if (string.IsNullOrEmpty(hr.ResourceTypeName)) continue;
                DiscoverySystem.MarkTypeKnownWithoutMassReveal(hr.ResourceTypeName);
            }

            var colony = PrologueController.Instance?.Colony;
            if (colony == null) return;
            foreach (var id in colony.KnownResources)
                DiscoverySystem.MarkTypeKnownWithoutMassReveal(PrologueResourceIdUtil.ToTypeName(id));
        }

        static float nextHandRefreshAt;

        static void NotifyPrologueDepositUncovered(HiddenResource hr)
        {
            if (hr == null || !PrologueController.IsActive) return;
            if (!PrologueResourceIdUtil.TryParse(hr.ResourceTypeName, out var id)) return;

            DiscoverySystem.MarkTypeKnownWithoutMassReveal(hr.ResourceTypeName);
            var colony = PrologueController.Instance?.Colony;
            if (colony == null) return;
            colony.MarkResourceKnown(id);
            if (Time.unscaledTime < nextHandRefreshAt) return;
            nextHandRefreshAt = Time.unscaledTime + 0.5f;
            PrologueController.Instance.RefreshHandUnlocks();
        }

        static void ApplyUnknownVisual(SectorNode node)
        {
            if (node.questionMarkGO != null)
            {
                node.questionMarkGO.SetActive(true);
                var tmp = node.questionMarkGO.GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    tmp.text = "?";
                    tmp.color = new Color(1f, 0.92f, 0.2f, 1f);
                }
            }

            if (node.visualGO == null) return;

            // No colored puck / gas mesh — only the floating "?" until the drone uncovers it.
            HideRenderers(node.visualGO);

            if (node.visualGO.TryGetComponent(out GatherableSupply gs))
            {
                gs.SetVisible(false);
                gs.ToggleColliders(false);
            }
        }

        static void HideRenderers(GameObject go)
        {
            if (go == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r != null) r.enabled = false;
            }
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.gameObject.SetActive(false);
            }
        }

        static void ShowRenderers(GameObject go)
        {
            if (go == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r != null) r.enabled = true;
            }
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null) continue;
                ps.gameObject.SetActive(true);
            }
        }

        static void ApplyTypedDotColor(SectorNode node)
        {
            if (node.visualGO == null) return;
            if (!node.visualGO.TryGetComponent(out MeshRenderer mr) || mr.material == null) return;

            Color c = node.type switch
            {
                SectorNode.NodeType.Minerals => new Color(0.25f, 0.65f, 1f),
                SectorNode.NodeType.Gas => new Color(0.2f, 1f, 0.35f),
                SectorNode.NodeType.Iron => new Color(0.85f, 0.55f, 0.35f),
                SectorNode.NodeType.Regolith => new Color(0.75f, 0.55f, 0.25f),
                SectorNode.NodeType.Feature => new Color(0.55f, 0.95f, 1f),
                SectorNode.NodeType.Nexus => new Color(1f, 0f, 1f),
                _ => Color.white
            };
            var mat = mr.material;
            mat.color = c;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", c * 1.4f);
            }
        }
    }
}
