using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using Unity.Behavior;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Represents a skilled Colony Engineer who automatically wanders around the base,
    /// inspecting buildings, and rushes to repair any damaged building or pressurized tube.
    /// </summary>
    public class ColonyEngineer : MonoBehaviour
    {
        public static List<ColonyEngineer> ActiveEngineers = new List<ColonyEngineer>();

        [Header("Survival Settings")]
        public float MaxOxygen = 100f;
        public float CurrentOxygen = 100f;
        public float OxygenDepletionRate = 12f;
        public float OxygenRefillRate = 30f;

        [Header("Equipment")]
        public bool HasSpacesuit = true;

        private AbstractCommandable commandable;
        private NavMeshAgent agent;
        private Collider col;
        private BaseBuilding currentBuilding;
        private bool isInside = false;

        [Header("Transit Settings")]
        private bool isTransit = false;
        private List<Vector3> transitPoints = new List<Vector3>();
        private int transitIndex = 0;
        private BaseBuilding transitTargetBuilding;
        private float transitSpeed = 8f;

        [Header("Food & Starvation")]
        private float foodTimer = 0f;
        private float starvationDamageTimer = 0f;
        private bool isStarving = false;

        [Header("Inspection & Repair Loop")]
        private AbstractCommandable repairTarget;
        private float wanderTimer = 0f;
        private float wanderWaitTime = 8f;
        private bool isWaitingInBuilding = false;

        private GameObject trackerGo;
        private UnityEngine.UI.Text trackerText;

        private void Awake()
        {
            ActiveEngineers.Add(this);
            commandable = GetComponent<AbstractCommandable>();
            agent = GetComponent<NavMeshAgent>();
            col = GetComponent<Collider>();
        }

        private void Start()
        {
            // Scale the unit down
            transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            
            if (commandable != null)
            {
                commandable.gameObject.name = "Colony Engineer";
            }

            // Disable standard military behaviors to make them purely peaceful
            var military = GetComponent<BaseMilitaryUnit>();
            if (military != null) military.enabled = false;

            // Disable background Behavior Graph AI so they don't override manual/custom commands
            var bgAgent = GetComponent<BehaviorGraphAgent>();
            if (bgAgent != null) bgAgent.enabled = false;

            CreateTrackerBadge();
            
            // Start outside on the surface and begin inspecting/repairing immediately
            wanderTimer = 1.5f; // Short delay before first inspect action
        }

        private void OnDestroy()
        {
            ActiveEngineers.Remove(this);
        }

        private void CreateTrackerBadge()
        {
            trackerGo = new GameObject("EngineerTracker_UI");
            trackerGo.transform.SetParent(transform, false);
            trackerGo.transform.localPosition = new Vector3(0f, 15f, 0f);

            Canvas canvas = trackerGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            trackerGo.transform.localScale = Vector3.one * 0.075f;
            trackerGo.AddComponent<FaceCamera>();

            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(trackerGo.transform, false);
            
            trackerText = textGO.AddComponent<UnityEngine.UI.Text>();
            trackerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (trackerText.font == null) trackerText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            trackerText.fontSize = 24;
            trackerText.alignment = TextAnchor.MiddleCenter;
            trackerText.color = Color.yellow; // Distinct yellow color for engineers!

            RectTransform rect = trackerText.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400f, 60f);
        }

        private void Update()
        {
            UpdateFoodConsumption();

            if (isTransit)
            {
                UpdateTubeTransit();
                UpdateTrackerText();
                return;
            }

            // High Priority: Scan for any damaged structure
            CheckForRepairs();

            if (repairTarget != null)
            {
                HandleRepairBehavior();
            }
            else
            {
                HandleInspectionWander();
            }

            if (isInside)
            {
                CurrentOxygen = Mathf.Min(CurrentOxygen + OxygenRefillRate * Time.deltaTime, MaxOxygen);
                UpdateTrackerText();
                return;
            }

            // Automatic Tube Transit triggers when moving on path
            TriggerTubeTransitCheck();

            // Vacuum Oxygen depletion logic
            UpdateOxygenDecay();

            // Enter building detection
            DetectEnterBuilding();

            UpdateTrackerText();
        }

        private void UpdateFoodConsumption()
        {
            Owner owner = Owner.Player1;
            foodTimer += Time.deltaTime;
            if (foodTimer >= 15f)
            {
                foodTimer = 0f;
                float currentFood = Supplies.Food.TryGetValue(owner, out float f) ? f : 0f;
                if (currentFood >= 1f)
                {
                    Supplies.UpdateFood(owner, currentFood - 1f);
                    isStarving = false;
                }
                else
                {
                    isStarving = true;
                }
            }

            if (isStarving)
            {
                starvationDamageTimer += Time.deltaTime;
                if (starvationDamageTimer >= 5f)
                {
                    starvationDamageTimer = 0f;
                    if (commandable != null)
                    {
                        commandable.TakeDamage(10);
                    }
                }
            }
            else
            {
                starvationDamageTimer = 0f;
            }
        }

        private void CheckForRepairs()
        {
            // Reset target if it is fully healed or destroyed
            if (repairTarget != null && (repairTarget == null || repairTarget.CurrentHealth >= repairTarget.MaxHealth))
            {
                repairTarget = null;
            }

            if (repairTarget == null)
            {
                // Scan buildings first
                var buildings = FindObjectsByType<BaseBuilding>(FindObjectsInactive.Exclude);
                foreach (var b in buildings)
                {
                    if (b != null && b.Progress.State == BuildingProgress.BuildingState.Completed && b.CurrentHealth < b.MaxHealth)
                    {
                        repairTarget = b;
                        isWaitingInBuilding = false;
                        if (isInside) ExitBuilding();
                        return;
                    }
                }

                // Scan tubes
                var tubes = FindObjectsByType<PressurizedTube>(FindObjectsInactive.Exclude);
                foreach (var t in tubes)
                {
                    if (t != null && t.CurrentHealth < t.MaxHealth)
                    {
                        repairTarget = t;
                        isWaitingInBuilding = false;
                        if (isInside) ExitBuilding();
                        return;
                    }
                }
            }
        }

        private void HandleRepairBehavior()
        {
            if (repairTarget == null) return;

            float dist = Vector3.Distance(transform.position, repairTarget.transform.position);
            float maxRepairDist = repairTarget is BaseBuilding ? 6.5f : 3.0f;
            if (dist <= maxRepairDist)
            {
                // Stand next to it and repair
                if (agent != null && agent.isActiveAndEnabled) agent.ResetPath();

                wanderTimer += Time.deltaTime;
                if (wanderTimer >= 0.5f)
                {
                    wanderTimer = 0f;
                    if (repairTarget is BaseBuilding building)
                    {
                        building.Heal(5);
                    }
                    else if (repairTarget is PressurizedTube tube)
                    {
                        tube.DamageTube(-5); // Negative damage heals
                    }
                }
            }
            else
            {
                if (isInside)
                {
                    ExitBuilding();
                }

                // Move towards repair site
                if (agent != null && agent.enabled && !agent.hasPath)
                {
                    agent.SetDestination(repairTarget.transform.position);
                }
            }
        }

        private void HandleInspectionWander()
        {
            if (isInside)
            {
                if (isWaitingInBuilding)
                {
                    wanderTimer += Time.deltaTime;
                    if (wanderTimer >= wanderWaitTime)
                    {
                        ExitBuilding();
                    }
                }
                return;
            }

            // Inspecting/Wandering - only search for a new building if not currently moving
            if (agent != null && agent.enabled && (agent.hasPath || agent.pathPending))
            {
                return;
            }

            wanderTimer += Time.deltaTime;
            if (wanderTimer >= 2f)
            {
                wanderTimer = 0f;
                var buildings = FindObjectsByType<BaseBuilding>(FindObjectsInactive.Exclude);
                List<BaseBuilding> completedBuildings = new List<BaseBuilding>();
                foreach (var b in buildings)
                {
                    if (b != null && b.Progress.State == BuildingProgress.BuildingState.Completed)
                    {
                        completedBuildings.Add(b);
                    }
                }

                if (completedBuildings.Count > 0)
                {
                    BaseBuilding target = completedBuildings[Random.Range(0, completedBuildings.Count)];
                    if (agent != null && agent.enabled)
                    {
                        agent.SetDestination(target.transform.position);
                    }
                }
            }
        }

        private void TriggerTubeTransitCheck()
        {
            if (agent != null && agent.isActiveAndEnabled && agent.hasPath)
            {
                Vector3 dest = agent.destination;
                BaseBuilding targetBuilding = null;
                var buildings = FindObjectsByType<BaseBuilding>(FindObjectsInactive.Exclude);
                foreach (var b in buildings)
                {
                    if (b != null && b.Progress.State == BuildingProgress.BuildingState.Completed)
                    {
                        if (Vector3.Distance(dest, b.transform.position) <= 4.0f)
                        {
                            targetBuilding = b;
                            break;
                        }
                    }
                }

                if (targetBuilding != null)
                {
                    BaseBuilding startBuilding = null;
                    foreach (var b in buildings)
                    {
                        if (b != null && b != targetBuilding && b.Progress.State == BuildingProgress.BuildingState.Completed)
                        {
                            if (Vector3.Distance(transform.position, b.transform.position) <= 4.0f)
                            {
                                startBuilding = b;
                                break;
                            }
                        }
                    }

                    if (startBuilding != null)
                    {
                        if (startBuilding.TryGetComponent(out PowerNode nodeA) && targetBuilding.TryGetComponent(out PowerNode nodeB))
                        {
                            if (nodeA.ConnectedNodes.Contains(nodeB) && nodeA.visualCords.ContainsKey(nodeB))
                            {
                                StartTubeTransit(startBuilding, targetBuilding, nodeA.visualCords[nodeB]);
                            }
                        }
                    }
                }
            }
        }

        private void StartTubeTransit(BaseBuilding startBuilding, BaseBuilding targetBuilding, GameObject tubeGO)
        {
            if (tubeGO == null) return;
            var lr = tubeGO.GetComponent<LineRenderer>();
            if (lr == null) return;

            isTransit = true;
            transitTargetBuilding = targetBuilding;
            transitPoints.Clear();

            for (int i = 0; i < lr.positionCount; i++)
            {
                transitPoints.Add(lr.GetPosition(i));
            }
            if (Vector3.Distance(transform.position, transitPoints[0]) > Vector3.Distance(transform.position, transitPoints[transitPoints.Count - 1]))
            {
                transitPoints.Reverse();
            }

            transitIndex = 0;
            
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.ResetPath();
                agent.enabled = false;
            }
            if (col != null) col.enabled = false;

            bool solidTech = BlueprintDraftManager.TubesAreSolid;
            transitSpeed = solidTech ? 12f : 7f;
        }

        private void UpdateTubeTransit()
        {
            if (transitPoints == null || transitPoints.Count == 0)
            {
                isTransit = false;
                return;
            }

            Vector3 targetPt = transitPoints[transitIndex];
            targetPt.y = transform.position.y; 

            transform.position = Vector3.MoveTowards(transform.position, targetPt, transitSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, targetPt) <= 0.15f)
            {
                transitIndex++;
                if (transitIndex >= transitPoints.Count)
                {
                    isTransit = false;
                    EnterBuilding(transitTargetBuilding);
                }
            }
        }

        private void UpdateOxygenDecay()
        {
            bool insideTube = false;
            var allPowerNodes = FindObjectsByType<PowerNode>(FindObjectsInactive.Exclude);
            HashSet<string> checkedPairs = new HashSet<string>();
            foreach (var node in allPowerNodes)
            {
                if (node == null) continue;
                foreach (var neighbor in node.ConnectedNodes)
                {
                    if (neighbor == null) continue;
                    string key = node.GetInstanceID() < neighbor.GetInstanceID() 
                        ? $"{node.GetInstanceID()}_{neighbor.GetInstanceID()}" 
                        : $"{neighbor.GetInstanceID()}_{node.GetInstanceID()}";
                    if (checkedPairs.Contains(key)) continue;
                    checkedPairs.Add(key);

                        float distToA = Vector3.Distance(transform.position, node.transform.position);
                        float distToB = Vector3.Distance(transform.position, neighbor.transform.position);
                        if (distToA > 4.5f && distToB > 4.5f && DistanceToSegment(transform.position, node.transform.position, neighbor.transform.position) <= 1.5f)
                        {
                            insideTube = true;
                            break;
                        }
                }
                if (insideTube) break;
            }

            bool inLifeSupport = false;
            var nodes = FindObjectsByType<LifeSupportNode>(FindObjectsInactive.Exclude);
            foreach (var node in nodes)
            {
                if (node != null && Vector3.Distance(transform.position, node.transform.position) <= node.Radius)
                {
                    inLifeSupport = true;
                    break;
                }
            }

            bool solidTech = BlueprintDraftManager.TubesAreSolid;

            if (insideTube)
            {
                float depletionRate = solidTech ? 0f : 0.3f;
                CurrentOxygen = Mathf.Max(CurrentOxygen - depletionRate * Time.deltaTime, 0f);
                if (CurrentOxygen <= 0f && commandable != null)
                {
                    commandable.TakeDamage(Mathf.RoundToInt(20f * Time.deltaTime));
                }
                if (agent != null) agent.speed = solidTech ? 12f : 7f;
            }
            else if (!inLifeSupport)
            {
                float depletionRate = solidTech ? 0.5f : 2.0f;
                CurrentOxygen = Mathf.Max(CurrentOxygen - depletionRate * Time.deltaTime, 0f);
                if (CurrentOxygen <= 0f && commandable != null)
                {
                    commandable.TakeDamage(Mathf.RoundToInt(20f * Time.deltaTime));
                }
                if (agent != null) agent.speed = 5f;
            }
            else
            {
                CurrentOxygen = Mathf.Min(CurrentOxygen + 5f * Time.deltaTime, MaxOxygen);
                if (agent != null) agent.speed = 5f;
            }
        }

        private void DetectEnterBuilding()
        {
            if (agent != null && agent.isActiveAndEnabled && agent.hasPath)
            {
                var buildings = FindObjectsByType<BaseBuilding>(FindObjectsInactive.Exclude);
                foreach (var b in buildings)
                {
                    if (b != null && b.Progress.State == BuildingProgress.BuildingState.Completed)
                    {
                        float distToBuilding = Vector3.Distance(transform.position, b.transform.position);
                        if (distToBuilding <= 6.0f && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
                        {
                            EnterBuilding(b);
                            break;
                        }
                    }
                }
            }
        }

        private void UpdateTrackerText()
        {
            if (trackerText == null) return;

            int o2Percent = Mathf.RoundToInt((CurrentOxygen / MaxOxygen) * 100f);
            bool solidTech = BlueprintDraftManager.TubesAreSolid;
            
            if (isInside && currentBuilding != null)
            {
                string bName = currentBuilding.BuildingSO != null ? currentBuilding.BuildingSO.Name : currentBuilding.gameObject.name;
                string state = isStarving ? "Starving" : (repairTarget != null ? "Resting" : "Inspecting");
                trackerText.text = $"👨‍🔧 ENG [{state} in {bName}] (O2: {o2Percent}%)";
                trackerText.color = isStarving ? Color.red : Color.green;
            }
            else if (isTransit)
            {
                string tType = solidTech ? "Solid" : "Inflatable";
                string state = isStarving ? "Starving" : "Travelling";
                trackerText.text = $"👨‍🔧 ENG [{state} in {tType} Tube] (O2: {o2Percent}%)";
                trackerText.color = isStarving ? Color.red : Color.blue;
            }
            else
            {
                string state = isStarving ? "Starving" : (repairTarget != null ? "Repairing" : "Moving");
                string sType = solidTech ? "Solid Suit" : "Flimsy Suit";
                trackerText.text = $"👨‍🔧 ENG [{state} / {sType}] (O2: {o2Percent}%)";
                trackerText.color = isStarving ? Color.red : Color.yellow;
            }
        }

        public void EnterBuilding(BaseBuilding building)
        {
            if (building == null) return;
            currentBuilding = building;
            isInside = true;
            isWaitingInBuilding = true;
            wanderTimer = 0f;
            wanderWaitTime = Random.Range(6f, 15f);

            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.ResetPath();
                agent.enabled = false;
            }
            if (col != null) col.enabled = false;

            transform.position = building.transform.position;
            SetVisualsActive(false);

            if (trackerGo != null)
            {
                trackerGo.transform.localPosition = new Vector3(0f, 30f, 0f);
            }
        }

        public void ExitBuilding()
        {
            if (!isInside || currentBuilding == null) return;

            isInside = false;
            isWaitingInBuilding = false;

            Vector3 spawnPos = currentBuilding.transform.position + Vector3.forward * 3f;
            if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            {
                spawnPos = hit.position;
            }

            if (agent != null)
            {
                agent.enabled = true;
                agent.Warp(spawnPos);
            }
            else
            {
                transform.position = spawnPos;
            }
            if (col != null) col.enabled = true;

            SetVisualsActive(true);

            if (trackerGo != null)
            {
                trackerGo.transform.localPosition = new Vector3(0f, 15f, 0f);
            }

            currentBuilding = null;
        }

        private void SetVisualsActive(bool active)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                string lowerName = r.gameObject.name.ToLower();
                if (lowerName.Contains("indicator") || lowerName.Contains("selection") || lowerName.Contains("tracker") || r is SpriteRenderer) continue;
                r.enabled = active;
            }
        }

        private float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            Vector3 ap = p - a;
            float t = Vector3.Dot(ap, ab) / Vector3.Dot(ab, ab);
            if (float.IsNaN(t) || float.IsInfinity(t)) return Vector3.Distance(p, a);
            t = Mathf.Clamp01(t);
            Vector3 closestPoint = a + t * ab;
            return Vector3.Distance(p, closestPoint);
        }
    }
}
