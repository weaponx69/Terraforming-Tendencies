using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using TMPro;
using UnityEngine;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>
    /// Keeps sector-0 gatherable "?" deposits on the Prologue hex disk and visible near camp.
    /// </summary>
    public static class PrologueSectorDepositBootstrap
    {
        public const int MinMysteryDepositsNearCamp = 6;
        public const int MaxHexDistFromCamp = 7;
        public const int MinHexDistFromCamp = 2;
        /// <summary>Another deposit won't sit on the adjacent hex (avoids obvious lines).</summary>
        const int MinHexGapBetweenDeposits = 2;

        public static void BootstrapSectorZero(PrologueController ctrl, PrologueColony colony)
        {
            if (ctrl == null || colony == null) return;
            if (SectorManager.Instance?.Sectors == null || SectorManager.Instance.Sectors.Count == 0) return;
            var sector = SectorManager.Instance.Sectors[0];
            if (sector?.Nodes == null) return;

            int homeTile = HomeTile(colony);
            var planet = PlanetGenerator.Instance;
            var claimedCells = new System.Collections.Generic.HashSet<Vector2Int>();

            foreach (var node in sector.Nodes)
            {
                if (node == null || node.type == SectorNode.NodeType.Nexus) continue;
                if (!WantsMysteryMarker(node)) continue;
                if (ctrl.TryGetTileIndex(node.position, out int t)
                    && ctrl.TryGetCell(t, out Vector2Int c))
                    claimedCells.Add(c);
            }

            var mysteryNodes = new List<SectorNode>();
            foreach (var node in sector.Nodes)
            {
                if (node == null || node.type == SectorNode.NodeType.Nexus) continue;
                if (!WantsMysteryMarker(node)) continue;
                if (node.isExplored) continue;
                mysteryNodes.Add(node);
            }
            Shuffle(mysteryNodes);

            foreach (var node in mysteryNodes)
            {
                if (!NeedsScatter(ctrl, colony, homeTile, node, claimedCells, out int tile, out Vector2Int cell))
                {
                    if (!PickDepositTile(ctrl, colony, homeTile, claimedCells, out tile, out cell))
                        continue;
                }

                claimedCells.Add(cell);
                SnapNodeToTile(ctrl, node, cell, planet);
                EnsureQuestionMark(node, cell, planet);
            }

            int near = CountUnexploredNearCamp(ctrl, colony, homeTile);
            int guard = 0;
            while (near < MinMysteryDepositsNearCamp && guard++ < 12)
            {
                if (!PickDepositTile(ctrl, colony, homeTile, claimedCells, out int tile, out Vector2Int cell))
                    break;
                claimedCells.Add(cell);
                var type = ExtraNodeType(guard);
                var flavor = type switch
                {
                    SectorNode.NodeType.Minerals => "A crystalline mineral deposit glistens in the light.",
                    SectorNode.NodeType.Gas => "Vaporous gases seep from fissures in the ground.",
                    SectorNode.NodeType.Iron => "A rich iron ore deposit, suitable for smelting.",
                    _ => "Loose regolith, useful for construction."
                };
                var extra = new SectorNode(type, Vector3.zero, flavor);
                sector.Nodes.Add(extra);
                SnapNodeToTile(ctrl, extra, cell, planet);
                EnsureGatherablePad(extra, planet);
                EnsureQuestionMark(extra, cell, planet);
                near++;
            }

            Debug.Log($"[Prologue] Mystery deposits near camp: {near} (target ≥{MinMysteryDepositsNearCamp}).");
        }

        static bool WantsMysteryMarker(SectorNode node)
        {
            if (node.type == SectorNode.NodeType.Feature)
            {
                string fl = node.labelOverride ?? string.Empty;
                if (fl.IndexOf("Water", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || fl.IndexOf("Glacier", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
                return true;
            }
            return SectorResourceBudget.IsGatherableNodeType(node.type);
        }

        static SectorNode.NodeType ExtraNodeType(int index) => (index % 4) switch
        {
            0 => SectorNode.NodeType.Minerals,
            1 => SectorNode.NodeType.Gas,
            2 => SectorNode.NodeType.Iron,
            _ => SectorNode.NodeType.Regolith
        };

        static int HomeTile(PrologueColony colony)
        {
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
            {
                if (colony.Board.At(i).Building == PrologueBuildingId.CommandPost)
                    return i;
            }
            return 0;
        }

        static int CountUnexploredNearCamp(PrologueController ctrl, PrologueColony colony, int homeTile)
        {
            if (SectorManager.Instance?.Sectors == null || SectorManager.Instance.Sectors.Count == 0) return 0;
            int n = 0;
            foreach (var node in SectorManager.Instance.Sectors[0].Nodes)
            {
                if (node == null || node.isExplored || !WantsMysteryMarker(node)) continue;
                if (!ctrl.TryGetTileIndex(node.position, out int tile)) continue;
                int d = colony.Dist(homeTile, tile);
                if (d >= MinHexDistFromCamp && d <= MaxHexDistFromCamp)
                    n++;
            }
            return n;
        }

        /// <summary>Keep a good on-disk spot only if it isn't stacked on another deposit.</summary>
        static bool NeedsScatter(
            PrologueController ctrl, PrologueColony colony, int homeTile, SectorNode node,
            HashSet<Vector2Int> claimed, out int tileIndex, out Vector2Int cell)
        {
            tileIndex = -1;
            cell = default;
            if (!ctrl.TryGetTileIndex(node.position, out tileIndex)
                || !ctrl.TryGetCell(tileIndex, out cell))
                return true;

            int d = colony.Dist(homeTile, tileIndex);
            if (d < MinHexDistFromCamp || d > MaxHexDistFromCamp)
                return true;

            if (claimed.Contains(cell))
                return true;

            foreach (var other in claimed)
            {
                if (ColonyTileGrid.HexDistance(cell, other) < MinHexGapBetweenDeposits)
                    return true;
            }

            return false;
        }

        static bool PickDepositTile(
            PrologueController ctrl, PrologueColony colony, int homeTile,
            HashSet<Vector2Int> claimed, out int tileIndex, out Vector2Int cell)
        {
            tileIndex = -1;
            cell = default;
            if (!ctrl.TryGetCell(homeTile, out Vector2Int homeCell))
                return false;

            // Random hex on a random ring — same idea as planet gen near camp, with spread from siblings.
            for (int attempt = 0; attempt < 64; attempt++)
            {
                if (TryRandomDepositCell(ctrl, colony, homeCell, claimed, out tileIndex, out cell))
                    return true;
            }

            // Fallback: shuffle all valid tiles and pick the one farthest from existing claims.
            var options = new List<(int tile, Vector2Int c, float score)>();
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
            {
                var t = colony.Board.At(i);
                if (t.Building != PrologueBuildingId.None && t.Building != PrologueBuildingId.CommandPost) continue;
                int d = colony.Dist(homeTile, i);
                if (d < MinHexDistFromCamp || d > MaxHexDistFromCamp) continue;
                if (!ctrl.TryGetCell(i, out Vector2Int c) || claimed.Contains(c)) continue;
                float spread = MinSeparationFromClaimed(c, claimed);
                if (spread < MinHexGapBetweenDeposits) continue;
                options.Add((i, c, spread + Random.Range(0f, 1.5f)));
            }
            if (options.Count == 0) return false;
            options.Sort((a, b) => b.score.CompareTo(a.score));
            int pick = Mathf.Min(Random.Range(0, 3), options.Count - 1);
            tileIndex = options[pick].tile;
            cell = options[pick].c;
            return true;
        }

        static bool TryRandomDepositCell(
            PrologueController ctrl, PrologueColony colony, Vector2Int homeCell,
            HashSet<Vector2Int> claimed, out int tileIndex, out Vector2Int cell)
        {
            tileIndex = -1;
            cell = default;
            int ring = Random.Range(MinHexDistFromCamp, MaxHexDistFromCamp + 1);
            int dq = Random.Range(-ring, ring + 1);
            int dr = Random.Range(-ring, ring + 1);
            cell = new Vector2Int(homeCell.x + dq, homeCell.y + dr);
            if (ColonyTileGrid.HexDistance(homeCell, cell) != ring) return false;
            if (claimed.Contains(cell)) return false;
            if (MinSeparationFromClaimed(cell, claimed) < MinHexGapBetweenDeposits) return false;

            Vector3 world = ColonyTileGrid.CellToWorldOnGround(cell);
            if (!ctrl.TryGetTileIndex(world, out tileIndex)) return false;
            var t = colony.Board.At(tileIndex);
            if (t.Building != PrologueBuildingId.None && t.Building != PrologueBuildingId.CommandPost)
                return false;
            return true;
        }

        static float MinSeparationFromClaimed(Vector2Int cell, HashSet<Vector2Int> claimed)
        {
            if (claimed == null || claimed.Count == 0) return MinHexGapBetweenDeposits;
            float best = float.MaxValue;
            foreach (var other in claimed)
                best = Mathf.Min(best, ColonyTileGrid.HexDistance(cell, other));
            return best;
        }

        static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        static void SnapNodeToTile(PrologueController ctrl, SectorNode node, Vector2Int cell, PlanetGenerator planet)
        {
            Vector3 ground = ColonyTileGrid.CellToWorldOnGround(cell);
            if (planet != null)
                ground.y = planet.SampleGroundY(ground);
            node.position = ground;
            node.isExplored = false;
            node.isDiscovered = true;

            if (node.visualGO != null)
            {
                node.visualGO.transform.position = ground;
                node.visualGO.transform.localScale = new Vector3(0.7f, 0.12f, 0.7f);
            }
        }

        static void EnsureGatherablePad(SectorNode node, PlanetGenerator planet)
        {
            if (!SectorResourceBudget.IsGatherableNodeType(node.type)) return;
            if (node.visualGO != null) return;

            var dot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dot.name = $"Node_{node.type}_Prologue";
            dot.transform.position = node.position;
            dot.transform.localScale = new Vector3(0.7f, 0.12f, 0.7f);
            if (planet != null)
                dot.transform.SetParent(planet.transform, false);

            string resourceType = node.type switch
            {
                SectorNode.NodeType.Minerals => "Minerals",
                SectorNode.NodeType.Gas => "Gas",
                SectorNode.NodeType.Iron => "Iron",
                _ => "Regolith"
            };
            var gs = dot.AddComponent<GatherableSupply>();
            gs.SetVisible(false);
            gs.ToggleColliders(false);
            var hr = dot.AddComponent<HiddenResource>();
            hr.ResourceTypeName = resourceType;
            Object.Destroy(dot.GetComponent<Collider>());
            node.visualGO = dot;
        }

        static void EnsureQuestionMark(SectorNode node, Vector2Int cell, PlanetGenerator planet)
        {
            Vector3 ground = ColonyTileGrid.CellToWorldOnGround(cell);
            if (planet != null)
                ground.y = planet.SampleGroundY(ground);

            float floatH = PrologueIceMarkers.DepositFloatHeight;
            Transform root;
            TextMeshPro tmp;

            if (node.questionMarkGO == null)
            {
                var parent = GameObject.Find("QuestionMarks");
                if (parent == null)
                {
                    parent = new GameObject("QuestionMarks");
                    if (planet != null)
                        parent.transform.SetParent(planet.transform, false);
                }
                var qmGo = new GameObject($"QuestionMark_{node.type}");
                qmGo.transform.SetParent(parent.transform, false);
                tmp = qmGo.AddComponent<TextMeshPro>();
                var mark = qmGo.AddComponent<PrologueDepositMark>();
                mark.GroundPoint = ground;
                node.questionMarkGO = qmGo;
                root = qmGo.transform;
            }
            else
            {
                root = node.questionMarkGO.transform;
                tmp = node.questionMarkGO.GetComponent<TextMeshPro>();
                if (node.questionMarkGO.TryGetComponent(out PrologueDepositMark mark))
                    mark.GroundPoint = ground;
            }

            StyleQuestionMark(root, tmp, ground, floatH);
            PrologueDepositMark.EnsureClickVolume(root, floatH);
        }

        public static void StyleQuestionMark(Transform root, TMP_Text tmp, Vector3 ground, float floatH)
        {
            if (root == null) return;
            root.position = ground + Vector3.up * floatH;
            float scale = PrologueIceMarkers.MarkerScale;
            root.localScale = Vector3.one * scale;

            if (tmp != null)
            {
                tmp.text = "?";
                tmp.fontSize = PrologueIceMarkers.MarkerFontSize;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = new Color(1f, 0.92f, 0.2f, 1f);
                tmp.fontStyle = FontStyles.Bold;
                tmp.outlineWidth = 0.25f;
                tmp.outlineColor = new Color(0.05f, 0.02f, 0.08f, 1f);
            }

            var pedestal = root.Find("Pedestal");
            if (pedestal == null)
            {
                var pedestalGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pedestalGo.name = "Pedestal";
                pedestalGo.transform.SetParent(root, false);
                pedestalGo.transform.localPosition = new Vector3(0f, -0.35f, 0f);
                pedestalGo.transform.localScale = new Vector3(0.55f, 0.08f, 0.55f);
                Object.Destroy(pedestalGo.GetComponent<Collider>());
                pedestal = pedestalGo.transform;
            }
            var r = pedestal.GetComponent<MeshRenderer>();
            if (r != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Unlit/Color");
                if (shader != null)
                {
                    var m = new Material(shader);
                    m.color = new Color(1f, 0.35f, 0.85f, 1f);
                    r.sharedMaterial = m;
                }
            }
        }
    }
}