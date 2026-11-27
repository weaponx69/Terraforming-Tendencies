using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    [CreateAssetMenu(menuName = "Terraforming/Planet Config", fileName = "New Planet Config", order = 0)]
    public class PlanetConfig : ScriptableObject
    {
        [Header("Terrain Generation")]
        public int MapWidth = 100;
        public int MapHeight = 100;
        [Tooltip("Horizontal scale for Perlin hills (larger = broader features).")]
        public float NoiseScale = 10f;
        [Tooltip("World-unit peak amplitude for hills/valleys.")]
        public float HeightMultiplier = 5f;
        [Tooltip("0 = hash from asset name. Same seed → same mountains for this planet type.")]
        public int TerrainSeed;
        [Tooltip("World radius around landing kept nearly flat for CP / early Survey.")]
        public float LandingFlatRadius = 28f;

        [Header("Surface maps (Easy: curated DEM + photo)")]
        [Tooltip("Grayscale DEM. Bright = high. Null → Perlin fallback.")]
        public Texture2D Heightmap;
        [Tooltip("World-unit vertical span from black (0) to white (1) on Heightmap.")]
        public float HeightAmplitude = 12f;
        [Tooltip("Terrain photo. This is the hills-and-valleys color.")]
        public Texture2D AlbedoMap;
        [Tooltip("Normal from that terrain photo so ridges catch the light.")]
        public Texture2D NormalMap;
        [Tooltip("Crevice darkening so the photo doesn't read as a flat sticker.")]
        public Texture2D OcclusionMap;
        [Tooltip("Seamless dust albedo tiled over the terrain photo.")]
        public Texture2D DetailAlbedoMap;
        [Tooltip("Quiet dust normal. Kept weak so it does not replace the terrain.")]
        public Texture2D DetailNormalMap;
        [Tooltip("How many times the dust maps tile across the planet mesh UV.")]
        public float DetailTiling = 32f;

        [Header("Difficulty Modifiers")]
        public int ResourceCount = 10;
        public float BaseDecayRate = 2f;
        public float ToxicityLevel = 0f;
        public float BiomassDrainRate = 1f; // Biomass lost per second
        public float IntegrityDrainRate = 0.5f; // Integrity lost per second

        [Header("Globe presentation")]
        [Tooltip("World-unit diameter of the repeating flat terrain patch (camera wrap / middle-mouse spin).")]
        public float PlanetPatchDiameter = 768f;
        [Tooltip("Horizon bend radius — only used when EnableHorizonCurvature is on.")]
        public float HorizonBodyRadiusMeters = 1737400f;
        [Tooltip("Shader vertex bend (off = flat geometry; use PlanetPatchDiameter for more space).")]
        public bool EnableHorizonCurvature;

        [Header("Baked surface (fast load)")]
        [Tooltip("When on, Play uses mesh already in the scene if SceneBakedGeometryRevision matches.")]
        public bool UseAuthoritativeBakedMesh;
        [Tooltip("Bump when you rebake the surface in the Editor — must match PlanetGenerator.SceneBakedGeometryRevision.")]
        public int BakedGeometryRevision;

        [Header("Sectors")]
        public int SectorsX = 3;
        public int SectorsY = 3;
        public float SectorOccupationRadius = 10f;

        [Header("Procedural Scatter")]
        public int EnvironmentDensity = 50;
        public GameObject[] EnvironmentPrefabs;
        
        [Header("Surface Features (Barren Planet)")]
        public int SurfaceFeatureDensity = 100;
        public GameObject[] SurfaceFeaturePrefabs;
        
        [Header("Surface Rocks (Terraforming Overlay)")]
        public GameObject[] SurfaceRockPrefabs;

        [Header("Resources")]
        public GameObject[] ResourcePrefabs;

        [Header("Gatherable deposit deny-list")]
        [Tooltip("When true, PlanetGenerator skips that deposit type. Missing fields deserialize false → all types allowed (Easy default). Planet select UI is future work.")]
        public bool DenyMinerals;
        public bool DenyGas;
        public bool DenyIron;
        public bool DenyRegolith;

        public bool AllowMinerals => !DenyMinerals;
        public bool AllowGas => !DenyGas;
        public bool AllowIron => !DenyIron;
        public bool AllowRegolith => !DenyRegolith;

        public bool IsGatherableTypeAllowed(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return false;
            if (typeName.Equals("Minerals", System.StringComparison.OrdinalIgnoreCase)) return AllowMinerals;
            if (typeName.Equals("Gas", System.StringComparison.OrdinalIgnoreCase)) return AllowGas;
            if (typeName.Equals("Iron", System.StringComparison.OrdinalIgnoreCase)) return AllowIron;
            if (typeName.Equals("Regolith", System.StringComparison.OrdinalIgnoreCase)) return AllowRegolith;
            return true;
        }

        public bool IsGatherableTypeAllowed(SectorNode.NodeType nodeType) => nodeType switch
        {
            SectorNode.NodeType.Minerals => AllowMinerals,
            SectorNode.NodeType.Gas => AllowGas,
            SectorNode.NodeType.Iron => AllowIron,
            SectorNode.NodeType.Regolith => AllowRegolith,
            _ => true
        };
    }
}
