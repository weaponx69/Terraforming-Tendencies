using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;
using System.Collections.Generic;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Manages hex grid overlay for the map, including Shroud generation and hex coordinate conversion.
    /// Maps 3D world space to pointy-topped hex coordinates and provides methods for grid operations.
    /// </summary>
    public class HexGridManager : MonoBehaviour
    {
        public static HexGridManager Instance { get; private set; }
        public static event System.Action OnStartingAreaRevealed;

        [Header("Grid Settings")]
        [SerializeField] private float cellSize = 2.5f;
        [SerializeField] private Vector2Int gridDimensions = new Vector2Int(50, 50);
        [SerializeField] private Transform gridRoot;
        
        [Header("Shroud Settings")]
        [SerializeField] private GameObject shroudTilePrefab;
        [SerializeField] private LayerMask shroudLayer;
        [SerializeField] private bool generateShroudOnStart = true;
        [SerializeField] private float startingAreaRevealRadius = 15f;

        public float StartingAreaRevealRadius => startingAreaRevealRadius;

        /// <summary>Outer radius — kept in sync with <see cref="GameDevTV.RTS.Player.ColonyTileGrid.TileSize"/>.</summary>
        public float CellSize => GameDevTV.RTS.Player.ColonyTileGrid.TileSize;

        public static bool HighlightTrace { get; private set; }

        public static void SetHighlightTrace(bool enabled)
        {
            HighlightTrace = enabled;
        }
        
        // Hex grid data
        private Dictionary<Vector2Int, HexTile> hexGrid = new Dictionary<Vector2Int, HexTile>();
        static readonly Vector2Int[] HoverNeighborDeltas =
        {
            new(+1, 0), new(+1, -1), new(0, -1),
            new(-1, 0), new(-1, +1), new(0, +1),
        };
        private Dictionary<Vector3, Vector2Int> worldToHexMap = new Dictionary<Vector3, Vector2Int>();
        private Vector3 gridOrigin;

        /// <summary>Prologue disk: one ring each — not ~600 shroud GameObjects with LineRenderers.</summary>
        LineRenderer sharedHoverRing;
        LineRenderer sharedHighlightRing;
        Vector2Int? sharedHoverCell;
        Vector2Int? sharedHighlightCell;
        
        // Flat-topped hex geometry driven by ColonyTileGrid (building-sized cells).
        private float HexWidth => GameDevTV.RTS.Player.ColonyTileGrid.HexWidth;
        private float HexHeight => GameDevTV.RTS.Player.ColonyTileGrid.HexHeight;

        private void OnEnable()
        {
            Instance = this;
            PlanetGenerator.OnPlanetGenerated += RevealStartingArea;
        }

        private void OnDisable()
        {
            PlanetGenerator.OnPlanetGenerated -= RevealStartingArea;
            if (Instance == this) HighlightTrace = false;
            if (Instance == this) Instance = null;
        }
        
        /// <summary>
        /// Represents a single hex tile in the grid.
        /// </summary>
        public class HexTile
        {
            public Vector2Int HexCoordinates { get; private set; }
            public Vector3 WorldPosition { get; private set; }
            public GameObject GameObject { get; private set; }
            public bool IsRevealed { get; private set; }
            private LineRenderer outline;
            private Material outlineMaterial;
            private bool isHighlighted;
            private bool isHovered;
            
            public HexTile(Vector2Int hexCoords, Vector3 worldPos, GameObject hexGO)
            {
                HexCoordinates = hexCoords;
                WorldPosition = worldPos;
                GameObject = hexGO;
                IsRevealed = false;
                outline = hexGO != null ? hexGO.GetComponent<LineRenderer>() : null;
                outlineMaterial = outline != null ? outline.material : null;
            }
            
            public void Reveal()
            {
                if (!IsRevealed)
                {
                    IsRevealed = true;
                    if (GameObject == null) return;
                    // Disable the shroud material or destroy the tile
                    // Disable the solid mesh, but leave the GameObject (and its LineRenderer) active!
                    if (GameObject != null)
                    {
                        Renderer[] renderers = GameObject.GetComponentsInChildren<Renderer>();
                        int count = 0;
                        foreach (Renderer r in renderers)
                        {
                            // Don't disable the LineRenderer we just added!
                            if (!(r is LineRenderer))
                            {
                                r.enabled = false;
                                count++;
                            }
                        }
                        if (count == 0) Debug.LogWarning($"[HexTile.Reveal] Found zero renderers to disable on {GameObject.name}! Is the shroud prefab missing a MeshRenderer?");
                    }
                    UpdateOutline();
                }
            }
            
            public void Destroy()
            {
                if (GameObject != null)
                {
                    UnityEngine.Object.Destroy(GameObject);
                }
            }

            public void SetHighlighted(bool highlighted)
            {
                isHighlighted = highlighted;
                if (HighlightTrace)
                {
                    Debug.Log($"[HexHighlight] Selected {HexCoordinates} -> {highlighted}; revealed={IsRevealed}, object={GameObject?.name ?? "NULL"}, outline={outline != null}");
                }
                if (GameObject == null)
                {
                    var mgr = Instance;
                    if (mgr == null) return;
                    if (highlighted) mgr.sharedHighlightCell = HexCoordinates;
                    else if (mgr.sharedHighlightCell == HexCoordinates) mgr.sharedHighlightCell = null;
                    mgr.RefreshSharedOutlines();
                    return;
                }
                UpdateOutline();
            }

            public void SetHovered(bool hovered)
            {
                isHovered = hovered;
                if (HighlightTrace)
                {
                    Debug.Log($"[HexHighlight] Hovered {HexCoordinates} -> {hovered}; revealed={IsRevealed}, object={GameObject?.name ?? "NULL"}, outline={outline != null}");
                }
                if (GameObject == null)
                {
                    var mgr = Instance;
                    if (mgr == null) return;
                    if (hovered) mgr.sharedHoverCell = HexCoordinates;
                    else if (mgr.sharedHoverCell == HexCoordinates) mgr.sharedHoverCell = null;
                    mgr.RefreshSharedOutlines();
                    return;
                }
                UpdateOutline();
            }

            private void UpdateOutline()
            {
                if (GameObject == null) return;
                if (outline == null)
                {
                    if (HighlightTrace) Debug.LogWarning($"[HexHighlight] Cannot update {HexCoordinates}: LineRenderer is missing.");
                    return;
                }

                // Only draw a hex when the cursor (or keyboard selection) is on it —
                // never leave a permanent cyan/dark grid over the whole map.
                bool show = isHovered || isHighlighted;
                outline.enabled = show;
                if (!show) return;

                Color color = isHighlighted ? new Color(0.1f, 0.55f, 1f) : Color.white;
                float width = isHighlighted ? 0.32f : 0.22f;
                outline.startColor = color;
                outline.endColor = color;
                outline.startWidth = width;
                outline.endWidth = width;
                if (outlineMaterial != null)
                {
                    if (outlineMaterial.HasProperty("_BaseColor")) outlineMaterial.SetColor("_BaseColor", color);
                    if (outlineMaterial.HasProperty("_Color")) outlineMaterial.SetColor("_Color", color);
                }
                RefreshOutlineGeometry();
            }

            /// <summary>Re-seat the ring on current terrain (planet DEM), not spawn-time flat Y.</summary>
            void RefreshOutlineGeometry()
            {
                if (outline == null) return;
                Vector3 center = GameDevTV.RTS.Player.ColonyTileGrid.CellToWorldOnGround(HexCoordinates);
                float lift = 0.14f;
                float radius = GameDevTV.RTS.Player.ColonyTileGrid.TileSize;
                var points = new Vector3[6];
                for (int i = 0; i < 6; i++)
                {
                    float angleRad = Mathf.PI / 180f * (60f * i);
                    points[i] = new Vector3(
                        center.x + radius * Mathf.Cos(angleRad),
                        center.y + lift,
                        center.z + radius * Mathf.Sin(angleRad));
                }
                outline.positionCount = 6;
                outline.SetPositions(points);
            }

            /// <summary>Re-apply outline visibility (e.g. after the hover-only policy lands).</summary>
            public void RefreshOutlineVisibility()
            {
                if (GameObject == null)
                {
                    isHighlighted = false;
                    isHovered = false;
                    return;
                }
                if (GameObject != null)
                {
                    foreach (var rend in GameObject.GetComponentsInChildren<Renderer>(true))
                    {
                        if (rend is LineRenderer) continue;
                        rend.enabled = false;
                    }
                }
                UpdateOutline();
            }
        }

        /// <summary>Force every hex outline to re-evaluate (hides the permanent grid).</summary>
        public void RefreshAllOutlineVisibility()
        {
            foreach (var tile in hexGrid.Values)
                tile.RefreshOutlineVisibility();
            sharedHoverCell = null;
            sharedHighlightCell = null;
            RefreshSharedOutlines();
        }

        /// <summary>
        /// Prologue colony is a small disk — replace planet-wide shroud hexes (10k+) with a local ring (~500).
        /// </summary>
        public void GenerateLocalHexDisk(Vector2Int centerCell, int axialRadius)
        {
            float size = CellSize;
            cellSize = size;
            if (gridRoot == null)
            {
                var rootGO = new GameObject("HexGridRoot");
                gridRoot = rootGO.transform;
            }
            ClearHexGrid();
            EnsureSharedOutlines();
            sharedHoverCell = null;
            sharedHighlightCell = null;
            int created = 0;
            for (int dq = -axialRadius; dq <= axialRadius; dq++)
            {
                for (int dr = -axialRadius; dr <= axialRadius; dr++)
                {
                    var cell = new Vector2Int(centerCell.x + dq, centerCell.y + dr);
                    if (GameDevTV.RTS.Player.ColonyTileGrid.HexDistance(centerCell, cell) > axialRadius)
                        continue;
                    Vector3 worldPos = HexToWorldPosition(cell);
                    var hexTile = new HexTile(cell, worldPos, null);
                    hexGrid[cell] = hexTile;
                    created++;
                }
            }
            foreach (var tile in hexGrid.Values)
                tile.Reveal();
            RefreshSharedOutlines();
            Debug.Log($"[HexGridManager] Prologue lightweight hex disk: {created} cells (center={centerCell}, r={axialRadius}, shared outline).");
        }

        void EnsureSharedOutlines()
        {
            if (sharedHoverRing != null && sharedHighlightRing != null) return;
            if (gridRoot == null)
            {
                var rootGO = new GameObject("HexGridRoot");
                gridRoot = rootGO.transform;
            }
            sharedHoverRing = CreateSharedRing("SharedHexHoverRing", Color.white, 0.22f);
            sharedHighlightRing = CreateSharedRing("SharedHexHighlightRing", new Color(0.1f, 0.55f, 1f), 0.32f);
        }

        LineRenderer CreateSharedRing(string name, Color color, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(gridRoot, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = 6;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCornerVertices = 2;
            lr.numCapVertices = 2;
            lr.sortingOrder = 100;
            lr.enabled = false;
            lr.startColor = color;
            lr.endColor = color;
            return lr;
        }

        internal void RefreshSharedOutlines()
        {
            ApplySharedRing(sharedHoverRing, sharedHoverCell, Color.white, 0.22f);
            ApplySharedRing(sharedHighlightRing, sharedHighlightCell, new Color(0.1f, 0.55f, 1f), 0.32f);
        }

        static void ApplySharedRing(LineRenderer lr, Vector2Int? cell, Color color, float width)
        {
            if (lr == null) return;
            if (!cell.HasValue)
            {
                lr.enabled = false;
                return;
            }
            lr.enabled = true;
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = width;
            lr.endWidth = width;
            Vector3 center = GameDevTV.RTS.Player.ColonyTileGrid.CellToWorldOnGround(cell.Value);
            float lift = 0.14f;
            float radius = GameDevTV.RTS.Player.ColonyTileGrid.TileSize;
            for (int i = 0; i < 6; i++)
            {
                float angleRad = Mathf.PI / 180f * (60f * i);
                var p = new Vector3(
                    center.x + radius * Mathf.Cos(angleRad),
                    center.y + lift,
                    center.z + radius * Mathf.Sin(angleRad));
                lr.SetPosition(i, p);
            }
        }
        
        /// <summary>
        /// Converts world position to hex coordinates using axial coordinate system.
        /// </summary>
        public Vector2Int WorldToHexCoordinates(Vector3 worldPosition)
        {
            return GameDevTV.RTS.Player.ColonyTileGrid.WorldToCell(worldPosition);
        }
        
        /// <summary>
        /// Converts hex coordinates to world position (center of hex).
        /// </summary>
        public Vector3 HexToWorldPosition(Vector2Int hexCoords)
        {
            return GameDevTV.RTS.Player.ColonyTileGrid.CellToWorldOnGround(hexCoords);
        }
        
        /// <summary>
        /// Reveals the hex tile at the specified world position.
        /// </summary>
        public void RevealHex(Vector3 worldPosition)
        {
            Vector2Int hexCoords = WorldToHexCoordinates(worldPosition);
            
            if (hexGrid.ContainsKey(hexCoords))
            {
                hexGrid[hexCoords].Reveal();
            }
        }
        
        /// <summary>
        /// Gets the hex tile at the specified world position.
        /// </summary>
        public HexTile GetHexAtWorldPosition(Vector3 worldPosition)
        {
            Vector2Int hexCoords = WorldToHexCoordinates(worldPosition);
            return GetHex(hexCoords);
        }

        /// <summary>O(1) hover pick — only checks the cell under the cursor and its six neighbors.</summary>
        public HexTile GetHexForHover(Vector3 groundPoint)
        {
            Vector2Int cell = WorldToHexCoordinates(groundPoint);
            HexTile direct = GetHex(cell);
            if (direct != null)
                return direct;

            float maxSq = (CellSize * 1.35f) * (CellSize * 1.35f);
            HexTile best = null;
            float bestSq = maxSq;
            foreach (var delta in HoverNeighborDeltas)
            {
                HexTile neighbor = GetHex(cell + delta);
                if (neighbor == null) continue;
                float sq = (neighbor.WorldPosition - groundPoint).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = neighbor;
                }
            }
            return best;
        }

        /// <summary>
        /// Returns true when the hex under this world position has been revealed to the player.
        /// </summary>
        public bool IsPositionRevealed(Vector3 worldPosition)
        {
            HexTile tile = GetHexAtWorldPosition(worldPosition);
            // No shroud tile means no fog cover at this spot — don't permanently hide sites.
            if (tile == null) return true;
            return tile.IsRevealed;
        }

        public static bool IsWorldPositionRevealed(Vector3 worldPosition)
        {
            return Instance == null || Instance.IsPositionRevealed(worldPosition);
        }

        public HexTile GetHex(Vector2Int coordinates)
        {
            return hexGrid.TryGetValue(coordinates, out HexTile tile) ? tile : null;
        }

        /// <summary>
        /// Nearest adjacent hex in a cardinal WASD/arrow direction. Includes shrouded tiles —
        /// camera pan may look at fog without revealing it.
        /// </summary>
        public HexTile GetNeighborInDirection(HexTile origin, Vector2Int direction, bool revealedOnly = false)
        {
            if (origin == null || direction == Vector2Int.zero) return null;

            Vector2Int step = direction switch
            {
                var d when d == Vector2Int.up => new Vector2Int(0, 1),
                var d when d == Vector2Int.down => new Vector2Int(0, -1),
                var d when d == Vector2Int.right => new Vector2Int(1, 0),
                var d when d == Vector2Int.left => new Vector2Int(-1, 0),
                _ => Vector2Int.zero
            };
            if (step == Vector2Int.zero) return null;

            HexTile next = GetHex(origin.HexCoordinates + step);
            if (next == null || (revealedOnly && !next.IsRevealed)) return null;
            return next;
        }

        public HexTile GetRevealedNeighborInDirection(HexTile origin, Vector2Int direction)
        {
            return GetNeighborInDirection(origin, direction, revealedOnly: true);
        }

        /// <summary>
        /// Nearest hex to a world point. Includes shrouded tiles for free camera focus.
        /// Does not reveal fog.
        /// </summary>
        public HexTile GetNearestHex(Vector3 worldPosition, bool revealedOnly = false)
        {
            HexTile fast = GetHexForHover(worldPosition);
            if (fast != null && (!revealedOnly || fast.IsRevealed))
                return fast;

            if (revealedOnly)
            {
                Vector2Int cell = WorldToHexCoordinates(worldPosition);
                foreach (var delta in HoverNeighborDeltas)
                {
                    HexTile t = GetHex(cell + delta);
                    if (t != null && t.IsRevealed)
                        return t;
                }
                return null;
            }

            return fast;
        }

        public HexTile GetNearestRevealedHex(Vector3 worldPosition)
        {
            return GetNearestHex(worldPosition, revealedOnly: true);
        }
        
        /// <summary>
        /// Generates the hex grid and Shroud overlay.
        /// </summary>
        public void GenerateHexGrid()
        {
            float size = CellSize;
            cellSize = size;
            gridOrigin = Vector3.zero;

            float mapW = 100f;
            float mapH = 100f;
            if (PlanetGenerator.Instance != null && PlanetGenerator.Instance.Config != null)
            {
                mapW = PlanetGenerator.Instance.Config.MapWidth * PlanetGenerator.Instance.CellSize;
                mapH = PlanetGenerator.Instance.Config.MapHeight * PlanetGenerator.Instance.CellSize;
            }

            // Cover the map in axial hexes that share ColonyTileGrid spacing.
            int qMin = Mathf.FloorToInt((-mapW * 0.1f) / (1.5f * size)) - 1;
            int qMax = Mathf.CeilToInt((mapW * 1.1f) / (1.5f * size)) + 1;
            int rMin = Mathf.FloorToInt((-mapH * 0.1f) / (Mathf.Sqrt(3f) * size)) - 1;
            int rMax = Mathf.CeilToInt((mapH * 1.1f) / (Mathf.Sqrt(3f) * size)) + 1;
            gridDimensions = new Vector2Int(qMax - qMin + 1, rMax - rMin + 1);

            if (gridRoot == null)
            {
                GameObject rootGO = new GameObject("HexGridRoot");
                gridRoot = rootGO.transform;
            }
            
            ClearHexGrid();
            
            for (int q = qMin; q <= qMax; q++)
            {
                for (int r = rMin; r <= rMax; r++)
                {
                    Vector2Int hexCoords = new Vector2Int(q, r);
                    Vector3 worldPos = HexToWorldPosition(hexCoords);
                    if (worldPos.x < -size * 2f || worldPos.z < -size * 2f
                        || worldPos.x > mapW + size * 2f || worldPos.z > mapH + size * 2f)
                        continue;

                    GameObject hexGO = CreateHexTile(worldPos, hexCoords);
                    HexTile hexTile = new HexTile(hexCoords, worldPos, hexGO);
                    hexGrid[hexCoords] = hexTile;
                    worldToHexMap[worldPos] = hexCoords;
                }
            }
            
            Debug.Log($"[HexGridManager] Generated {hexGrid.Count} building-sized hexes (radius={size:F2}, width={HexWidth:F2}).");
        }
        
        /// <summary>
        /// Creates a single hex tile GameObject.
        /// </summary>
        private GameObject CreateHexTile(Vector3 position, Vector2Int hexCoords)
        {
            GameObject hexGO;
            
            if (shroudTilePrefab != null)
            {
                // Use the provided shroud tile prefab
                hexGO = Instantiate(shroudTilePrefab, position, Quaternion.identity, gridRoot);
                hexGO.layer = LayerMask.NameToLayer("TransparentFX");
                float meshScale = CellSize * 1.9f * 0.95f; // diameter ≈ hex width with small gap
                hexGO.transform.localScale = new Vector3(meshScale, 0.1f, meshScale);
            }
            else
            {
                // Create a simple hex cylinder as fallback
                hexGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                hexGO.transform.position = position;
                float meshScale = CellSize * 1.9f * 0.95f;
                hexGO.transform.localScale = new Vector3(meshScale, 0.1f, meshScale);
                hexGO.layer = LayerMask.NameToLayer("TransparentFX"); // TransparentFX is ignored by PlanetGenerator NavMesh bake!
                
                // Make it semi-transparent
                Renderer renderer = hexGO.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = new Color(0.2f, 0.2f, 0.2f, 0.7f);
                }
            }
            
            Collider[] colliders = hexGO.GetComponentsInChildren<Collider>();
            foreach (Collider c in colliders) {
                UnityEngine.Object.Destroy(c);
            }
            
            var modifier = hexGO.AddComponent<NavMeshModifier>();
            if (modifier != null) {
                modifier.ignoreFromBuild = true;
            }

            // Hide shroud cylinder / mesh — hexes should not paint the map; outline is hover-only.
            foreach (var rend in hexGO.GetComponentsInChildren<Renderer>(true))
            {
                if (rend is LineRenderer) continue;
                rend.enabled = false;
            }

            // Hover / selection outline only (starts disabled).
            LineRenderer lr = hexGO.AddComponent<LineRenderer>();
            lr.useWorldSpace = true; // Use world space so the prefab's scale doesn't warp the lines!
            lr.loop = true;
            lr.positionCount = 6;
            lr.startWidth = 0.15f;
            lr.endWidth = 0.15f;
            lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCornerVertices = 2;
            lr.numCapVertices = 2;
            lr.sortingOrder = 100;
            lr.enabled = false;
            lr.startColor = Color.cyan;
            lr.endColor = Color.cyan;
            
            // Draw a flat-topped hexagon (corners at 0, 60, 120, 180, 240, 300)
            Vector3[] points = new Vector3[6];
            float radius = CellSize;
            for (int i = 0; i < 6; i++)
            {
                float angle_deg = 60f * i; // Flat-topped
                float angle_rad = Mathf.PI / 180f * angle_deg;
                points[i] = position + new Vector3(radius * Mathf.Cos(angle_rad), 0.75f, radius * Mathf.Sin(angle_rad));
            }
            lr.SetPositions(points);

            hexGO.name = $"Hex_{hexCoords.x}_{hexCoords.y}";
            return hexGO;
        }
        
        
        /// <summary>
        /// Reveals all hexes within a certain world-space radius around a position.
        /// </summary>
        public void RevealHexesAroundPosition(Vector3 position, float radius)
        {
            // Zoo Code's coordinate conversion is mathematically broken for finding neighbors,
            // so we will use a hyper-fast distance check over all hexes.
            float sqrRadius = radius * radius;
            foreach (HexTile hexTile in hexGrid.Values)
            {
                if (!hexTile.IsRevealed)
                {
                    // Check if hex is within the sphere radius
                    float sqrDistance = (hexTile.WorldPosition - position).sqrMagnitude;
                    if (sqrDistance <= sqrRadius)
                    {
                        hexTile.Reveal();
                    }
                }
            }
        }
        
        private float explorationTimer = 0f;
        
        private void Update()
        {
            if (GameDevTV.RTS.Prologue.PrologueController.IsActive)
                return;

            // Only run exploration logic every 0.25 seconds to save performance
            explorationTimer += Time.deltaTime;
            if (explorationTimer >= 0.25f)
            {
                explorationTimer = 0f;
                
                // Find all units and buildings. In a fully optimized version, we'd use a central registry.
                // For now, the lookup is throttled to four times a second.
                GameDevTV.RTS.Units.AbstractCommandable[] commandables = FindObjectsByType<GameDevTV.RTS.Units.AbstractCommandable>();
                
                foreach (var cmd in commandables)
                {
                    // Only reveal for Player 1 (the local player)
                    if (cmd.Owner == GameDevTV.RTS.Units.Owner.Player1)
                    {
                        float sightRadius = 10f; // Default sight radius
                        
                        if (cmd.UnitSO != null && cmd.UnitSO.SightConfig != null)
                        {
                            sightRadius = cmd.UnitSO.SightConfig.SightRadius;
                        }
                        
                        RevealHexesAroundPosition(cmd.transform.position, sightRadius);
                    }
                }
            }
        }

        /// <summary>
        /// Clears all hex tiles from the grid.


        /// </summary>
        private void ClearHexGrid()
        {
            foreach (HexTile hexTile in hexGrid.Values)
            {
                if (hexTile.GameObject != null)
                {
                    Destroy(hexTile.GameObject);
                }
            }
            
            hexGrid.Clear();
            worldToHexMap.Clear();
        }
        
        /// <summary>
        /// Gets all hex tiles within a specified radius of a center position.
        /// </summary>
        public List<HexTile> GetHexesInRadius(Vector3 center, int radius)
        {
            List<HexTile> nearbyHexes = new List<HexTile>();
            Vector2Int centerHex = WorldToHexCoordinates(center);
            
            for (int q = -radius; q <= radius; q++)
            {
                for (int r = -radius; r <= radius; r++)
                {
                    Vector2Int hexCoords = new Vector2Int(centerHex.x + q, centerHex.y + r);
                    
                    if (hexGrid.ContainsKey(hexCoords))
                    {
                        nearbyHexes.Add(hexGrid[hexCoords]);
                    }
                }
            }
            
            return nearbyHexes;
        }
        
        /// <summary>
        /// Reveals all hex tiles within a specified radius.
        /// </summary>
        public void RevealHexesInRadius(Vector3 center, int radius)
        {
            List<HexTile> hexes = GetHexesInRadius(center, radius);
            
            foreach (HexTile hexTile in hexes)
            {
                hexTile.Reveal();
            }
            
            Debug.Log($"[HexGridManager] Revealed {hexes.Count} hex tiles in radius {radius} of {center}");
        }
        
        private void Start()
        {
            if (generateShroudOnStart)
            {
                if (PlanetGenerator.Instance != null && PlanetGenerator.Instance.Config != null)
                {
                    GenerateHexGrid();
                    RevealStartingArea();
                }
            }
        }

        private void RevealStartingArea()
        {
            if (PlanetGenerator.Instance == null || PlanetGenerator.Instance.Config == null)
                return;

            // Prologue builds a small local disk after planet sync — skip planet-wide hex spawn.
            if (UnityEngine.Object.FindAnyObjectByType<GameDevTV.RTS.Prologue.PrologueController>(
                    UnityEngine.FindObjectsInactive.Include) != null)
            {
                Debug.Log("[HexGridManager] Planet generated with Prologue in scene — deferring hex disk to Prologue sync.");
                OnStartingAreaRevealed?.Invoke();
                return;
            }

            GenerateHexGrid();

            // Combolands-style: no strategic shroud — reveal the whole planet.
            RevealAllHexes();
            OnStartingAreaRevealed?.Invoke();
        }

        /// <summary>Reveal every hex tile (disables shroud meshes).</summary>
        public void RevealAllHexes()
        {
            foreach (var tile in hexGrid.Values)
                tile?.Reveal();
        }
        

    }
}