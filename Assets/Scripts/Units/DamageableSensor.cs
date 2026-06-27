using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using UnityEngine;
using GameDevTV.RTS.VisualScriptingStubs;

namespace GameDevTV.RTS.Units
{
    [IncludeInSettings(true)]
    [RequireComponent(typeof(SphereCollider))]
    public class DamageableSensor : MonoBehaviour
    {
        public List<IDamageable> Damageables => visibleDamageables.ToList();
        [field: SerializeField] public Owner Owner { get; set; }

        public delegate void UnitDetectionEvent(IDamageable damageable);
        public event UnitDetectionEvent OnUnitEnter;
        public event UnitDetectionEvent OnUnitExit;

        private SphereCollider sphereCollider;
        private HashSet<IDamageable> visibleDamageables = new();
        private HashSet<IDamageable> allDamageables = new();

        private void Awake()
        {
            sphereCollider = GetComponent<SphereCollider>();
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
            foreach (IDamageable damageable in allDamageables)
            {
                // A plain interface != null check does NOT use Unity's overloaded null
                // operator, so we must cast to UnityEngine.Object to detect destroyed
                // objects before touching .Transform (which would throw).
                if (damageable == null || (damageable is Object obj && obj == null)) continue;
                if (damageable.Transform != null && damageable.Transform.TryGetComponent(out IHideable hideable))
                {
                    hideable.OnVisibilityChanged -= HandleVisibilityChange;
                }
            }
            Bus<UnitDeathEvent>.UnregisterForAll(HandleUnitDeath);
        }

        private void HandleVisibilityChange(IHideable hideable, bool isVisible)
        {
            if (hideable == null || hideable.Transform == null) return;

            IDamageable damageable = hideable.Transform.GetComponent<IDamageable>();
            if (damageable == null) return;

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
            if (evt.Unit == null || (evt.Unit is Object o && o == null)) return;
            if (allDamageables.Contains(evt.Unit))
            {
                Collider col = evt.Unit.GetComponent<Collider>();
                if (col != null)
                {
                    OnTriggerExit(col);
                }
            }
        }

        public void SetupFromRange(float range)
        {
            sphereCollider.radius = range;
        }

        public void SetupFrom(AttackConfigSO attackConfig)
        {
            sphereCollider.radius = attackConfig.AttackRange;
        }
    }
}
