#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Text;
using GameDevTV.RTS.Commands;
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
    /// DEVELOPMENT / Editor only: a <b>teaching + edge-case</b> player bot.
    /// Purpose is to show how Colony Acts play feels and to poke failure paths —
    /// not to silently force a win (see <see cref="ColonyActsDevDemo"/> for that).
    /// Places from the real hand APIs; narrates each decision to Console + Act banner.
    /// CLI: unity command eval "return GameDevTV.RTS.Player.ColonyActsPlayerBot.RunWatch();"
    ///      unity command eval "return GameDevTV.RTS.Player.ColonyActsPlayerBot.RunStress();"
    /// Menu: Terraforming Tendencies / Dev / Run Colony Acts Player Bot (Watch|Stress)
    /// </summary>
    public static class ColonyActsPlayerBot
    {
        public enum Mode
        {
            /// <summary>Slow, narrated — watch the loop teach itself.</summary>
            Watch,
            /// <summary>Same play APIs, but deliberately probes gates / stuck paths.</summary>
            Stress
        }

        private static bool running;
        private static Mode mode = Mode.Watch;
        private static string lastResult = "idle";
        private static string lastNarration = "";

        public static string Run() => RunWatch();

        public static string RunWatch() => Begin(Mode.Watch);

        public static string RunStress() => Begin(Mode.Stress);

        public static string Status()
        {
            var acts = ColonyActManager.Instance;
            if (acts == null) return "no acts";
            int bank = Supplies.Materials != null && Supplies.Materials.TryGetValue(Owner.Player1, out int m) ? m : 0;
            return $"running={running} mode={mode} result={lastResult} act={acts.CurrentAct}/{acts.TotalActs} " +
                   $"bank={bank}/{acts.CorpQuota} climate={acts.IsClimateMet} weeks={acts.WeeksRemaining} " +
                   $"ended={acts.IsRunEnded} between={acts.IsBetweenActs} hand={(CardDeckController.Instance?.Hand?.Count ?? 0)} " +
                   $"last={lastNarration}";
        }

        private static string Begin(Mode runMode)
        {
            if (!Application.isPlaying)
                return "FAIL: Enter Play Mode first (unity command editor_play).";

            if (running)
                return "BUSY: Player bot already running.";

            var host = Object.FindAnyObjectByType<ColonyActManager>();
            if (host == null)
                return "FAIL: No ColonyActManager.";

            mode = runMode;
            ColonyActManager.DevIgnoreSectorTerraformGate = false;
            var runner = host.gameObject.GetComponent<PlayerBotRunner>();
            if (runner == null)
                runner = host.gameObject.AddComponent<PlayerBotRunner>();
            running = true;
            lastResult = "STARTED";
            lastNarration = $"mode={mode}";
            runner.Begin(runMode);
            return mode == Mode.Watch
                ? "STARTED: Watch bot — slow + narrated (Console / Colony Acts banner). Status() to poll."
                : "STARTED: Stress bot — probes gates & edge cases while playing. Status() to poll.";
        }

#if UNITY_EDITOR
        [MenuItem("Terraforming Tendencies/Dev/Run Colony Acts Player Bot (Watch)")]
        private static void MenuWatch()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[ColonyActsPlayerBot] Enter Play Mode first.");
                return;
            }
            Debug.Log(RunWatch());
        }

        [MenuItem("Terraforming Tendencies/Dev/Run Colony Acts Player Bot (Stress)")]
        private static void MenuStress()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[ColonyActsPlayerBot] Enter Play Mode first.");
                return;
            }
            Debug.Log(RunStress());
        }
