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
        [Header("Stack Dimensions")]
        [SerializeField] private float stackHeight = 5f;
        [SerializeField] private float stackRadius = 0.3f;
        [SerializeField] private float baseHeight = 0.5f;
        [SerializeField] private float baseRadius = 0.7f;

        [Header("Appearance")]
        [SerializeField] private Color metalColor = new Color(0.50f, 0.52f, 0.54f);

        [Header("Smoke")]
        [SerializeField] private float smokeEmissionRate = 6f;
        [SerializeField] private float smokeSpeed = 0.6f;

        private BaseBuilding building;
        private ParticleSystem smokePS;
        private Transform visualRoot; // parent of all generated geometry

        private void Awake()
        {
            building = GetComponent<BaseBuilding>();

            // Hide any existing renderer that shipped with the prefab so it doesn't compete.
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
                r.enabled = false;

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

            Material metalMat = MakeMetal(metalColor);

            // Wide base at ground level
            MakeCylinder("Base", visualRoot,
                new Vector3(0, baseHeight * 0.5f, 0),
                new Vector3(baseRadius * 2f, baseHeight * 0.5f, baseRadius * 2f),
                metalMat);

            // Main tall stack
            MakeCylinder("Stack", visualRoot,
                new Vector3(0, baseHeight + stackHeight * 0.5f, 0),
                new Vector3(stackRadius * 2f, stackHeight * 0.5f, stackRadius * 2f),
                metalMat);

            // Flared lip at the very top
            MakeCylinder("Lip", visualRoot,
                new Vector3(0, baseHeight + stackHeight + 0.06f, 0),
                new Vector3(stackRadius * 3f, 0.06f, stackRadius * 3f),
                metalMat);
        }

        private static void MakeCylinder(string objName, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = objName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            Destroy(go.GetComponent<CapsuleCollider>()); // colliders handled by BaseBuilding
            go.GetComponent<MeshRenderer>().material = mat;
        }

        private static Material MakeMetal(Color color)
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.SetFloat("_Metallic", 0.80f);
            mat.SetFloat("_Glossiness", 0.25f);
            return mat;
        }

        // ── Smoke ─────────────────────────────────────────────────────────────

        private void BuildSmokeEffect()
        {
            GameObject smokeGO = new GameObject("SmokeEffect");
            smokeGO.transform.SetParent(transform, false);
            smokeGO.transform.localPosition = new Vector3(0f, baseHeight + stackHeight + 0.15f, 0f);

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
            shape.radius    = stackRadius * 0.5f;

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
            vel.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

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
            if (smokePS != null && !smokePS.isPlaying)
                smokePS.Play();
        }
    }
}
