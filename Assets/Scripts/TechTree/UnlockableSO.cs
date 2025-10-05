using System;
using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.TechTree
{
    public abstract class UnlockableSO : ScriptableObject, ICloneable
    {
        [field: SerializeField] public string Name { get; private set; } = "Unit";
        [field: SerializeField] public bool IsOneTimeUnlock { get; private set; }
        [field: SerializeField] public float BuildTime { get; private set; } = 5;
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField] public SupplyCostSO Cost { get; private set; }
        [field: SerializeField] public TechTreeSO TechTree { get; private set; }
        [field: SerializeField] protected List<UnlockableSO> unlockRequirements { get; private set; } = new();

        public IEnumerable<UnlockableSO> UnlockRequirements => unlockRequirements.ToList();

        public virtual object Clone()
        {
            UnlockableSO copy = Instantiate(this);

            copy.Cost = Cost == null ? null : Instantiate(Cost);

            return copy;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override bool Equals(object other)
        {
            return other is UnlockableSO unlockableSO && GetHashCode().Equals(unlockableSO.GetHashCode());
        }
    }
}