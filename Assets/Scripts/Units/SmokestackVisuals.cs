using UnityEngine;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Attach to the Oxygen Processor prefab.
    /// Procedurally replaces its mesh with a tall metallic smokestack and adds
    /// a particle smoke effect that activates only when construction completes.
    /// </summary>
    [DefaultExecutionOrder(-100)] // Run Awake before BaseBuilding so MainRenderer is set correctly.
    [RequireComponent(typeof(BaseBuilding))]
    public class SmokestackVisuals : MonoBehaviour
    {
        [Header("Monolith Dimensions")]
        [SerializeField] private float width = 1.5f;
        [SerializeField] private float height = 6f;
        [SerializeField] private float depth = 1.5f;

        [Header("Custom Visuals")]
        [SerializeField] private GameObject visualPrefab;

        [Header("Appearance")]
        [SerializeField] private Color ghostColor = new Color(0.45f, 0.55f, 0.7f, 0.22f);  // translucent site/construction ghost
        [SerializeField] private Color finalColor = new Color(0.38f, 0.40f, 0.42f);           // dark industrial monolith

        [Header("Smoke")]
        [SerializeField] private float smokeEmissionRate = 6f;
        [SerializeField] private float smokeSpeed = 0.6f;

        // Exposed so BaseBuilding can query it during InitializeAsGhost and StartBuilding.
        public float Height => height;
        public Material GhostMaterial  { get; private set; }
        public Material FinalMaterial  { get; private set; }

        private BaseBuilding building;
        private ParticleSystem smokePS;
        private Transform visualRoot; // parent of all generated geometry

        private void Awake()
        {
            building = GetComponent<BaseBuilding>();

            // Create materials first — BaseBuilding.InitializeAsGhost may query GhostMaterial
            // immediately after Awake() via Worker.Build().
            GhostMaterial  = MakeMetal(ghostColor, metallic: 0.65f, smoothness: 0.15f, transparent: true);
            FinalMaterial  = MakeMetal(finalColor, metallic: 0.85f, smoothness: 0.20f, transparent: false);

            // Destroy any existing renderer/filter that shipped with the prefab so it doesn't compete.
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
            {
                if (r.gameObject != gameObject) DestroyImmediate(r.gameObject);
                else
                {
                    DestroyImmediate(r);
                    if (TryGetComponent<MeshFilter>(out var mf)) DestroyImmediate(mf);
                }
            }

            BuildGeometry();
            BuildSmokeEffect();

            // Tell BaseBuilding to treat the stack as the primary renderer
            // (used for ghost/placement material during construction).
            building.SetMainRenderer(visualRoot.GetComponentInChildren<MeshRenderer>());
        }

        // ── Geometry ─────────────────────────────────────────────────────────

        private void BuildGeometry()
        {
            visualRoot = new GameObject("SmokestackRoot").transform;
            visualRoot.SetParent(transform, false);
            visualRoot.localPosition = Vector3.zero;

            Material metalMat = GhostMaterial;

            GameObject go;
            if (visualPrefab != null)
            {
                go = Instantiate(visualPrefab, visualRoot);
                go.name = "CustomMonolith";
                // Scale the prefab to match dimensions, assuming it's a normalized 1x1x1 mesh.
                go.transform.localPosition = new Vector3(0, height * 0.5f, 0);
                go.transform.localScale = new Vector3(width, height, depth); 
            }
            else
            {
                // Single Monolith Cube
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Monolith";
                go.transform.SetParent(visualRoot, false);
                // Center of the cube needs to be half-height so it sits on the ground
                go.transform.localPosition = new Vector3(0, height * 0.5f, 0);
                go.transform.localScale = new Vector3(width, height, depth);
                Destroy(go.GetComponent<BoxCollider>()); // colliders handled by BaseBuilding
            }

            // Remove any colliders on the generated geometry
            foreach (var col in go.GetComponentsInChildren<Collider>())
            {
                Destroy(col);
            }

            // Assign materials
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                r.material = metalMat;
            }
        }

        private static Material MakeMetal(Color color, float metallic = 0.80f, float smoothness = 0.25f, bool transparent = false)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (transparent)
            {
                // URP Lit transparent surface (old Standard _Mode flags alone are ignored by URP).
                if (mat.HasProperty("_Surface"))
                {
                    mat.SetFloat("_Surface", 1f);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
                }

                mat.SetFloat("_Mode", 2); // Fade (Standard fallback)
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            mat.color = color;
            mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Glossiness", smoothness);
            return mat;
        }

        // ── Smoke ─────────────────────────────────────────────────────────────

        private void BuildSmokeEffect()
        {
            GameObject smokeGO = new GameObject("SmokeEffect");
            smokeGO.transform.SetParent(transform, false);
            // Place smoke exactly at the top of the monolith
            smokeGO.transform.localPosition = new Vector3(0f, height + 0.1f, 0f);

            smokePS = smokeGO.AddComponent<ParticleSystem>();

            var main = smokePS.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime  = new ParticleSystem.MinMaxCurve(3f, 5f);
            main.startSpeed     = new ParticleSystem.MinMaxCurve(smokeSpeed * 0.6f, smokeSpeed);
            main.startSize      = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startColor     = new ParticleSystem.MinMaxGradient(
                new Color(0.75f, 0.75f, 0.75f, 0.55f),
                new Color(0.92f, 0.92f, 0.92f, 0.25f));
            main.maxParticles   = 80;
            main.gravityModifier = 0f;

            var emission = smokePS.emission;
            emission.rateOverTime = smokeEmissionRate;

            // Narrow upward cone
            var shape = smokePS.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle     = 8f;
            shape.radius    = Mathf.Min(width, depth) * 0.4f;

            // Fade out & expand over lifetime
            var col = smokePS.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new(Color.white, 0f), new(Color.white, 1f) },
                new GradientAlphaKey[] { new(0.55f, 0f), new(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var size = smokePS.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 2.5f));

            // Slight horizontal drift for realism
            var vel = smokePS.velocityOverLifetime;
            vel.enabled = true;
            vel.space   = ParticleSystemSimulationSpace.Local;
            // Use simple constant velocity to avoid mixed-mode errors
            vel.x = new ParticleSystem.MinMaxCurve(0.1f);
            vel.z = new ParticleSystem.MinMaxCurve(0.1f);

            // Soft particle material
            var psr = smokeGO.GetComponent<ParticleSystemRenderer>();
            Shader smokeShader = Shader.Find("Particles/Standard Unlit")
                              ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                              ?? Shader.Find("Standard");
            if (smokeShader != null)
            {
                var smokeMat = new Material(smokeShader);
                smokeMat.color = new Color(1f, 1f, 1f, 0.4f);
                psr.material = smokeMat;
            }

            // Stay stopped until construction finishes
            smokePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // Called by BaseBuilding.CompleteConstruction()
        public void ActivateSmoke()
        {
            // Swap every smokestack renderer to the final dark-metal material.
            if (visualRoot != null)
            {
                foreach (var r in visualRoot.GetComponentsInChildren<MeshRenderer>())
                    r.material = FinalMaterial;
            }

            if (smokePS != null && !smokePS.isPlaying)
                smokePS.Play();
        }
    }
}
