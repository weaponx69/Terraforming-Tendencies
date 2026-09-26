using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Natural disasters (meteors, etc.). During Colony Acts, wall-clock waves are quiet;
    /// strikes come from <see cref="ColonyActDisaster"/> when strong cards are played.
    /// </summary>
    public class NaturalEventManager : MonoBehaviour
    {
        public static NaturalEventManager Instance { get; private set; }

        [Header("Wave Timing")]
        [SerializeField] private float firstWaveDelay = 180f;
        [SerializeField] private float timeBetweenWaves = 120f;

        [Header("Wave Content")]
        [SerializeField] private int baseEventsPerWave = 1;
        [SerializeField] private int eventsAddedPerWave = 1;
        [SerializeField] private float eventInterval = 15f;

        [Header("Targeting")]
        [SerializeField, Range(0f, 1f)] private float chanceToTargetColony = 0.6f;

        [Header("Event Prefabs")]
        [SerializeField] private GameObject[] eventPrefabs;

        [Header("Fallback Meteor Settings")]
        [SerializeField] private float fallbackDamageRadius = 5f;
        [SerializeField] private int fallbackDamageAmount = 25;
        [SerializeField] private int fallbackMaxHealth = 50;
        [SerializeField] private float fallbackFallHeight = 40f;
        [SerializeField] private float fallbackFallSpeed = 35f;

#pragma warning disable CS0414
        [SerializeField] private bool autoStart = false;
#pragma warning restore CS0414

        public int CurrentWave { get; private set; }

        private Coroutine waveRoutine;
        private static readonly List<GameObject> registeredHazards = new();

        public static void RegisterHazard(GameObject hazardPrefab)
        {
            if (hazardPrefab == null) return;
            if (!registeredHazards.Contains(hazardPrefab))
            {
                registeredHazards.Add(hazardPrefab);
                Debug.Log($"[NaturalEventManager] Registered hazard: {hazardPrefab.name}. Pool={registeredHazards.Count}");
            }
        }

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var existing = FindAnyObjectByType<NaturalEventManager>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Instance = existing;
                existing.gameObject.SetActive(true);
                return;
            }
            var go = new GameObject(nameof(NaturalEventManager));
            Instance = go.AddComponent<NaturalEventManager>();
        }

        public static void StopAssaultIfAny()
        {
            var mgr = Instance != null
                ? Instance
                : FindAnyObjectByType<NaturalEventManager>(FindObjectsInactive.Include);
            mgr?.StopAssault();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SubscribeToFirstBuilding()
        {
            registeredHazards.Clear();
            Bus<BuildingSpawnEvent>.OnEvent[Owner.Player1] += OnFirstBuildingSpawned;
        }

        private static void OnFirstBuildingSpawned(BuildingSpawnEvent evt)
        {
            Bus<BuildingSpawnEvent>.OnEvent[Owner.Player1] -= OnFirstBuildingSpawned;
            EnsureExists();

            if (ColonyActManager.Instance != null && ColonyActManager.Instance.IsRunActive)
            {
                Instance?.StopAssault();
                return;
            }

            Instance?.BeginAssault();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        public void BeginAssault()
        {
            if (ColonyActManager.Instance != null && ColonyActManager.Instance.IsRunActive)
                return;
            if (waveRoutine == null)
                waveRoutine = StartCoroutine(WaveLoop());
        }

        public void StopAssault()
        {
            if (waveRoutine != null)
            {
                StopCoroutine(waveRoutine);
                waveRoutine = null;
            }
        }

        /// <summary>Immediate single strike (Colony Acts card disaster).</summary>
        public void TriggerStrike(GameObject preferredPrefab = null)
        {
            Vector3 targetPos = GetTargetPosition();
            if (preferredPrefab != null)
            {
                RegisterHazard(preferredPrefab);
                Instantiate(preferredPrefab, targetPos, Quaternion.identity);
                Debug.Log($"[NaturalEventManager] TriggerStrike prefab={preferredPrefab.name}");
                return;
            }
            SpawnEvent();
        }

        public void TryTurnThreat(int turnNumber)
        {
            if (ColonyActManager.Instance != null && ColonyActManager.Instance.IsRunActive)
                return;
            if (waveRoutine == null) return;
            if (turnNumber < 20) return;
            if (turnNumber % 12 != 0) return;

            float chance = Mathf.Clamp(0.02f + turnNumber * 0.002f, 0.02f, 0.08f);
            if (Random.value > chance) return;
            SpawnEvent();
        }

        private IEnumerator WaveLoop()
        {
            yield return new WaitForSeconds(firstWaveDelay);
            while (true)
            {
                if (ColonyActManager.Instance != null && ColonyActManager.Instance.IsRunActive)
                {
                    yield return new WaitForSeconds(timeBetweenWaves);
                    continue;
                }

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
            var pool = new List<GameObject>();
            if (eventPrefabs != null)
            {
                foreach (var p in eventPrefabs)
                    if (p != null) pool.Add(p);
            }
            foreach (var p in registeredHazards)
                if (p != null) pool.Add(p);

            if (pool.Count > 0)
            {
                GameObject prefab = pool[Random.Range(0, pool.Count)];
                if (prefab != null)
                {
                    Instantiate(prefab, targetPos, Quaternion.identity);
                    return;
                }
            }

            CreateFallbackMeteor(targetPos);
        }

        private void CreateFallbackMeteor(Vector3 targetPos)
        {
            GameObject meteor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            meteor.name = "Meteor (fallback)";
            meteor.transform.position = targetPos;
            meteor.transform.localScale = Vector3.one * 2f;

            var impact = meteor.AddComponent<NaturalEventImpact>();
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
            var colony = GetColonyTargets();
            if (colony.Count > 0 && Random.value <= chanceToTargetColony)
                return colony[Random.Range(0, colony.Count)].transform.position;
            return GetRandomPlanetPosition();
        }

        private List<AbstractCommandable> GetColonyTargets()
        {
            var result = new List<AbstractCommandable>();
            foreach (AbstractCommandable c in AbstractCommandable.ActiveCommandables)
            {
                if (c != null && c.Owner == Owner.Player1)
                    result.Add(c);
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
