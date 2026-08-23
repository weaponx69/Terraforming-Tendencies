using UnityEngine;
using System.Collections.Generic;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Manages hex grid overlay for the map, including Shroud generation and hex coordinate conversion.
    /// Maps 3D world space to pointy-topped hex coordinates and provides methods for grid operations.
    /// </summary>
    public class HexGridManager : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private float cellSize = 2.0f;
        [SerializeField] private Vector2Int gridDimensions = new Vector2Int(50, 50);
        [SerializeField] private Transform gridRoot;
        
        [Header("Shroud Settings")]
        [SerializeField] private GameObject shroudTilePrefab;
        [SerializeField] private LayerMask shroudLayer = LayerMask.NameToLayer("Shroud");
        [SerializeField] private bool generateShroudOnStart = true;
        
        // Hex grid data
        private Dictionary<Vector2Int, HexTile> hexGrid = new Dictionary<Vector2Int, HexTile>();
        private Dictionary<Vector3, Vector2Int> worldToHexMap = new Dictionary<Vector3, Vector2Int>();
        
        // Configuration for pointy-topped hexagons
        private const float HEX_HEIGHT = 1.732f; // sqrt(3) * cellSize
        private const float HEX_WIDTH = 2.0f;   // 2 * cellSize
        
        /// <summary>
        /// Represents a single hex tile in the grid.
        /// </summary>
        public class HexTile
        {
            public Vector2Int HexCoordinates { get; private set; }
            public Vector3 WorldPosition { get; private set; }
            public GameObject GameObject { get; private set; }
            public bool IsRevealed { get; private set; }
            
            public HexTile(Vector2Int hexCoords, Vector3 worldPos, GameObject hexGO)
            {
                HexCoordinates = hexCoords;
                WorldPosition = worldPos;
                GameObject = hexGO;
                IsRevealed = false;
            }
            
            public void Reveal()
            {
                if (!IsRevealed)
                {
                    IsRevealed = true;
                    // Disable the shroud material or destroy the tile
                    Renderer renderer = GameObject.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        Material[] materials = renderer.materials;
                        for (int i = 0; i < materials.Length; i++)
                        {
                            materials[i].color = Color.clear; // Make transparent
                        }
                    }
                }
            }
            
            public void Destroy()
            {
                if (GameObject != null)
                {
                    Destroy(GameObject);
                }
            }
        }
        
        /// <summary>
        /// Converts world position to hex coordinates using axial coordinate system.
        /// </summary>
        public Vector2Int WorldToHexCoordinates(Vector3 worldPosition)
        {
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
            float z = hexCoords.y * (HEX_HEIGHT * 0.5f);
            return new Vector3(x, 0, z);
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
        
        /// <summary>
        /// Generates the hex grid and Shroud overlay.
        /// </summary>
        public void GenerateHexGrid()
        {
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
            }
            else
            {
                // Create a simple hex cylinder as fallback
                hexGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                hexGO.transform.position = position;
                hexGO.transform.localScale = new Vector3(cellSize, 0.1f, cellSize);
                hexGO.layer = LayerMask.NameToLayer("Shroud");
                
                // Make it semi-transparent
                Renderer renderer = hexGO.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = new Color(0.2f, 0.2f, 0.2f, 0.7f);
                }
            }
            
            hexGO.name = $"Hex_{hexCoords.x}_{hexCoords.y}";
            return hexGO;
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
                GenerateHexGrid();
            }
        }
        
        private void Update()
        {
            // Optional: Update logic for hex grid if needed
        }
    }
}