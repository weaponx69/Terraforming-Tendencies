#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Text;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// DEVELOPMENT / Editor only: autoplay through all Colony Acts to a run win.
    /// CLI: unity command eval "return GameDevTV.RTS.Player.ColonyActsDevDemo.Run();" --json
    /// Menu: Terraforming Tendencies / Dev / Run Colony Acts Demo
    /// </summary>
    public static class ColonyActsDevDemo
    {
        private static bool running;

        public static string Run()
        {
            if (!Application.isPlaying)
                return "FAIL: Enter Play Mode first (unity command editor_play).";

            if (running)
                return "BUSY: Demo already running.";

            var host = Object.FindAnyObjectByType<ColonyActManager>();
            if (host == null)
                return "FAIL: No ColonyActManager.";

            ColonyActManager.DevIgnoreSectorTerraformGate = false;
            var runner = host.gameObject.GetComponent<DemoRunner>();
            if (runner == null)
                runner = host.gameObject.AddComponent<DemoRunner>();
            running = true;
            runner.Begin();
            return "STARTED: Colony Acts full-run demo (watch Console / call Status()).";
        }

        public static string Status()
        {
            var acts = ColonyActManager.Instance;
            if (acts == null) return "no acts";
            return $"running={running} act={acts.CurrentAct}/{acts.TotalActs} " +
                   $"quota={acts.MaterialsEarnedThisAct}/{acts.CorpQuota} " +
                   $"climate={acts.IsClimateMet} weeks={acts.WeeksRemaining} " +
                   $"ended={acts.IsRunEnded} between={acts.IsBetweenActs}";
        }

#if UNITY_EDITOR
        [MenuItem("Terraforming Tendencies/Dev/Run Colony Acts Demo")]
        private static void MenuRun()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[ColonyActsDevDemo] Enter Play Mode first.");
                return;
            }
            Debug.Log(Run());
        }
#endif

        private sealed class DemoRunner : MonoBehaviour
        {
            public void Begin() => StartCoroutine(PlayThrough());

            private IEnumerator PlayThrough()
            {
                var sb = new StringBuilder();
                sb.AppendLine("[ColonyActsDevDemo] Starting full-run…");
                float prevScale = Time.timeScale;
                Time.timeScale = 8f;

                try
                {
                    var acts = ColonyActManager.Instance;
                    int guard = 0;
                    while (acts != null && !acts.IsRunEnded && guard++ < 80)
                    {
                        if (acts.IsBetweenActs)
                        {
                            acts.CompleteBetweenActShopAndAdvance();
                            yield return null;
                            continue;
                        }

                        EnsureCommandPosts();
                        GrantQuotaAndClimate(acts);
                        yield return null;

                        if (BetweenActShopUI.IsOpen)
                            acts.CompleteBetweenActShopAndAdvance();

                        yield return null;
                        acts = ColonyActManager.Instance;
                    }

                    acts = ColonyActManager.Instance;
                    if (acts != null && acts.IsRunEnded)
                        sb.AppendLine("RESULT: WIN — run ended (check Acts cleared).");
                    else
                        sb.AppendLine($"RESULT: STOPPED — {Status()}");
                }
                finally
                {
                    Time.timeScale = prevScale;
                    running = false;
                    ColonyActManager.DevIgnoreSectorTerraformGate = false;
                }

                Debug.Log(sb.ToString());
            }

            private static void GrantQuotaAndClimate(ColonyActManager acts)
            {
                if (acts == null || acts.IsBetweenActs || acts.IsRunEnded) return;

                int need = Mathf.Max(0, acts.CorpQuota - acts.MaterialsEarnedThisAct + 5);
                if (need > 0)
                    acts.CreditMaterials(need, spawnPopupAt: null);

                if (!acts.IsClimateMet)
                {
                    acts.GetActClimateRequirements(out float needT, out float needA, out float needW);
                    float t = Supplies.Temperature.TryGetValue(Owner.Player1, out float tv) ? tv : -60f;
                    float a = Supplies.Atmosphere.TryGetValue(Owner.Player1, out float av) ? av : 0.01f;
                    float w = Supplies.Water.TryGetValue(Owner.Player1, out float wv) ? wv : 0f;
                    float targetT = acts.BaselineTemperature + needT;
                    float targetA = acts.BaselineAtmosphere + needA;
                    float targetW = acts.BaselineWater + needW;
                    if (t < targetT - 0.01f)
                        Supplies.UpdateTemperature(Owner.Player1, targetT);
                    if (a < targetA - 0.001f)
                        Supplies.UpdateAtmosphere(Owner.Player1, targetA);
                    if (w < targetW - 0.01f)
                        Supplies.UpdateWater(Owner.Player1, targetW);
                }

                if (acts.CurrentAct >= acts.TotalActs)
                    ColonyActManager.DevIgnoreSectorTerraformGate = true;
            }

            private static void EnsureCommandPosts()
            {
                var sm = SectorManager.Instance;
                if (sm?.Sectors == null) return;

                for (int i = 0; i < sm.Sectors.Count; i++)
                {
                    var sector = sm.Sectors[i];
                    if (sector == null) continue;
                    if (!SectorColonization.SectorHasCommandPost(sector))
                        SectorColonization.TryAutoPlaceCommandPost(sector, Owner.Player1, out _);
                }
            }
        }
    }
}
#endif
