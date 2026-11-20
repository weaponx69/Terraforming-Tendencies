using System;
using System.Collections.Generic;

namespace GameDevTV.RTS.Prologue
{
    public sealed class PrologueColony
    {
        public PrologueBoardState Board { get; } = new();
        public int Week { get; private set; }
        public int Oxygen { get; private set; } = PrologueData.LandingOxygen;
        public int IceStock { get; private set; }
        public int Colonists { get; private set; } = PrologueData.StartColonists;
        public int PrologueStep { get; private set; }
        public bool DustStormActive { get; private set; }
        public bool SolarDarkened => DustStormActive;
        public int ShuttleLandWeek { get; private set; } = -1;
        public bool ShuttleLanded { get; private set; }
        public int StormLandWeek { get; private set; } = -1;
        public HashSet<PrologueBuildingId> Unlocked { get; } = new();

        Func<int, int, int> _dist = (a, b) => a == b ? 0 : 99;

        public static PrologueColony CreateLanding()
        {
            var c = new PrologueColony();
            for (int i = 0; i < 16; i++)
                c.Board.Tiles.Add(new PrologueTile { Index = i, Lit = i == 0, Revealed = i == 0 });
            c.Board.At(0).Building = PrologueBuildingId.CommandPost;
            c.Board.At(0).Lit = true;
            c.Board.At(0).Revealed = true;
            c.Oxygen = PrologueData.LandingOxygen;
            c.Colonists = PrologueData.StartColonists;
            return c;
        }

        public void SetDistanceFn(Func<int, int, int> fn) => _dist = fn;

        public int Dist(int a, int b) => _dist(a, b);

        public void DebugPlaceComplete(int tile, PrologueBuildingId id)
        {
            Board.At(tile).Building = id;
            Board.At(tile).Revealed = true;
        }

        public void DebugForceStorm(bool on) => DustStormActive = on;

        public void StartPrologue()
        {
            PrologueStep = 0;
            Unlocked.Clear();
            ShuttleLandWeek = -1;
            ShuttleLanded = false;
            StormLandWeek = -1;
            DustStormActive = false;
            Board.FirstIceTiles.Clear();
        }

        public bool Preview(int tile, PrologueBuildingId id, out string reason)
        {
            var cell = Board.At(tile);
            if (cell.Building != PrologueBuildingId.None || SiteOnTile(tile))
            {
                reason = "occupied";
                return false;
            }
            if (!Unlocked.Contains(id))
            {
                reason = "Not yet: finish the objective at the top first";
                return false;
            }
            if (id == PrologueBuildingId.Aquifer)
            {
                if (!HasIceInHarvestRange(tile))
                {
                    reason = $"Ice Aquifer must be within {PrologueData.HarvestRange} tiles of revealed ice";
                    return false;
                }
                // Step 4+: the second drill must reach the NEW ice, not the first deposit.
                if (PrologueStep >= 4 && NewIceTile() >= 0 && !ReachesNewIce(tile))
                {
                    reason = $"Build this Ice Aquifer within {PrologueData.HarvestRange} tiles of the newly found ice";
                    return false;
                }
            }
            reason = "";
            return true;
        }

        public bool TryPlace(int tile, PrologueBuildingId id, out string reason)
        {
            if (!Preview(tile, id, out reason)) return false;
            int buildWeeks = PrologueData.Get(id).BuildWeeks;
            if (buildWeeks <= 0)
            {
                Board.At(tile).Building = id;
            }
            else
            {
                Board.Sites.Add(new PrologueSite
                {
                    Tile = tile,
                    Id = id,
                    WeeksDone = 0,
                    WeeksTotal = buildWeeks
                });
            }
            StoryCheck(new List<string>());
            reason = "";
            return true;
        }

        /// <summary>
        /// Fellowship v1: place as a finished building (skip multi-week site wait) so the board matches Unreal step checks immediately.
        /// </summary>
        public bool TryPlaceImmediate(int tile, PrologueBuildingId id, out string reason)
        {
            if (!Preview(tile, id, out reason)) return false;
            Board.At(tile).Building = id;
            Board.At(tile).Revealed = true;
            // Drop any site that was queued on this tile.
            Board.Sites.RemoveAll(s => s.Tile == tile);
            StoryCheck(new List<string>());
            reason = "";
            return true;
        }

