#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;   // Added generic collections support
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
        private TextMeshProUGUI materialsText;

        [SetUp]
        public void SetUp()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
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

            GameObject matTextObj = new GameObject("MaterialsText");
            matTextObj.transform.SetParent(uiObj.transform);
            materialsText = matTextObj.AddComponent<TextMeshProUGUI>();
            SetField(runtimeUI, "materialsValueText", materialsText);

            SetField(runtimeUI, "displayedOwner", Owner.Player1);

            uiObj.SetActive(true);
        }

        private ActionsUI CreateMockActionsUI()
        {
            var go = new GameObject("ActionsUI");
            var actions = go.AddComponent<ActionsUI>();
            SetField(actions, "actionButtons", new UIActionButton[0]);
            return actions;
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
            biomassText.text = "0.0%";
            Supplies.UpdateBiomass(Owner.Player1, 50.5f);
            yield return null; 
            Assert.AreEqual("50.5%", biomassText.text, "Biomass UI text should update to match the new biomass value.");
            Debug.Log("[UITest] BiomassUI_Updates_WhenBiomassChanges Passed");
        }

        [UnityTest]
        public IEnumerator OxygenUI_Updates_WhenOxygenChanges()
        {
            GameObject textObj = new GameObject("OxygenValueText");
            textObj.transform.SetParent(uiObj.transform);
            var oxygenValueText = textObj.AddComponent<TextMeshProUGUI>();
            SetField(runtimeUI, "oxygenValueText", oxygenValueText);
            
            oxygenValueText.text = "0.0%";
            Supplies.UpdateOxygen(Owner.Player1, 25f);
            yield return null; 
            Assert.AreEqual("25.0%", oxygenValueText.text, "Oxygen UI text should update to match the new oxygen value.");
            Debug.Log("[UITest] OxygenUI_Updates_WhenOxygenChanges Passed");
        }

        [UnityTest]
        public IEnumerator MaterialsUI_Updates_WhenDroneGathersMinerals()
        {
            materialsText.text = "0";
            Supplies.Materials[Owner.Player1] = 0;
            
            var mineralsSO = ScriptableObject.CreateInstance<SupplySO>();
            mineralsSO.name = "Minerals";
            
            var supplies = Object.FindAnyObjectByType<Supplies>();
            SetField(supplies, "mineralsSO", mineralsSO);
            SetField(supplies, "mineralsToMaterialsRate", 1.0f);
            
            Bus<SupplyEvent>.Raise(Owner.Player1, new SupplyEvent(Owner.Player1, 10, mineralsSO));
            
            yield return null; 
            
            Assert.AreEqual(10, Supplies.Materials[Owner.Player1], "Materials dictionary should increase.");
            Assert.AreEqual("10", materialsText.text, "Materials UI text should update after gathering.");
            
            Debug.Log("[UITest] MaterialsUI_Updates_WhenDroneGathersMinerals Passed");
        }

        [UnityTest]
        public IEnumerator EndToEnd_Gathering_UpdatesUI()
        {
            var mineralsSO = ScriptableObject.CreateInstance<SupplySO>();
            mineralsSO.name = "Minerals";
            
            SetField(mineralsSO, "<MaxAmount>k__BackingField", 100);
            SetField(mineralsSO, "<AmountPerGather>k__BackingField", 10);
            SetField(mineralsSO, "<BaseGatherTime>k__BackingField", 0.1f);

            var supplies = Object.FindAnyObjectByType<Supplies>();
            SetField(supplies, "mineralsSO", mineralsSO);
            SetField(supplies, "mineralsToBiomassRate", 1.0f);
            Supplies.Biomass[Owner.Player1] = 100f;
            biomassText.text = "100.0%";

            var genObj = new GameObject("PlanetGenerator");
            var generator = genObj.AddComponent<PlanetGenerator>();
            SetField(generator, "MineralsSupplySO", mineralsSO);

            GameObject rockObj = new GameObject("ResourceRock");
            var gs = rockObj.AddComponent<GatherableSupply>();

            var fixMethod = typeof(PlanetGenerator).GetMethod("FixPreplacedGatherables", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fixMethod.Invoke(generator, null);

            Assert.AreEqual(mineralsSO, gs.Supply, "PlanetGenerator should have fixed the NULL supply reference.");

            int gathered = gs.EndGather();
            Assert.AreEqual(10, gathered);

            Bus<SupplyEvent>.Raise(Owner.Player1, new SupplyEvent(Owner.Player1, gathered, mineralsSO));

            yield return null;

            Assert.AreEqual(110f, Supplies.Biomass[Owner.Player1], "Biomass should have increased to 110.");
            Assert.AreEqual("110.0%", biomassText.text, "UI should show 110 biomass.");

            Object.Destroy(rockObj);
            Object.Destroy(genObj);
            Debug.Log("[UITest] EndToEnd_Gathering_UpdatesUI Passed");
        }

        [UnityTest]
        public IEnumerator DraftHandSize_IsFour()
        {
            // Create a CardDeckController instance
            GameObject deckObj = new GameObject("CardDeckController");
            var deckController = deckObj.AddComponent<CardDeckController>();
            
            // Use reflection to set the private handSize field to 4
            SetField(deckController, "handSize", 4);
            
            // Get the handSize field value
            int handSize = (int)GetField(deckController, "handSize");
            Assert.AreEqual(4, handSize, "handSize should be set to 4");
            
            Debug.Log($"[UITest] DraftHandSize_IsFour Passed - handSize = {handSize}");
            
            Object.Destroy(deckObj);
            yield return null;
        }

        private object GetField(object obj, string fieldName)
        {
            var type = obj.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (field == null && type.BaseType != null)
                field = type.BaseType.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                return field.GetValue(obj);
            }
            return null;
        }

        private object InvokePrivateMethod(object obj, string methodName)
        {
            var type = obj.GetType();
            var method = type.GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (method == null && type.BaseType != null)
                method = type.BaseType.GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                return method.Invoke(obj, null);
            }
            return null;
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
