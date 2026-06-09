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
        public SectorManager.Sector sector;
        private GameObject realCommandPostPrefab;

        private Vector3 startPosition;
        private List<GameObject> segments = new List<GameObject>();
        private float segmentLength = 2.0f;
        private int segmentBiomassCost = 2; // Cost per section

        private int neededSegments = 0;
        private int builtSegments = 0;
        private bool isAssemblyPhase = false;

        // Right-click cycle: 0 = growing (never interacted), 1 = paused, 2 = resumed (next click cancels)
        private int cycleStep = 0;

        public bool IsPaused { get; private set; }

        public enum ExpansionAction { Paused, Resumed, Cancelled }

        public float GetProgress()
        {
            if (neededSegments <= 0) return 1f;
            if (isAssemblyPhase) return 1f;

            return (float)builtSegments / neededSegments;
        }

        /// <summary>
        /// Advances the right-click interaction cycle on this expansion:
        /// Pause -> Resume -> Cancel. Returns the action that was performed.
        /// </summary>
        public ExpansionAction CycleRightClick()
        {
            if (cycleStep == 0)
            {
                IsPaused = true;
                cycleStep = 1;
                SetSegmentsPausedVisual(true);
                Debug.Log($"[Expansion] Paused expansion to {sector.Center}. Biomass drain halted.");
                return ExpansionAction.Paused;
            }
            else if (cycleStep == 1)
            {
                IsPaused = false;
                cycleStep = 2;
                SetSegmentsPausedVisual(false);
                Debug.Log($"[Expansion] Resumed expansion to {sector.Center}.");
                return ExpansionAction.Resumed;
            }
            else
            {
                Debug.Log($"[Expansion] Cancelled expansion to {sector.Center}.");
                CancelExpansion();
                return ExpansionAction.Cancelled;
            }
        }

        private void SetSegmentsPausedVisual(bool paused)
        {
            Color c = paused
                ? new Color(1f, 0.8f, 0f, 0.8f)   // amber while paused
                : new Color(0f, 0.8f, 1f, 0.8f);  // cyan while active

            foreach (var seg in segments)
            {
                if (seg == null) continue;
                var renderer = seg.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.material != null)
                {
                    renderer.material.color = c;
                }
            }
        }

            public void Initialize(Vector3 target, SectorManager.Sector sec, GameObject realPrefab)
            {
                targetPosition = target;
                sector = sec;
                realCommandPostPrefab = realPrefab;

                startPosition = FindNearestCompletedCommandCenter();

                float totalDist = Vector3.Distance(startPosition, targetPosition);
                neededSegments = Mathf.CeilToInt(totalDist / segmentLength);
                builtSegments = 0;
            }

        private Vector3 FindNearestCompletedCommandCenter()
        {
            BaseBuilding best = null;
            float minDistance = float.MaxValue;

            // 1. Search for completed Command Center/Post specifically
            foreach (var b in BaseBuilding.ActiveBuildings)
            {
                if (b == null || b.Owner != Owner.Player1) continue;
                if (b.Progress.State != BuildingProgress.BuildingState.Completed) continue;
                if (b.name.Contains("Command", System.StringComparison.OrdinalIgnoreCase) || 
                    (b.BuildingSO != null && b.BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase)))
                {
                    float dist = Vector3.Distance(targetPosition, b.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        best = b;
                    }
                }
            }

            if (best != null) return best.transform.position;

            // 2. Fallback: Search for ANY completed Player 1 building in case of naming differences
            foreach (var b in BaseBuilding.ActiveBuildings)
            {
                if (b == null || b.Owner != Owner.Player1) continue;
                if (b.Progress.State != BuildingProgress.BuildingState.Completed) continue;

                float dist = Vector3.Distance(targetPosition, b.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    best = b;
                }
            }

            if (best != null) return best.transform.position;

            // 3. Fallback: Scan scene directly in case registration in ActiveBuildings has a timing delay
            var allBuildings = Object.FindObjectsByType<BaseBuilding>(FindObjectsInactive.Include);
            foreach (var b in allBuildings)
            {
                if (b == null || b.Owner != Owner.Player1) continue;
                if (b.Progress.State != BuildingProgress.BuildingState.Completed) continue;

                float dist = Vector3.Distance(targetPosition, b.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    best = b;
                }
            }

            // 4. Ultimate fallback: if absolutely no completed player buildings exist, start at targetPosition
            return best != null ? best.transform.position : targetPosition;
        }

        private void Update()
        {
            // Update loop no longer auto-builds the pipeline! 
            // Drones now call BuildNextSegment() physically.
            if (!IsCompleted && !isAssemblyPhase && builtSegments >= neededSegments)
            {
                Debug.Log($"[Expansion] Growth complete by drones. Starting boot-up sequence for {sector.Center}");
                StartCoroutine(BootUpSequence());
            }
        }

        public bool HasPendingSegments()
        {
            return !IsCompleted && !isAssemblyPhase && !IsPaused && builtSegments < neededSegments;
        }

        public Vector3 GetNextSegmentPosition()
        {
            if (builtSegments >= neededSegments) return targetPosition;

            Vector3 diff = targetPosition - startPosition;
            Vector3 dir = diff.normalized;
            float spawnDist = (builtSegments + 0.5f) * segmentLength;
            Vector3 spawnPos = startPosition + dir * spawnDist;

            Ray ray = new Ray(spawnPos + Vector3.up * 50f, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit groundHit, 100f, LayerMask.GetMask("Default", "Terrain")))
            {
                spawnPos.y = groundHit.point.y;
            }

            return spawnPos;
        }

        public bool CanAffordNextSegment()
        {
            if (Supplies.Biomass == null) return false;
            return Supplies.Biomass.TryGetValue(Owner.Player1, out int b) && b >= segmentBiomassCost;
        }

        public bool BuildNextSegment()
        {
            if (!HasPendingSegments()) return false;
            if (!CanAffordNextSegment()) return false;

            // Drain cost
            if (Supplies.Biomass.TryGetValue(Owner.Player1, out int b))
            {
                int nextVal = Mathf.Max(0, b - segmentBiomassCost);
                Supplies.Biomass[Owner.Player1] = nextVal;
                Supplies.RaiseBiomassChanged(Owner.Player1, nextVal);
            }

            // Spawn the segment physically
            Vector3 spawnPos = GetNextSegmentPosition();
            Vector3 dir = (targetPosition - startPosition).normalized;

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
            segComp.Initialize(this, builtSegments);
            segments.Add(seg);

            builtSegments++;
            return true;
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

            builtSegments = segments.Count;
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