        public void RevealTiles(IEnumerable<int> path)
        {
            foreach (int index in path)
            {
                var t = Board.At(index);
                t.Revealed = true;
                t.Lit = true;
                if (PrologueStep == 0
                    && (t.Feature == PrologueFeature.Ice || t.Feature == PrologueFeature.Glacier)
                    && t.DepositLeft > 0)
                {
                    Board.FirstIceTiles.Add(index);
                }
                PrologueSearchedTiles.Instance?.MarkTile(index);
            }
            PrologueSearchedTiles.Instance?.RefreshAll();
            StoryCheck(new List<string>());
        }

        public string ObjectiveText(out PrologueBuildingId suggestBuild, out bool wantDrone)
        {
            suggestBuild = PrologueBuildingId.None;
            wantDrone = false;
            switch (PrologueStep)
            {
                case 0:
                    wantDrone = true;
                    return "Find ice: play Survey Team, paint up to 5 tiles toward a yellow ?, then ✓ (3 days)";
                case 1:
                    suggestBuild = PrologueBuildingId.Aquifer;
                    return "Build an ice drill (Ice Aquifer) within 3 tiles of the ice";
                case 2:
                    suggestBuild = PrologueBuildingId.Oxygen;
                    return "Build an Oxygen Processor and give it power (a Solar Array beside it)";
                case 3:
                    suggestBuild = PrologueBuildingId.Habitat;
                    if (ShuttleLandWeek >= 0)
                    {
                        int weeksLeft = Math.Max(0, ShuttleLandWeek - Week);
                        return $"Build two Habitat Domes before the shuttle lands ({weeksLeft} week{(weeksLeft == 1 ? "" : "s")})";
                    }
                    return "Build two Habitat Domes for the shuttle crew";
                case 4:
                {
                    int newIce = NewIceTile();
                    if (newIce < 0)
                    {
                        wantDrone = true;
                        return "Find more ice: play Survey Team, paint toward a yellow ?, then ✓";
                    }
                    suggestBuild = PrologueBuildingId.Aquifer;
                    return "Build a second ice drill (Ice Aquifer) within 3 tiles of the new ice";
                }
                case 5:
                    if (!HasBuilding(PrologueBuildingId.WindTurbine))
                    {
                        suggestBuild = PrologueBuildingId.WindTurbine;
                        return "Build a Wind Turbine beside the Oxygen Processor before the dust storm";
                    }
                    return "Store oxygen and ride out the dust storm";
                default:
                    return "Prologue complete";
            }
        }

        public int NewIceTile()
        {
            foreach (var t in Board.Tiles)
            {
                if (!t.Revealed || t.DepositLeft <= 0) continue;
                if (t.Feature != PrologueFeature.Ice && t.Feature != PrologueFeature.Glacier) continue;
                if (Board.FirstIceTiles.Contains(t.Index)) continue;
                return t.Index;
            }
            return -1;
        }

        public bool ReachesNewIce(int cell)
        {
            int ice = NewIceTile();
            if (ice < 0) return false;
            return Dist(cell, ice) <= PrologueData.HarvestRange;
        }

        public bool SecondIceDrill()
        {
            bool firstDepleted = FirstIceDepleted();
            foreach (var t in Board.Tiles)
            {
                if (t.Building != PrologueBuildingId.Aquifer || t.Broken) continue;
                if (ReachesNewIce(t.Index)) return true;
                if (firstDepleted && AquiferReachesOtherIce(t.Index)) return true;
            }
            return false;
        }

        public int O2Need()
        {
            if (Colonists <= 0) return 0;
            return (int)Math.Ceiling(Colonists * PrologueData.O2PerColonist);
        }

        public double O2Runway()
        {
            int need = O2Need();
            return need <= 0 ? 1e9 : (double)Oxygen / need;
        }

        public int TotalBeds()
        {
            int beds = 0;
            foreach (var t in Board.Tiles)
            {
                if (t.Building == PrologueBuildingId.None || t.Broken) continue;
                beds += PrologueData.Get(t.Building).Beds;
            }
            return beds;
        }

