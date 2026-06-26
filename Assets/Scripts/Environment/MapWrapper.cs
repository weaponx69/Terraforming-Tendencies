using UnityEngine;
using UnityEngine.AI;
using GameDevTV.RTS.Units;
using Unity.VisualScripting;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Wraps the camera around the map edges for seamless scrolling.
    /// <para>
    /// Heavy logic (camera position math, Cinemachine warp notification,
    /// GameObject.Find lookups) stays in C#. VS reads <see cref="MapWidth"/>
    /// and <see cref="MapHeight"/> for HUD/minimap scaling.
    /// </para>
    /// </summary>
    [IncludeInSettings(true)]
    public class MapWrapper : MonoBehaviour
    {
        private float mapWidthWorld;
        private float mapHeightWorld;

        /// <summary>World-space width of the map in units.</summary>
        [Inspectable]
        public float MapWidth => mapWidthWorld;

        /// <summary>World-space height of the map in units.</summary>
        [Inspectable]
        public float MapHeight => mapHeightWorld;

        private void Start()
        {
            if (PlanetGenerator.Instance != null && PlanetGenerator.Instance.Config != null)
            {
                mapWidthWorld = PlanetGenerator.Instance.Config.MapWidth * PlanetGenerator.Instance.CellSize;
                mapHeightWorld = PlanetGenerator.Instance.Config.MapHeight * PlanetGenerator.Instance.CellSize;
            }
        }

        private void Update()
        {
            if (mapWidthWorld <= 0 || mapHeightWorld <= 0) return;

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Transform camRig = mainCam.transform.parent != null ? mainCam.transform.parent : mainCam.transform;
                
                GameObject camTargetObj = GameObject.Find("Camera Target");
                if (camTargetObj != null)
                {
                    camRig = camTargetObj.transform;
                }

                Vector3 camPos = camRig.position;
                bool camWrapped = false;

                if (camPos.x < 0) { camPos.x += mapWidthWorld; camWrapped = true; }
                else if (camPos.x > mapWidthWorld) { camPos.x -= mapWidthWorld; camWrapped = true; }

                if (camPos.z < 0) { camPos.z += mapHeightWorld; camWrapped = true; }
                else if (camPos.z > mapHeightWorld) { camPos.z -= mapHeightWorld; camWrapped = true; }

                if (camWrapped)
                {
                    Vector3 delta = camPos - camRig.position;
                    camRig.position = camPos;
                    
                    var vcam = Object.FindAnyObjectByType<Unity.Cinemachine.CinemachineCamera>();
                    if (vcam != null)
                    {
                        vcam.OnTargetObjectWarped(camRig, delta);
                    }
                }
            }
        }
    }
}
