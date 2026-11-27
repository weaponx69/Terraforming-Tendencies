using GameDevTV.RTS.MainGame;
using GameDevTV.RTS.Player;
using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// MainGame: bend the patch into a planetoid as camera altitude rises; S-curve pitch when zoomed in.
    /// Shader bend is vertex-only — local relief stays readable when strength is moderate.
    /// </summary>
    [DefaultExecutionOrder(1010)]
    public class PlanetHorizonFromZoom : MonoBehaviour
    {
        PlanetGenerator planet;
        CurvedWorldUpdater curvedUpdater;
        bool shadersSwapped;

        public static bool PlanetoidView { get; private set; }

        void Awake()
        {
            planet = GetComponent<PlanetGenerator>();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || !MainGameController.IsActive || planet?.Config == null)
            {
                PlanetoidView = false;
                return;
            }

            // Photo/DEM albedo: vertex bend reads as a white void when close; tactical view stays flat.
            if (planet.Config.AlbedoMap != null)
            {
                PlanetoidView = false;
                Shader.SetGlobalFloat("_CurveStrength", 0f);
                return;
            }

            var input = PlayerInput.Instance;
            Camera cam = input != null ? input.GameplayCamera : Camera.main;
            if (cam == null) return;

            float zoom = input != null ? input.PlaytestZoomDistance : 40f;
            float minZ = input != null ? input.PlaytestMinZoom : 7.5f;
            float maxZ = input != null ? input.PlaytestMaxZoom : 200f;
            float farT = Mathf.InverseLerp(minZ + 8f, maxZ * 0.94f, zoom);
            farT = Mathf.Clamp01(farT);

            float patch = planet.Config.PlanetPatchDiameter > 1f
                ? planet.Config.PlanetPatchDiameter
                : 512f;
            // Config may store a real-world body radius; for zoom presentation use a patch-sized sphere.
            float bodyR = planet.Config.HorizonBodyRadiusMeters;
            if (bodyR < 64f || bodyR > patch * 3f)
                bodyR = Mathf.Max(patch * 0.85f, 256f);
            float half = patch * 0.5f;
            float maxStrength = (half * half) / (2f * bodyR);
            maxStrength = Mathf.Clamp(maxStrength, 0.00006f, 0.008f);
            float strength = maxStrength * Mathf.SmoothStep(0f, 1f, farT * farT);

            // Photo/DEM surfaces stay on URP Lit — swapping to Custom/URP_CurvedWorld can lose maps and read as a white void.
            if (planet.Config.AlbedoMap == null && strength > 0.00002f && !shadersSwapped)
            {
                planet.ApplyCurvedWorldShader(gameObject, forceZoomPresentation: true);
                shadersSwapped = true;
            }

            if (strength < 0.00001f)
            {
                Shader.SetGlobalFloat("_CurveStrength", 0f);
            }
            else
            {
                Shader.SetGlobalVector("_CurveOrigin", cam.transform.position);
                Shader.SetGlobalFloat("_CurveStrength", strength);
                if (curvedUpdater == null)
                    curvedUpdater = GetComponentInChildren<CurvedWorldUpdater>(true);
                if (curvedUpdater != null)
                {
                    curvedUpdater.CurveOrigin = cam.transform;
                    curvedUpdater.CurveStrength = strength;
                }
            }

            PlanetoidView = farT > 0.78f;
        }

        void OnDisable()
        {
            PlanetoidView = false;
            Shader.SetGlobalFloat("_CurveStrength", 0f);
        }
    }
}