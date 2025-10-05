using System.Reflection;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.TechTree
{
    [CreateAssetMenu(fileName = "Additive Int Modifier", menuName = "Tech Tree/Modifiers/Additive Int Modifier", order = 160)]
    public class AdditiveIntModifierSO : UpgradeSO
    {
        [field: SerializeField] public int Amount { get; private set; }

        public override void Apply(AbstractUnitSO unit)
        {
            Debug.Log($"{Name} is applying {Amount} to {PropertyPath}.");

            try
            {
                int currentValue = GetPropertyValue<int>(unit, out object target, out PropertyInfo attributeField);
                Debug.Log($"Adding {Amount} to {PropertyPath}'s current value of {currentValue}");
                currentValue += Amount;
                attributeField.SetValue(target, currentValue);
                Debug.Log($"Updated value to: {attributeField.GetValue(target)}");
            }
            catch(InvalidPathSpecifiedException) {}
        }
    }
}
