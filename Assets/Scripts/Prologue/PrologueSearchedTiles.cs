using System;
using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>
    /// Dirt road from the Command Post along hexes the survey has already walked.
    /// Roads are built incrementally — no full-board refresh walks.
    /// </summary>
    public class PrologueSearchedTiles : MonoBehaviour
    {
        public static PrologueSearchedTiles Instance { get; private set; }

        /// <summary>One ground texture tints surveyed hexes (no per-edge meshes).</summary>
        public static bool UseBakedPathOverlay;

        /// <summary>When overlay is on: brown disc per hex tile only — no segment lines between tiles.</summary>
        public static bool TileTintOnly = true;

        /// <summary>Legacy perf hog — many Lit cube primitives per trail edge.</summary>
        public static bool UseCubeRoadMeshes;

        readonly HashSet<long> roadKeys = new();
        readonly List<Vector2Int> neighborScratch = new(6);
        readonly List<CombineInstance> roadCombine = new(256);

        GameObject batchedRoadRoot;
        MeshFilter batchedMeshFilter;
        Mesh batchedMesh;
        bool roadBatchDirty;
        static Mesh sharedUnitCube;
        static readonly Color Dirt = new(0.40f, 0.24f, 0.12f, 1f);

        int commandPostTile = -1;
        int nearestRevealedToHome = -1;
        int nearestRevealedDist = int.MaxValue;
        bool commandPostRoadLinked;
        bool overlayDirty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Ensure()
        {
            if (UnityEngine.Object.FindAnyObjectByType<PrologueSearchedTiles>() != null) return;
            var go = new GameObject("PrologueSearchedTiles");
            DontDestroyOnLoad(go);
            go.AddComponent<PrologueSearchedTiles>();
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Clear();
        }

        /// <summary>Bootstrap / perf soak only: clear and replay incremental extend per revealed tile.</summary>
        public void RefreshAll() => RebuildFromRevealed();

        void RebuildFromRevealed()
        {
            var ctrl = PrologueController.Instance;
            if (ctrl?.Colony == null)
            {
                Clear();
                return;
            }

            Clear();
            ResetCommandPostTracking(ctrl);
            var batch = new List<int>();
            var board = ctrl.Colony.Board;
            for (int i = 0; i < board.Tiles.Count; i++)
            {
                if (board.At(i).Revealed) batch.Add(i);
            }

            ExtendForTiles(batch);
            FlushOverlay();
        }

        public void MarkTile(int tileIndex)
        {
            var ctrl = PrologueController.Instance;
            if (ctrl == null) return;
            if (ctrl.TryGetCell(tileIndex, out Vector2Int cell))
            {
                Vector3 world = ColonyTileGrid.CellToWorld(cell, 0f);
                PrologueVisionThrottle.QueueHexReveal(world);
            }
            ExtendForTiles(new[] { tileIndex });
        }

        /// <summary>Add road segments for newly surveyed tiles (6-neighbor edges only).</summary>
        public void RefreshForTiles(IReadOnlyList<int> newTiles) => ExtendForTiles(newTiles);

        public void ExtendForTiles(IReadOnlyList<int> newTiles)
        {
            if (newTiles == null || newTiles.Count == 0) return;
            var ctrl = PrologueController.Instance;
            if (ctrl?.Colony == null) return;

            if (commandPostTile < 0)
                ResetCommandPostTracking(ctrl);

            for (int n = 0; n < newTiles.Count; n++)
            {
                int tile = newTiles[n];
                if (ctrl.TryGetCell(tile, out Vector2Int cell))
                {
                    Vector3 world = ColonyTileGrid.CellToWorld(cell, 0f);
                    PrologueVisionThrottle.QueueHexReveal(world);
                }

                if (!ctrl.Colony.Board.At(tile).Revealed) continue;

                TrackNearestRevealed(ctrl, tile);
                ExtendOneTile(ctrl, tile);
                TryLinkCommandPostAdjacent(ctrl, tile);
            }

            TryCommandPostFallback(ctrl);
            if (roadBatchDirty) FlushRoadBatch();
        }

        /// <summary>Called once per frame from <see cref="PrologueVisionThrottle"/> — not on survey confirm.</summary>
        public void ApplyDeferredOverlay()
        {
            FlushOverlay();
            FlushRoadBatch();
        }

        void ResetCommandPostTracking(PrologueController ctrl)
        {
            commandPostTile = ResolveCommandPostTile(ctrl);
            nearestRevealedToHome = -1;
            nearestRevealedDist = int.MaxValue;
            commandPostRoadLinked = false;
        }

        void TrackNearestRevealed(PrologueController ctrl, int tile)
        {
            if (commandPostTile < 0) return;
            int d = ctrl.Colony.Dist(commandPostTile, tile);
            if (d >= nearestRevealedDist) return;
            nearestRevealedDist = d;
            nearestRevealedToHome = tile;
        }

        void ExtendOneTile(PrologueController ctrl, int tile)
        {
            if (UseBakedPathOverlay && TileTintOnly)
            {
                TintSurveyedTile(ctrl, tile);
                return;
            }

            var board = ctrl.Colony.Board;
            if (!ctrl.TryGetCell(tile, out Vector2Int cell)) return;

            neighborScratch.Clear();
            ColonyTileGrid.CollectOrthogonalNeighborCells(cell, neighborScratch);
            bool hasNeighborRoad = false;
            for (int i = 0; i < neighborScratch.Count; i++)
            {
                if (!ctrl.TryGetTileIndex(ColonyTileGrid.CellToWorldOnGround(neighborScratch[i]), out int nb)) continue;
                if (nb == tile || !board.At(nb).Revealed) continue;

                int a = Mathf.Min(tile, nb);
                int b = Mathf.Max(tile, nb);
                EnsureRoad(Key(a, b), a, b, ctrl);
                hasNeighborRoad = true;
            }

            if (!hasNeighborRoad)
                EnsurePad(Key(tile, tile), tile, ctrl);
        }

        void TryLinkCommandPostAdjacent(PrologueController ctrl, int tile)
        {
            if (commandPostTile < 0 || commandPostRoadLinked) return;
            if (ctrl.Colony.Dist(commandPostTile, tile) != 1) return;

            if (UseBakedPathOverlay && TileTintOnly)
            {
                TintSurveyedTile(ctrl, commandPostTile);
                commandPostRoadLinked = true;
                return;
            }

            if (!ctrl.TryGetCell(commandPostTile, out Vector2Int homeCell)
                || !ctrl.TryGetCell(tile, out Vector2Int tileCell)) return;

            long key = Key(commandPostTile, tile);
            Vector3 origin = CommandPostOrigin(ctrl, homeCell);
            EnsureSegment(key, origin, ColonyTileGrid.CellToWorldOnGround(tileCell), $"DirtRoad_CP_{tile}");
            commandPostRoadLinked = true;
        }

        void TryCommandPostFallback(PrologueController ctrl)
        {
            if (commandPostRoadLinked || commandPostTile < 0 || nearestRevealedToHome < 0) return;

            if (UseBakedPathOverlay && TileTintOnly)
            {
                TintSurveyedTile(ctrl, commandPostTile);
                TintSurveyedTile(ctrl, nearestRevealedToHome);
                commandPostRoadLinked = true;
                return;
            }

            if (!ctrl.TryGetCell(commandPostTile, out Vector2Int homeCell)
                || !ctrl.TryGetCell(nearestRevealedToHome, out Vector2Int nearCell)) return;

            long key = Key(commandPostTile, nearestRevealedToHome);
            Vector3 origin = CommandPostOrigin(ctrl, homeCell);
            EnsureSegment(key, origin, ColonyTileGrid.CellToWorldOnGround(nearCell), $"DirtRoad_CP_{nearestRevealedToHome}");
            commandPostRoadLinked = true;
        }

        void TintSurveyedTile(PrologueController ctrl, int tile)
        {
            if (!UseBakedPathOverlay || !ctrl.TryGetCell(tile, out Vector2Int cell)) return;
            ProloguePathGroundOverlay.Instance?.PaintPad(ColonyTileGrid.CellToWorldOnGround(cell));
            overlayDirty = true;
        }

        void FlushOverlay()
        {
            if (!overlayDirty || !UseBakedPathOverlay) return;
            var overlay = ProloguePathGroundOverlay.Instance;
            if (overlay == null) return;
            overlay.ApplyImmediate();
            overlayDirty = false;
        }

        /// <summary>Paint-mode preview + confirm repair — dirt on overlay even before colony RevealTiles.</summary>
        public void PaintTrailTiles(IReadOnlyList<int> tileIndices, bool immediateGpu = true)
        {
            if (tileIndices == null || tileIndices.Count == 0) return;
            var ctrl = PrologueController.Instance;
            if (ctrl == null) return;
            if (!UseBakedPathOverlay && PrologueController.IsActive)
                UseBakedPathOverlay = true;
            if (!UseBakedPathOverlay) return;

            EnsureOverlayReady(ctrl);
            var overlay = ProloguePathGroundOverlay.Instance;
            for (int i = 0; i < tileIndices.Count; i++)
            {
                if (!ctrl.TryGetCell(tileIndices[i], out Vector2Int cell)) continue;
                Vector3 world = ColonyTileGrid.CellToWorldOnGround(cell);
                overlay?.PaintPad(world);
            }

            overlayDirty = true;
            if (immediateGpu)
                overlay?.ApplyImmediate();
            else
            {
                overlay?.TryApplyNow(force: false);
                FlushOverlay();
            }
        }

        void EnsureOverlayReady(PrologueController ctrl) => EnsureOverlayReadyPublic(ctrl);

        public void EnsureOverlayReadyPublic(PrologueController ctrl)
        {
            if (!UseBakedPathOverlay || ctrl == null) return;
            var overlay = ProloguePathGroundOverlay.Instance;
            if (overlay == null)
            {
                ProloguePathGroundOverlay.SetEnabled(true);
                overlay = ProloguePathGroundOverlay.Instance;
            }
            overlay?.FitBoardIfNeeded(ctrl);
        }

        static Vector3 CommandPostOrigin(PrologueController ctrl, Vector2Int homeCell)
        {
            Vector3 origin = ColonyTileGrid.CellToWorldOnGround(homeCell);
            foreach (var building in BaseBuilding.ActiveBuildings)
            {
                if (building == null || building.BuildingSO == null) continue;
                if (building.BuildingSO.Name.IndexOf("Command", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                origin = building.transform.position;
                origin.y = ColonyTileGrid.SampleGroundY(origin);
                break;
            }
            return origin;
        }

        static int ResolveCommandPostTile(PrologueController ctrl)
        {
            var board = ctrl.Colony.Board;
            for (int i = 0; i < board.Tiles.Count; i++)
            {
                if (board.At(i).Building == PrologueBuildingId.CommandPost) return i;
            }
            return -1;
        }

        void EnsureRoad(long key, int a, int b, PrologueController ctrl)
        {
            if (!ctrl.TryGetCell(a, out Vector2Int cellA) || !ctrl.TryGetCell(b, out Vector2Int cellB)) return;
            EnsureSegment(
                key,
                ColonyTileGrid.CellToWorldOnGround(cellA),
                ColonyTileGrid.CellToWorldOnGround(cellB),
                $"DirtRoad_{a}_{b}");
        }

        void EnsureSegment(long key, Vector3 from, Vector3 to, string name)
        {
            if (UseBakedPathOverlay)
            {
                if (!TileTintOnly)
                {
                    ProloguePathGroundOverlay.Instance?.PaintSegment(from, to);
                    overlayDirty = true;
                }
                return;
            }

            if (!UseCubeRoadMeshes) return;

            if (roadKeys.Contains(key)) return;
            Vector3 flat = new Vector3(to.x - from.x, 0f, to.z - from.z);
            if (flat.sqrMagnitude < 0.01f) return;

            Vector3 mid = (from + to) * 0.5f;
            mid.y = ColonyTileGrid.SampleGroundY(mid) + 0.07f;
            var rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            var scale = new Vector3(1.25f, 0.06f, flat.magnitude);
            Matrix4x4 matrix = Matrix4x4.TRS(mid, rotation, scale);
            roadCombine.Add(new CombineInstance { mesh = UnitCubeMesh(), transform = matrix });
            roadKeys.Add(key);
            roadBatchDirty = true;

            if (commandPostTile >= 0)
            {
                int a = (int)(key >> 32);
                int b = (int)(key & 0xFFFFFFFFL);
                if (a == commandPostTile || b == commandPostTile)
                    commandPostRoadLinked = true;
            }
        }

        void EnsurePad(long key, int tile, PrologueController ctrl)
        {
            if (!ctrl.TryGetCell(tile, out Vector2Int cell)) return;

            if (UseBakedPathOverlay)
            {
                TintSurveyedTile(ctrl, tile);
                return;
            }

            if (!UseCubeRoadMeshes) return;

            if (roadKeys.Contains(key)) return;

            Vector3 pos = ColonyTileGrid.CellToWorldOnGround(cell);
            pos.y += 0.07f;
            var scale = new Vector3(1.6f, 0.06f, 1.6f);
            roadCombine.Add(new CombineInstance
            {
                mesh = UnitCubeMesh(),
                transform = Matrix4x4.TRS(pos, Quaternion.identity, scale)
            });
            roadKeys.Add(key);
            roadBatchDirty = true;
        }

        void EnsureBatchRoot()
        {
            if (batchedRoadRoot != null) return;
            batchedRoadRoot = new GameObject("BatchedDirtRoads");
            batchedRoadRoot.transform.SetParent(transform, false);
            batchedMeshFilter = batchedRoadRoot.AddComponent<MeshFilter>();
            var renderer = batchedRoadRoot.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = DirtMaterial();
        }

        void FlushRoadBatch()
        {
            if (!UseCubeRoadMeshes || !roadBatchDirty || roadCombine.Count == 0) return;
            EnsureBatchRoot();
            if (batchedMesh == null)
                batchedMesh = new Mesh { name = "PrologueDirtRoadBatch", indexFormat = IndexFormat.UInt32 };
            batchedMesh.CombineMeshes(roadCombine.ToArray(), true, true);
            batchedMesh.RecalculateBounds();
            batchedMeshFilter.sharedMesh = batchedMesh;
            roadBatchDirty = false;
        }

        static Mesh UnitCubeMesh()
        {
            if (sharedUnitCube != null) return sharedUnitCube;
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sharedUnitCube = temp.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.Destroy(temp);
            return sharedUnitCube;
        }

        static long Key(int a, int b)
        {
            if (a > b) (a, b) = (b, a);
            return ((long)a << 32) | (uint)b;
        }

        static Material dirtMat;

        static Material DirtMaterial()
        {
            if (dirtMat != null) return dirtMat;
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
            dirtMat = new Material(shader);
            if (dirtMat.HasProperty("_BaseColor")) dirtMat.SetColor("_BaseColor", Dirt);
            if (dirtMat.HasProperty("_Color")) dirtMat.SetColor("_Color", Dirt);
            dirtMat.color = Dirt;
            return dirtMat;
        }

        public void Clear()
        {
            roadKeys.Clear();
            roadCombine.Clear();
            roadBatchDirty = false;
            if (batchedRoadRoot != null)
            {
                Destroy(batchedRoadRoot);
                batchedRoadRoot = null;
                batchedMeshFilter = null;
            }
            if (batchedMesh != null)
            {
                Destroy(batchedMesh);
                batchedMesh = null;
            }
            if (UseBakedPathOverlay)
            {
                ProloguePathGroundOverlay.Instance?.ClearPixels();
                ProloguePathGroundOverlay.Instance?.ApplyImmediate();
            }
            overlayDirty = false;
            commandPostTile = -1;
            nearestRevealedToHome = -1;
            nearestRevealedDist = int.MaxValue;
            commandPostRoadLinked = false;
        }
    }
}