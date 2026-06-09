using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.Environment
{
    public class EnergyPipelineManager : MonoBehaviour
    {
        public bool IsCompleted { get; private set; }

        private Vector3 targetPosition;
        private SectorManager.Sector sector;
        private GameObject realCommandPostPrefab;

        private Vector3 startPosition;
        private List<GameObject> segments = new List<GameObject>();
        private float segmentLength = 2.0f;
        private float growthSpeed = 2.0f;
        private float resourceDrainInterval = 1.0f;
        private int resourceDrainAmount = 5;

        private float currentGrowthDist = 0f;
        private float lastDrainTime;
        private bool isAssemblyPhase = false;

        public void Initialize(Vector3 target, SectorManager.Sector sec, GameObject realPrefab)
        {
            targetPosition = target;
            sector = sec;
            realCommandPostPrefab = realPrefab;

            startPosition = FindNearestCompletedCommandCenter();
            lastDrainTime = Time.time;
        }

        private Vector3 FindNearestCompletedCommandCenter()
        {
            BaseBuilding best = null;
            float minDistance = float.MaxValue;
            foreach (var b in BaseBuilding.ActiveBuildings)
            {
                if (b == null || b.Owner != Owner.Player1) continue;
                if (b.Progress.State != BuildingProgress.BuildingState.Completed) continue;
                if (b.name.Contains("Command") || (b.BuildingSO != null && b.BuildingSO.Name.Contains("Command")))
                {
                    float dist = Vector3.Distance(targetPosition, b.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        best = b;
                    }
                }
            }
            return best != null ? best.transform.position : Vector3.zero;
        }

        private void Update()
        {
            if (IsCompleted || isAssemblyPhase) return;

            if (Time.time >= lastDrainTime + resourceDrainInterval)
            {
                lastDrainTime = Time.time;
                DrainResources();
            }

            float totalDist = Vector3.Distance(startPosition, targetPosition);
            if (currentGrowthDist < totalDist)
            {
                if (HasResources())
                {
                    currentGrowthDist += growthSpeed * Time.deltaTime;
                    if (currentGrowthDist > totalDist) currentGrowthDist = totalDist;

                    UpdateSegments();
                }
            }
            else
            {
                StartCoroutine(BootUpSequence());
            }
        }

        private bool HasResources()
        {
            if (Supplies.Biomass == null) return false;
            return Supplies.Biomass.TryGetValue(Owner.Player1, out int b) && b >= resourceDrainAmount;
        }

        private void DrainResources()
        {
            if (Supplies.Biomass == null) return;
            if (Supplies.Biomass.TryGetValue(Owner.Player1, out int b))
            {
                int nextVal = Mathf.Max(0, b - resourceDrainAmount);
                Supplies.Biomass[Owner.Player1] = nextVal;
                Supplies.RaiseBiomassChanged(Owner.Player1, nextVal);
            }
        }

        private void UpdateSegments()
        {
            Vector3 dir = (targetPosition - startPosition).normalized;
            int neededSegments = Mathf.FloorToInt(currentGrowthDist / segmentLength);

            while (segments.Count < neededSegments)
            {
                float spawnDist = (segments.Count + 0.5f) * segmentLength;
                Vector3 spawnPos = startPosition + dir * spawnDist;

                Ray ray = new Ray(spawnPos + Vector3.up * 50f, Vector3.down);
                if (Physics.Raycast(ray, out RaycastHit groundHit, 100f, LayerMask.GetMask("Default", "Terrain")))
                {
                    spawnPos.y = groundHit.point.y;
                }

                GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                seg.name = "PipelineSegment";
                seg.transform.position = spawnPos;
                seg.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);
                seg.transform.localScale = new Vector3(0.5f, segmentLength * 0.5f, 0.5f);

                var renderer = seg.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    if (mat == null) mat = new Material(Shader.Find("Unlit/Color"));
                    mat.color = new Color(0f, 0.8f, 1f, 0.8f);
                    renderer.material = mat;
                }

                PipelineSegment segComp = seg.AddComponent<PipelineSegment>();
                segComp.Initialize(this, segments.Count);

                segments.Add(seg);
            }
        }

        public void HandleSegmentDestroyed(int index)
        {
            for (int i = segments.Count - 1; i >= index; i--)
            {
                if (segments[i] != null)
                {
                    Destroy(segments[i]);
                }
                segments.RemoveAt(i);
            }

            currentGrowthDist = segments.Count * segmentLength;
        }

        public void CancelExpansion()
        {
            foreach (var seg in segments)
            {
                if (seg != null) Destroy(seg);
            }
            segments.Clear();

            if (ColonyExpansionManager.Instance != null)
            {
                ColonyExpansionManager.Instance.VetoSector(sector);
            }

            Destroy(gameObject);
        }

        private IEnumerator BootUpSequence()
        {
            isAssemblyPhase = true;

            yield return new WaitForSeconds(5.0f);

            GameObject cc = Instantiate(realCommandPostPrefab, targetPosition, Quaternion.identity);
            if (cc.TryGetComponent(out BaseBuilding building))
            {
                building.enabled = true;
                building.Owner = Owner.Player1;
                building.CompleteConstruction();
            }

            foreach (var seg in segments)
            {
                if (seg != null) Destroy(seg);
            }
            segments.Clear();

            IsCompleted = true;

            if (ColonyExpansionManager.Instance != null)
            {
                ColonyExpansionManager.Instance.ClearExpansion(sector);
            }

            Destroy(gameObject);
        }
    }
}
