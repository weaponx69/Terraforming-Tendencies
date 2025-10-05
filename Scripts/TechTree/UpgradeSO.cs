using System;
using System.Reflection;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.TechTree
{
    public abstract class UpgradeSO : UnlockableSO, IModifier
    {
        [field: SerializeField] public string PropertyPath { get; private set; }

        public abstract void Apply(AbstractUnitSO unit);

        protected T GetPropertyValue<T>(AbstractUnitSO unit, out object target, out PropertyInfo propertyInfo)
        {
            // if PropertyPath = "AttackConfig/Damage"...
            string[] attributes = PropertyPath.Split("/"); // ["AttackConfig", "Damage"]

            Type type = unit.GetType();
            target = unit;

            for (int i = 0; i < attributes.Length - 1; i++)
            {
                propertyInfo = type.GetProperty(attributes[i]);

                if (propertyInfo == null)
                {
                    Debug.LogError($"Unable to apply modifier {Name} to attribute {PropertyPath} because" +
                        $" it does not exist on {unit.Name}!");
                    throw new InvalidPathSpecifiedException(attributes[i]);
                }

                target = propertyInfo.GetValue(target); // target is now AttackConfigSO!
                type = target.GetType(); // type is now AttackConfigSO instead of AbstractUnitSO!
            }

            propertyInfo = type.GetProperty(attributes[^1]); // Damage!

            if (propertyInfo == null)
            {
                Debug.LogError($"Unable to apply modifier {Name} to attribute {PropertyPath} because" +
                        $" it does not exist on {unit.Name}!");
                throw new InvalidPathSpecifiedException(attributes[^1]);
            }

            T returnValue = default;
            try
            {
                returnValue = (T)propertyInfo.GetValue(target);
            }
            catch (InvalidCastException)
            {
                Debug.LogError($"Expected {PropertyPath} to be an int, but it wasn't!");
            }

            return returnValue;
        }
    }
}