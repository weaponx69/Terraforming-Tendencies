using System;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>
    /// Mine building on a discovered deposit — weekly typed stock credit + harvest drone VFX.
    /// </summary>
    public class PrologueMineExtractor : MonoBehaviour
    {
        public PrologueResourceId ResourceType { get; private set; }
        public GatherableSupply Deposit { get; private set; }

        PrologueHarvestDrones drones;
        Func<int, int> takeDelegate;
        bool registered;

        public void Bind(PrologueResourceId type, GatherableSupply deposit)
        {
            Unregister();
            ResourceType = type;
            Deposit = deposit;
            takeDelegate ??= TakeFromDeposit;
            Register();
            EnsureDrones();
        }

        void OnEnable()
        {
            if (Deposit != null) Register();
        }

        void OnDisable() => Unregister();

        void OnDestroy()
        {
            Unregister();
            if (drones != null)
            {
                drones.Clear();
                Destroy(drones);
                drones = null;
            }
        }

        void Update()
        {
            if ((Deposit == null || Deposit.Amount <= 0) && drones != null)
                drones.Clear();
        }

        void Register()
        {
            if (registered || Deposit == null) return;
            var colony = PrologueController.Instance != null ? PrologueController.Instance.Colony : null;
            if (colony == null) return;
            takeDelegate ??= TakeFromDeposit;
            colony.RegisterMineSource(ResourceType, takeDelegate);
            registered = true;
        }

        void Unregister()
        {
            if (!registered) return;
            var colony = PrologueController.Instance != null ? PrologueController.Instance.Colony : null;
            if (takeDelegate != null)
                colony?.UnregisterMineSource(takeDelegate);
            registered = false;
        }

        int TakeFromDeposit(int want)
        {
            if (Deposit == null || want <= 0) return 0;
            int g = Mathf.Min(want, Mathf.Max(0, Deposit.Amount));
            if (g <= 0) return 0;
            Deposit.Amount -= g;
            return g;
        }

        void EnsureDrones()
        {
            if (drones == null)
                drones = gameObject.AddComponent<PrologueHarvestDrones>();
            drones.Begin(transform.position, 2);
        }

        /// <summary>Find a discovered deposit of this type on the same colony tile as <paramref name="world"/>.</summary>
        public static GatherableSupply FindDepositAt(Vector3 world, PrologueResourceId type)
        {
            var cell = ColonyTileGrid.WorldToCell(world);
            string want = PrologueResourceIdUtil.ToTypeName(type);
            GatherableSupply best = null;
            float bestDist = float.MaxValue;
            foreach (var hr in Object.FindObjectsByType<HiddenResource>(FindObjectsInactive.Exclude))
            {
                if (hr == null || !hr.IsDiscovered) continue;
                if (!string.Equals(hr.ResourceTypeName, want, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                if (ColonyTileGrid.WorldToCell(hr.transform.position) != cell) continue;
                if (!hr.TryGetComponent(out GatherableSupply gs)) continue;
                float d = (hr.transform.position - world).sqrMagnitude;
                if (d >= bestDist) continue;
                bestDist = d;
                best = gs;
            }
            return best;
        }

        public static bool TryAttachToBuilding(BaseBuilding building, BuildingSO so)
        {
            if (!PrologueController.IsActive || building == null || so == null) return false;
            if (!DiscoverySystem.TryGetMineResourceType(so, out string typeName)) return false;
            if (!PrologueResourceIdUtil.TryParse(typeName, out var id)) return false;

            var deposit = FindDepositAt(building.transform.position, id);
            if (deposit == null) return false;

            var existing = building.GetComponent<PrologueMineExtractor>();
            if (existing == null) existing = building.gameObject.AddComponent<PrologueMineExtractor>();
            existing.Bind(id, deposit);

            var colony = PrologueController.Instance?.Colony;
            if (colony != null)
            {
                colony.MarkResourceKnown(id);
                PrologueController.Instance.RefreshHandUnlocks();
            }
            return true;
        }
    }
}
