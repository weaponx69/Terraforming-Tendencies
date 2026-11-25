using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.Units;
using TMPro;
using UnityEngine;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>
    /// See-through building ghost on a surveyed deposit. Each pad is created once and kept in
    /// <see cref="ghosts"/> until the player builds or the deposit is no longer eligible.
    /// </summary>
    public class PrologueRevealGhosts : MonoBehaviour
    {
        public static PrologueRevealGhosts Instance { get; private set; }

        const float GhostAlpha = 0.62f;

        /// <summary>Flat hex footprint instead of full site mesh (RTS view only needs silhouette).</summary>
        public static bool SilhouetteFootprintsOnly;

        /// <summary>Floating building names on suggest ghosts (off — mesh tint + cards are enough).</summary>
        public static bool ShowWorldLabels;

        public static bool FrustumCullRevealGhosts;

        readonly Dictionary<string, GameObject> ghosts = new();
        Plane[] frustumScratch = new Plane[6];
        int cullFrame = -1;
        static readonly Dictionary<string, Mesh> MeshCache = new();
        static readonly Dictionary<int, Material> TintMats = new();

        static bool bootstrapped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Ensure()
        {
            if (Object.FindAnyObjectByType<PrologueRevealGhosts>() != null) return;
            var go = new GameObject("PrologueRevealGhosts");
            DontDestroyOnLoad(go);
            go.AddComponent<PrologueRevealGhosts>();
        }

        void Awake() => Instance = this;

        void LateUpdate()
        {
            if (!FrustumCullRevealGhosts || ghosts.Count == 0) return;
            if (cullFrame == Time.frameCount) return;
            cullFrame = Time.frameCount;

            var cam = PlayerInput.Instance != null ? PlayerInput.Instance.GameplayCamera : Camera.main;
            if (cam == null) return;
            GeometryUtility.CalculateFrustumPlanes(cam, frustumScratch);

            foreach (var kv in ghosts)
            {
                var go = kv.Value;
                if (go == null) continue;
                if (!go.TryGetComponent(out GhostCullCache cache))
                    cache = go.AddComponent<GhostCullCache>();
                cache.ApplyFrustum(frustumScratch);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Clear();
            bootstrapped = false;
        }

        /// <summary>Legacy entry — bootstraps once, then incremental APIs own updates.</summary>
        public static void Refresh()
        {
            if (!PrologueController.IsActive) return;
            if (Instance == null)
                new GameObject("PrologueRevealGhosts").AddComponent<PrologueRevealGhosts>();
            BootstrapFromColonyIfNeeded();
        }

        /// <summary>After planet sync — spawn ghosts for anything already surveyed.</summary>
        public static void BootstrapFromColonyIfNeeded()
        {
            if (!PrologueController.IsActive || bootstrapped) return;
            if (Instance == null)
                new GameObject("PrologueRevealGhosts").AddComponent<PrologueRevealGhosts>();
            Instance.SyncAlreadyRevealed();
            SuppressRedundantWorldLabels();
            bootstrapped = true;
        }

        /// <summary>Hide duplicate deposit/? text once meshes and ghosts mark the site.</summary>
        public static void SuppressRedundantWorldLabels()
        {
            if (!PrologueController.IsActive) return;

            foreach (var hr in Object.FindObjectsByType<HiddenResource>(FindObjectsInactive.Include))
            {
                if (hr == null) continue;
                var label = hr.transform.Find("DepositLabel");
                if (label != null)
                    label.gameObject.SetActive(false);
            }

            var sm = SectorManager.Instance;
            if (sm?.Sectors == null) return;
            foreach (var sector in sm.Sectors)
            {
                if (sector?.Nodes == null) continue;
                foreach (var node in sector.Nodes)
                {
                    if (node == null || !node.isExplored) continue;
                    if (node.questionMarkGO != null)
                        node.questionMarkGO.SetActive(false);
                }
            }
        }

        public static void ResetSession()
        {
            bootstrapped = false;
            MeshCache.Clear();
            Instance?.Clear();
        }

        /// <summary>Drops cached meshes and re-spawns ghosts (e.g. after toggling silhouette mode).</summary>
        public static void ForceResyncMeshes()
        {
            MeshCache.Clear();
            if (Instance == null) return;
            Instance.Clear();
            bootstrapped = false;
            BootstrapFromColonyIfNeeded();
            SuppressRedundantWorldLabels();
        }

        public static void OnTileSurveyRevealed(int tileIndex) => Instance?.TryEnsureIceAt(tileIndex);

        public static void OnDepositNodeExplored(SectorNode node) => Instance?.TryEnsureAtNode(node);

        public static void OnPadBuilt(Vector3 world)
        {
            PrologueBuildingRegistry.Invalidate();
            Instance?.RemoveAtCell(ColonyTileGrid.WorldToCell(world));
        }

        void SyncAlreadyRevealed()
        {
            var colony = PrologueController.Instance?.Colony;
            if (colony == null) return;
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
                TryEnsureIceAt(i);

            var sm = SectorManager.Instance;
            if (sm?.Sectors == null) return;
            foreach (var sector in sm.Sectors)
            {
                if (sector?.Nodes == null) continue;
                foreach (var node in sector.Nodes)
                    TryEnsureAtNode(node);
            }
        }

        void TryEnsureIceAt(int tileIndex)
        {
            var ctrl = PrologueController.Instance;
            var colony = ctrl?.Colony;
            if (colony == null || !ctrl.TryGetCell(tileIndex, out Vector2Int cell)) return;

            var tile = colony.Board.At(tileIndex);
            if (tile.DepositLeft <= 0) return;
            if (tile.Feature != PrologueFeature.Ice && tile.Feature != PrologueFeature.Glacier) return;
            if (!tile.Revealed) return;

            var aquifer = LoadByName("Water Ice Aquifer", "Cards/WaterIceAquiferCard");
            if (aquifer == null) return;
            if (AquiferCovers(cell)) return;

            string key = IceKey(cell);
            if (ghosts.TryGetValue(key, out var existing) && existing != null) return;

            if (!colony.TryFindAquiferPadNearIce(tileIndex, out int padTile)
                || !ctrl.TryGetCell(padTile, out Vector2Int padCell))
            {
                PrologueDepositVisuals.SeatNodesForBoardTile(ctrl, tileIndex);
                return;
            }

            PrologueDepositVisuals.SeatNodesForBoardTile(ctrl, tileIndex);
            var ghost = SpawnGhost(
                aquifer,
                ColonyTileGrid.CellToWorldOnHighestGround(padCell),
                padCell,
                null,
                padTile);
            if (ghost != null) ghosts[key] = ghost;
        }

        void TryEnsureAtNode(SectorNode node)
        {
            if (node == null || !node.isExplored || node.type == SectorNode.NodeType.Nexus) return;

            var ctrl = PrologueController.Instance;
            BuildingSO building = BuildingForNode(node);
            if (building == null) return;

            if (node.type == SectorNode.NodeType.Feature
                && building.Name.IndexOf("Aquifer", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var colony = ctrl?.Colony;
                if (colony != null && ctrl != null && ctrl.TryGetTileIndex(node.position, out int iceTile))
                {
                    TryEnsureIceAt(iceTile);
                    HideNodeMarker(node);
                    return;
                }
            }

            Vector2Int cell = ColonyTileGrid.WorldToCell(node.position);
            string iceKey = IceKey(cell);
            if (node.type == SectorNode.NodeType.Feature && ghosts.ContainsKey(iceKey))
            {
                HideNodeMarker(node);
                return;
            }

            if (OccupiedBy(cell, building))
            {
                HideNodeMarker(node);
                HideDepositBeacon(cell);
                return;
            }

            string key = NodeKey(node);
            if (ghosts.TryGetValue(key, out var existing) && existing != null) return;

            Vector2Int depositCell = cell;
            int boardTile = -1;
            Vector3 world;
            if (ctrl != null && ctrl.TryGetTileIndex(node.position, out int tile) && ctrl.TryGetCell(tile, out depositCell))
            {
                boardTile = tile;
                world = ColonyTileGrid.CellToWorldOnHighestGround(depositCell);
            }
            else
            {
                depositCell = ColonyTileGrid.WorldToCell(node.position);
                world = ColonyTileGrid.CellToWorldOnHighestGround(depositCell);
            }
            var ghost = SpawnGhost(building, world, depositCell, null, boardTile);
            if (ghost == null) return;
            ghosts[key] = ghost;
            HideNodeMarker(node);
        }

        void RemoveAtCell(Vector2Int cell)
        {
            string iceKey = IceKey(cell);
            RemoveGhostKey(iceKey);

            var stale = new List<string>();
            foreach (var kv in ghosts)
            {
                if (kv.Value == null)
                {
                    stale.Add(kv.Key);
                    continue;
                }
                if (ColonyTileGrid.WorldToCell(kv.Value.transform.position) == cell)
                    stale.Add(kv.Key);
            }
            foreach (string k in stale)
                RemoveGhostKey(k);
        }

        void RemoveGhostKey(string key)
        {
            if (ghosts.TryGetValue(key, out var go) && go != null)
                Destroy(go);
            ghosts.Remove(key);
        }

        static string IceKey(Vector2Int cell) => $"ice:{cell.x}:{cell.y}";

        static string NodeKey(SectorNode node)
        {
            int x = Mathf.RoundToInt(node.position.x * 2f);
            int z = Mathf.RoundToInt(node.position.z * 2f);
            return $"node:{node.type}:{x}:{z}";
        }

        static BuildingSO BuildingForNode(SectorNode node)
        {
            return node.type switch
            {
                SectorNode.NodeType.Gas => LoadByName("Gas Well", "Cards/GasWellCard"),
                SectorNode.NodeType.Iron => LoadByName("Iron Ore Extractor", "Cards/IronOreExtractorCard"),
                SectorNode.NodeType.Regolith => LoadByName("Basalt Strip-Mine", "Cards/BasaltStripMineCard"),
                SectorNode.NodeType.Minerals => LoadByName("Deep-Core Mining Laser", "Cards/DeepCoreMiningLaserCard"),
                SectorNode.NodeType.Feature => FeatureBuilding(node),
                _ => null
            };
        }

        static BuildingSO FeatureBuilding(SectorNode node)
        {
            string label = node.labelOverride ?? string.Empty;
            if (label.IndexOf("Water", System.StringComparison.OrdinalIgnoreCase) >= 0
                || label.IndexOf("Glacier", System.StringComparison.OrdinalIgnoreCase) >= 0
                || label.IndexOf("Ice", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return LoadByName("Water Ice Aquifer", "Cards/WaterIceAquiferCard");
            return null;
        }

        static bool AquiferCovers(Vector2Int iceCell)
        {
            foreach (var building in PrologueBuildingRegistry.PlacedBuildings)
            {
                if (building == null || building.BuildingSO == null) continue;
                if (building.BuildingSO.Name.IndexOf("Aquifer", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                Vector2Int cell = ColonyTileGrid.WorldToCell(building.transform.position);
                if (ColonyTileGrid.HexDistance(iceCell, cell) <= PrologueData.HarvestRange)
                    return true;
            }
            return false;
        }

        static bool OccupiedBy(Vector2Int cell, BuildingSO building)
        {
            foreach (var existing in PrologueBuildingRegistry.PlacedBuildings)
            {
                if (existing == null) continue;
                if (ColonyTileGrid.WorldToCell(existing.transform.position) != cell) continue;
                if (existing.BuildingSO != null && existing.BuildingSO.Name == building.Name)
                    return true;
                if (existing.name.IndexOf(building.Name, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        static GameObject SpawnGhost(
            BuildingSO building,
            Vector3 world,
            Vector2Int cell = default,
            string subtitle = null,
            int boardTileIndex = -1)
        {
            if (building == null) return null;
            bool footprint = SilhouetteFootprintsOnly;
            Mesh mesh = footprint ? FootprintMesh(building) : MeshFor(building);
            if (mesh == null) return null;

            world = ColonyTileGrid.CellToWorldOnHighestGround(cell);
            world.y += 0.06f;

            Color tint = TintFor(building);
            var ghost = new GameObject($"RevealGhost_{building.Name}");
            ghost.transform.position = world;
            ghost.transform.rotation = Quaternion.identity;

            var meshGo = new GameObject("Building Mesh");
            meshGo.transform.SetParent(ghost.transform, false);
            var filter = meshGo.AddComponent<MeshFilter>();
            var renderer = meshGo.AddComponent<MeshRenderer>();
            filter.sharedMesh = mesh;
            var mat = SharedTint(tint);
            int slots = Mathf.Max(1, mesh.subMeshCount);
            var mats = new Material[slots];
            for (int i = 0; i < slots; i++) mats[i] = mat;
            renderer.sharedMaterials = mats;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var box = ghost.AddComponent<BoxCollider>();
            box.center = mesh.bounds.center;
            box.size = mesh.bounds.size;

            SeatOnGround(ghost, world.y);
            if (ShowWorldLabels)
                AddName(ghost, DisplayLabel(building), tint, cell, footprint, subtitle);

            if (boardTileIndex >= 0)
            {
                var pad = ghost.AddComponent<PrologueRevealGhostPad>();
                pad.BoardTileIndex = boardTileIndex;
            }

            return ghost;
        }

        static string DisplayLabel(BuildingSO building)
        {
            if (building == null) return "Deposit";
            if (building.Name.IndexOf("Aquifer", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "Ice Aquifer";
            if (building.Name.IndexOf("Oxygen", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "O₂ Plant";
            return building.Name;
        }

        static Mesh FootprintMesh(BuildingSO building)
        {
            float r = ColonyTileGrid.TileSize * 0.92f;
            float h = building != null && building.Name.IndexOf("Aquifer", System.StringComparison.OrdinalIgnoreCase) >= 0
                ? 0.35f
                : 0.12f;
            var mesh = new Mesh { name = "GhostFootprint" };
            mesh.vertices = new[]
            {
                new Vector3(-r, 0f, 0f), new Vector3(-r * 0.5f, 0f, r * 0.866f), new Vector3(r * 0.5f, 0f, r * 0.866f),
                new Vector3(r, 0f, 0f), new Vector3(r * 0.5f, 0f, -r * 0.866f), new Vector3(-r * 0.5f, 0f, -r * 0.866f),
                new Vector3(0f, h, 0f)
            };
            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2, 0, 4, 3, 0, 5, 4, 0, 1, 5,
                6, 1, 2, 6, 2, 3, 6, 3, 4, 6, 4, 5, 6, 5, 1
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh MeshFor(BuildingSO building)
        {
            if (building == null) return null;
            string name = building.Name ?? string.Empty;
            if (MeshCache.TryGetValue(name, out var cached) && cached != null)
                return cached;

            Mesh mesh = null;
            if (name.IndexOf("Aquifer", System.StringComparison.OrdinalIgnoreCase) >= 0)
                mesh = WaterIceAquiferMeshBuilder.SharedMesh;
            else
                mesh = ColonyBuildingMeshLibrary.Build(name);

            if (mesh == null)
                mesh = MeshFromBuildingPrefab(building);

            if (mesh == null)
            {
                mesh = new Mesh { name = "RevealPad" };
                mesh.vertices = new[]
                {
                    new Vector3(-1.4f, 0.05f, -1.2f), new Vector3(1.4f, 0.05f, -1.2f),
                    new Vector3(1.4f, 0.05f, 1.2f), new Vector3(-1.4f, 0.05f, 1.2f),
                    new Vector3(-1.1f, 1.2f, -0.4f), new Vector3(1.1f, 1.2f, -0.4f),
                    new Vector3(1.1f, 1.2f, 0.4f), new Vector3(-1.1f, 1.2f, 0.4f)
                };
                mesh.triangles = new[]
                {
                    0, 2, 1, 0, 3, 2,
                    4, 5, 6, 4, 6, 7,
                    0, 1, 5, 0, 5, 4,
                    1, 2, 6, 1, 6, 5,
                    2, 3, 7, 2, 7, 6,
                    3, 0, 4, 3, 4, 7
                };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            }

            MeshCache[name] = mesh;
            return mesh;
        }

        static Mesh MeshFromBuildingPrefab(BuildingSO building)
        {
            if (building?.Prefab == null) return null;
            var filters = building.Prefab.GetComponentsInChildren<MeshFilter>(true);
            if (filters == null || filters.Length == 0) return null;

            var root = building.Prefab.transform;
            var combines = new List<CombineInstance>(filters.Length);
            foreach (var filter in filters)
            {
                if (filter == null || filter.sharedMesh == null) continue;
                combines.Add(new CombineInstance
                {
                    mesh = filter.sharedMesh,
                    transform = root.worldToLocalMatrix * filter.transform.localToWorldMatrix
                });
            }

            if (combines.Count == 0) return null;
            if (combines.Count == 1)
                return combines[0].mesh;

            var combined = new Mesh { name = $"{building.Name}_RevealGhost" };
            combined.CombineMeshes(combines.ToArray(), true, true);
            combined.RecalculateBounds();
            return combined;
        }

        static void SeatOnGround(GameObject ghost, float groundY)
        {
            GameDevTV.RTS.Utilities.ReservedSiteBuildUtility.SeatVisualsOnGround(ghost, 0.06f);
        }

        static Vector3 LabelOffsetMeters(Vector2Int cell)
        {
            if (cell.x == 0 && cell.y == 0) return Vector3.zero;
            int h = Mathf.Abs(cell.x * 73856093 ^ cell.y * 19349663);
            float y = (h % 80) * 0.04f;
            float x = ((h % 31) - 15) * 0.22f;
            float z = ((h % 37) - 18) * 0.22f;
            return new Vector3(x, y, z);
        }

        static void AddName(GameObject ghost, string buildingName, Color tint, Vector2Int cell, bool footprint, string subtitle)
        {
            float top = ghost.transform.position.y + (footprint ? 0.45f : 2.4f);
            foreach (var renderer in ghost.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled) continue;
                top = Mathf.Max(top, renderer.bounds.max.y);
            }

            Vector3 labelOff = LabelOffsetMeters(cell);
            float labelHeight = top - ghost.transform.position.y + 0.85f + labelOff.y;
            var textGo = new GameObject("Ghost Name");
            textGo.transform.SetParent(ghost.transform, false);
            textGo.transform.localPosition = new Vector3(labelOff.x, labelHeight, labelOff.z);
            textGo.transform.localScale = Vector3.one * (footprint ? 1.35f : 1.75f);
            var billboard = PrologueBillboardLabel.Ensure(textGo);
            if (billboard != null)
            {
                billboard.AnchorToGround = false;
                billboard.SetReadability(30f, 1.15f, 1.85f);
            }

            var tmp = textGo.AddComponent<TextMeshPro>();
            tmp.text = string.IsNullOrEmpty(subtitle)
                ? buildingName
                : $"{buildingName}\n<size=70%>{subtitle}</size>";
            tmp.fontSize = footprint ? 6.5f : 7.5f;
            tmp.lineSpacing = -4f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.outlineWidth = 0.28f;
            tmp.outlineColor = new Color(tint.r * 0.2f, tint.g * 0.2f, tint.b * 0.2f, 1f);
            tmp.enableAutoSizing = false;
            tmp.raycastTarget = false;
        }

        static Color TintFor(BuildingSO building)
        {
            string n = building?.Name ?? string.Empty;
            if (n.IndexOf("Aquifer", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return new Color(0.45f, 0.78f, 1f, GhostAlpha);
            if (n.IndexOf("Gas", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return new Color(0.45f, 1f, 0.55f, GhostAlpha);
            if (n.IndexOf("Iron", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return new Color(1f, 0.62f, 0.32f, GhostAlpha);
            if (n.IndexOf("Basalt", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Regolith", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return new Color(0.95f, 0.78f, 0.42f, GhostAlpha);
            return new Color(0.4f, 0.72f, 1f, GhostAlpha);
        }

        static Material SharedTint(Color tint)
        {
            int key = tint.GetHashCode();
            if (TintMats.TryGetValue(key, out var existing) && existing != null)
                return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
            Color glow = new Color(tint.r, tint.g, tint.b, 1f) * 0.55f;
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", glow);
            }
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            mat.renderQueue = 3000;
            TintMats[key] = mat;
            return mat;
        }

        static void HideDepositBeacon(Vector2Int cell)
        {
            PrologueResourceFog.ForEachHiddenAtCell(cell, hr =>
            {
                var label = hr.transform.Find("DepositLabel");
                if (label != null) label.gameObject.SetActive(false);
                if (hr.TryGetComponent<MeshRenderer>(out var mr))
                    mr.enabled = false;
            });
        }

        static void HideNodeMarker(SectorNode node)
        {
            if (node == null) return;
            if (node.questionMarkGO != null)
                node.questionMarkGO.SetActive(false);
            HideDepositMesh(node.visualGO);
        }

        static void HideDepositMesh(GameObject go)
        {
            if (go == null) return;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != null) renderer.enabled = false;
            }
        }

        static BuildingSO LoadByName(string buildingName, string cardPath)
        {
            BuildingSO building = BlueprintDraftManager.GetBuildingSOByName(buildingName);
            if (building != null) return building;
            var card = Resources.Load<UnlockBuildingCardSO>(cardPath);
            if (card?.buildingToUnlock == null) return null;
            BlueprintDraftManager.RegisterBuildingSO(card.buildingToUnlock);
            return card.buildingToUnlock;
        }

        void Clear()
        {
            foreach (var kv in ghosts)
            {
                if (kv.Value != null) Destroy(kv.Value);
            }
            ghosts.Clear();
        }

        sealed class GhostCullCache : MonoBehaviour
        {
            MeshRenderer[] renderers;
            TMPro.TextMeshPro label;

            void Awake()
            {
                renderers = GetComponentsInChildren<MeshRenderer>(true);
                label = GetComponentInChildren<TMPro.TextMeshPro>(true);
            }

            public void ApplyFrustum(Plane[] planes)
            {
                if (renderers == null || renderers.Length == 0)
                {
                    renderers = GetComponentsInChildren<MeshRenderer>(true);
                    label = GetComponentInChildren<TMPro.TextMeshPro>(true);
                }

                bool any = false;
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    bool vis = GeometryUtility.TestPlanesAABB(planes, r.bounds);
                    r.enabled = vis;
                    if (vis) any = true;
                }

                if (label != null) label.enabled = any;
            }
        }
    }
}