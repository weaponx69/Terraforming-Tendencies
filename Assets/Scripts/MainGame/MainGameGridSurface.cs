using GameDevTV.RTS.Player;
using UnityEngine;

namespace GameDevTV.RTS.MainGame
{
    /// <summary>
    /// Flat prologue board: world XZ → axial hex cell without physics queries.
    /// </summary>
    public static class MainGameGridSurface
    {
        public const float DefaultGroundY = 0f;

        public static Vector2Int WorldXZToCell(float worldX, float worldZ) =>
            ColonyTileGrid.WorldToCell(new Vector3(worldX, 0f, worldZ));

        public static Vector2Int WorldToCell(Vector3 world) => ColonyTileGrid.WorldToCell(world);

        public static bool TryScreenPointToGroundCell(Camera cam, Vector2 screen, float groundY, out Vector2Int cell)
        {
            cell = default;
            if (cam == null) return false;

            var plane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
            Ray ray = cam.ScreenPointToRay(screen);
            if (!plane.Raycast(ray, out float enter)) return false;

            Vector3 hit = ray.GetPoint(enter);
            cell = ColonyTileGrid.WorldToCell(hit);
            return true;
        }

        public static bool TryScreenPointToGround(Camera cam, Vector2 screen, float groundY, out Vector3 ground)
        {
            ground = default;
            if (cam == null) return false;

            var plane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
            Ray ray = cam.ScreenPointToRay(screen);
            if (!plane.Raycast(ray, out float enter)) return false;

            ground = ray.GetPoint(enter);
            ground.y = groundY;
            return true;
        }
    }
}