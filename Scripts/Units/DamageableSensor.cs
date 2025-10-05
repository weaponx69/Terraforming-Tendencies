using System;
using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using UnityEngine;

namespace GameDevTV.RTS.Units
{
    [RequireComponent(typeof(SphereCollider))]
    public class DamageableSensor : MonoBehaviour
    {
        public List<IDamageable> Damageables => visibleDamageables.ToList();
        [field: SerializeField] public Owner Owner { get; set; }

        public delegate void UnitDetectionEvent(IDamageable damageable);
        public event UnitDetectionEvent OnUnitEnter;
        public event UnitDetectionEvent OnUnitExit;

        private new SphereCollider collider;
        private HashSet<IDamageable> visibleDamageables = new();
        private HashSet<IDamageable> allDamageables = new();

        private void Awake()
        {
            collider = GetComponent<SphereCollider>();
        }

        private void OnTriggerEnter(Collider collider)
        {
            if (collider.TryGetComponent(out IDamageable damageable) && damageable.Owner != Owner)
            {
                allDamageables.Add(damageable);
                if (collider.TryGetComponent(out IHideable hideable))
                {
                    hideable.OnVisibilityChanged += HandleVisibilityChange;
                    if (hideable.IsVisible)
                    {
                        visibleDamageables.Add(damageable);
                        OnUnitEnter?.Invoke(damageable);
                    }
                }
                else
                {
                    visibleDamageables.Add(damageable);
                    OnUnitEnter?.Invoke(damageable);
                }
            }

            if (allDamageables.Count == 1)
            {
                Bus<UnitDeathEvent>.RegisterForAll(HandleUnitDeath);
            }
        }

        private void OnTriggerExit(Collider collider)
        {
            if (collider.TryGetComponent(out IDamageable damageable)
                && allDamageables.Remove(damageable) && visibleDamageables.Remove(damageable))
            {
                OnUnitExit?.Invoke(damageable);
            }

            if (collider.TryGetComponent(out IHideable hideable))
            {
                hideable.OnVisibilityChanged -= HandleVisibilityChange;
            }

            if (allDamageables.Count == 0)
            {
                Bus<UnitDeathEvent>.UnregisterForAll(HandleUnitDeath);
            }
        }

        private void OnDestroy()
        {
            foreach(IDamageable damageable in allDamageables)
            {
                if (damageable.Transform.TryGetComponent(out IHideable hideable))
                {
                    hideable.OnVisibilityChanged -= HandleVisibilityChange;
                }
            }
            Bus<UnitDeathEvent>.UnregisterForAll(HandleUnitDeath);
        }

        private void HandleVisibilityChange(IHideable hideable, bool isVisible)
        {
            IDamageable damageable = hideable.Transform.GetComponent<IDamageable>();
            if (isVisible)
            {
                visibleDamageables.Add(damageable);
                OnUnitEnter?.Invoke(damageable);
            }
            else
            {
                visibleDamageables.Remove(damageable);
                OnUnitExit?.Invoke(damageable);
            }
        }

        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            if (allDamageables.Contains(evt.Unit))
            {
                OnTriggerExit(evt.Unit.GetComponent<Collider>());
            }
        }

        public void SetupFrom(AttackConfigSO attackConfig)
        {
            collider.radius = attackConfig.AttackRange;
        }
    }
}
