using UnityEngine;

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
            cachedMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            return cachedMaterial;
        }
    }
}