        public bool IsPowered(int tile)
        {
            var b = Board.At(tile).Building;
            if (b == PrologueBuildingId.None) return false;
            var info = PrologueData.Get(b);
            if (!info.NeedsPower) return true;
            if (SolarDarkened)
            {
                foreach (var t in Board.Tiles)
                {
                    if (t.Building != PrologueBuildingId.WindTurbine || t.Broken) continue;
                    if (Dist(t.Index, tile) <= 1) return true;
                }
                return false;
            }
            foreach (var t in Board.Tiles)
            {
                if (t.Broken) continue;
                if (t.Building != PrologueBuildingId.Solar && t.Building != PrologueBuildingId.WindTurbine) continue;
                if (Dist(t.Index, tile) <= 1) return true;
            }
            return false;
        }

        public bool AquiferReachesIce(int aquiferTile)
        {
            foreach (var t in Board.Tiles)
            {
                if (!t.Revealed || t.DepositLeft <= 0) continue;
                if (t.Feature != PrologueFeature.Ice && t.Feature != PrologueFeature.Glacier) continue;
                if (Dist(aquiferTile, t.Index) <= PrologueData.HarvestRange) return true;
            }
            return false;
        }

        public List<string> EndWeek()
        {
            var log = new List<string>();

            // 1) Advance sites
            for (int i = Board.Sites.Count - 1; i >= 0; i--)
            {
                var s = Board.Sites[i];
                s.WeeksDone++;
                if (s.WeeksDone >= s.WeeksTotal)
                {
                    Board.At(s.Tile).Building = s.Id;
                    log.Add($"Completed {PrologueData.Get(s.Id).DisplayName}");
                    Board.Sites.RemoveAt(i);
                }
            }

            // 2) Harvest ice from aquifers
            foreach (var t in Board.Tiles)
            {
                if (t.Building != PrologueBuildingId.Aquifer || t.Broken) continue;
                int gained = 0;
                int cap = PrologueData.Get(PrologueBuildingId.Aquifer).IceHarvestPerWeek;
                foreach (var ice in Board.Tiles)
                {
                    if (!ice.Revealed || ice.DepositLeft <= 0) continue;
                    if (ice.Feature != PrologueFeature.Ice && ice.Feature != PrologueFeature.Glacier) continue;
                    if (Dist(t.Index, ice.Index) > PrologueData.HarvestRange) continue;
                    int take = Math.Min(cap - gained, ice.DepositLeft);
                    ice.DepositLeft -= take;
                    gained += take;
                    if (gained >= cap) break;
                }
                IceStock += gained;
            }

            // 3) Oxygen processors
            foreach (var t in Board.Tiles)
            {
                if (t.Building != PrologueBuildingId.Oxygen || t.Broken) continue;
                if (!IsPowered(t.Index)) { log.Add("Oxygen Processor unpowered"); continue; }
                var info = PrologueData.Get(PrologueBuildingId.Oxygen);
                if (IceStock < info.IceConsumedPerWeek) { log.Add("Oxygen Processor starved for ice"); continue; }
                IceStock -= info.IceConsumedPerWeek;
                Oxygen += info.OxygenProducedPerWeek;
            }

            // 4) Breathe
            int need = O2Need();
            int breathed = Math.Min(need, Oxygen);
            Oxygen -= breathed;
            if (breathed < need) log.Add("AIR CRITICAL: not enough oxygen");

            // 5) Shuttle
            if (!ShuttleLanded && ShuttleLandWeek >= 0 && Week + 1 >= ShuttleLandWeek)
            {
                ShuttleLanded = true;
                Colonists += PrologueData.PrologueShuttleSize;
                log.Add($"{PrologueData.PrologueShuttleSize} colonists landed");
            }

            // 6) Storm start
            if (StormLandWeek >= 0 && Week + 1 >= StormLandWeek)
                DustStormActive = true;

            // 7) Advance week
            Week++;

            // 8) Story
            StoryCheck(log);
            return log;
        }

