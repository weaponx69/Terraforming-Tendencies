using UnityEngine;
using UnityEngine.Rendering;

namespace GameDevTV.RTS.Utilities
{
    /// <summary>
    /// Builds a transparent selection ring material for ground quads.
    /// </summary>
    public static class SelectionIndicatorUtility
    {
        private static Material cachedMaterial;

        public static Material GetRingMaterial()
        {
            if (cachedMaterial != null)
            {
                return cachedMaterial;
            }

            Texture2D ringTexture = Resources.Load<Texture2D>("Textures/Selection Ring");
            if (ringTexture == null)
            {
                ringTexture = Resources.Load<Texture2D>("Selection Ring");
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            cachedMaterial = new Material(shader);
            if (ringTexture != null)
            {
                cachedMaterial.mainTexture = ringTexture;
                cachedMaterial.SetTexture("_BaseMap", ringTexture);
            }

            cachedMaterial.color = new Color(0.2f, 1f, 0.25f, 0.9f);
            cachedMaterial.SetColor("_BaseColor", new Color(0.2f, 1f, 0.25f, 0.9f));
            cachedMaterial.renderQueue = (int)RenderQueue.Transparent;

            return cachedMaterial;
        }

        /// <summary>
        /// Forces an existing prefab/runtime selection indicator to use the transparent ring.
        /// </summary>
        public static void ApplyTo(GameObject indicator)
        {
            if (indicator == null) return;

            Collider collider = indicator.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            MeshFilter meshFilter = indicator.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                if (quad != null)
                {
                    meshFilter.sharedMesh = quad;
                }
            }

            indicator.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            MeshRenderer meshRenderer = indicator.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sharedMaterial = GetRingMaterial();
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
            }
        }
    }
}
