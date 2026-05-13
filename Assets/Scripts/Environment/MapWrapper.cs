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

            // Wrap Units
            AbstractUnit[] units = FindObjectsOfType<AbstractUnit>();
            foreach (var unit in units)
            {
                if (unit.Agent != null)
                {
                    Vector3 pos = unit.transform.position;
                    bool wrapped = false;

                    if (pos.x < 0) { pos.x += mapWidthWorld; wrapped = true; }
                    else if (pos.x > mapWidthWorld) { pos.x -= mapWidthWorld; wrapped = true; }

                    if (pos.z < 0) { pos.z += mapHeightWorld; wrapped = true; }
                    else if (pos.z > mapHeightWorld) { pos.z -= mapHeightWorld; wrapped = true; }

                    if (wrapped)
                    {
                        // Warp the NavMeshAgent safely
                        unit.Agent.Warp(pos);
                    }
                }
            }

            // Wrap Camera (Assumes Camera is attached to a Player/Camera Rig)
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Transform camParent = mainCam.transform.parent != null ? mainCam.transform.parent : mainCam.transform;
                Vector3 camPos = camParent.position;
                bool camWrapped = false;

                if (camPos.x < 0) { camPos.x += mapWidthWorld; camWrapped = true; }
                else if (camPos.x > mapWidthWorld) { camPos.x -= mapWidthWorld; camWrapped = true; }

                if (camPos.z < 0) { camPos.z += mapHeightWorld; camWrapped = true; }
                else if (camPos.z > mapHeightWorld) { camPos.z -= mapHeightWorld; camWrapped = true; }

                if (camWrapped)
                {
                    camParent.position = camPos;
                }
            }
        }
    }
}
