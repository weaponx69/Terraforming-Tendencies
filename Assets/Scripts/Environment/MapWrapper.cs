using UnityEngine;
using UnityEngine.AI;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Environment
{
    public class MapWrapper : MonoBehaviour
    {
        private float mapWidthWorld;
        private float mapHeightWorld;

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

            // --- REMOVED: Unit Wrapping ---
            // Units should not be wrapped automatically because they may be pathing 
            // to expansion/ghost resources outside the 0-100 bounds.
            // Tiled NavMesh handles the edge crossing naturally.

            // Wrap Camera (Assumes Camera is attached to a Player/Camera Rig)
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Transform camRig = mainCam.transform.parent != null ? mainCam.transform.parent : mainCam.transform;
                
                // If there's an RTS Camera Target, wrap that instead so we don't fight Cinemachine
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
                    
                    // If we use Cinemachine, notify it of the warp so it doesn't sweep across the map
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
