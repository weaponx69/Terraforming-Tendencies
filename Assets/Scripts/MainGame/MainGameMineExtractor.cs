using System;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.MainGame
{
    /// <summary>
    /// Mine building on a discovered deposit — weekly typed stock credit + harvest drone VFX.
    /// </summary>
    public class MainGameMineExtractor : MonoBehaviour
    {
        public MainGameResourceId ResourceType { get; private set; }
        public GatherableSupply Deposit { get; private set; }

        MainGameHarvestDrones drones;
        Func<int, int> takeDelegate;
        bool registered;

        public void Bind(MainGameResourceId type, GatherableSupply deposit)
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

        void Register()
        {
            if (registered || Deposit == null) return;
            var colony = MainGameController.Instance != null ? MainGameController.Instance.Colony : null;
            if (colony == null) return;
            takeDelegate ??= TakeFromDeposit;
            colony.RegisterMineSource(ResourceType, takeDelegate);
            registered = true;
        }

        void Unregister()
        {
            if (!registered) return;
            var colony = MainGameController.Instance != null ? MainGameController.Instance.Colony : null;
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
            if (Deposit.Amount <= 0 && drones != null)
                drones.Clear();
            return g;
        }

        void EnsureDrones()
        {
            if (drones == null)
                drones = gameObject.AddComponent<MainGameHarvestDrones>();
            drones.Begin(transform.position, 2);
        }

        /// <summary>Find a discovered deposit of this type on the same colony tile as <paramref name="world"/>.</summary>
        public static GatherableSupply FindDepositAt(Vector3 world, MainGameResourceId type)
        {
            var cell = ColonyTileGrid.WorldToCell(world);
            string want = MainGameResourceIdUtil.ToTypeName(type);
            GatherableSupply best = null;
            float bestDist = float.MaxValue;
            foreach (var hr in UnityEngine.Object.FindObjectsByType<HiddenResource>(FindObjectsInactive.Exclude))
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
            if (!MainGameController.IsActive || building == null || so == null) return false;
            if (!DiscoverySystem.TryGetMineResourceType(so, out string typeName)) return false;
            if (!MainGameResourceIdUtil.TryParse(typeName, out var id)) return false;

            var deposit = FindDepositAt(building.transform.position, id);
            if (deposit == null) return false;

            var existing = building.GetComponent<MainGameMineExtractor>();
            if (existing == null) existing = building.gameObject.AddComponent<MainGameMineExtractor>();
            existing.Bind(id, deposit);

            var colony = MainGameController.Instance?.Colony;
            if (colony != null)
            {
                colony.MarkResourceKnown(id);
                MainGameController.Instance.RefreshHandUnlocks();
            }
            return true;
        }
    }
}
