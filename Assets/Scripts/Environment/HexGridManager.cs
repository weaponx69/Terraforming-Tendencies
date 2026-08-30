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
        [SerializeField] private float cellSize = 2.0f;
        [SerializeField] private Vector2Int gridDimensions = new Vector2Int(50, 50);
        [SerializeField] private Transform gridRoot;
        
        [Header("Shroud Settings")]
        [SerializeField] private GameObject shroudTilePrefab;
        [SerializeField] private LayerMask shroudLayer;
        [SerializeField] private bool generateShroudOnStart = true;
        [SerializeField] private float startingAreaRevealRadius = 15f;

        public static bool HighlightTrace { get; private set; }

        public static void SetHighlightTrace(bool enabled)
        {
            HighlightTrace = enabled;
        }
        
        // Hex grid data
        private Dictionary<Vector2Int, HexTile> hexGrid = new Dictionary<Vector2Int, HexTile>();
        private Dictionary<Vector3, Vector2Int> worldToHexMap = new Dictionary<Vector3, Vector2Int>();
        private Vector3 gridOrigin;
        
        // Configuration for pointy-topped hexagons
        private const float HEX_HEIGHT = 1.732f; // sqrt(3) * cellSize
        private const float HEX_WIDTH = 2.0f;   // 2 * cellSize

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
                UpdateOutline();
            }

            public void SetHovered(bool hovered)
            {
                isHovered = hovered;
                if (HighlightTrace)
                {
                    Debug.Log($"[HexHighlight] Hovered {HexCoordinates} -> {hovered}; revealed={IsRevealed}, object={GameObject?.name ?? "NULL"}, outline={outline != null}");
                }
                UpdateOutline();
            }

            private void UpdateOutline()
            {
                if (outline == null)
                {
                    if (HighlightTrace) Debug.LogWarning($"[HexHighlight] Cannot update {HexCoordinates}: LineRenderer is missing.");
                    return;
                }

                outline.enabled = IsRevealed;
                if (!IsRevealed) return;

                Color color = isHighlighted ? new Color(0.1f, 0.55f, 1f) : isHovered ? Color.white : Color.cyan;
                float width = isHighlighted ? 0.32f : isHovered ? 0.22f : 0.1f;
                outline.startColor = color;
                outline.endColor = color;
                outline.startWidth = width;
                outline.endWidth = width;
                if (outlineMaterial != null)
                {
                    if (outlineMaterial.HasProperty("_BaseColor")) outlineMaterial.SetColor("_BaseColor", color);
                    if (outlineMaterial.HasProperty("_Color")) outlineMaterial.SetColor("_Color", color);
                }
            }
        }
        
        /// <summary>
        /// Converts world position to hex coordinates using axial coordinate system.
        /// </summary>
        public Vector2Int WorldToHexCoordinates(Vector3 worldPosition)
        {
            worldPosition -= gridOrigin;
            // Convert world position to hex coordinates using pointy-topped formula
            float q = (worldPosition.x / (HEX_WIDTH * 0.75f)) - (worldPosition.z / (HEX_HEIGHT * 0.5f));
            float r = (worldPosition.z / (HEX_HEIGHT * 0.5f));
            
            // Round to nearest hex
            int hexQ = Mathf.RoundToInt(q);
            int hexR = Mathf.RoundToInt(r);
            int hexS = -hexQ - hexR; // For axial coordinates, s = -q - r
            
            return new Vector2Int(hexQ, hexR);
        }
        
        /// <summary>
        /// Converts hex coordinates to world position (center of hex).
        /// </summary>
        public Vector3 HexToWorldPosition(Vector2Int hexCoords)
        {
            float x = hexCoords.x * (HEX_WIDTH * 0.75f);
            float z = hexCoords.y * HEX_HEIGHT;
            // Stagger odd columns (pointy-topped) OR flat-topped? 
            // HEX_WIDTH=2.0, HEX_HEIGHT=1.732 -> Flat-topped geometry
            // Flat-topped means columns stagger by half height
            if (hexCoords.x % 2 != 0)
            {
                z += HEX_HEIGHT * 0.5f;
            }
            return gridOrigin + new Vector3(x, 0f, z);
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
            
            if (hexGrid.ContainsKey(hexCoords))
            {
                return hexGrid[hexCoords];
            }
            
            return null;
        }

        public HexTile GetHex(Vector2Int coordinates)
        {
            return hexGrid.TryGetValue(coordinates, out HexTile tile) ? tile : null;
        }

        public HexTile GetRevealedNeighborInDirection(HexTile origin, Vector2Int direction)
        {
            if (origin == null || direction == Vector2Int.zero) return null;

            Vector3 desiredDirection = new Vector3(direction.x, 0f, direction.y).normalized;
            HexTile nearest = null;
            float nearestDistance = float.MaxValue;
            float maximumDistance = Mathf.Max(HEX_HEIGHT, HEX_WIDTH) * 1.25f;

            foreach (HexTile candidate in hexGrid.Values)
            {
                if (candidate == origin || !candidate.IsRevealed) continue;

                Vector3 offset = candidate.WorldPosition - origin.WorldPosition;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance > maximumDistance || distance < 0.01f) continue;

                float directionAlignment = Vector3.Dot(offset / distance, desiredDirection);
                if (directionAlignment < 0.45f) continue;

                if (distance < nearestDistance)
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        public HexTile GetNearestRevealedHex(Vector3 worldPosition)
        {
            HexTile nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (HexTile tile in hexGrid.Values)
            {
                if (!tile.IsRevealed) continue;

                float distance = (tile.WorldPosition - worldPosition).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearest = tile;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }
        
        /// <summary>
        /// Generates the hex grid and Shroud overlay.
        /// </summary>
        public void GenerateHexGrid()
        {
            if (PlanetGenerator.Instance != null && PlanetGenerator.Instance.Config != null)
            {
                gridDimensions = new Vector2Int(
                    Mathf.CeilToInt(PlanetGenerator.Instance.Config.MapWidth / cellSize * 1.2f), 
                    Mathf.CeilToInt(PlanetGenerator.Instance.Config.MapHeight / cellSize * 1.2f)
                );

                float gridWidth = (gridDimensions.x - 1) * (HEX_WIDTH * 0.75f);
                float gridHeight = (gridDimensions.y - 1) * HEX_HEIGHT;
                gridOrigin = new Vector3(
                    PlanetGenerator.Instance.Config.MapWidth * PlanetGenerator.Instance.CellSize / 2f - gridWidth / 2f,
                    0f,
                    PlanetGenerator.Instance.Config.MapHeight * PlanetGenerator.Instance.CellSize / 2f - gridHeight / 2f);
            }
            if (gridRoot == null)
            {
                GameObject rootGO = new GameObject("HexGridRoot");
                gridRoot = rootGO.transform;
            }
            
            // Clear existing grid
            ClearHexGrid();
            
            // Generate hex tiles for the entire grid
            for (int q = 0; q < gridDimensions.x; q++)
            {
                for (int r = 0; r < gridDimensions.y; r++)
                {
                    Vector2Int hexCoords = new Vector2Int(q, r);
                    Vector3 worldPos = HexToWorldPosition(hexCoords);
                    
                    // Create hex tile GameObject
                    GameObject hexGO = CreateHexTile(worldPos, hexCoords);
                    
                    // Create and store hex tile data
                    HexTile hexTile = new HexTile(hexCoords, worldPos, hexGO);
                    hexGrid.Add(hexCoords, hexTile);
                    
                    // Store mapping for quick lookup
                    worldToHexMap[worldPos] = hexCoords;
                }
            }
            
            Vector3 gridMin = HexToWorldPosition(Vector2Int.zero);
            Vector3 gridMax = HexToWorldPosition(new Vector2Int(gridDimensions.x - 1, gridDimensions.y - 1));
            Vector3 gridCenter = (gridMin + gridMax) * 0.5f;
            Debug.Log($"[HexGridManager] World bounds min={gridMin}, max={gridMax}, center={gridCenter}, origin={gridOrigin}");
            Debug.Log($"[HexGridManager] Generated hex grid with {hexGrid.Count} tiles");
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
                hexGO.transform.localScale = new Vector3(cellSize * 0.95f, 0.1f, cellSize * 0.95f); // 5% gap to show honeycomb pattern
            }
            else
            {
                // Create a simple hex cylinder as fallback
                hexGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                hexGO.transform.position = position;
                hexGO.transform.localScale = new Vector3(cellSize * 0.95f, 0.1f, cellSize * 0.95f); // 5% gap to show honeycomb pattern
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

            // ADD PERMANENT OUTLINE
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
            lr.startColor = Color.cyan; // Solid cyan!
            lr.endColor = Color.cyan;
            
            // Draw a flat-topped hexagon (corners at 0, 60, 120, 180, 240, 300)
            Vector3[] points = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float angle_deg = 60f * i; // Flat-topped
                float angle_rad = Mathf.PI / 180f * angle_deg;
                // Radius is cellSize = HEX_WIDTH / 2.0f
                float radius = HEX_WIDTH * 0.5f;
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
            if (!generateShroudOnStart || PlanetGenerator.Instance == null || PlanetGenerator.Instance.Config == null)
            {
                return;
            }

            GenerateHexGrid();

            Vector3 center = new Vector3(
                PlanetGenerator.Instance.Config.MapWidth * PlanetGenerator.Instance.CellSize / 2f,
                0f,
                PlanetGenerator.Instance.Config.MapHeight * PlanetGenerator.Instance.CellSize / 2f);
            if (SectorManager.Instance != null && SectorManager.Instance.Sectors.Count > 0)
            {
                center = SectorManager.Instance.Sectors[0].Center;
            }

            RevealHexesAroundPosition(center, startingAreaRevealRadius);
            OnStartingAreaRevealed?.Invoke();
        }
        

    }
}