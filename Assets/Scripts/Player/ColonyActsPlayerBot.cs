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
    /// DEVELOPMENT / Editor only: plays Colony Acts through real player APIs
    /// (hand → place / PlayCard → weeks → shop). No CreditMaterials / climate cheats.
    /// CLI: unity command eval "return GameDevTV.RTS.Player.ColonyActsPlayerBot.Run();" --json
    /// Menu: Terraforming Tendencies / Dev / Run Colony Acts Player Bot
    /// </summary>
    public static class ColonyActsPlayerBot
    {
        private static bool running;
        private static string lastResult = "idle";

        public static string Run()
        {
            if (!Application.isPlaying)
                return "FAIL: Enter Play Mode first (unity command editor_play).";

            if (running)
                return "BUSY: Player bot already running.";

            var host = Object.FindAnyObjectByType<ColonyActManager>();
            if (host == null)
                return "FAIL: No ColonyActManager.";

            ColonyActManager.DevIgnoreSectorTerraformGate = false;
            var runner = host.gameObject.GetComponent<PlayerBotRunner>();
            if (runner == null)
                runner = host.gameObject.AddComponent<PlayerBotRunner>();
            running = true;
            lastResult = "STARTED";
            runner.Begin();
            return "STARTED: Colony Acts player bot (places from hand; watch Console / Status()).";
        }

        public static string Status()
        {
            var acts = ColonyActManager.Instance;
            if (acts == null) return "no acts";
            int bank = Supplies.Materials != null && Supplies.Materials.TryGetValue(Owner.Player1, out int m) ? m : 0;
            return $"running={running} result={lastResult} act={acts.CurrentAct}/{acts.TotalActs} " +
                   $"bank={bank}/{acts.CorpQuota} climate={acts.IsClimateMet} weeks={acts.WeeksRemaining} " +
                   $"ended={acts.IsRunEnded} between={acts.IsBetweenActs} hand={(CardDeckController.Instance?.Hand?.Count ?? 0)}";
        }

#if UNITY_EDITOR
        [MenuItem("Terraforming Tendencies/Dev/Run Colony Acts Player Bot")]
        private static void MenuRun()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[ColonyActsPlayerBot] Enter Play Mode first.");
                return;
            }
            Debug.Log(Run());
        }
