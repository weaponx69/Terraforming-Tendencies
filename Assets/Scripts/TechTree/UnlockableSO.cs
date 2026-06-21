using System;
using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.TechTree
{
    public abstract class UnlockableSO : ScriptableObject, ICloneable
    {
        [field: SerializeField] public string Name { get; set; } = "Unit";
        [field: SerializeField] public bool IsOneTimeUnlock { get; private set; }
        [field: SerializeField] public float BuildTime { get; set; } = 5;
        [field: SerializeField] public Sprite Icon { get; set; }
        [field: SerializeField] public SupplyCostSO Cost { get; set; }
        [field: SerializeField] public TechTreeSO TechTree { get; private set; }
        [field: SerializeField] protected List<UnlockableSO> unlockRequirements { get; private set; } = new();

        public IEnumerable<UnlockableSO> UnlockRequirements => unlockRequirements?.Where(r => r != null).ToList() ?? Enumerable.Empty<UnlockableSO>();

        public virtual object Clone()
        {
            UnlockableSO copy = Instantiate(this);

            copy.Cost = Cost == null ? null : Instantiate(Cost);

            return copy;
        }

        private string GetUnlockableName()
        {
            string n = Name;
            if (string.IsNullOrEmpty(n) || n == "Unit")
            {
                n = name;
            }
            return n ?? "";
        }

        public override int GetHashCode()
        {
            return GetUnlockableName().GetHashCode();
        }

        public override bool Equals(object other)
        {
            if (other is not UnlockableSO otherUnlockable) return false;
            return string.Equals(GetUnlockableName(), otherUnlockable.GetUnlockableName());
        }
    }
}