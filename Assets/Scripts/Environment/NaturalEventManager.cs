using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Periodically unleashes waves of "natural events" (meteor strikes, etc.) against
    /// the terraforming colony. Each event either targets a random player-owned structure
    /// or a random spot on the current planet. Waves escalate over time.
    ///
    /// Auto-initializes when the first player building is constructed — no manual scene setup needed.
    /// </summary>
    public class NaturalEventManager : MonoBehaviour
    {
        [Header("Wave Timing")]
        [Tooltip("Delay before the very first wave begins.")]
        [SerializeField] private float firstWaveDelay = 60f;
        [Tooltip("Rest time between the end of one wave and the start of the next.")]
        [SerializeField] private float timeBetweenWaves = 50f;

        [Header("Wave Content")]
        [Tooltip("Number of events in the first wave.")]
        [SerializeField] private int baseEventsPerWave = 3;
        [Tooltip("Extra events added to each subsequent wave.")]
        [SerializeField] private int eventsAddedPerWave = 1;
        [Tooltip("Delay between individual events within a wave.")]
        [SerializeField] private float eventInterval = 8.0f;

        [Header("Targeting")]
        [Tooltip("Probability that an event homes in on a player structure instead of a random spot.")]
        [SerializeField, Range(0f, 1f)] private float chanceToTargetColony = 0.6f;

        [Header("Event Prefabs")]
        [Tooltip("Random event prefab is chosen from this list. Each needs a NaturalEventImpact component.")]
        [SerializeField] private GameObject[] eventPrefabs;

        [Header("Fallback Meteor Settings")]
        [Tooltip("Damage radius for fallback meteors.")]
        [SerializeField] private float fallbackDamageRadius = 5f;
        [Tooltip("Damage amount for fallback meteors.")]
        [SerializeField] private int fallbackDamageAmount = 25;
        [Tooltip("Max health for fallback meteors.")]
        [SerializeField] private int fallbackMaxHealth = 50;
        [Tooltip("Fall height for fallback meteors.")]
        [SerializeField] private float fallbackFallHeight = 40f;
        [Tooltip("Fall speed for fallback meteors.")]
        [SerializeField] private float fallbackFallSpeed = 35f;

        [Header("Debug")]
#pragma warning disable CS0414
        [SerializeField] private bool autoStart = false;
#pragma warning restore CS0414

        public int CurrentWave { get; private set; }

        private Coroutine waveRoutine;
        private bool hasStarted;

        private static List<GameObject> registeredHazards = new List<GameObject>();

        /// <summary>Registers a hazard prefab to the active natural event pool.</summary>
        public static void RegisterHazard(GameObject hazardPrefab)
        {
            if (hazardPrefab == null) return;
            if (!registeredHazards.Contains(hazardPrefab))
            {
                registeredHazards.Add(hazardPrefab);
                Debug.Log($"[NaturalEventManager] Registered new hazard: {hazardPrefab.name}. Total active hazards: {registeredHazards.Count}");
            }
        }

        // ── Auto-initialization ──────────────────────────────────────────────
        // Spawns the manager on scene load (via RuntimeInitializeOnLoadMethod),
        // then waits for the first player building to be constructed before
        // beginning the assault waves.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SubscribeToFirstBuilding()
        {
            Debug.Log("[NaturalEventManager.DIAG] SubscribeToFirstBuilding() called via RuntimeInitializeOnLoadMethod");
            
            registeredHazards.Clear();

            // This callback survives scene reloads, so the event bus subscription
            // inside it is established before any scene object exists.
            int subscriberCount = Bus<BuildingSpawnEvent>.OnEvent[Owner.Player1]?.GetInvocationList()?.Length ?? 0;
            Bus<BuildingSpawnEvent>.OnEvent[Owner.Player1] += OnFirstBuildingSpawned;
            Debug.Log($"[NaturalEventManager.DIAG] Subscribed OnFirstBuildingSpawned. Subscribers now: {subscriberCount + 1}");
        }

        private static void OnFirstBuildingSpawned(BuildingSpawnEvent evt)
        {
            Debug.Log($"[NaturalEventManager.DIAG] OnFirstBuildingSpawned received! Building={evt.Building?.name}, Owner={evt.Owner}");
            
            // Unsubscribe immediately so this only fires once
            Bus<BuildingSpawnEvent>.OnEvent[Owner.Player1] -= OnFirstBuildingSpawned;

            var existingManager = FindAnyObjectByType<NaturalEventManager>(FindObjectsInactive.Include);
            if (existingManager != null)
            {
                Debug.Log("[NaturalEventManager.DIAG] Manager already exists, starting assault on existing manager.");
                existingManager.gameObject.SetActive(true);
                existingManager.enabled = true;
                existingManager.BeginAssault();
                return;
            }

            GameObject go = new GameObject("NaturalEventManager");
            var manager = go.AddComponent<NaturalEventManager>();
            manager.BeginAssault();
            Debug.Log("[NaturalEventManager] Auto-spawned after first building. Waves will begin after firstWaveDelay.");
        }

        private void Awake()
        {
            // Ensure only one instance exists
            if (FindObjectsByType<NaturalEventManager>(FindObjectsInactive.Exclude).Length > 1)
            {
                Destroy(this);
            }
        }

        /// <summary>Starts the wave loop. Safe to call once.</summary>
        public void BeginAssault()
        {
            if (waveRoutine == null)
            {
                waveRoutine = StartCoroutine(WaveLoop());
            }
        }

        public void StopAssault()
        {
            if (waveRoutine != null)
            {
                StopCoroutine(waveRoutine);
                waveRoutine = null;
            }
        }

        /// <summary>Chance-based single threat during turn resolution.</summary>
        public void TryTurnThreat(int turnNumber)
        {
            if (waveRoutine == null) return;

            float chance = Mathf.Clamp(0.05f + turnNumber * 0.02f, 0.05f, 0.35f);
            if (Random.value > chance) return;

            Debug.Log($"[NaturalEventManager] Turn {turnNumber} threat triggered ({chance:P0} chance).");
            SpawnEvent();
        }

        private IEnumerator WaveLoop()
        {
            Debug.Log($"[NaturalEventManager] WaveLoop started. First wave in {firstWaveDelay}s. Time={Time.time:F1}s");
            yield return new WaitForSeconds(firstWaveDelay);

            while (true)
            {
                CurrentWave++;
                Debug.Log($"[NaturalEventManager] === WAVE {CurrentWave} STARTING === Time={Time.time:F1}s");
                yield return RunWave(CurrentWave);
                Debug.Log($"[NaturalEventManager] === WAVE {CurrentWave} COMPLETE === Time={Time.time:F1}s. Next wave in {timeBetweenWaves}s");
                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }

        private IEnumerator RunWave(int waveNumber)
        {
            int count = baseEventsPerWave + eventsAddedPerWave * (waveNumber - 1);
            Debug.Log($"[NaturalEventManager] Wave {waveNumber}: spawning {count} events at {eventInterval}s intervals");
            for (int i = 0; i < count; i++)
            {
                SpawnEvent();
                yield return new WaitForSeconds(eventInterval);
            }
        }

        private void SpawnEvent()
        {
            Vector3 targetPos = GetTargetPosition();
            Debug.Log($"[NaturalEventManager] SpawnEvent target=({targetPos.x:F1}, {targetPos.y:F1}, {targetPos.z:F1})");

            // Build combined pool of default event prefabs + dynamically unlocked card hazards
            List<GameObject> pool = new List<GameObject>();
            if (eventPrefabs != null)
            {
                foreach (var p in eventPrefabs)
                {
                    if (p != null) pool.Add(p);
                }
            }
            foreach (var p in registeredHazards)
            {
                if (p != null) pool.Add(p);
            }

            Debug.Log($"[NaturalEventManager] Event pool has {pool.Count} prefabs ({eventPrefabs?.Length ?? 0} default + {registeredHazards.Count} hazard(s))");

            if (pool.Count > 0)
            {
                GameObject prefab = pool[Random.Range(0, pool.Count)];
                Debug.Log($"[NaturalEventManager] Spawning event prefab: {(prefab != null ? prefab.name : "NULL")}");
                if (prefab != null)
                {
                    Instantiate(prefab, targetPos, Quaternion.identity);
                    return;
                }
            }

            // Fallback: create a simple meteor from a sphere primitive
            Debug.Log("[NaturalEventManager] Pool empty or null prefab — creating fallback meteor.");
            CreateFallbackMeteor(targetPos);
        }

        /// <summary>
        /// Creates a simple sphere meteor at runtime when no prefab is assigned.
        /// </summary>
        private void CreateFallbackMeteor(Vector3 targetPos)
        {
            Debug.Log($"[NaturalEventManager] Creating fallback meteor at ({targetPos.x:F1}, {targetPos.y:F1}, {targetPos.z:F1})");
            GameObject meteor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            meteor.name = "Meteor (fallback)";
            meteor.transform.position = targetPos;
            meteor.transform.localScale = Vector3.one * 2f;

            // Add the impact component
            var impact = meteor.AddComponent<NaturalEventImpact>();
            // Reflect to set serialized fields since they're private
            SetPrivateField(impact, "damageRadius", fallbackDamageRadius);
            SetPrivateField(impact, "damageAmount", fallbackDamageAmount);
            SetPrivateField(impact, "maxHealth", fallbackMaxHealth);
            SetPrivateField(impact, "currentHealth", fallbackMaxHealth);
            SetPrivateField(impact, "fallHeight", fallbackFallHeight);
            SetPrivateField(impact, "fallSpeed", fallbackFallSpeed);
            Debug.Log($"[NaturalEventManager] Fallback meteor created. damageAmount={fallbackDamageAmount}, damageRadius={fallbackDamageRadius}");
        }

        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(obj, value);
        }

        private Vector3 GetTargetPosition()
        {
            List<AbstractCommandable> colony = GetColonyTargets();
            if (colony.Count > 0 && Random.value <= chanceToTargetColony)
            {
                Vector3 pos = colony[Random.Range(0, colony.Count)].transform.position;
                Debug.Log($"[NaturalEventManager] Targeting colony at ({pos.x:F1}, {pos.y:F1}, {pos.z:F1}) — {colony.Count} targets available");
                return pos;
            }
            Vector3 randomPos = GetRandomPlanetPosition();
            Debug.Log($"[NaturalEventManager] Random planet position: ({randomPos.x:F1}, {randomPos.y:F1}, {randomPos.z:F1}) — colony targets: {colony.Count}");
            return randomPos;
        }

        private List<AbstractCommandable> GetColonyTargets()
        {
            List<AbstractCommandable> result = new();
            foreach (AbstractCommandable c in AbstractCommandable.ActiveCommandables)
            {
                if (c != null && c.Owner == Owner.Player1)
                {
                    result.Add(c);
                }
            }
            return result;
        }

        private Vector3 GetRandomPlanetPosition()
        {
            if (PlanetGenerator.Instance != null && PlanetGenerator.Instance.Config != null)
            {
                float width = PlanetGenerator.Instance.Config.MapWidth * PlanetGenerator.Instance.CellSize;
                float height = PlanetGenerator.Instance.Config.MapHeight * PlanetGenerator.Instance.CellSize;
                return new Vector3(Random.Range(0f, width), 0f, Random.Range(0f, height));
            }
            return Vector3.zero;
        }
    }
}