        public void StoryCheck(List<string> log)
        {
            log ??= new List<string>();
            bool advanced;
            do
            {
                advanced = false;
                switch (PrologueStep)
                {
                    case 0:
                        if (AnyRevealedIce())
                        {
                            if (Board.FirstIceTiles.Count == 0)
                                PopulateFirstIceTiles();
                            Unlocked.Add(PrologueBuildingId.Aquifer);
                            PrologueStep = 1;
                            advanced = true;
                        }
                        break;
                    case 1:
                        if (AnyCompleteAquiferReachingIce())
                        {
                            Unlocked.Add(PrologueBuildingId.Oxygen);
                            Unlocked.Add(PrologueBuildingId.Solar);
                            PrologueStep = 2;
                            advanced = true;
                        }
                        break;
                    case 2:
                        if (AnyPoweredOxygen())
                        {
                            Unlocked.Add(PrologueBuildingId.Habitat);
                            if (ShuttleLandWeek < 0)
                                ShuttleLandWeek = Week + PrologueData.PrologueShuttleWeeks;
                            PrologueStep = 3;
                            advanced = true;
                        }
                        break;
                    case 3:
                        if (TotalBeds() >= 20)
                        {
                            PrologueStep = 4;
                            // Ensure a second hidden ice pad exists for the drone objective.
                            PrologueController.Instance?.EnsureMinimumIceDeposits(2);
                            advanced = true;
                        }
                        break;
                    case 4:
                        if (SecondIceDrill())
                        {
                            Unlocked.Add(PrologueBuildingId.WindTurbine);
                            if (StormLandWeek < 0)
                                StormLandWeek = Week + PrologueData.PrologueStormWeeks;
                            PrologueStep = 5;
                            advanced = true;
                        }
                        break;
                    case 5:
                        if (StormLandWeek >= 0
                            && Week >= StormLandWeek + PrologueData.PrologueStormWeeks
                            && Oxygen > 0)
                        {
                            DustStormActive = false;
                            PrologueStep = 6;
                            advanced = true;
                        }
                        break;
                }
            } while (advanced && PrologueStep < PrologueData.PrologueSteps);
        }

        bool SiteOnTile(int tile)
        {
            foreach (var s in Board.Sites)
            {
                if (s.Tile == tile) return true;
            }
            return false;
        }

        bool HasIceInHarvestRange(int tile)
        {
            foreach (var t in Board.Tiles)
            {
                if (!t.Revealed || t.DepositLeft <= 0) continue;
                if (t.Feature != PrologueFeature.Ice && t.Feature != PrologueFeature.Glacier) continue;
                if (Dist(tile, t.Index) <= PrologueData.HarvestRange) return true;
            }
            return false;
        }

        bool AnyRevealedIce()
        {
            foreach (var t in Board.Tiles)
            {
                if (!t.Revealed) continue;
                if (t.Feature != PrologueFeature.Ice && t.Feature != PrologueFeature.Glacier) continue;
                return true; // DepositLeft>0 preferred; zero-deposit ice still advances 0→1
            }
            return false;
        }

        void PopulateFirstIceTiles()
        {
            foreach (var t in Board.Tiles)
            {
                if (!t.Revealed || t.DepositLeft <= 0) continue;
                if (t.Feature != PrologueFeature.Ice && t.Feature != PrologueFeature.Glacier) continue;
                Board.FirstIceTiles.Add(t.Index);
            }
        }

        bool AnyCompleteAquiferReachingIce()
        {
            foreach (var t in Board.Tiles)
            {
                if (t.Building != PrologueBuildingId.Aquifer || t.Broken) continue;
                if (AquiferReachesIce(t.Index)) return true;
            }
            return false;
        }

        bool AnyPoweredOxygen()
        {
            foreach (var t in Board.Tiles)
            {
                if (t.Building != PrologueBuildingId.Oxygen || t.Broken) continue;
                if (IsPowered(t.Index)) return true;
            }
            return false;
        }

        bool HasBuilding(PrologueBuildingId id)
        {
            foreach (var t in Board.Tiles)
            {
                if (t.Building == id && !t.Broken) return true;
            }
            return false;
        }

        bool FirstIceDepleted()
        {
            if (Board.FirstIceTiles.Count == 0) return false;
            foreach (int idx in Board.FirstIceTiles)
            {
                if (Board.At(idx).DepositLeft > 0) return false;
            }
            return true;
        }

        bool AquiferReachesOtherIce(int aquiferTile)
        {
            foreach (var t in Board.Tiles)
            {
                if (!t.Revealed || t.DepositLeft <= 0) continue;
                if (t.Feature != PrologueFeature.Ice && t.Feature != PrologueFeature.Glacier) continue;
                if (Board.FirstIceTiles.Contains(t.Index)) continue;
                if (Dist(aquiferTile, t.Index) <= PrologueData.HarvestRange) return true;
            }
            return false;
        }
    }
}
