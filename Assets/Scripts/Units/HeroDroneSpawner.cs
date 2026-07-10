using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Spawns the player's Hero Drone (mobile command center) once the planet has been
    /// generated, places it at the player's start location, marks it as Player1, and links
    /// it into <see cref="PlayerInput"/> so WASD piloting works with no manual scene wiring.
    /// Defers spawning until a Player1 command post exists so the drone always appears
    /// near the base rather than at map center.
    /// </summary>
    public class HeroDroneSpawner : MonoBehaviour
    {
        [Tooltip("Resources path (no extension) of the Hero Drone prefab.")]
        [SerializeField] private string heroPrefabResourcePath = "Units/Hero Drone";
        [Tooltip("Spawn the drone this many world units in front of the start point so it isn't on top of the base.")]
        [SerializeField] private float spawnForwardOffset = 6f;
        [Tooltip("Maximum seconds to wait for the initial command post before falling back to map center.")]
        [SerializeField] private float maxWaitForBase = 10f;

        private bool hasSpawned;

        private void OnEnable()
        {
            PlanetGenerator.OnPlanetGenerated += HandlePlanetGenerated;
            // If the planet already generated before this component woke up, spawn immediately.
            if (PlanetGenerator.Instance != null && Application.isPlaying)
            {
                HandlePlanetGenerated();
            }
        }

        private void OnDisable()
        {
            PlanetGenerator.OnPlanetGenerated -= HandlePlanetGenerated;
        }

        private void HandlePlanetGenerated()
        {
            if (hasSpawned) return;
            hasSpawned = true;
            StartCoroutine(WaitForBaseAndSpawn());
        }

        private IEnumerator WaitForBaseAndSpawn()
        {
            GameObject prefab = Resources.Load<GameObject>(heroPrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogError($"[HeroDroneSpawner] Could not load Hero Drone prefab at Resources/{heroPrefabResourcePath}.");
                yield break;
            }

            // Wait until a Player1 base exists (the GreedyAIController spawns it in Start()).
            float waited = 0f;
            while (waited < maxWaitForBase)
            {
                BaseBuilding playerBase = BaseBuilding.ActiveBuildings
                    .FirstOrDefault(b => b != null && b.Owner == Owner.Player1);
                if (playerBase != null) break;
                yield return new WaitForSeconds(0.25f);
                waited += 0.25f;
            }

            Vector3 spawnPos = ResolveSpawnPosition();

            // Keep the drone on the NavMesh if one is baked.
            if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 25f, NavMesh.AllAreas))
            {
                spawnPos = hit.position;
            }

            GameObject instance = Instantiate(prefab, spawnPos, Quaternion.identity);
            instance.name = "Hero Drone";

            if (instance.TryGetComponent(out AbstractCommandable commandable))
            {
                commandable.Owner = Owner.Player1;
            }

            // Attach LifeSupportNode to make the Hero Drone act as a mobile support zone
            if (!instance.TryGetComponent<LifeSupportNode>(out _))
            {
                var lifeSupport = instance.AddComponent<LifeSupportNode>();
                lifeSupport.Radius = 30f;
            }

            // Link into PlayerInput so WASD pilots this drone.
            PlayerInput input = Object.FindAnyObjectByType<PlayerInput>(FindObjectsInactive.Include);
            if (input != null && instance.TryGetComponent(out HeroDroneController hero))
            {
                input.AssignHeroDrone(hero);
            }

            Debug.Log($"[HeroDroneSpawner] Hero Drone spawned at {spawnPos}.");
        }

        private Vector3 ResolveSpawnPosition()
        {
            // Prefer the player's first command building if one exists.
            BaseBuilding playerBase = BaseBuilding.ActiveBuildings
                .FirstOrDefault(b => b != null && b.Owner == Owner.Player1);
            if (playerBase != null)
            {
                return playerBase.transform.position + playerBase.transform.forward * spawnForwardOffset;
            }

            // Fall back to the first sector center (matches GreedyAI initial base location).
            PlanetGenerator pg = PlanetGenerator.Instance;
            if (pg != null && pg.Config != null)
            {
                float w = pg.Config.MapWidth * pg.CellSize;
                float h = pg.Config.MapHeight * pg.CellSize;
                float secW = w / pg.Config.SectorsX;
                float secH = h / pg.Config.SectorsY;
                return new Vector3(secW * 0.5f, 0f, secH * 0.5f);
            }

            return Vector3.zero;
        }
    }
}
