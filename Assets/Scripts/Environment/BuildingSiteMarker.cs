using System.Collections.Generic;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Shows a building ghost preview at a reserved pad so the player knows what goes there.
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
        private BoxCollider clickCollider;
        private readonly List<Material> tintedMaterials = new();
        private bool highlighted;

        public void Initialize(BuildingSiteSlot site)
        {
            Site = site;
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
            if (clickCollider != null)
            {
                clickCollider.enabled = selectable;
            }

            SetHighlight(selectable);
        }

        public void SetHighlight(bool highlighted)
        {
            this.highlighted = highlighted;
            ApplyTint();
        }

        public void RefreshVisibility()
        {
            if (Site == null)
            {
                gameObject.SetActive(false);
                return;
            }

            if (Site.IsOccupied)
            {
                gameObject.SetActive(false);
                return;
            }

            if (Site.Kind == BuildingSiteKind.PairedBuilding)
            {
                bool showPreview = previewBuilding != null &&
                                   Site.Cluster != null &&
                                   Site.Cluster.SolarBuilding != null;
                gameObject.SetActive(showPreview && ghostInstance != null);
                return;
            }

            gameObject.SetActive(ghostInstance != null);
        }

        private void RebuildGhost()
        {
            DestroyGhost();

            BuildingSO building = BuildingSiteGhostUtility.ResolveBuildingForSite(Site, previewBuilding);
            if (building == null)
            {
                return;
            }

            GameObject prefab = BuildingSiteGhostUtility.GetGhostPrefab(building);
            if (prefab == null)
            {
                return;
            }

            ghostInstance = Instantiate(prefab, transform);
            ghostInstance.name = $"GhostPreview_{building.Name}";
            ghostInstance.transform.localPosition = Vector3.zero;
            ghostInstance.transform.localRotation = Quaternion.identity;

            if (ghostInstance.TryGetComponent(out BaseBuilding baseBuilding))
            {
                baseBuilding.InitializeAsGhost(building.PlacementMaterial, Owner.Player1);
            }

            StripSimulationComponents(ghostInstance);
            CacheTintMaterials();
            FitClickCollider();
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

            if (clickCollider != null)
            {
                clickCollider.enabled = false;
            }
        }

        private void StripSimulationComponents(GameObject root)
        {
            foreach (var col in root.GetComponentsInChildren<Collider>())
            {
                if (col != clickCollider)
                {
                    Destroy(col);
                }
            }

            foreach (var nav in root.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>())
            {
                Destroy(nav);
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

        private void FitClickCollider()
        {
            clickCollider = GetComponent<BoxCollider>();
            if (clickCollider == null)
            {
                clickCollider = gameObject.AddComponent<BoxCollider>();
            }

            clickCollider.isTrigger = true;
            clickCollider.enabled = false;

            if (ghostInstance == null)
            {
                clickCollider.center = Vector3.zero;
                clickCollider.size = Vector3.one * 3f;
                return;
            }

            Renderer[] renderers = ghostInstance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                clickCollider.center = Vector3.zero;
                clickCollider.size = Vector3.one * 3f;
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            clickCollider.center = transform.InverseTransformPoint(bounds.center);
            clickCollider.size = bounds.size;
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
