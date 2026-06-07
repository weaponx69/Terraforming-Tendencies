using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Periodically unleashes waves of "natural events" (meteor strikes, etc.) against
    /// the terraforming colony. Each event either targets a random player-owned structure
    /// or a random spot on the current planet. Waves escalate over time.
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

        [Header("Debug")]
        [SerializeField] private bool autoStart = true;

        public int CurrentWave { get; private set; }

        private Coroutine waveRoutine;

        private void Start()
        {
            if (autoStart)
            {
                BeginAssault();
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
            if (eventPrefabs == null || eventPrefabs.Length == 0)
            {
                Debug.LogWarning("[NaturalEventManager] No event prefabs assigned.", this);
                return;
            }

            Vector3 targetPos = GetTargetPosition();
            GameObject prefab = eventPrefabs[Random.Range(0, eventPrefabs.Length)];
            if (prefab != null)
            {
                Instantiate(prefab, targetPos, Quaternion.identity);
            }
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
