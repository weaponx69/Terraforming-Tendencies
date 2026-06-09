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
            try
            {
                float currentValue = GetPropertyValue<float>(unit, out object target, out PropertyInfo attributeField);
                currentValue += Amount;
                SetValue(target, attributeField, currentValue);
            }
            catch(InvalidPathSpecifiedException) {}
        }
    }
}
