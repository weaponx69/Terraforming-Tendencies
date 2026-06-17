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

        protected void SetValue<T>(object target, PropertyInfo propertyInfo, T value)
        {
            if (propertyInfo.CanWrite)
            {
                propertyInfo.SetValue(target, value);
            }
            else
            {
                Type type = target.GetType();
                string propName = propertyInfo.Name;
                string[] fieldNames = new string[]
                {
                    propName,
                    char.ToLower(propName[0]) + propName.Substring(1),
                    "_" + char.ToLower(propName[0]) + propName.Substring(1),
                    "_" + propName,
                    $"<{propName}>k__BackingField"
                };

                FieldInfo fieldInfo = null;
                Type currentType = type;
                while (currentType != null && fieldInfo == null)
                {
                    foreach (var fn in fieldNames)
                    {
                        fieldInfo = currentType.GetField(fn, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (fieldInfo != null) break;
                    }
                    currentType = currentType.BaseType;
                }

                if (fieldInfo != null)
                {
                    fieldInfo.SetValue(target, value);
                }
                else
                {
                    throw new System.ArgumentException($"Property {propName} has no setter and no matching backing field was found.");
                }
            }
        }
    }
}