#endif

        private sealed class PlayerBotRunner : MonoBehaviour
        {
            private Mode runMode;
            private int stuckTicks;
            private int focusSectorCursor;
            private int actionsTaken;
            private int edgeCasesProbed;
            private readonly List<Vector2Int> neighborScratch = new(8);
            private readonly StringBuilder log = new();

            public void Begin(Mode m)
            {
                runMode = m;
                StartCoroutine(PlayLoop());
            }

            private IEnumerator PlayLoop()
            {
                log.Clear();
                log.AppendLine($"[ColonyActsPlayerBot] {runMode} — teach the loop / probe edges (no cheats).");
                float prevScale = Time.timeScale;
                // Watch: realtime so you can follow. Stress: modest speedup.
                Time.timeScale = runMode == Mode.Watch ? 1f : 2f;
                stuckTicks = 0;
                focusSectorCursor = 0;
                actionsTaken = 0;
                edgeCasesProbed = 0;

                try
                {
                    float waitUntil = Time.unscaledTime + 90f;
                    while (Time.unscaledTime < waitUntil)
                    {
                        var a = ColonyActManager.Instance;
                        if (a != null && a.IsRunActive)
                            break;
                        yield return null;
                    }

                    var acts = ColonyActManager.Instance;
                    if (acts == null || !acts.IsRunActive)
                    {
                        Finish("FAIL: Acts never became active.");
                        yield break;
                    }

                    Narrate(acts,
                        $"Act {acts.CurrentAct} {acts.CurrentActName}: earn {acts.CorpQuota} Mats in bank, meet climate, spend weeks on plays.",
                        6f);

                    int guard = 0;
                    int lastAct = acts.CurrentAct;
                    while (acts != null && !acts.IsRunEnded && guard++ < 2500)
                    {
                        if (acts.CurrentAct != lastAct)
                        {
                            lastAct = acts.CurrentAct;
                            Narrate(acts,
                                $"Entered Act {acts.CurrentAct} {acts.CurrentActName} — quota {acts.CorpQuota}, weeks {acts.WeeksRemaining}.",
                                5f);
                            yield return WaitReadable(1.2f);
                        }

                        if (acts.IsBetweenActs)
                        {
                            yield return HandleShopNarrated(acts);
                            stuckTicks = 0;
                            acts = ColonyActManager.Instance;
                            continue;
                        }

                        // Stress mode: occasionally poke a known gate before a real play.
                        if (runMode == Mode.Stress && actionsTaken > 0 && actionsTaken % 7 == 0)
                        {
                            yield return ProbeEdgeCase(acts);
                        }

                        bool acted = TryOneAction(acts, out string why);
                        if (acted)
                        {
                            stuckTicks = 0;
                            actionsTaken++;
                            Narrate(acts, why, 3.5f);
                            yield return WaitReadable(runMode == Mode.Watch ? 1.4f : 0.45f);
                        }
                        else
                        {
                            stuckTicks++;
                            if (stuckTicks == 1 || stuckTicks % 15 == 0)
                                Narrate(acts, why, 2.5f);
                        }

                        if (stuckTicks > 25)
                        {
                            Narrate(acts, "No legal play here — Q/E to another sector.", 2f);
                            FocusNextSector();
                            stuckTicks = 0;
                            yield return WaitReadable(0.8f);
                        }

                        if (stuckTicks > 60)
                        {
                            Finish($"STUCK (useful!): {Status()} — hand may lack geology / weeks / CP focus.");
                            break;
                        }

                        // Watch: pause briefly even when idle so HUD can catch up.
                        if (runMode == Mode.Watch && !acted)
                            yield return WaitReadable(0.35f);
                        else
                            yield return null;

                        acts = ColonyActManager.Instance;
                    }

                    acts = ColonyActManager.Instance;
                    if (acts != null && acts.IsRunEnded)
                    {
                        string detail = GameOverManager.LastOutcomeDetail ?? string.Empty;
                        bool failed = detail.IndexOf("failed", System.StringComparison.OrdinalIgnoreCase) >= 0
                            || detail.IndexOf("weeks ran out", System.StringComparison.OrdinalIgnoreCase) >= 0;
                        if (failed)
                            Finish($"RESULT: FAIL — {detail} (edge case surfaced)");
                        else
                            Finish($"RESULT: WIN — played through ({actionsTaken} actions, {edgeCasesProbed} probes).");
                    }
                    else if (lastResult == "STARTED")
                    {
                        Finish($"RESULT: STOPPED — {Status()}");
                    }
                }
                finally
                {
                    Time.timeScale = prevScale;
                    running = false;
                    ColonyActManager.DevIgnoreSectorTerraformGate = false;
                }

                Debug.Log(log.ToString());
            }

            private void Finish(string result)
            {
                lastResult = result;
                log.AppendLine(result);
                Debug.Log($"[ColonyActsPlayerBot] {result}");
            }

            private static IEnumerator WaitReadable(float unscaledSeconds)
            {
                float until = Time.unscaledTime + unscaledSeconds;
                while (Time.unscaledTime < until)
                    yield return null;
            }

            private void Narrate(ColonyActManager acts, string message, float bannerSeconds)
            {
                if (string.IsNullOrEmpty(message)) return;
                lastNarration = message;
                log.AppendLine($"  · {message}");
                Debug.Log($"[ColonyActsPlayerBot] {message}");
                acts?.ShowStatusBanner($"<color=#8FE7FF><b>BOT</b></color>  {message}", bannerSeconds);
            }

            private IEnumerator HandleShopNarrated(ColonyActManager acts)
            {
                Narrate(acts,
                    $"Between-Act shop — {acts.TerraCoins} Terra-Coins. Buying upgrades (if any), then Continue.",
                    4f);
                yield return WaitReadable(runMode == Mode.Watch ? 1.5f : 0.4f);

                if (acts.TerraCoins < 20)
                {
                    Narrate(acts, "Edge: shop with &lt;20 coins — Continue with no purchase.", 3f);
                    edgeCasesProbed++;
                }
                else
                {
                    if (acts.TrySpendTerraCoins(20))
                    {
                        acts.PurchaseUpgrade(ColonyActManager.ShopUpgradeId.ExtraWeeks);
                        Narrate(acts, "Bought Extra Weeks (+2 next Act).", 3f);
                    }
                    if (runMode == Mode.Stress && acts.TerraCoins >= 25
                        && acts.TrySpendTerraCoins(25))
                    {
                        acts.PurchaseUpgrade(ColonyActManager.ShopUpgradeId.AdjacencyBump);
                        Narrate(acts, "Stress: also bought Adjacency bump.", 3f);
                    }
                }

                yield return WaitReadable(runMode == Mode.Watch ? 1.2f : 0.3f);
                if (BetweenActShopUI.IsOpen || acts.IsBetweenActs)
                    acts.CompleteBetweenActShopAndAdvance();
            }

            /// <summary>
            /// Intentionally tries illegal / gated plays to confirm toasts &amp; fail paths fire.
            /// Does not cheat past the gate — expects failure.
            /// </summary>
            private IEnumerator ProbeEdgeCase(ColonyActManager acts)
            {
                edgeCasesProbed++;
                var deck = CardDeckController.Instance;
                if (deck?.Hand == null) yield break;

                // 1) CP on an already-claimed sector (should toast / fail).
                for (int i = 0; i < deck.Hand.Count; i++)
                {
                    if (deck.Hand[i] is not UnlockBuildingCardSO u
                        || !BuildingSiteRegistry.IsCommandPostBuilding(u.buildingToUnlock))
                        continue;

                    var sm = SectorManager.Instance;
                    if (sm?.Sectors == null) break;
                    for (int s = 0; s < sm.Sectors.Count; s++)
                    {
                        if (sm.Sectors[s] == null || !SectorColonization.SectorHasCommandPost(sm.Sectors[s]))
                            continue;
                        SectorColonization.FocusCameraOnSector(s);
                        Narrate(acts, "Probe: Command Post on claimed sector (expect fail toast).", 3f);
                        PlaceBuildingCard(i, u.buildingToUnlock, sm.Sectors[s].Center, expectFail: true);
                        yield return WaitReadable(runMode == Mode.Watch ? 1.2f : 0.35f);
                        yield break;
                    }
                }

                // 2) Geology card without matching feature / wrong sector (expect fail).
                for (int i = 0; i < deck.Hand.Count; i++)
                {
                    if (deck.Hand[i] is not UnlockBuildingCardSO u || u.buildingToUnlock == null)
                        continue;
                    if (!DiscoverySystem.TryGetRequiredSectorFeature(u.buildingToUnlock, out var need))
                        continue;
                    // Drop on focused CP position — usually wrong geology.
                    var cp = SectorColonization.FindFocusedPlayerCommandPost();
                    if (cp == null) continue;
                    Narrate(acts,
                        $"Probe: {u.buildingToUnlock.Name} needs {need} — placing off-feature (expect fail).",
                        3f);
                    PlaceBuildingCard(i, u.buildingToUnlock, cp.transform.position, expectFail: true);
                    yield return WaitReadable(runMode == Mode.Watch ? 1.2f : 0.35f);
                    yield break;
                }

                // 3) Log week-budget edge when a card costs more than remaining weeks.
                for (int i = 0; i < deck.Hand.Count; i++)
                {
                    var card = deck.Hand[i];
                    if (card == null) continue;
                    int cost = CardDeckController.GetWeekCost(card);
                    if (cost > acts.WeeksRemaining && cost > 0)
                    {
                        Narrate(acts,
                            $"Probe noted: '{card.cardName}' costs {cost} weeks but only {acts.WeeksRemaining} left.",
                            3f);
                        yield return WaitReadable(0.6f);
                        yield break;
                    }
                }

                Narrate(acts, "Probe: no juicy gate this tick (hand/state).", 2f);
                yield return null;
            }

            private bool TryOneAction(ColonyActManager acts, out string why)
            {
                why = "idle";
                var deck = CardDeckController.Instance;
                if (deck?.Hand == null || deck.Hand.Count == 0)
                {
                    deck?.FillHand();
                    why = "Hand empty — drawing.";
                    return false;
                }

                EnsureUsefulFocus(acts);
                int focus = SectorColonization.GetFocusedSectorIndex();
                string sectorName = SectorColonization.GetSectorDisplayName(focus);

                int bestIdx = -1;
                float bestScore = float.NegativeInfinity;
                Vector3 bestPos = Vector3.zero;
                bool bestIsBuilding = false;
                BuildingSO bestBuilding = null;
                string bestReason = "";

                int skippedIrrelevant = 0;
                int skippedWeeks = 0;

                for (int i = 0; i < deck.Hand.Count; i++)
                {
                    var card = deck.Hand[i];
                    if (card == null) continue;
                    if (!CardDeckController.IsCardRelevantInFocusedSector(card))
                    {
                        skippedIrrelevant++;
                        continue;
                    }

                    int weekCost = CardDeckController.GetWeekCost(card);
                    if (weekCost > acts.WeeksRemaining)
                    {
                        skippedWeeks++;
                        continue;
                    }

                    if (card is UnlockBuildingCardSO unlock && unlock.buildingToUnlock != null)
                    {
                        if (!TryFindPlacement(unlock.buildingToUnlock, acts, out Vector3 pos, out float score, out string placeWhy))
                            continue;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestIdx = i;
                            bestPos = pos;
                            bestIsBuilding = true;
                            bestBuilding = unlock.buildingToUnlock;
                            bestReason = placeWhy;
                        }
                    }
                    else if (card is SpawnUnitCardSO)
                    {
                        if (!card.IsGateMet() || !card.CanApply()) continue;
                        float score = acts.IsScoreMet ? 40f : 70f;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestIdx = i;
                            bestIsBuilding = false;
                            bestBuilding = null;
                            bestReason = "spawn Mining Drone (week mining)";
                        }
                    }
                    else if (card.CanApply() && card.IsGateMet())
                    {
                        float score = 8f;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestIdx = i;
                            bestIsBuilding = false;
                            bestBuilding = null;
                            bestReason = $"play '{card.cardName}'";
                        }
                    }
                }

                if (bestIdx < 0)
                {
                    why = skippedWeeks > 0
                        ? $"{sectorName}: cards need more weeks ({skippedWeeks} gated)."
                        : skippedIrrelevant > 0
                            ? $"{sectorName}: hand filtered to other sectors ({skippedIrrelevant})."
                            : $"{sectorName}: no legal placement found.";
                    FocusNextSector();
                    return false;
                }

                int weeksBefore = acts.WeeksRemaining;
                int bankBefore = Supplies.Materials != null && Supplies.Materials.TryGetValue(Owner.Player1, out int bm) ? bm : 0;

                if (bestIsBuilding && bestBuilding != null)
                {
                    bool ok = PlaceBuildingCard(bestIdx, bestBuilding, bestPos, expectFail: false);
                    int weeksAfter = ColonyActManager.Instance != null ? ColonyActManager.Instance.WeeksRemaining : weeksBefore;
                    int bankAfter = Supplies.Materials != null && Supplies.Materials.TryGetValue(Owner.Player1, out int am) ? am : bankBefore;
                    why = ok
                        ? $"Place {bestBuilding.Name} in {sectorName} ({bestReason}). Weeks {weeksBefore}→{weeksAfter}, Mats {bankBefore}→{bankAfter}."
                        : $"Tried {bestBuilding.Name} in {sectorName} but place failed ({bestReason}).";
                    return ok;
                }

                string cardName = deck.Hand[bestIdx]?.cardName ?? "card";
                deck.PlayCard(bestIdx);
                why = $"Play {cardName} in {sectorName} ({bestReason}).";
                return true;
            }

            private void EnsureUsefulFocus(ColonyActManager acts)
            {
                var sm = SectorManager.Instance;
                if (sm?.Sectors == null || sm.Sectors.Count == 0) return;

                if (HandHasCommandPost())
                {
                    for (int i = 0; i < sm.Sectors.Count; i++)
                    {
                        var s = sm.Sectors[i];
                        if (s != null && !SectorColonization.SectorHasCommandPost(s))
                        {
                            SectorColonization.FocusCameraOnSector(i);
                            return;
                        }
                    }
                }

                int focus = SectorColonization.GetFocusedSectorIndex();
                if (focus >= 0 && focus < sm.Sectors.Count)
                {
                    var cur = sm.Sectors[focus];
                    if (cur != null && SectorColonization.SectorHasCommandPost(cur))
                        return;
                }

                FocusNextSector();
            }

            private void FocusNextSector()
            {
                var sm = SectorManager.Instance;
                if (sm?.Sectors == null || sm.Sectors.Count == 0) return;
                focusSectorCursor = ((focusSectorCursor % sm.Sectors.Count) + sm.Sectors.Count) % sm.Sectors.Count;
                SectorColonization.FocusCameraOnSector(focusSectorCursor);
                focusSectorCursor++;
            }

            private static bool HandHasCommandPost()
            {
                var hand = CardDeckController.Instance?.Hand;
                if (hand == null) return false;
                for (int i = 0; i < hand.Count; i++)
                {
                    if (hand[i] is UnlockBuildingCardSO u
                        && BuildingSiteRegistry.IsCommandPostBuilding(u.buildingToUnlock))
                        return true;
                }
                return false;
            }

            private bool TryFindPlacement(
                BuildingSO building,
                ColonyActManager acts,
                out Vector3 worldPos,
                out float score,
                out string reason)
            {
                worldPos = Vector3.zero;
                score = float.NegativeInfinity;
                reason = "";
                if (building == null) return false;

                ColonyActManager.GetTileValues(building, out _, out _, out string tag);
                string goal = UnlockBuildingCardSO.ClassifyBuildingGoal(building);

                if (BuildingSiteRegistry.IsCommandPostBuilding(building))
                {
                    Vector3 hint = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
                    if (!SectorColonization.TryGetFocusedCommandPostPlacement(hint, out worldPos, out _, out string fail))
                    {
                        reason = fail ?? "no unclaimed CP pad in focus";
                        return false;
                    }
                    score = 1000f;
                    reason = "claim focused sector";
                    return true;
                }

                if (BuildingSiteRegistry.IsMineBuilding(building))
                {
                    if (!DiscoverySystem.TryGetAutoMinePlacement(building, out worldPos, out string fail))
                    {
                        reason = fail ?? "no free deposit";
                        return false;
                    }
                    score = acts.IsScoreMet ? 80f : 400f;
                    reason = "mine deposit (quota)";
                    return true;
                }

                if (DiscoverySystem.TryGetRequiredSectorFeature(building, out var feature))
                {
                    if (!DiscoverySystem.TryGetAutoFeaturePlacement(building, out worldPos, out string fail))
                    {
                        reason = fail ?? $"need {feature} sector";
                        return false;
                    }
                    score = PriorityForGoal(goal, tag, acts) + 50f;
                    reason = $"geology {feature}";
                    return true;
                }

                if (!TrySampleFreePlacement(building, acts, out worldPos, out score))
                {
                    reason = "no free hex near colony";
                    return false;
                }
                reason = tag == "Power" ? "power adjacency"
                    : (tag is "Heat" or "Air" or "Water") ? "climate tile"
                    : "adjacency / mats";
                return true;
            }

            private bool TrySampleFreePlacement(BuildingSO building, ColonyActManager acts, out Vector3 worldPos, out float score)
            {
                worldPos = Vector3.zero;
                score = float.NegativeInfinity;

                BaseBuilding cp = SectorColonization.FindFocusedPlayerCommandPost();
                Vector3 origin = cp != null
                    ? cp.transform.position
                    : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);

                var occupied = ColonyTileGrid.GetOccupiedCells(Owner.Player1);
                var seeds = new List<Vector2Int>(32) { ColonyTileGrid.WorldToCell(origin) };
                if (occupied != null)
                {
                    foreach (var cell in occupied)
                        seeds.Add(cell);
                }

                var candidates = new HashSet<Vector2Int>();
                for (int s = 0; s < seeds.Count; s++)
                {
                    ColonyTileGrid.CollectOrthogonalNeighborCells(seeds[s], neighborScratch);
                    for (int n = 0; n < neighborScratch.Count; n++)
                        candidates.Add(neighborScratch[n]);
                    for (int n = 0; n < neighborScratch.Count; n++)
                    {
                        var ring = new List<Vector2Int>(6);
                        ColonyTileGrid.CollectOrthogonalNeighborCells(neighborScratch[n], ring);
                        for (int r = 0; r < ring.Count; r++)
                            candidates.Add(ring[r]);
                    }
                }

                var cmd = ScriptableObject.CreateInstance<BuildBuildingCommand>();
                cmd.Building = building;
                cmd.HandIndex = 0;

                ColonyActManager.GetTileValues(building, out _, out _, out string tag);
                string goal = UnlockBuildingCardSO.ClassifyBuildingGoal(building);
                float basePri = PriorityForGoal(goal, tag, acts);

                bool found = false;
                foreach (var cell in candidates)
                {
                    if (occupied != null && occupied.Contains(cell)) continue;
                    Vector3 raw = ColonyTileGrid.CellToWorld(cell, origin.y);
                    Vector3 snap = ColonyTileGrid.SnapForPlacement(raw, Owner.Player1, out _);
                    if (!cmd.AllRestrictionsPass(snap, Owner.Player1, requireWorker: false))
                        continue;

                    float s = basePri;
                    if (acts != null)
                    {
                        var preview = acts.PreviewPlacement(building, snap);
                        s += preview.EstimatedTotal * 2f;
                        if (preview.IsClimateTile && !acts.IsClimateMet) s += 80f;
                        if (tag == "Power") s += 60f;
                    }

                    if (s > score)
                    {
                        score = s;
                        worldPos = snap;
                        found = true;
                    }
                }

                Object.Destroy(cmd);
                return found;
            }

            private static float PriorityForGoal(string goal, string tag, ColonyActManager acts)
            {
                bool needMats = acts != null && !acts.IsScoreMet;
                bool needClimate = acts != null && !acts.IsClimateMet;

                return goal switch
                {
                    "COMMAND POST" => 1000f,
                    "MATERIALS" => needMats ? 400f : 60f,
                    "POWER" => 200f,
                    "TEMPERATURE" => needClimate ? 300f : 40f,
                    "ATMOSPHERE" => needClimate ? 300f : 40f,
                    "WATER" => needClimate ? 300f : 40f,
                    "POPULATION" => 50f,
                    _ => tag switch
                    {
                        "Industry" => needMats ? 350f : 50f,
                        "Heat" or "Air" or "Water" => needClimate ? 280f : 40f,
                        "Power" => 180f,
                        "Anchor" => 70f,
                        _ => 20f
                    }
                };
            }

            private static bool PlaceBuildingCard(int handIndex, BuildingSO building, Vector3 worldPos, bool expectFail)
            {
                int handBefore = CardDeckController.Instance?.Hand?.Count ?? 0;
                var cmd = ScriptableObject.CreateInstance<BuildBuildingCommand>();
                cmd.Name = building.Name;
                cmd.Building = building;
                cmd.Icon = building.Icon;
                cmd.HandIndex = handIndex;

                var hit = new RaycastHit { point = worldPos };
                cmd.Handle(new CommandContext(Owner.Player1, null, hit));

                bool consumed = cmd.HandIndex < 0
                    || (CardDeckController.Instance?.Hand?.Count ?? handBefore) < handBefore;
                Object.Destroy(cmd);

                if (expectFail)
                    return !consumed; // success of probe = gate held
                return consumed;
            }
        }
    }
}
#endif
