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

        // Gatherable crawler-fuel deposits placed alongside the supply line.
        private GameObject regolithDepositPrefab;
        private GameObject ironDepositPrefab;
        private int segmentsSinceLastDeposit = 0;
        private const int DEPOSIT_INTERVAL = 4;        // expose deposits every N segments
        private const float DEPOSIT_AHEAD_OFFSET = 4.5f; // distance ahead/behind the foundry along travel
        private const float DEPOSIT_PAIR_SPREAD = 1.5f;  // lateral gap so the pair doesn't overlap

        private int neededSegments = 0;
        private int builtSegments = 0;
        private bool isAssemblyPhase = false;

        private GameObject spawnedForge;

        // Right-click cycle: 0 = growing (never interacted), 1 = paused, 2 = resumed (next click cancels)
        private int cycleStep = 0;

        public bool IsPaused { get; private set; }

        public Vector3 StartPosition => startPosition;
        public float SegmentLength => segmentLength;
        public int BuiltSegments => builtSegments;

        public enum ExpansionAction { Paused, Resumed, Cancelled }

        public float GetProgress()
        {
            if (neededSegments <= 0) return 1f;
            if (isAssemblyPhase) return 1f;

            return (float)builtSegments / neededSegments;
        }

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

            // Pre-build 4 segments for free to establish the starting line so the crawler
            // has room to move and doesn't sit on top of the base.
            for (int i = 0; i < 4 && builtSegments < neededSegments; i++)
            {
                SpawnSegmentPhysically(true); // true = free
            }

            SpawnForge();
        }

        private void SpawnSegmentPhysically(bool isFree)
        {
            if (builtSegments >= neededSegments) return;

            // Drain cost if not free
            if (!isFree && Supplies.Biomass != null && Supplies.Biomass.TryGetValue(Owner.Player1, out int b))
            {
                int nextVal = Mathf.Max(0, b - segmentBiomassCost);
                Supplies.Biomass[Owner.Player1] = nextVal;
                Supplies.RaiseBiomassChanged(Owner.Player1, nextVal);
            }

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

            // Periodically expose minable deposits alongside the line.
            TryExposeSupplyLineDeposits(spawnPos, dir);
        }

        public bool BuildNextSegment()
        {
            if (!HasPendingSegments()) return false;
            if (!CanAffordNextSegment()) return false;

            SpawnSegmentPhysically(false); // false = not free
            return true;
        }

        private void TryExposeSupplyLineDeposits(Vector3 segmentPos, Vector3 dir)
        {
            segmentsSinceLastDeposit++;
            if (segmentsSinceLastDeposit < DEPOSIT_INTERVAL) return;
            segmentsSinceLastDeposit = 0;

            ExposeForgeDeposits();
        }

        public void ExposeForgeDeposits()
        {
            LoadDepositPrefabs();

            // Anchor on the foundry crawler so fuel appears relative to the machine.
            Vector3 anchor = spawnedForge != null ? spawnedForge.transform.position : startPosition;

            Vector3 travelDir = targetPosition - startPosition;
            travelDir = travelDir.sqrMagnitude > 0.001f ? travelDir.normalized : Vector3.forward;

            Vector3 side = Vector3.Cross(travelDir, Vector3.up).normalized;
            if (side.sqrMagnitude < 0.001f) side = Vector3.right;

            // A Regolith + Iron pair ahead of the foundry...
            Vector3 ahead = anchor + travelDir * DEPOSIT_AHEAD_OFFSET;
            ExposeDeposit(regolithDepositPrefab, ahead + side * DEPOSIT_PAIR_SPREAD, "Regolith");
            ExposeDeposit(ironDepositPrefab, ahead - side * DEPOSIT_PAIR_SPREAD, "Iron");

            // ...and another pair behind it.
            Vector3 behind = anchor - travelDir * DEPOSIT_AHEAD_OFFSET;
            ExposeDeposit(regolithDepositPrefab, behind + side * DEPOSIT_PAIR_SPREAD, "Regolith");
            ExposeDeposit(ironDepositPrefab, behind - side * DEPOSIT_PAIR_SPREAD, "Iron");
        }

        private void SpawnForge()
        {
            GameObject foundryPrefab = null;
#if UNITY_EDITOR
            foundryPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Units/Buildings/Foundry/Foundry.prefab");
#endif
            if (foundryPrefab == null) foundryPrefab = Resources.Load<GameObject>("Buildings/Foundry");

            if (foundryPrefab != null)
            {
                Vector3 toTarget = targetPosition - startPosition;
                Vector3 spawnDir = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : Vector3.forward;
                Vector3 forgeSpawnPos = startPosition + spawnDir * 8f;

                Ray forgeGroundRay = new Ray(forgeSpawnPos + Vector3.up * 50f, Vector3.down);
                if (Physics.Raycast(forgeGroundRay, out RaycastHit forgeGroundHit, 100f, LayerMask.GetMask("Default", "Terrain")))
                {
                    forgeSpawnPos.y = forgeGroundHit.point.y;
                }

                spawnedForge = Instantiate(foundryPrefab, forgeSpawnPos, Quaternion.identity);
                spawnedForge.name = "FoundryCrawler";
                
                var crawler = spawnedForge.GetComponent<FoundryCrawler>();
                if (crawler == null) crawler = spawnedForge.AddComponent<FoundryCrawler>();
                
                crawler.PipelineManager = this;
                crawler.targetPosition = targetPosition;
                crawler.movementSpeed = 0.5f;
                crawler.isOnPipeline = true;

                // Ensure the crawler is properly initialized and not permanently stuffed full
                crawler.ResetHoppers();

                ExposeInitialDeposits();
            }
            else
            {
                Debug.LogWarning("[Pipeline] Could not load Foundry prefab to spawn forge!");
            }
        }

        private void ExposeInitialDeposits()
        {
            LoadDepositPrefabs();

            Vector3 travelDir = targetPosition - startPosition;
            travelDir = travelDir.sqrMagnitude > 0.001f ? travelDir.normalized : Vector3.forward;

            Vector3 side = Vector3.Cross(travelDir, Vector3.up).normalized;
            if (side.sqrMagnitude < 0.001f) side = Vector3.right;

            for (int i = 1; i <= 2; i++)
            {
                Vector3 spot = startPosition + travelDir * (DEPOSIT_AHEAD_OFFSET * i);
                ExposeDeposit(regolithDepositPrefab, spot + side * DEPOSIT_PAIR_SPREAD, "Regolith");
                ExposeDeposit(ironDepositPrefab, spot - side * DEPOSIT_PAIR_SPREAD, "Iron");
            }
        }

        private void OnDestroy()
        {
            if (!IsCompleted && spawnedForge != null) Destroy(spawnedForge);
        }

        private Vector3 FindNearestCompletedCommandCenter()
        {
            BaseBuilding best = null;
            float minDistance = float.MaxValue;

            foreach (var b in BaseBuilding.ActiveBuildings)
            {
                if (b == null || b.Owner != Owner.Player1) continue;
                if (b.Progress.State != BuildingProgress.BuildingState.Completed) continue;
                if (b.name.Contains("Command", System.StringComparison.OrdinalIgnoreCase) || 
                    (b.BuildingSO != null && b.BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase)))
                {
                    float dist = Vector3.Distance(targetPosition, b.transform.position);
                    if (dist < minDistance) { minDistance = dist; best = b; }
                }
            }

            if (best != null) return best.transform.position;

            var allBuildings = Object.FindObjectsByType<BaseBuilding>(FindObjectsInactive.Include);
            foreach (var b in allBuildings)
            {
                if (b == null || b.Owner != Owner.Player1) continue;
                if (b.Progress.State != BuildingProgress.BuildingState.Completed) continue;

                float dist = Vector3.Distance(targetPosition, b.transform.position);
                if (dist < minDistance) { minDistance = dist; best = b; }
            }

            return best != null ? best.transform.position : targetPosition;
        }

        private void Update()
        {
            if (!IsCompleted && !isAssemblyPhase && builtSegments >= neededSegments)
            {
                var crawler = spawnedForge != null ? spawnedForge.GetComponent<FoundryCrawler>() : null;
                if (crawler == null || crawler.HasReachedTarget)
                {
                    Debug.Log($"[Expansion] Growth complete and crawler arrived at target. Starting boot-up sequence for {sector.Center}");
                    StartCoroutine(BootUpSequence());
                }
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

        private void LoadDepositPrefabs()
        {
#if UNITY_EDITOR
            if (regolithDepositPrefab == null) regolithDepositPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Gatherable Supplies/Regolith.prefab");
            if (ironDepositPrefab == null) ironDepositPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Gatherable Supplies/Iron.prefab");
#endif
            if (regolithDepositPrefab == null) regolithDepositPrefab = Resources.Load<GameObject>("Gatherable Supplies/Regolith");
            if (ironDepositPrefab == null) ironDepositPrefab = Resources.Load<GameObject>("Gatherable Supplies/Iron");
        }

        private void ExposeDeposit(GameObject prefab, Vector3 position, string label)
        {
            if (prefab == null) return;

            Ray ray = new Ray(position + Vector3.up * 50f, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit groundHit, 100f, LayerMask.GetMask("Default", "Terrain")))
            {
                position.y = groundHit.point.y;
            }

            GameObject deposit = Instantiate(prefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            deposit.name = label;
            
            if (PlanetGenerator.Instance != null)
            {
                deposit.transform.SetParent(PlanetGenerator.Instance.transform);
            }

            AddPersistentLabel(deposit, label);
        }

        private void AddPersistentLabel(GameObject deposit, string label)
        {
            GameObject labelGo = new GameObject("Deposit Label");
            labelGo.transform.SetParent(deposit.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 3f, 0f);

            TMPro.TextMeshPro text = labelGo.AddComponent<TMPro.TextMeshPro>();
            text.text = label;
            text.fontSize = 4;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.color = label == "Iron" ? new Color(0.7f, 0.78f, 0.9f) : new Color(0.95f, 0.78f, 0.5f);

            RectTransform rt = labelGo.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(6f, 2f);

            labelGo.AddComponent<GameDevTV.RTS.UI.Components.FaceCamera>();
        }

        public void HandleSegmentDestroyed(int index)
        {
            for (int i = segments.Count - 1; i >= index; i--)
            {
                if (segments[i] != null) Destroy(segments[i]);
                segments.RemoveAt(i);
            }
            builtSegments = segments.Count;
        }

        public void CancelExpansion()
        {
            foreach (var seg in segments) if (seg != null) Destroy(seg);
            segments.Clear();
            if (ColonyExpansionManager.Instance != null) ColonyExpansionManager.Instance.VetoSector(sector);
            Destroy(gameObject);
        }

        private IEnumerator BootUpSequence()
        {
            isAssemblyPhase = true;

            yield return new WaitForSeconds(5.0f);

            GameObject cc = Instantiate(realCommandPostPrefab, targetPosition, Quaternion.identity);
            cc.SetActive(true); // Ensure it's active immediately
            
            if (cc.TryGetComponent(out BaseBuilding building))
            {
                building.Owner = Owner.Player1;
                building.enabled = true;
                building.CompleteConstruction();
                building.ClearQueue(); // Wipe any default queue state

                // A freshly established command center builds a Probe drone first.
                AbstractUnitSO probeSO = Resources.Load<AbstractUnitSO>("Units/Probe");
                if (probeSO != null)
                {
                    building.BuildPriorityUnlockable(probeSO);
                    Debug.Log("[Expansion] Queued starter probe as PRIORITY first item in the new Command Post queue.");
                }
                else
                {
                    Debug.LogWarning("[Expansion] Could not load Probe SO at Resources/Units/Probe to auto-build first probe.");
                }
            }

            foreach (var seg in segments)
            {
                if (seg != null)
                {
                    var segComp = seg.GetComponent<PipelineSegment>();
                    if (segComp != null) Destroy(segComp);
                }
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
