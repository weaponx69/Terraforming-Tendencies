using System.Text;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.MainGame
{
    /// <summary>
    /// One-shot scene stats for CLI / debugging FPS (call while in Play).
    /// </summary>
    public static class MainGameSceneStats
    {
        public static string Report()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"playing={Application.isPlaying} prologue={MainGameController.IsActive}");
            sb.AppendLine($"fps_smoothed={MainGamePerformanceBudget.SmoothedFps()}");

            var all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude);
            int dirt = 0, scaffold = 0, revealGhost = 0;
            foreach (var go in all)
            {
                if (go == null) continue;
                string n = go.name;
                if (n.StartsWith("DirtRoad")) dirt++;
                if (n == ConstructionScaffold.ObjectName) scaffold++;
                if (n.StartsWith("RevealGhost_")) revealGhost++;
            }

            int rendOn = 0, castShadows = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (r == null || !r.enabled) continue;
                rendOn++;
                if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) castShadows++;
            }

            int wrapVisible = 0;
            if (PlanetGenerator.Instance != null
                && PlanetGenerator.Instance.TryGetComponent(out TerrainWrapStreamer streamer))
                wrapVisible = streamer.VisibleTileCount;

            int iceMarkers = 0;
            if (MainGameIceMarkers.Instance != null)
            {
                foreach (Transform ch in MainGameIceMarkers.Instance.transform)
                    if (ch != null) iceMarkers++;
            }

            sb.AppendLine($"gos_active={all.Length} renderers_enabled={rendOn} shadow_casters={castShadows}");
            sb.AppendLine($"buildings_activeList={BaseBuilding.ActiveBuildings?.Count ?? 0}");
            sb.AppendLine($"dirtRoad_cubes={dirt} scaffolds={scaffold} reveal_ghosts={revealGhost}");
            sb.AppendLine($"terrain_wrap_tiles_visible={wrapVisible} baked_path={MainGameSearchedTiles.UseBakedPathOverlay}");
            sb.AppendLine($"ice_marker_objects={iceMarkers}");

            var colony = MainGameController.Instance?.Colony;
            if (colony != null)
            {
                sb.AppendLine($"board_tiles={colony.Board.Tiles.Count} step={colony.MainGameStep} week={colony.Week}");
            }

            return sb.ToString().TrimEnd();
        }
    }
}