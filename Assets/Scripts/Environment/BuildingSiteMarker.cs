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
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // Site-pad previews stay very translucent so finished buildings read clearly.
        private static readonly Color IdleTint = new Color(0.45f, 0.7f, 0.95f, 0.16f);
        private static readonly Color IdleFresnel = new Color(0.35f, 0.55f, 0.8f, 0.2f);
        private static readonly Color SelectTint = new Color(0.25f, 0.75f, 1f, 0.4f);
        private static readonly Color SelectFresnel = new Color(1.2f, 0.7f, 0.15f, 0.55f);
        private const float IdleBaseAlpha = 0.18f;
        private const float SelectBaseAlpha = 0.38f;

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
            previewBuilding = null;
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
            RebuildGhost();
            RefreshVisibility();
        }

        public void SetSelectable(bool selectable)
        {
            isSelectable = selectable;
            EnsureClickCollider();
            ApplyColliderEnabledState();
            SetHighlight(selectable);
            RefreshVisibility();
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

            if (!ShouldShowGhostPreview())
            {
                DestroyGhost();
                gameObject.SetActive(false);
                return;
            }

            if (ghostInstance == null)
            {
                RebuildGhost();
            }

            gameObject.SetActive(ghostInstance != null);
            ApplyColliderEnabledState();
        }

        private bool ShouldShowGhostPreview()
        {
            if (Site == null) return false;

            // Always show fog-revealed reserved pads so the bootstrap sites
            // (Command Post, Solar, Oxygen Processor) stay visible in the starting area.
            return Site.Kind switch
            {
                BuildingSiteKind.CommandPost => true,
                BuildingSiteKind.Solar => true,
                BuildingSiteKind.PairedBuilding => true,
                BuildingSiteKind.Mine => isSelectable,
                _ => isSelectable
            };
        }

        private static bool IsSiteVisibleInWorld(BuildingSiteSlot site)
        {
            return BuildingSiteRegistry.IsSiteVisibleToPlayer(site);
        }

        private bool IsSiteVisibleInWorld()
        {
            return IsSiteVisibleInWorld(Site);
        }

        private void RebuildGhost()
        {
            DestroyGhost();

            if (!IsSiteVisibleInWorld() || !ShouldShowGhostPreview())
            {
                return;
            }

            BuildingSO building = BuildingSiteGhostUtility.ResolveBuildingForSite(Site, previewBuilding);
            if (building == null)
            {
                return;
            }

            // Prefer dedicated ghost variants when available; fall back to the solid prefab.
            GameObject prefab = BuildingSiteGhostUtility.GetGhostPrefab(building);
            if (prefab == null)
            {
                prefab = building.Prefab;
            }
            if (prefab == null)
            {
                return;
            }

            // Instantiate under an inactive holder so Awake/OnEnable/Start never run on the
            // building simulation components (they would complete construction and occupy the pad).
            var holder = new GameObject("GhostSpawnHolder");
            holder.SetActive(false);
            holder.transform.SetParent(transform, false);

            ghostInstance = Instantiate(prefab, holder.transform);
            ghostInstance.name = $"GhostPreview_{building.Name}";
            ghostInstance.transform.localPosition = Vector3.zero;
            ghostInstance.transform.localRotation = Quaternion.identity;

            Material ghostMaterial = building.PlacementMaterial;
            NeutralizeGhostSimulation(ghostInstance, ghostMaterial);
            HideSelectionIndicators(ghostInstance);
            StripSimulationComponents(ghostInstance);

            ghostInstance.transform.SetParent(transform, false);
            DestroyImmediate(holder);
            ghostInstance.SetActive(true);

            // Re-assert after activation: SmokestackVisuals/etc. Awake builds meshes on activate,
            // so ghost materials must be applied after that geometry exists.
            NeutralizeGhostSimulation(ghostInstance, ghostMaterial);
            StripSimulationComponents(ghostInstance);
            ApplyGhostMaterialFallback(ghostInstance, ghostMaterial);

            CacheTintMaterials();
            FitClickCollider();
            ApplyColliderEnabledState();
            ApplyTint();
        }

        private static void NeutralizeGhostSimulation(GameObject root, Material ghostMaterial)
        {
            if (root == null) return;

            foreach (var buildingComp in root.GetComponentsInChildren<BaseBuilding>(true))
            {
                if (buildingComp == null) continue;
                // Force paused ghost state so Start will not CompleteConstruction / RaiseSpawnEvent
                // even if the component is briefly enabled.
                buildingComp.InitializeAsGhost(ghostMaterial, buildingComp.Owner);
                buildingComp.enabled = false;
            }

            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null) behaviour.enabled = false;
            }
        }

        private void DestroyGhost()
        {
            tintedMaterials.Clear();
            if (ghostInstance != null)
            {
                DestroyImmediate(ghostInstance);
                ghostInstance = null;
            }

            ApplyColliderEnabledState();
        }

        private static void HideSelectionIndicators(GameObject root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Selection Indicator")
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private void StripSimulationComponents(GameObject root)
        {
            foreach (var col in root.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(col);
            }

            foreach (var nav in root.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true))
            {
                Object.DestroyImmediate(nav);
            }
        }

        private static void ApplyGhostMaterialFallback(GameObject root, Material ghostMaterial)
        {
            if (root == null) return;

            Material source = ghostMaterial;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                string nameLower = renderer.gameObject.name.ToLowerInvariant();
                if (nameLower.Contains("vision") || nameLower.Contains("selection") || nameLower.Contains("indicator") || nameLower.Contains("smoke"))
                {
                    continue;
                }

                if (source != null)
                {
                    // Always use the translucent placement shader for site pads — procedural
                    // building meshes (e.g. Oxygen Processor) otherwise stay fully opaque.
                    var mats = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                    for (int i = 0; i < mats.Length; i++)
                    {
                        mats[i] = new Material(source);
                        ApplyOpacityToMaterial(mats[i], IdleTint, IdleFresnel, IdleBaseAlpha);
                    }
                    renderer.materials = mats;
                }
                else if (renderer.sharedMaterial != null)
                {
                    renderer.material = CreateTransparentFallback(renderer.sharedMaterial, IdleTint);
                }
            }
        }

        private static Material CreateTransparentFallback(Material source, Color tint)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat;
            if (shader != null)
            {
                mat = new Material(shader);
            }
            else
            {
                mat = new Material(source);
            }

            ConfigureTransparentSurface(mat);
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, tint);
            if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, tint);
            if (mat.HasProperty(TintId)) mat.SetColor(TintId, tint);
            return mat;
        }

        private static void ConfigureTransparentSurface(Material mat)
        {
            if (mat == null) return;

            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f); // Transparent
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            }

            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
        }

        private void CacheTintMaterials()
        {
            tintedMaterials.Clear();
            if (ghostInstance == null) return;

            foreach (var renderer in ghostInstance.GetComponentsInChildren<Renderer>())
            {
                if (renderer == null || renderer.material == null) continue;
                string nameLower = renderer.gameObject.name.ToLowerInvariant();
                if (nameLower.Contains("vision") || nameLower.Contains("selection") || nameLower.Contains("indicator"))
                {
                    continue;
                }

                tintedMaterials.Add(renderer.material);
            }
        }

        private static void ApplyOpacityToMaterial(Material material, Color tint, Color fresnel, float baseAlpha)
        {
            if (material == null) return;

            if (material.HasProperty(TintId))
            {
                material.SetColor(TintId, tint);
            }

            if (material.HasProperty(FresnelId))
            {
                material.SetColor(FresnelId, fresnel);
            }

            if (material.HasProperty(BaseColorId))
            {
                Color baseColor = material.GetColor(BaseColorId);
                baseColor.a = baseAlpha;
                material.SetColor(BaseColorId, baseColor);
            }

            if (material.HasProperty(ColorId))
            {
                Color color = material.GetColor(ColorId);
                color.a = baseAlpha;
                material.SetColor(ColorId, color);
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
            Color tint = highlighted ? SelectTint : IdleTint;
            Color fresnel = highlighted ? SelectFresnel : IdleFresnel;
            float baseAlpha = highlighted ? SelectBaseAlpha : IdleBaseAlpha;

            foreach (Material material in tintedMaterials)
            {
                ApplyOpacityToMaterial(material, tint, fresnel, baseAlpha);
            }
        }

        private void OnDestroy()
        {
            DestroyGhost();
        }
    }
}