#endif

        private sealed class PlayerBotRunner : MonoBehaviour
        {
            private int stuckTicks;
            private int focusSectorCursor;
            private readonly List<Vector2Int> neighborScratch = new(8);
            private readonly StringBuilder log = new();

            public void Begin() => StartCoroutine(PlayLoop());

            private IEnumerator PlayLoop()
            {
                log.Clear();
                log.AppendLine("[ColonyActsPlayerBot] Starting real playthrough…");
                float prevScale = Time.timeScale;
                // Slightly sped up so a full run finishes in a reasonable CLI wait,
                // but not so fast that placement / week spend races itself.
                Time.timeScale = 3f;
                stuckTicks = 0;
                focusSectorCursor = 0;

                try
                {
                    // Wait for Acts to start after planet gen.
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
                        lastResult = "FAIL: Acts never became active.";
                        log.AppendLine(lastResult);
                        yield break;
                    }

                    int guard = 0;
                    while (acts != null && !acts.IsRunEnded && guard++ < 2500)
                    {
                        if (acts.IsBetweenActs)
                        {
                            HandleShop(acts);
                            stuckTicks = 0;
                            yield return null;
                            acts = ColonyActManager.Instance;
                            continue;
                        }

                        bool acted = TryOneAction(acts);
                        if (acted)
                            stuckTicks = 0;
                        else
                            stuckTicks++;

                        if (stuckTicks > 40)
                        {
                            // Rotate focus and try again a few times before giving up.
                            focusSectorCursor++;
                            FocusNextSector();
                            stuckTicks = 0;
                            if (!TryOneAction(acts))
                                stuckTicks = 20;
                        }

                        if (stuckTicks > 80)
                        {
                            lastResult = $"STUCK: {Status()}";
                            log.AppendLine(lastResult);
                            break;
                        }

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
                            lastResult = $"RESULT: FAIL — {detail}";
                        else
                            lastResult = "RESULT: WIN — run ended via real play.";
                    }
                    else if (string.IsNullOrEmpty(lastResult) || lastResult == "STARTED")
                    {
                        lastResult = $"RESULT: STOPPED — {Status()}";
                    }

                    log.AppendLine(lastResult);
                }
                finally
                {
                    Time.timeScale = prevScale;
                    running = false;
                    ColonyActManager.DevIgnoreSectorTerraformGate = false;
                }

                Debug.Log(log.ToString());
            }

            private static void HandleShop(ColonyActManager acts)
            {
                // Prefer Extra Weeks when the previous Act felt week-tight; then Continue.
                if (acts.TerraCoins >= 20)
                    TryBuy(acts, ColonyActManager.ShopUpgradeId.ExtraWeeks, 20);
                if (acts.TerraCoins >= 20)
                    TryBuy(acts, ColonyActManager.ShopUpgradeId.GeologyBonus, 20);
                if (acts.TerraCoins >= 25)
                    TryBuy(acts, ColonyActManager.ShopUpgradeId.AdjacencyBump, 25);

                if (BetweenActShopUI.IsOpen || acts.IsBetweenActs)
                    acts.CompleteBetweenActShopAndAdvance();
            }

            private static void TryBuy(ColonyActManager acts, ColonyActManager.ShopUpgradeId id, int cost)
            {
                if (acts.TrySpendTerraCoins(cost))
                    acts.PurchaseUpgrade(id);
            }

            private bool TryOneAction(ColonyActManager acts)
            {
                var deck = CardDeckController.Instance;
                if (deck?.Hand == null || deck.Hand.Count == 0)
                {
                    deck?.FillHand();
                    return false;
                }

                EnsureUsefulFocus(acts);

                // Priority: CP → mine → unmet climate → power → drone → other mats tiles.
                int bestIdx = -1;
                float bestScore = float.NegativeInfinity;
                Vector3 bestPos = Vector3.zero;
                bool bestIsBuilding = false;
                BuildingSO bestBuilding = null;

                for (int i = 0; i < deck.Hand.Count; i++)
                {
                    var card = deck.Hand[i];
                    if (card == null) continue;
                    if (!CardDeckController.IsCardRelevantInFocusedSector(card)) continue;

                    int weekCost = CardDeckController.GetWeekCost(card);
                    if (weekCost > acts.WeeksRemaining) continue;

                    if (card is UnlockBuildingCardSO unlock && unlock.buildingToUnlock != null)
                    {
                        if (!TryFindPlacement(unlock.buildingToUnlock, acts, out Vector3 pos, out float score))
                            continue;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestIdx = i;
                            bestPos = pos;
                            bestIsBuilding = true;
                            bestBuilding = unlock.buildingToUnlock;
                        }
                    }
                    else if (card is SpawnUnitCardSO)
                    {
                        if (!card.IsGateMet() || !card.CanApply()) continue;
                        float score = 40f; // Mining drones help week mining.
                        if (!acts.IsScoreMet) score += 30f;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestIdx = i;
                            bestIsBuilding = false;
                            bestBuilding = null;
                        }
                    }
                    else if (card.CanApply() && card.IsGateMet())
                    {
                        // Low-value non-buildings (scouting etc.) — only if nothing else.
                        float score = 5f;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestIdx = i;
                            bestIsBuilding = false;
                            bestBuilding = null;
                        }
                    }
                }

                if (bestIdx < 0)
                {
                    // No play in this sector — try another.
                    FocusNextSector();
                    return false;
                }

                if (bestIsBuilding && bestBuilding != null)
                    return PlaceBuildingCard(bestIdx, bestBuilding, bestPos);

                deck.PlayCard(bestIdx);
                return true;
            }

            private void EnsureUsefulFocus(ColonyActManager acts)
            {
                var sm = SectorManager.Instance;
                if (sm?.Sectors == null || sm.Sectors.Count == 0) return;

                // Prefer an unclaimed sector if we have a CP in hand.
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

                // Else prefer a claimed sector that still needs climate / terraforming.
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

            private bool TryFindPlacement(BuildingSO building, ColonyActManager acts, out Vector3 worldPos, out float score)
            {
                worldPos = Vector3.zero;
                score = float.NegativeInfinity;
                if (building == null) return false;

                ColonyActManager.GetTileValues(building, out _, out _, out string tag);
                string goal = UnlockBuildingCardSO.ClassifyBuildingGoal(building);

                // Command Post — focused unclaimed sector pad.
                if (BuildingSiteRegistry.IsCommandPostBuilding(building))
                {
                    Vector3 hint = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
                    if (!SectorColonization.TryGetFocusedCommandPostPlacement(hint, out worldPos, out _, out _))
                        return false;
                    score = 1000f;
                    return true;
                }

                // Mines — auto deposit.
                if (BuildingSiteRegistry.IsMineBuilding(building))
                {
                    if (!DiscoverySystem.TryGetAutoMinePlacement(building, out worldPos, out _))
                        return false;
                    score = acts.IsScoreMet ? 80f : 400f;
                    return true;
                }

                // Geology-gated features.
                if (DiscoverySystem.TryGetRequiredSectorFeature(building, out _))
                {
                    if (!DiscoverySystem.TryGetAutoFeaturePlacement(building, out worldPos, out _))
                        return false;
                    score = PriorityForGoal(goal, tag, acts) + 50f;
                    return true;
                }

                // Free-ground: sample near focused CP / occupied tiles.
                if (!TrySampleFreePlacement(building, acts, out worldPos, out score))
                    return false;
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
                var seeds = new List<Vector2Int>(32);
                Vector2Int originCell = ColonyTileGrid.WorldToCell(origin);
                seeds.Add(originCell);
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
                    // Second ring.
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
                cmd.HandIndex = 0; // mark as card place for restriction path

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
                        if (preview.IsClimateTile && !acts.IsClimateMet)
                            s += 80f;
                        if (tag == "Power")
                            s += 60f;
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

            private static bool PlaceBuildingCard(int handIndex, BuildingSO building, Vector3 worldPos)
            {
                var cmd = ScriptableObject.CreateInstance<BuildBuildingCommand>();
                cmd.Name = building.Name;
                cmd.Building = building;
                cmd.Icon = building.Icon;
                cmd.HandIndex = handIndex;

                var hit = new RaycastHit { point = worldPos };
                cmd.Handle(new CommandContext(Owner.Player1, null, hit));

                // Command may have consumed the card (HandIndex cleared) or failed silently.
                bool ok = cmd.HandIndex < 0;
                Object.Destroy(cmd);
                return ok;
            }
        }
    }
}
#endif
