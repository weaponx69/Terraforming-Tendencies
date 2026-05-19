#if UNITY_INCLUDE_TESTS
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TMPro;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.UI.Containers;
using GameDevTV.RTS.UI.Components;
using UnityEngine.SceneManagement;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;

namespace GameDevTV.RTS.Tests
{
    public class UITests
    {
        private GameObject uiObj;
        private RuntimeUI runtimeUI;
        private TextMeshProUGUI biomassText;
        private GameObject suppliesObj;
        private GameObject gameOverManagerObj;

        [SetUp]
        public void SetUp()
        {
            // If we are in the main scene, we don't want to create duplicates for scene-independent tests
            // But usually [SetUp] runs before every [UnityTest]
            
            suppliesObj = new GameObject("Supplies");
            suppliesObj.AddComponent<Supplies>();
            
            gameOverManagerObj = new GameObject("GameOverManager");
            gameOverManagerObj.AddComponent<GameOverManager>();

            uiObj = new GameObject("RuntimeUI");
            uiObj.SetActive(false); 
            runtimeUI = uiObj.AddComponent<RuntimeUI>();
            
            SetField(runtimeUI, "actionsUI", CreateMockActionsUI());
            SetField(runtimeUI, "buildingSelectedUI", new GameObject("BuildingSelectedUI").AddComponent<BuildingSelectedUI>());
            SetField(runtimeUI, "unitIconUI", new GameObject("UnitIconUI").AddComponent<UnitIconUI>());
            SetField(runtimeUI, "singleUnitSelectedUI", new GameObject("SingleUnitSelectedUI").AddComponent<SingleUnitSelectedUI>());
            SetField(runtimeUI, "unitTransportUI", new GameObject("UnitTransportUI").AddComponent<UnitTransportUI>());

            GameObject textObj = new GameObject("BiomassText");
            textObj.transform.SetParent(uiObj.transform);
            biomassText = textObj.AddComponent<TextMeshProUGUI>();
            SetField(runtimeUI, "biomassValueText", biomassText);

            SetField(runtimeUI, "displayedOwner", Owner.Player1);

            uiObj.SetActive(true);
        }

        private ActionsUI CreateMockActionsUI()
        {
            var go = new GameObject("ActionsUI");
            var actions = go.AddComponent<ActionsUI>();
            SetPrivateField(actions, "actionButtons", new UIActionButton[0]);
            return actions;
        }

        private void SetPrivateField(object obj, string fieldName, object value)
        {
            var type = obj.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field == null && type.BaseType != null) 
                field = type.BaseType.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (field != null) field.SetValue(obj, value);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(uiObj);
            Object.Destroy(suppliesObj);
            Object.Destroy(gameOverManagerObj);
        }

        [UnityTest]
        public IEnumerator BiomassUI_Updates_WhenBiomassChanges()
        {
            biomassText.text = "0";
            Supplies.RaiseBiomassChanged(Owner.Player1, 500);
            yield return null; 
            Assert.AreEqual("500", biomassText.text, "Biomass UI text should update to match the new biomass value.");
            Debug.Log("[UITest] BiomassUI_Updates_WhenBiomassChanges Passed");
        }

        [UnityTest]
        public IEnumerator OxygenUI_Updates_WhenOxygenChanges()
        {
            GameObject textObj = new GameObject("OxygenValueText");
            textObj.transform.SetParent(uiObj.transform);
            var oxygenValueText = textObj.AddComponent<TextMeshProUGUI>();
            SetPrivateField(runtimeUI, "oxygenValueText", oxygenValueText);
            
            oxygenValueText.text = "0";
            Supplies.UpdateOxygen(Owner.Player1, 25);
            yield return null; 
            Assert.AreEqual("25", oxygenValueText.text, "Oxygen UI text should update to match the new oxygen value.");
            Debug.Log("[UITest] OxygenUI_Updates_WhenOxygenChanges Passed");
        }

        [UnityTest]
        public IEnumerator BiomassUI_Updates_WhenDroneGathersMinerals()
        {
            // Initial state
            biomassText.text = "0";
            Supplies.Biomass[Owner.Player1] = 0;
            
            // We need a SupplySO reference that matches the one in Supplies
            var mineralsSO = ScriptableObject.CreateInstance<SupplySO>();
            mineralsSO.name = "Minerals";
            
            var supplies = Object.FindAnyObjectByType<Supplies>();
            SetPrivateField(supplies, "mineralsSO", mineralsSO);
            SetPrivateField(supplies, "mineralsToBiomassRate", 1.0f);
            
            // Trigger gathering event
            Bus<SupplyEvent>.Raise(Owner.Player1, new SupplyEvent(Owner.Player1, 10, mineralsSO));
            
            yield return null; // Wait for UI update
            
            Assert.AreEqual(10, Supplies.Biomass[Owner.Player1], "Biomass dictionary should increase.");
            Assert.AreEqual("10", biomassText.text, "Biomass UI text should update after gathering.");
            
            Debug.Log("[UITest] BiomassUI_Updates_WhenDroneGathersMinerals Passed");
        }

