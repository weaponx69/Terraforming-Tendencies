using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.Environment;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Represents a human colonist on Mars.
    /// Can enter buildings to stay safe, and needs oxygen/spacesuit when outside.
    /// </summary>
    public class MartianColonist : MonoBehaviour
    {
        public static MartianColonist Instance { get; private set; }

        [Header("Survival Settings")]
        public float MaxOxygen = 100f;
        public float CurrentOxygen = 100f;
        public float OxygenDepletionRate = 12f; // Drains in ~8 seconds without suit/LS
        public float OxygenRefillRate = 30f;    // Refills quickly inside

        [Header("Equipment")]
        public bool HasSpacesuit = false;

        private AbstractCommandable commandable;
        private NavMeshAgent agent;
        private Collider col;
        private BaseBuilding currentBuilding;
        private bool isInside = false;

        private GameObject trackerGo;
        private UnityEngine.UI.Text trackerText;

        private void Awake()
        {
            Instance = this;
            commandable = GetComponent<AbstractCommandable>();
            agent = GetComponent<NavMeshAgent>();
            col = GetComponent<Collider>();
        }

        private void Start()
        {
            // Scale the unit down significantly so it represents a micro-human fitting inside buildings
            transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            
            if (commandable != null)
            {
                commandable.gameObject.name = "Colony Commander";
                commandable.Owner = Owner.Player1;
            }

            // Sync initial spacesuit tech status from draft manager
            HasSpacesuit = GameDevTV.RTS.Player.BlueprintDraftManager.HasSpacesuits;

            CreateTrackerBadge();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void CreateTrackerBadge()
        {
            // Create a billboard world-space canvas that stays active no matter what to show vitals
            trackerGo = new GameObject("VitalsTracker_UI");
            trackerGo.transform.SetParent(transform, false);
            
            // Local Y offset adjusted for the 0.2 scale (15.0f local Y is 3.0f world units)
            trackerGo.transform.localPosition = new Vector3(0f, 15f, 0f);

            Canvas canvas = trackerGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            
            // Base UI scale
            trackerGo.transform.localScale = Vector3.one * 0.075f;

            // Make it billboard to face the player camera
            trackerGo.AddComponent<FaceCamera>();

            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(trackerGo.transform, false);
            
            trackerText = textGO.AddComponent<UnityEngine.UI.Text>();
            trackerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (trackerText.font == null) trackerText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            trackerText.fontSize = 24;
            trackerText.alignment = TextAnchor.MiddleCenter;
            trackerText.color = Color.cyan;

            RectTransform rect = trackerText.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400f, 60f);
        }

        private void Update()
        {
            if (isInside)
            {
                // Refill oxygen
                CurrentOxygen = Mathf.Min(CurrentOxygen + OxygenRefillRate * Time.deltaTime, MaxOxygen);
                UpdateTrackerText();
                return;
            }

            // Tube path detection: check if walking along any connected PowerNodes (tubes)
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

                    float distToTube = DistanceToSegment(transform.position, node.transform.position, neighbor.transform.position);
                    if (distToTube <= 1.5f) // Within tube radius!
                    {
                        insideTube = true;
                        break;
                    }
                }
                if (insideTube) break;
            }

            bool inLifeSupport = false;
            var nodes = FindObjectsByType<GameDevTV.RTS.Environment.LifeSupportNode>(FindObjectsInactive.Exclude);
            foreach (var node in nodes)
            {
                if (node != null && Vector3.Distance(transform.position, node.transform.position) <= node.Radius)
                {
                    inLifeSupport = true;
                    break;
                }
            }

            // Apply tech level parameters
            bool solidTech = GameDevTV.RTS.Player.BlueprintDraftManager.TubesAreSolid;

            if (insideTube)
            {
                // Inflatable tubes leak slightly (0.3/s), Solid tubes seal perfectly (0.0/s)
                float depletionRate = solidTech ? 0f : 0.3f;
                CurrentOxygen = Mathf.Max(CurrentOxygen - depletionRate * Time.deltaTime, 0f);
                if (CurrentOxygen <= 0f && commandable != null)
                {
                    commandable.TakeDamage(Mathf.RoundToInt(20f * Time.deltaTime));
                }

                // Speed boost inside solid pressurized tubes
                if (agent != null)
                {
                    agent.speed = solidTech ? 12f : 7f; 
                }
            }
            else if (!inLifeSupport)
            {
                // Spacesuits are unlocked by default: Flimsy (2.0/s) vs Armored/Solid (0.5/s)
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
                // Safe inside Life Support zone
                CurrentOxygen = Mathf.Min(CurrentOxygen + 5f * Time.deltaTime, MaxOxygen);
                if (agent != null) agent.speed = 5f;
            }

            // Enter building detection:
            if (agent != null && agent.isActiveAndEnabled && agent.hasPath)
            {
                var buildings = FindObjectsByType<BaseBuilding>(FindObjectsInactive.Exclude);
                foreach (var b in buildings)
                {
                    if (b != null && b.Progress.State == BuildingProgress.BuildingState.Completed)
                    {
                        float distToBuilding = Vector3.Distance(transform.position, b.transform.position);
                        if (distToBuilding <= 2.2f && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
                        {
                            EnterBuilding(b);
                            break;
                        }
                    }
                }
            }

            UpdateTrackerText();
        }

        private void UpdateTrackerText()
        {
            if (trackerText == null) return;

            int o2Percent = Mathf.RoundToInt((CurrentOxygen / MaxOxygen) * 100f);
            bool solidTech = GameDevTV.RTS.Player.BlueprintDraftManager.TubesAreSolid;
            
            if (isInside && currentBuilding != null)
            {
                string bName = currentBuilding.BuildingSO != null ? currentBuilding.BuildingSO.Name : currentBuilding.gameObject.name;
                trackerText.text = $"👨‍🚀 CMD [Sheltered in {bName}] (O2: {o2Percent}%)";
                trackerText.color = Color.green;
            }
            else
            {
                // Check if currently inside a tube to show custom text
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

                        if (DistanceToSegment(transform.position, node.transform.position, neighbor.transform.position) <= 1.5f)
                        {
                            insideTube = true;
                            break;
                        }
                    }
                    if (insideTube) break;
                }

                if (insideTube)
                {
                    string tType = solidTech ? "Solid" : "Inflatable";
                    trackerText.text = $"👨‍🚀 CMD [Inside {tType} Tube] (O2: {o2Percent}%)";
                    trackerText.color = Color.blue;
                }
                else
                {
                    string sType = solidTech ? "Solid Suit" : "Flimsy Suit";
                    if (o2Percent < 30)
                    {
                        trackerText.text = $"👨‍🚀 CMD [{sType}] (O2: {o2Percent}% ⚠️)";
                        trackerText.color = Color.red;
                    }
                    else
                    {
                        trackerText.text = $"👨‍🚀 CMD [{sType}] (O2: {o2Percent}%)";
                        trackerText.color = Color.cyan;
                    }
                }
            }
        }

        public void EnterBuilding(BaseBuilding building)
        {
            if (building == null) return;
            currentBuilding = building;
            isInside = true;

            // Stop movement
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.ResetPath();
                agent.enabled = false;
            }

            if (col != null) col.enabled = false;

            // Snap physical position to the center of the building
            transform.position = building.transform.position;

            // Hide visuals
            SetVisualsActive(false);

            // Move the vital tracker badge higher so it floats above the building's roof mesh (Y 30.0f is 6.0f world units)
            if (trackerGo != null)
            {
                trackerGo.transform.localPosition = new Vector3(0f, 30f, 0f);
            }

            Debug.Log($"[MartianColonist] Entered building: {building.gameObject.name}");
        }

        public void ExitBuilding()
        {
            if (!isInside || currentBuilding == null) return;

            isInside = false;

            // Position outside the building
            Vector3 spawnPos = currentBuilding.transform.position + Vector3.forward * 3f;
            if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            {
                spawnPos = hit.position;
            }
            transform.position = spawnPos;

            if (agent != null)
            {
                agent.enabled = true;
            }
            if (col != null) col.enabled = true;

            // Show visuals
            SetVisualsActive(true);

            // Restore lower height offset when walking outside (Y 15.0f is 3.0f world units)
            if (trackerGo != null)
            {
                trackerGo.transform.localPosition = new Vector3(0f, 15f, 0f);
            }

            currentBuilding = null;
            Debug.Log("[MartianColonist] Exited building onto Martian surface.");
        }

        private void SetVisualsActive(bool active)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                // Ignore the tracker UI canvas/renderer completely so vitals are always visible!
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

        public bool IsAlive => commandable != null && commandable.CurrentHealth > 0;
        public bool IsInside => isInside;
        public BaseBuilding CurrentBuilding => currentBuilding;
    }
}
