using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using TMPro;
using UnityEngine;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>
    /// Unreal-style fog for Prologue: materials / geology stay "?" until the survey drone
    /// paints over them. No colored pucks / gas meshes until uncovered.
    /// </summary>
    public static class PrologueResourceFog
    {
        const float RevealRadius = 14f;

        /// <summary>Hide every deposit / feature mesh; show only "?" until survey reveal.</summary>
        public static void ApplyHiddenState()
        {
            if (!PrologueController.IsActive) return;

            DiscoverySystem.Reset();
            // Reset clears type knowledge; restore from pads / colony so reapply during boot
            // does not refuse mine cards after Survey already uncovered a deposit.
            RestoreKnownTypesAfterFogReset();

            foreach (var hr in Object.FindObjectsByType<HiddenResource>(FindObjectsInactive.Include))
            {
                if (hr == null || hr.IsDiscovered) continue;
                hr.ConcealForPrologue();
            }

            var sm = SectorManager.Instance;
            if (sm?.Sectors == null) return;

            foreach (var sector in sm.Sectors)
            {
                if (sector?.Nodes == null) continue;
                foreach (var node in sector.Nodes)
                {
                    if (node == null) continue;
                    // Keep tiles the Survey Team already uncovered.
                    if (node.isExplored) continue;
                    node.isDiscovered = true; // mystery spot only
                    ApplyUnknownVisual(node);
                }
            }

            // Also hide any orphan gatherable pads not linked through SectorNode.visualGO.
            foreach (var gs in Object.FindObjectsByType<GatherableSupply>(FindObjectsInactive.Include))
            {
                if (gs == null) continue;
                if (gs.TryGetComponent(out HiddenResource hr) && hr.IsDiscovered) continue;
                HideRenderers(gs.gameObject);
                gs.SetVisible(false);
                gs.ToggleColliders(false);
            }

            Debug.Log("[PrologueResourceFog] Unsearched deposits/features hidden as '?' until survey.");
        }

        public static void RevealNearTile(PrologueController ctrl, int tileIndex)
        {
            if (ctrl == null || !ctrl.TryGetCell(tileIndex, out Vector2Int cell)) return;
            Vector3 world = ColonyTileGrid.CellToWorld(cell, 0f);
            RevealNearWorld(world);
        }

        public static void RevealNearWorld(Vector3 world)
        {
            if (!PrologueController.IsActive) return;

            float r2 = RevealRadius * RevealRadius;

            foreach (var hr in Object.FindObjectsByType<HiddenResource>(FindObjectsInactive.Include))
            {
                if (hr == null || hr.IsDiscovered) continue;
                Vector3 p = hr.transform.position;
                float dx = p.x - world.x;
                float dz = p.z - world.z;
                if (dx * dx + dz * dz <= r2)
                {
                    hr.ForceDiscover();
                    NotifyPrologueDepositUncovered(hr);
                }
            }

            var sm = SectorManager.Instance;
            if (sm?.Sectors == null) return;

            foreach (var sector in sm.Sectors)
            {
                if (sector?.Nodes == null) continue;
                foreach (var node in sector.Nodes)
                {
                    if (node == null || node.isExplored) continue;
                    float dx = node.position.x - world.x;
                    float dz = node.position.z - world.z;
                    if (dx * dx + dz * dz > r2) continue;
                    RevealNode(node);
                }
            }

            // Also mark nearby Prologue ice pads searched/revealed so Aquifer range checks pass.
            var ctrl = PrologueController.Instance;
            if (ctrl?.Colony != null && ctrl.TryGetTileIndex(world, out int center))
            {
                var reveal = new List<int>();
                for (int i = 0; i < ctrl.Colony.Board.Tiles.Count; i++)
                {
                    if (ctrl.Colony.Dist(center, i) > 1) continue;
                    var t = ctrl.Colony.Board.At(i);
                    if (t.Revealed) continue;
                    if (t.Feature == PrologueFeature.Ice || t.Feature == PrologueFeature.Glacier
                        || ctrl.Colony.Dist(center, i) == 0)
                        reveal.Add(i);
                }
                if (reveal.Count > 0)
                    ctrl.Colony.RevealTiles(reveal);
            }
        }

        static void RevealNode(SectorNode node)
        {
            node.isExplored = true;
            node.isDiscovered = false;

            if (node.visualGO != null)
            {
                node.visualGO.SetActive(true);
                ShowRenderers(node.visualGO);
                ApplyTypedDotColor(node);
            }

            if (node.questionMarkGO != null)
            {
                var tmp = node.questionMarkGO.GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    string label = !string.IsNullOrEmpty(node.labelOverride)
                        ? node.labelOverride
                        : node.type.ToString();
                    tmp.text = label;
                    tmp.color = node.type == SectorNode.NodeType.Feature
                        ? new Color(0.55f, 0.95f, 1f)
                        : new Color(1f, 0.95f, 0.55f);
                }
            }

            if (node.visualGO != null
                && node.visualGO.TryGetComponent(out HiddenResource hr))
            {
                if (!hr.IsDiscovered)
                    hr.ForceDiscover();
                NotifyPrologueDepositUncovered(hr);
            }
        }

        static void RestoreKnownTypesAfterFogReset()
        {
            foreach (var hr in Object.FindObjectsByType<HiddenResource>(FindObjectsInactive.Include))
            {
                if (hr == null || !hr.IsDiscovered) continue;
                if (string.IsNullOrEmpty(hr.ResourceTypeName)) continue;
                DiscoverySystem.MarkTypeKnownWithoutMassReveal(hr.ResourceTypeName);
            }

            var colony = PrologueController.Instance?.Colony;
            if (colony == null) return;
            foreach (var id in colony.KnownResources)
                DiscoverySystem.MarkTypeKnownWithoutMassReveal(PrologueResourceIdUtil.ToTypeName(id));
        }

        static void NotifyPrologueDepositUncovered(HiddenResource hr)
        {
            if (hr == null || !PrologueController.IsActive) return;
            if (!PrologueResourceIdUtil.TryParse(hr.ResourceTypeName, out var id)) return;

            DiscoverySystem.MarkTypeKnownWithoutMassReveal(hr.ResourceTypeName);
            var colony = PrologueController.Instance?.Colony;
            if (colony == null) return;
            colony.MarkResourceKnown(id);
            PrologueController.Instance.RefreshHandUnlocks();
        }

        static void ApplyUnknownVisual(SectorNode node)
        {
            if (node.questionMarkGO != null)
            {
                node.questionMarkGO.SetActive(true);
                var tmp = node.questionMarkGO.GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    tmp.text = "?";
                    tmp.color = new Color(1f, 0.92f, 0.2f, 1f);
                }
            }

            if (node.visualGO == null) return;

            // No colored puck / gas mesh — only the floating "?" until the drone uncovers it.
            HideRenderers(node.visualGO);

            if (node.visualGO.TryGetComponent(out GatherableSupply gs))
            {
                gs.SetVisible(false);
                gs.ToggleColliders(false);
            }
        }

        static void HideRenderers(GameObject go)
        {
            if (go == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r != null) r.enabled = false;
            }
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.gameObject.SetActive(false);
            }
        }

        static void ShowRenderers(GameObject go)
        {
            if (go == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r != null) r.enabled = true;
            }
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null) continue;
                ps.gameObject.SetActive(true);
            }
        }

        static void ApplyTypedDotColor(SectorNode node)
        {
            if (node.visualGO == null) return;
            if (!node.visualGO.TryGetComponent(out MeshRenderer mr) || mr.material == null) return;

            Color c = node.type switch
            {
                SectorNode.NodeType.Minerals => new Color(0.25f, 0.65f, 1f),
                SectorNode.NodeType.Gas => new Color(0.2f, 1f, 0.35f),
                SectorNode.NodeType.Iron => new Color(0.85f, 0.55f, 0.35f),
                SectorNode.NodeType.Regolith => new Color(0.75f, 0.55f, 0.25f),
                SectorNode.NodeType.Feature => new Color(0.55f, 0.95f, 1f),
                SectorNode.NodeType.Nexus => new Color(1f, 0f, 1f),
                _ => Color.white
            };
            var mat = mr.material;
            mat.color = c;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", c * 1.4f);
            }
        }
    }
}
