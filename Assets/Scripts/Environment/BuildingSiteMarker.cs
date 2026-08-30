using System.Collections.Generic;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Shows a building ghost preview at a reserved pad so the player knows what goes there.
    /// Click colliders are only enabled while site selection is active.
    /// </summary>
    public class BuildingSiteMarker : MonoBehaviour
    {
        public BuildingSiteSlot Site { get; private set; }

        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int FresnelId = Shader.PropertyToID("_FresnelColor");
        private static readonly Color SelectTint = new Color(0.2f, 0.65f, 1f, 2f);
        private static readonly Color SelectFresnel = new Color(4f, 1.7f, 0f, 2f);

        private GameObject ghostInstance;
        private BuildingSO previewBuilding;
        private Collider clickCollider;
        private readonly List<Material> tintedMaterials = new();
        private bool highlighted;
        private bool isSelectable;

        public void Initialize(BuildingSiteSlot site)
        {
            Site = site;
            isSelectable = false;
            RebuildGhost();
            RefreshVisibility();
        }

        public void SetPreviewBuilding(BuildingSO building)
        {
            previewBuilding = building;
            RebuildGhost();
            RefreshVisibility();
        }

        public void ClearPreview()
        {
            previewBuilding = null;
            if (Site?.Kind == BuildingSiteKind.PairedBuilding)
            {
                DestroyGhost();
            }
            else
            {
                RebuildGhost();
            }

            RefreshVisibility();
        }

        public void SetSelectable(bool selectable)
        {
            isSelectable = selectable;
            EnsureClickCollider();
            ApplyColliderEnabledState();
            SetHighlight(selectable);
        }

        public void SetHighlight(bool highlighted)
        {
            this.highlighted = highlighted;
            ApplyTint();
        }

        public void RefreshVisibility()
        {
            if (Site == null || !IsSiteVisibleInWorld())
            {
                gameObject.SetActive(false);
                return;
            }

            if (Site.IsOccupied)
            {
                gameObject.SetActive(false);
                return;
            }

            if (ghostInstance == null && Site.Kind != BuildingSiteKind.PairedBuilding)
            {
                RebuildGhost();
            }

            if (Site.Kind == BuildingSiteKind.PairedBuilding)
            {
                bool showPreview = previewBuilding != null &&
                                   Site.Cluster != null &&
                                   Site.Cluster.SolarBuilding != null;
                gameObject.SetActive(showPreview && ghostInstance != null);
                ApplyColliderEnabledState();
                return;
            }

            gameObject.SetActive(ghostInstance != null);
            ApplyColliderEnabledState();
        }

        private static bool IsSiteVisibleInWorld(BuildingSiteSlot site)
        {
            if (site?.Sector == null)
            {
                return true;
            }

            return site.Sector.IsExplored && !site.Sector.IsLocked;
        }

        private bool IsSiteVisibleInWorld()
        {
            return IsSiteVisibleInWorld(Site);
        }

        private void RebuildGhost()
        {
            DestroyGhost();

            if (!IsSiteVisibleInWorld())
            {
                return;
            }

            BuildingSO building = BuildingSiteGhostUtility.ResolveBuildingForSite(Site, previewBuilding);
            if (building == null)
            {
                return;
            }

            // Prefer the real building prefab so InitializeAsGhost works. Visual-only ghost
            // variants (e.g. SolarPanel Ghost Variant) have no BaseBuilding and look solid.
            GameObject prefab = building.Prefab != null
                ? building.Prefab
                : BuildingSiteGhostUtility.GetGhostPrefab(building);
            if (prefab == null)
            {
                return;
            }

            ghostInstance = Instantiate(prefab, transform);
            ghostInstance.name = $"GhostPreview_{building.Name}";
            ghostInstance.transform.localPosition = Vector3.zero;
            ghostInstance.transform.localRotation = Quaternion.identity;

            BaseBuilding baseBuilding = ghostInstance.GetComponentInChildren<BaseBuilding>(true);
            if (baseBuilding != null)
            {
                baseBuilding.enabled = true;
                baseBuilding.InitializeAsGhost(building.PlacementMaterial, Owner.Player1);
            }
            else
            {
                ApplyGhostMaterialFallback(ghostInstance, building.PlacementMaterial);
            }

            StripSimulationComponents(ghostInstance);
            CacheTintMaterials();
            FitClickCollider();
            ApplyColliderEnabledState();
            ApplyTint();
        }

        private void DestroyGhost()
        {
            tintedMaterials.Clear();
            if (ghostInstance != null)
            {
                Destroy(ghostInstance);
                ghostInstance = null;
            }

            ApplyColliderEnabledState();
        }

        private void StripSimulationComponents(GameObject root)
        {
            foreach (var col in root.GetComponentsInChildren<Collider>(true))
            {
                Destroy(col);
            }

            foreach (var nav in root.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true))
            {
                Destroy(nav);
            }
        }

        private static void ApplyGhostMaterialFallback(GameObject root, Material ghostMaterial)
        {
            if (root == null || ghostMaterial == null) return;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                string nameLower = renderer.gameObject.name.ToLowerInvariant();
                if (nameLower.Contains("vision") || nameLower.Contains("selection") || nameLower.Contains("indicator"))
                {
                    continue;
                }

                renderer.sharedMaterial = ghostMaterial;
            }
        }

        private void CacheTintMaterials()
        {
            tintedMaterials.Clear();
            if (ghostInstance == null) return;

            foreach (var renderer in ghostInstance.GetComponentsInChildren<Renderer>())
            {
                if (renderer == null) continue;
                tintedMaterials.Add(renderer.material);
            }
        }

        private void EnsureClickCollider()
        {
            if (clickCollider == null)
            {
                FitClickCollider();
            }
        }

        private void FitClickCollider()
        {
            clickCollider = GetComponent<Collider>();
            if (clickCollider == null)
            {
                clickCollider = gameObject.AddComponent<BoxCollider>();
            }

            // Trigger colliders still raycast with QueryTriggerInteraction.Collide,
            // but they won't block unit/building selection physics the same way.
            clickCollider.isTrigger = true;

            if (ghostInstance == null)
            {
                if (clickCollider is BoxCollider fallbackBox)
                {
                    fallbackBox.center = Vector3.up * 1.5f;
                    fallbackBox.size = new Vector3(6f, 4f, 6f);
                }
                ApplyColliderEnabledState();
                return;
            }

            Renderer[] renderers = ghostInstance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                if (clickCollider is BoxCollider emptyBox)
                {
                    emptyBox.center = Vector3.up * 1.5f;
                    emptyBox.size = new Vector3(6f, 4f, 6f);
                }
                ApplyColliderEnabledState();
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            if (clickCollider is BoxCollider box)
            {
                box.center = transform.InverseTransformPoint(bounds.center);
                Vector3 size = bounds.size;
                size.x = Mathf.Max(size.x + 1f, 4f);
                size.z = Mathf.Max(size.z + 1f, 4f);
                size.y = Mathf.Max(size.y, 3f);
                box.size = size;
            }

            ApplyColliderEnabledState();
        }

        private void ApplyColliderEnabledState()
        {
            if (clickCollider == null) return;
            clickCollider.enabled = isSelectable && ghostInstance != null && gameObject.activeInHierarchy;
        }

        private void ApplyTint()
        {
            foreach (Material material in tintedMaterials)
            {
                if (material == null || !material.HasProperty(TintId)) continue;

                if (highlighted)
                {
                    material.SetColor(TintId, SelectTint);
                    if (material.HasProperty(FresnelId))
                    {
                        material.SetColor(FresnelId, SelectFresnel);
                    }
                }
                else if (material.HasProperty(TintId))
                {
                    material.SetColor(TintId, Color.white);
                    if (material.HasProperty(FresnelId))
                    {
                        material.SetColor(FresnelId, Color.white);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            DestroyGhost();
        }
    }
}
