#if UNITY_INCLUDE_TESTS
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.AI;
using GameDevTV.RTS.Environment;
using Unity.AI.Navigation;

namespace GameDevTV.RTS.Tests
{
    public class ProceduralNavMeshTests
    {
        private GameObject generatorObj;

        [TearDown]
        public void TearDown()
        {
            if (generatorObj != null) Object.Destroy(generatorObj);
        }

        [UnityTest]
        public IEnumerator PlanetGenerator_BakesNavMesh_ForAllRegisteredAgentTypes()
        {
            // Setup a minimal PlanetGenerator
            generatorObj = new GameObject("PlanetGenerator");
            var generator = generatorObj.AddComponent<PlanetGenerator>();
            
            // Mock a basic PlanetConfig so it doesn't crash
            generator.Config = ScriptableObject.CreateInstance<PlanetConfig>();
            generator.Config.MapWidth = 50;
            generator.Config.MapHeight = 50;
            generator.Config.SurfaceFeatureDensity = 0; // No features to speed up test
            
            // Give it time to generate the planet and bake NavMeshes (Start method execution)
            yield return null;
            yield return null;

            // Get all NavMesh surfaces on the generator
            var surfaces = generatorObj.GetComponentsInChildren<NavMeshSurface>();
            
            // Check how many agent types are actually registered in the project settings
            int registeredAgentCount = NavMesh.GetSettingsCount();

            // Assert that the generator dynamically added a surface for EVERY agent type
            Assert.AreEqual(registeredAgentCount, surfaces.Length, 
                $"PlanetGenerator should have {registeredAgentCount} NavMeshSurfaces, one for each agent type, but found {surfaces.Length}.");

            // --- Regression Test: Verify TransparentFX is excluded ---
            int transparentLayer = LayerMask.NameToLayer("TransparentFX");
            foreach (var s in surfaces)
            {
                if (s.collectObjects == CollectObjects.Children) continue;

                Assert.AreEqual(0, (s.layerMask.value & (1 << transparentLayer)), 
                    $"NavMeshSurface for agent {s.agentTypeID} MUST exclude the TransparentFX layer to avoid baking thousands of ghosts!");
            }

            // Verify each agent type actually has a valid NavMesh baked
            for (int i = 0; i < registeredAgentCount; i++)
            {
                NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(i);
                
                // Create a test agent of this type
                GameObject testAgentObj = new GameObject($"TestAgent_Type_{settings.agentTypeID}");
                testAgentObj.transform.position = new Vector3(25, 0.1f, 25); // Center of the 50x50 map

                NavMeshAgent agent = testAgentObj.AddComponent<NavMeshAgent>();
                agent.agentTypeID = settings.agentTypeID;
                
                // Wait one frame for the agent to initialize against the NavMesh
                yield return null;
                
                Assert.IsTrue(agent.isOnNavMesh, 
                    $"NavMeshAgent of Type ID {settings.agentTypeID} failed to bind to the NavMesh! The Procedural Generation didn't bake its layer.");
                
                Object.Destroy(testAgentObj);
            }
        }

        [UnityTest]
        public IEnumerator PlanetGenerator_AssignsTransparentFXLayer_ToGhosts()
        {
            // Setup a minimal PlanetGenerator
            generatorObj = new GameObject("PlanetGenerator");
            var generator = generatorObj.AddComponent<PlanetGenerator>();
            generator.Config = ScriptableObject.CreateInstance<PlanetConfig>();
            generator.Config.MapWidth = 50;
            generator.Config.MapHeight = 50;
            generator.Config.SurfaceFeatureDensity = 2; // Spawn some features to create ghosts
            generator.Config.SurfaceFeaturePrefabs = new GameObject[] { new GameObject("TestFeature") };

            yield return null;
            yield return null;

            int transparentLayer = LayerMask.NameToLayer("TransparentFX");
            bool foundGhost = false;

            foreach (Transform child in generatorObj.transform)
            {
                if (child.name.Contains("Ghost"))
                {
                    foundGhost = true;
                    // Check root and all children
                    foreach (var r in child.GetComponentsInChildren<Transform>(true))
                    {
                        Assert.AreEqual(transparentLayer, r.gameObject.layer, 
                            $"Object '{r.name}' (child of {child.name}) is not on the TransparentFX layer! This will cause massive performance hangs during NavMesh bakes.");
                    }
                }
            }

            Assert.IsTrue(foundGhost, "Test setup failed: No ghost objects were created to verify.");
        }
        }
        }
#endif
