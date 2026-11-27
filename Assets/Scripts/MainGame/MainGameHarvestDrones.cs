using System.Collections.Generic;
using GameDevTV.RTS.Player;
using UnityEngine;

namespace GameDevTV.RTS.MainGame
{
    /// <summary>
    /// Presentation-only Survey-style drones looping a mine pad.
    /// </summary>
    public class MainGameHarvestDrones : MonoBehaviour
    {
        readonly List<GameObject> drones = new();
        readonly List<Vector3> waypoints = new();
        readonly List<int> wanderTargets = new();
        Vector3 center;
        float radius = 3.5f;

        public void Begin(Vector3 padCenter, int count = 2)
        {
            // Harvest drones are off for now. The mine still pays its stock on End Week.
            Clear();
        }

        void Update()
        {
            if (drones.Count == 0 || waypoints.Count == 0) return;
            float speed = 4.2f * Time.deltaTime;
            for (int i = 0; i < drones.Count; i++)
            {
                var go = drones[i];
                if (go == null) continue;
                int wi = wanderTargets[i];
                Vector3 goal = waypoints[wi];
                goal.y = ColonyTileGrid.SampleGroundY(goal) + 4.8f + (i % 2) * 0.6f;
                Vector3 pos = go.transform.position;
                go.transform.position = Vector3.MoveTowards(pos, goal, speed);
                Vector3 flat = goal - pos;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.01f)
                {
                    go.transform.rotation = Quaternion.Slerp(
                        go.transform.rotation,
                        Quaternion.LookRotation(flat.normalized, Vector3.up),
                        8f * Mathf.Max(Time.deltaTime, 0.01f));
                }
                if (Vector3.Distance(go.transform.position, goal) < 0.3f)
                    wanderTargets[i] = (wi + 1) % waypoints.Count;

                var bob = go.transform.position;
                bob.y += Mathf.Sin(Time.unscaledTime * 3f + i) * 0.02f;
                go.transform.position = bob;
            }
        }

        void OnDestroy() => Clear();

        public void Clear()
        {
            for (int i = 0; i < drones.Count; i++)
            {
                if (drones[i] != null) Destroy(drones[i]);
            }
            drones.Clear();
            wanderTargets.Clear();
            waypoints.Clear();
        }

        void BuildWaypoints()
        {
            waypoints.Clear();
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f;
                waypoints.Add(center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
        }

        static GameObject BuildMiniDrone(string name)
        {
            var root = new GameObject(name);
            var mat = Unlit(new Color(0.2f, 0.9f, 1f));
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.7f, 0.2f, 0.7f);
            Object.Destroy(body.GetComponent<Collider>());
            body.GetComponent<MeshRenderer>().sharedMaterial = mat;
            for (int a = 0; a < 4; a++)
            {
                float x = (a < 2 ? 1f : -1f) * 0.55f;
                float z = (a % 2 == 0 ? 1f : -1f) * 0.55f;
                var rotor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rotor.name = $"Rotor_{a}";
                rotor.transform.SetParent(root.transform, false);
                rotor.transform.localPosition = new Vector3(x, 0.12f, z);
                rotor.transform.localScale = new Vector3(0.55f, 0.025f, 0.55f);
                Object.Destroy(rotor.GetComponent<Collider>());
                rotor.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            return root;
        }

        static Material Unlit(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Standard");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            m.color = c;
            return m;
        }
    }
}
