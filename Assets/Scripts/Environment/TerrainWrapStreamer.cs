using System.Collections.Generic;
using GameDevTV.RTS.Player;
using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Draws only the planet wrap copies the camera can see, plus the next copy
    /// along a grab flick, so the ground is already there when the view arrives.
    /// </summary>
    public class TerrainWrapStreamer : MonoBehaviour
    {
        struct Tile
        {
            public int X;
            public int Z;
            public float MinX;
            public float MaxX;
            public float MinZ;
            public float MaxZ;
            public MeshRenderer Renderer;
            public MeshCollider Collider;
        }

        readonly List<Tile> tiles = new();
        Vector3 lastFocus;
        bool hasLast;
        float mapWidth = 1f;
        float mapHeight = 1f;

        public int VisibleTileCount { get; private set; }

        public void Bind(PlanetGenerator planet)
        {
            tiles.Clear();
            if (planet != null && planet.TryGetPatchWorldSize(out float w, out float h))
            {
                mapWidth = Mathf.Max(1f, w);
                mapHeight = Mathf.Max(1f, h);
            }

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null || !child.name.StartsWith("Terrain Ghost")) continue;
                if (!TryParseTile(child.name, out int x, out int z)) continue;
                tiles.Add(new Tile
                {
                    X = x,
                    Z = z,
                    MinX = x * mapWidth,
                    MaxX = (x + 1) * mapWidth,
                    MinZ = z * mapHeight,
                    MaxZ = (z + 1) * mapHeight,
                    Renderer = child.GetComponent<MeshRenderer>(),
                    Collider = child.GetComponent<MeshCollider>()
                });
            }

            hasLast = false;
            if (Application.isPlaying)
                Refresh(PlayerInput.GetCameraFocusPosition(), Vector3.zero);
            else
                SetAll(false);
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || tiles.Count == 0) return;

            Vector3 focus = PlayerInput.GetCameraFocusPosition();
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            Vector3 velocity = Vector3.zero;
            if (hasLast)
            {
                velocity = (focus - lastFocus) / dt;
                const float maxSpeed = 90f;
                if (velocity.sqrMagnitude > maxSpeed * maxSpeed)
                    velocity = velocity.normalized * maxSpeed;
            }
            lastFocus = focus;
            hasLast = true;
            Refresh(focus, velocity);
        }

        public void Refresh(Vector3 worldFocus, Vector3 worldVelocity)
        {
            Vector3 local = transform.InverseTransformPoint(worldFocus);
            Vector3 localVelocity = transform.InverseTransformVector(worldVelocity);
            float speed = worldVelocity.magnitude;
            float lead = Mathf.Clamp(0.85f + speed * 0.035f, 0.85f, 2.4f);
            Vector3 ahead = local + localVelocity * lead;
            float reach = ViewReach(worldFocus) + Mathf.Min(speed * 0.45f, 48f);

            int shown = 0;
            for (int i = 0; i < tiles.Count; i++)
            {
                Tile tile = tiles[i];
                float near = DistToRect(local.x, local.z, tile.MinX, tile.MaxX, tile.MinZ, tile.MaxZ);
                float soon = DistToRect(ahead.x, ahead.z, tile.MinX, tile.MaxX, tile.MinZ, tile.MaxZ);
                bool on = near <= reach || soon <= reach;
                if (on) shown++;
                if (tile.Renderer != null && tile.Renderer.enabled != on)
                    tile.Renderer.enabled = on;
                if (tile.Collider != null && tile.Collider.enabled != on)
                    tile.Collider.enabled = on;
            }
            VisibleTileCount = shown;
        }

        float ViewReach(Vector3 worldFocus)
        {
            Camera cam = PlayerInput.Instance != null
                ? PlayerInput.Instance.GameplayCamera
                : Camera.main;
            if (cam == null) return 48f;
            return Mathf.Max(36f, Vector3.Distance(cam.transform.position, worldFocus) * 1.15f);
        }

        void SetAll(bool on)
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                Tile tile = tiles[i];
                if (tile.Renderer != null) tile.Renderer.enabled = on;
                if (tile.Collider != null) tile.Collider.enabled = on;
            }
            VisibleTileCount = on ? tiles.Count : 0;
        }

        static float DistToRect(float px, float pz, float minX, float maxX, float minZ, float maxZ)
        {
            float dx = px < minX ? minX - px : px > maxX ? px - maxX : 0f;
            float dz = pz < minZ ? minZ - pz : pz > maxZ ? pz - maxZ : 0f;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static bool TryParseTile(string name, out int x, out int z)
        {
            x = 0;
            z = 0;
            int open = name.IndexOf('(');
            int comma = name.IndexOf(',', open + 1);
            int close = name.IndexOf(')', comma + 1);
            if (open < 0 || comma < 0 || close < 0) return false;
            return int.TryParse(name.Substring(open + 1, comma - open - 1), out x)
                && int.TryParse(name.Substring(comma + 1, close - comma - 1), out z);
        }
    }
}
