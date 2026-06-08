#if UNITY_INCLUDE_TESTS
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Environment;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameDevTV.RTS.Tests
{
    /// <summary>
    /// Verifies that a Rifleman (combat) drone detects a meteor within range and
    /// damages/destroys it through the direct-drive C# combat loop in
    /// <see cref="MeteorWarriorDrone"/>.
    /// </summary>
    public class MeteorCombatTests
    {
        private const string DronePrefabPath = "Assets/Units/Rifleman Drone/Rifleman Drone.prefab";
        private const string MeteorPrefabPath = "Assets/Prefabs/NaturalEvents/Meteor.prefab";

        private GameObject droneObj;
        private GameObject meteorObj;

        [SetUp]
        public void SetUp()
        {
            // The behavior graph spams harmless "No Animator set" warnings during spawn.
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (droneObj != null) Object.DestroyImmediate(droneObj);
            if (meteorObj != null) Object.DestroyImmediate(meteorObj);
        }

        [UnityTest]
        public IEnumerator Drone_OnSpawn_DoesNotLogNoAnimatorWarning()
        {
#if !UNITY_EDITOR
            Assert.Ignore("This integration test loads prefabs via AssetDatabase and only runs in the Editor.");
            yield break;
#else
            GameObject dronePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DronePrefabPath);
            Assert.IsNotNull(dronePrefab, "Rifleman Drone prefab not found at " + DronePrefabPath);

            bool animatorWarningSeen = false;
            string offendingMessage = null;
            Application.LogCallback handler = (condition, stack, type) =>
            {
                if (condition != null && condition.Contains("No Animator set"))
                {
                    animatorWarningSeen = true;
                    offendingMessage = condition;
                }
            };
            Application.logMessageReceived += handler;

            try
            {
                droneObj = Object.Instantiate(dronePrefab, new Vector3(0f, 4f, 0f), Quaternion.identity);
                var drone = droneObj.GetComponent<MeteorWarriorDrone>();
                Assert.IsNotNull(drone, "Spawned drone has no MeteorWarriorDrone component.");
                drone.Owner = Owner.Player1;

                // Let Awake/Start, the first-frame Self-binding repair, and several graph
                // ticks run. The animator nodes execute during these ticks.
                for (int i = 0; i < 60; i++) yield return null;
            }
            finally
            {
                Application.logMessageReceived -= handler;
            }

            Assert.IsFalse(
                animatorWarningSeen,
                "A 'No Animator set' warning was logged after the drone spawned: " + offendingMessage);
#endif
        }

        [UnityTest]
        public IEnumerator RiflemanDrone_ShootsDownRealisticallyFallingMeteor_BeforeImpact()
        {
#if !UNITY_EDITOR
            Assert.Ignore("This integration test loads prefabs via AssetDatabase and only runs in the Editor.");
            yield break;
#else
            // --- Load prefabs (these assets are not in Resources) ---
            GameObject dronePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DronePrefabPath);
            GameObject meteorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeteorPrefabPath);
            Assert.IsNotNull(dronePrefab, "Rifleman Drone prefab not found at " + DronePrefabPath);
            Assert.IsNotNull(meteorPrefab, "Meteor prefab not found at " + MeteorPrefabPath);

            // --- Spawn the drone and make it a player-owned combat unit ---
            droneObj = Object.Instantiate(dronePrefab, new Vector3(0f, 4f, 0f), Quaternion.identity);
            var drone = droneObj.GetComponent<MeteorWarriorDrone>();
            Assert.IsNotNull(drone, "Spawned drone has no MeteorWarriorDrone component.");
            drone.Owner = Owner.Player1; // Must differ from the meteor's Owner (Unowned) for detection.

            // Let Awake/Start run: sets up the AttackConfig, the DamageableSensor range, etc.
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsTrue(droneObj != null, "Drone destroyed itself during Start (likely missing UnitSO).");

            // --- Spawn a meteor with its REAL falling behavior (no hovering rig) ---
            // NaturalEventImpact.Start() treats the spawn position as the ground impact
            // point, then lifts itself up by fallHeight and falls back down at fallSpeed.
            // We place the impact point on the ground a few units from the drone, fully
            // inside the 20-unit attack range, and leave fallHeight/fallSpeed/HP at the
            // prefab's real values. The meteor will fall fast and impact in ~1.1s unless
            // the drone shoots it down first.
            meteorObj = Object.Instantiate(meteorPrefab);
            var meteor = meteorObj.GetComponent<NaturalEventImpact>();
            Assert.IsNotNull(meteor, "Meteor prefab has no NaturalEventImpact component.");

            // Ground impact point 3 units horizontally from the drone.
            meteorObj.transform.position = new Vector3(3f, 0f, 0f);

            int initialHealth = meteor.CurrentHealth;
            Assert.Greater(initialHealth, 0, "Meteor should start with positive health.");

            // --- Watch the meteor fall and track its state every frame ---
            // Success criterion: the meteor is destroyed while still WELL above the ground
            // (y > MidAirThreshold). A meteor only self-destructs on impact at ground level
            // (within 0.1 units of its impact point), so a mid-air disappearance can ONLY
            // mean the drone shot it down.
            const float MidAirThreshold = 5f;
            float lastY = meteorObj.transform.position.y;
            int minHealth = initialHealth;
            bool destroyedMidAir = false;
            float timeout = 6f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                if (meteor == null || meteorObj == null)
                {
                    // Destroyed: was it mid-air (drone kill) or at ground (impact)?
                    destroyedMidAir = lastY > MidAirThreshold;
                    break;
                }

                lastY = meteorObj.transform.position.y;
                int hp = meteor.CurrentHealth;
                if (hp < minHealth) minHealth = hp;

                elapsed += Time.deltaTime;
                yield return null;
            }

            // First, the drone must have actually dealt damage.
            Assert.Less(
                minHealth, initialHealth,
                "Rifleman Drone never damaged the falling meteor (minHealth=" + minHealth +
                ", initialHealth=" + initialHealth + "). The drone is not engaging.");

            // Then, it must have destroyed it before it reached the ground.
            Assert.IsTrue(
                destroyedMidAir,
                "Drone damaged the meteor (minHealth=" + minHealth + ") but did NOT shoot it down " +
                "before impact. Last observed altitude=" + lastY + " (needed > " + MidAirThreshold +
                "). The meteor reached the ground / timed out.");
#endif
        }
    }
}
#endif
