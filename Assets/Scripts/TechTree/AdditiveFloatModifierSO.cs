using System.Reflection;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.TechTree
{
    [CreateAssetMenu(fileName = "Additive Float Modifier", menuName = "Tech Tree/Modifiers/Additive Float Modifier", order = 161)]
    public class AdditiveFloatModifierSO : UpgradeSO
    {
        [field: SerializeField] public float Amount { get; private set; }

        public override void Apply(AbstractUnitSO unit)
        {
            Debug.Log($"{Name} is applying {Amount} to {PropertyPath}.");

            try
            {
                float currentValue = GetPropertyValue<float>(unit, out object target, out PropertyInfo attributeField);
                Debug.Log($"Adding {Amount} to {PropertyPath}'s current value of {currentValue}");
                currentValue += Amount;
                attributeField.SetValue(target, currentValue);
                Debug.Log($"Updated value to: {attributeField.GetValue(target)}");
            }
            catch(InvalidPathSpecifiedException) {}
        }
    }
}
