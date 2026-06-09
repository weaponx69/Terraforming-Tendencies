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
            try
            {
                int currentValue = GetPropertyValue<int>(unit, out object target, out PropertyInfo attributeField);
                currentValue += Amount;
                SetValue(target, attributeField, currentValue);
            }
            catch(InvalidPathSpecifiedException) {}
        }
    }
}
