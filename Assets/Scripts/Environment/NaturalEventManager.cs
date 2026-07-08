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
        [SerializeField] private float firstWaveDelay = 20f;
        [Tooltip("Rest time between the end of one wave and the start of the next.")]
        [SerializeField] private float timeBetweenWaves = 30f;

        [Header("Wave Content")]
        [Tooltip("Number of events in the first wave.")]
        [SerializeField] private int baseEventsPerWave = 3;
        [Tooltip("Extra events added to each subsequent wave.")]
        [SerializeField] private int eventsAddedPerWave = 1;
        [Tooltip("Delay between individual events within a wave.")]
        [SerializeField] private float eventInterval = 1.5f;

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

            var existingManager = FindAnyObjectByType<NaturalEventManager>();
            if (existingManager != null)
            {
                Debug.Log("[NaturalEventManager.DIAG] Manager already exists, starting assault on existing manager.");
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
                Destroy(gameObject);
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

        private IEnumerator WaveLoop()
        {
            yield return new WaitForSeconds(firstWaveDelay);

            while (true)
            {
                CurrentWave++;
                yield return RunWave(CurrentWave);
                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }

        private IEnumerator RunWave(int waveNumber)
        {
            int count = baseEventsPerWave + eventsAddedPerWave * (waveNumber - 1);
            for (int i = 0; i < count; i++)
            {
                SpawnEvent();
                yield return new WaitForSeconds(eventInterval);
            }
        }

        private void SpawnEvent()
        {
            Vector3 targetPos = GetTargetPosition();

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

            if (pool.Count > 0)
            {
                GameObject prefab = pool[Random.Range(0, pool.Count)];
                if (prefab != null)
                {
                    Instantiate(prefab, targetPos, Quaternion.identity);
                    return;
                }
            }

            // Fallback: create a simple meteor from a sphere primitive
            CreateFallbackMeteor(targetPos);
        }

        /// <summary>
        /// Creates a simple sphere meteor at runtime when no prefab is assigned.
        /// </summary>
        private void CreateFallbackMeteor(Vector3 targetPos)
        {
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
                return colony[Random.Range(0, colony.Count)].transform.position;
            }
            return GetRandomPlanetPosition();
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