        [UnityTest]
        public IEnumerator MainScene_RuntimeUI_IsCorrectlyConfigured()
{
            // Load the main scene if not loaded
            if (SceneManager.GetActiveScene().name != "Terraforming-Tendencies")
            {
                yield return SceneManager.LoadSceneAsync("Terraforming-Tendencies", LoadSceneMode.Single);
            }

            RuntimeUI ui = Object.FindAnyObjectByType<RuntimeUI>();
            Assert.IsNotNull(ui, "RuntimeUI not found in scene 'Terraforming-Tendencies'!");

            var biomassField = typeof(RuntimeUI).GetField("biomassValueText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var biomassValueText = (TextMeshProUGUI)biomassField.GetValue(ui);
            Assert.IsNotNull(biomassValueText, "biomassValueText reference is NULL in RuntimeUI in scene!");

            var oxygenField = typeof(RuntimeUI).GetField("oxygenValueText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var oxygenValueText = (TextMeshProUGUI)oxygenField.GetValue(ui);
            Assert.IsNotNull(oxygenValueText, "oxygenValueText reference is NULL in RuntimeUI in scene!");
            
            Debug.Log("[UITest] MainScene_RuntimeUI_IsCorrectlyConfigured Passed");
        }
    [UnityTest]
    public IEnumerator EndToEnd_Gathering_UpdatesUI()
    {
        Debug.Log("[UITest] EndToEnd_Gathering_UpdatesUI Starting");
        // 1. Setup Environment
        var mineralsSO = ScriptableObject.CreateInstance<SupplySO>();
        mineralsSO.name = "Minerals";
        
        // Use reflection for private setters
        SetField(mineralsSO, "<MaxAmount>k__BackingField", 100);
        SetField(mineralsSO, "<AmountPerGather>k__BackingField", 10);
        SetField(mineralsSO, "<BaseGatherTime>k__BackingField", 0.1f);

        var supplies = Object.FindAnyObjectByType<Supplies>();
        SetField(supplies, "mineralsSO", mineralsSO);
        SetField(supplies, "mineralsToBiomassRate", 1.0f);
        Supplies.Biomass[Owner.Player1] = 100;
        biomassText.text = "100";

        // Add a PlanetGenerator to the scene for the test
        var genObj = new GameObject("PlanetGenerator");
        var generator = genObj.AddComponent<PlanetGenerator>();
        SetField(generator, "MineralsSupplySO", mineralsSO);

        // 2. Create a Gatherable with NULL supply (simulating editor-generated state)
        GameObject rockObj = new GameObject("ResourceRock");
        var gs = rockObj.AddComponent<GatherableSupply>();
        // gs.Supply is null by default

        // 3. PlanetGenerator fixes it
        var fixMethod = typeof(PlanetGenerator).GetMethod("FixPreplacedGatherables", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        fixMethod.Invoke(generator, null);

        Assert.AreEqual(mineralsSO, gs.Supply, "PlanetGenerator should have fixed the NULL supply reference.");

        // 4. Drone Gathers (Simulated)
        int gathered = gs.EndGather();
        Assert.AreEqual(10, gathered);

        Bus<SupplyEvent>.Raise(Owner.Player1, new SupplyEvent(Owner.Player1, gathered, mineralsSO));

        yield return null;

        // 5. Verify UI
        Assert.AreEqual(110, Supplies.Biomass[Owner.Player1], "Biomass should have increased to 110.");
        Assert.AreEqual("110", biomassText.text, "UI should show 110 biomass.");

        Object.Destroy(rockObj);
        Object.Destroy(genObj);
        Debug.Log("[UITest] EndToEnd_Gathering_UpdatesUI Passed");
    }

    private void SetField(object obj, string fieldName, object value)
    {
        var type = obj.GetType();
        var field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (field == null && type.BaseType != null) 
            field = type.BaseType.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        
        if (field != null) 
        {
            field.SetValue(obj, value);
        }
        else
        {
            Debug.LogError($"[UITest] Could not find field {fieldName} on {obj.GetType().Name}");
        }
    }
}
    }
    #endif
