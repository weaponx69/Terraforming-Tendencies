using System;
using System.Collections.Generic;
using System.Text;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using UnityEngine;

namespace GameDevTV.RTS.Prologue
{
    public sealed class PrologueColony
    {
        public PrologueBoardState Board { get; } = new();
        public int Week { get; private set; }
        public int Oxygen { get; private set; } = PrologueData.LandingOxygen;
        public int IceStock { get; private set; }
        public int Colonists { get; private set; } = PrologueData.StartColonists;
        public int Satisfaction { get; private set; } = PrologueData.SatisfactionStart;
        public int Faith { get; private set; } = PrologueData.FaithStart;
        public bool ContractCancelled { get; private set; }
        public string ContractMessage { get; private set; } = "";
        public bool ColonistsWorking => Satisfaction >= PrologueData.WorkThreshold;
        public int PrologueStep { get; private set; }
        int stepMarked;
        public bool DustStormActive { get; private set; }
        public bool SolarDarkened => DustStormActive;
        public int Energy { get; private set; }
        public int ShuttleLandWeek { get; private set; } = -1;
        public bool ShuttleLanded { get; private set; }
        public int StormLandWeek { get; private set; } = -1;
        public HashSet<PrologueBuildingId> Unlocked { get; } = new();
        public PrologueResearch Research { get; private set; }

        /// <summary>Deposit types uncovered by Survey (seats matching mine cards).</summary>
        public HashSet<PrologueResourceId> KnownResources { get; } = new();

        readonly Dictionary<PrologueResourceId, int> stocks = new()
        {
            { PrologueResourceId.Minerals, 0 },
            { PrologueResourceId.Gas, 0 },
            { PrologueResourceId.Iron, 0 },
            { PrologueResourceId.Regolith, 0 }
        };

        /// <summary>
        /// Test/runtime mine sources: take up to <c>want</c> from the linked deposit, return gained.
        /// </summary>
        readonly List<(PrologueResourceId type, Func<int, int> take)> mineSources = new();

        Func<int, int, int> _dist = (a, b) => a == b ? 0 : 99;
        Func<int, int> _supply;

        public int GetStock(PrologueResourceId id) =>
            stocks.TryGetValue(id, out int v) ? v : 0;

        public void AddStock(PrologueResourceId id, int amount)
        {
            if (amount <= 0) return;
            stocks[id] = GetStock(id) + amount;
        }

        public bool TrySpendStock(PrologueResourceId id, int amount)
        {
            if (amount <= 0) return true;
            if (GetStock(id) < amount) return false;
            stocks[id] = GetStock(id) - amount;
            return true;
        }

        public void MarkResourceKnown(PrologueResourceId id) => KnownResources.Add(id);

        public bool KnowsResource(PrologueResourceId id) => KnownResources.Contains(id);

        public void RegisterMineSource(PrologueResourceId type, Func<int, int> takeFromDeposit)
        {
            if (takeFromDeposit == null) return;
            mineSources.Add((type, takeFromDeposit));
        }

        public void UnregisterMineSource(Func<int, int> takeFromDeposit)
        {
            mineSources.RemoveAll(m => ReferenceEquals(m.take, takeFromDeposit));
        }

        public void ClearMineSources() => mineSources.Clear();

        public static PrologueColony CreateLanding()
        {
            var c = new PrologueColony();
            for (int i = 0; i < 16; i++)
                c.Board.Tiles.Add(new PrologueTile { Index = i, Lit = false, Revealed = false });
            c.Board.At(0).Building = PrologueBuildingId.CommandPost;
            c.Oxygen = PrologueData.LandingOxygen;
            c.Colonists = PrologueData.StartColonists;
            c.AddStock(PrologueResourceId.Minerals, PrologueData.StartingMinerals);
            c.Research = new PrologueResearch();
            c.RefillEnergy();
            return c;
        }

        public void SetDistanceFn(Func<int, int, int> fn) => _dist = fn;

        public void SetSupplyDistanceFn(Func<int, int> fn) => _supply = fn;

        public int Dist(int a, int b) => _dist(a, b);

        /// <summary>Hexes from this tile to the nearest finished building that can stage a haul.</summary>
        public int SupplyDistance(int tile)
        {
            if (_supply != null) return _supply(tile);
            int best = int.MaxValue;
            foreach (var t in Board.Tiles)
            {
                if (t.Index == tile || t.Building == PrologueBuildingId.None || t.Broken) continue;
                int d = Dist(tile, t.Index);
                if (d < best) best = d;
            }
            return best == int.MaxValue ? 0 : best;
        }

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
            KnownResources.Clear();
            ClearMineSources();
            foreach (var id in PrologueResourceIdUtil.All)
                stocks[id] = 0;
            AddStock(PrologueResourceId.Minerals, PrologueData.StartingMinerals);
            ShuttleLandWeek = -1;
            ShuttleLanded = false;
            StormLandWeek = -1;
            DustStormActive = false;
            Satisfaction = PrologueData.SatisfactionStart;
            Faith = PrologueData.FaithStart;
            ContractCancelled = false;
            ContractMessage = "";
            stepMarked = 0;
            Board.FirstIceTiles.Clear();
            Board.SecondIceTiles.Clear();
            RefillEnergy();
        }

        /// <summary>Track non-first ice so step 4 drill / survey bots aim at the right deposit.</summary>
        public void RefreshSecondIceTiles()
        {
            Board.SecondIceTiles.Clear();
            foreach (var t in Board.Tiles)
            {
                if (t.DepositLeft <= 0) continue;
                if (t.Feature != PrologueFeature.Ice && t.Feature != PrologueFeature.Glacier) continue;
                if (Board.FirstIceTiles.Contains(t.Index)) continue;
                Board.SecondIceTiles.Add(t.Index);
            }
        }

        public int EnergyGeneration()
        {
            int gen = SolarDarkened ? 0 : PrologueData.CampEnergyGeneration;
            foreach (var t in Board.Tiles)
            {
                if (t.Building == PrologueBuildingId.None || t.Broken) continue;
                // Sites still building do not generate.
                bool underConstruction = false;
                foreach (var s in Board.Sites)
                    if (s.Tile == t.Index) { underConstruction = true; break; }
                if (underConstruction) continue;
                int output = PrologueData.Get(t.Building).PowerOutput;
                if (SolarDarkened && t.Building == PrologueBuildingId.Solar)
                    continue;
                gen += output;
            }
            return gen;
        }

        public bool TrySpendEnergy(int amount)
        {
            if (amount <= 0) return true;
            if (Energy < amount) return false;
            Energy -= amount;
            return true;
        }

        void RefillEnergy()
        {
            Energy = EnergyGeneration();
        }

        public void DebugSetCrew(int satisfaction, int faith)
        {
            Satisfaction = satisfaction;
            Faith = faith;
        }

        public void DebugSetOxygen(int oxygen) => Oxygen = oxygen;

        public void DebugSetIce(int ice) => IceStock = ice;

        public void DebugScheduleStorm(int landWeek) => StormLandWeek = landWeek;

        public void DebugSetProgress(int step, int week)
        {
            PrologueStep = step;
            Week = week;
            stepMarked = step;
        }

        public void DebugLandShuttle() => ShuttleLanded = true;

        public bool Preview(int tile, PrologueBuildingId id, out string reason)
        {
            var cell = Board.At(tile);
            if (cell.Building != PrologueBuildingId.None || SiteOnTile(tile))
            {
                reason = "That tile is already taken.";
                return false;
            }
            if (!Unlocked.Contains(id))
            {
                reason = "Not yet: finish the objective at the top first";
                return false;
            }
            if (id == PrologueBuildingId.Aquifer)
            {
                if (cell.Feature == PrologueFeature.Ice || cell.Feature == PrologueFeature.Glacier)
                {
                    reason = "Place the Ice Aquifer on the hex beside the deposit — not on the ice.";
                    return false;
                }
                if (!HasIceInHarvestRange(tile))
                {
                    reason = $"Ice Aquifer must be within {PrologueData.HarvestRange} tiles of revealed ice";
                    return false;
                }
                // Step 4+: the second drill must reach the NEW ice, not the first deposit.
                if (PrologueStep >= 4 && !SecondIceDrill() && SecondIceTargetTile() >= 0 && !ReachesNewIce(tile))
                {
                    reason = $"Build this Ice Aquifer within {PrologueData.HarvestRange} tiles of the newly found ice";
                    return false;
                }
            }
            if (id == PrologueBuildingId.WindTurbine || id == PrologueBuildingId.Solar)
            {
                int reach = 2;
                if (Research != null && Research.IsUnlocked(PrologueResearchId.LongerPowerReach))
                    reach = 3;
                if (!TryGetOxygenProcessorTile(out int oxygenTile))
                {
                    reason = "Build the Atmosphere Processor first.";
                    return false;
                }
                if (Dist(tile, oxygenTile) > reach)
                {
                    reason = $"Place within {reach} tiles of the Atmosphere Processor.";
                    return false;
                }
            }
            int mineralCost = PrologueData.MineralCost(id);
            if (mineralCost > 0 && GetStock(PrologueResourceId.Minerals) < mineralCost)
            {
                reason = $"Need {mineralCost} minerals (have {GetStock(PrologueResourceId.Minerals)})";
                return false;
            }
            reason = "";
            return true;
        }

        public bool TryPlace(int tile, PrologueBuildingId id, out string reason)
        {
            if (!Preview(tile, id, out reason)) return false;
            int mineralCost = PrologueData.MineralCost(id);
            if (mineralCost > 0 && !TrySpendStock(PrologueResourceId.Minerals, mineralCost))
            {
                reason = $"Need {mineralCost} minerals";
                return false;
            }
            int buildWeeks = WeeksForJob(id, tile);
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
            Board.At(tile).Revealed = true;
            StoryCheck(new List<string>());
            reason = "";
            return true;
        }

        /// <summary>
        /// Tests / cheats: finish construction immediately (still pays mineral cost via Preview/spend).
        /// </summary>
        public bool TryPlaceImmediate(int tile, PrologueBuildingId id, out string reason)
        {
            if (!Preview(tile, id, out reason)) return false;
            int mineralCost = PrologueData.MineralCost(id);
            if (mineralCost > 0 && !TrySpendStock(PrologueResourceId.Minerals, mineralCost))
            {
                reason = $"Need {mineralCost} minerals";
                return false;
            }
            Board.At(tile).Building = id;
            Board.At(tile).Revealed = true;
            Board.Sites.RemoveAll(s => s.Tile == tile);
            StoryCheck(new List<string>());
            reason = "";
            return true;
        }

        public void RevealTiles(IEnumerable<int> path)
        {
            var newlyRevealed = new List<int>();
            foreach (int index in path)
            {
                var t = Board.At(index);
                bool wasRevealed = t.Revealed;
                t.Revealed = true;
                t.Lit = true;
                if (PrologueStep == 0
                    && (t.Feature == PrologueFeature.Ice || t.Feature == PrologueFeature.Glacier)
                    && t.DepositLeft > 0)
                {
                    Board.FirstIceTiles.Add(index);
                }
                if (!wasRevealed)
                    newlyRevealed.Add(index);
            }

            if (newlyRevealed.Count > 0)
                PrologueSearchedTiles.Instance?.ExtendForTiles(newlyRevealed);

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
                    return "Find ice: play Survey Team, paint a trail toward a yellow ?, then [OK] (3 days)";
                case 1:
                    suggestBuild = PrologueBuildingId.Aquifer;
                    return $"Build Ice Aquifer within 3 of ice ({PrologueData.MineralCost(PrologueBuildingId.Aquifer)} minerals, {PrologueData.Get(PrologueBuildingId.Aquifer).BuildWeeks} wk)";
                case 2:
                    suggestBuild = PrologueBuildingId.Oxygen;
                    return $"Build Atmosphere Processor ({PrologueData.MineralCost(PrologueBuildingId.Oxygen)} minerals, {PrologueData.Get(PrologueBuildingId.Oxygen).BuildWeeks} wk)";
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
                        return "Find more ice: play Survey Team, paint toward a yellow ?, then [OK]";
                    }
                    suggestBuild = PrologueBuildingId.Aquifer;
                    return "Build a second ice drill (Ice Aquifer) within 3 tiles of the new ice";
                }
                case 5:
                    if (!HasBuilding(PrologueBuildingId.WindTurbine))
                    {
                        suggestBuild = PrologueBuildingId.WindTurbine;
                        return "Place a Windmill or Solar Array within 2 tiles of the Atmosphere Processor (wind works in the dust storm)";
                    }
                    return "Store oxygen and ride out the dust storm";
                default:
                    return "Prologue complete";
            }
        }

        public bool HasUnrevealedIce()
        {
            foreach (var t in Board.Tiles)
            {
                if (t.DepositLeft <= 0) continue;
                if (t.Feature != PrologueFeature.Ice && t.Feature != PrologueFeature.Glacier) continue;
                if (!t.Revealed) return true;
            }
            return false;
        }

        /// <summary>Ice pads or unexplored deposit "?" still worth an Orbital ping.</summary>
        public bool HasUnrevealedTargets()
        {
            if (HasUnrevealedIce()) return true;
            var ctrl = PrologueController.Instance;
            if (ctrl == null || SectorManager.Instance?.Sectors == null
                || SectorManager.Instance.Sectors.Count == 0)
                return false;

            int homeTile = -1;
            for (int i = 0; i < Board.Tiles.Count; i++)
            {
                if (Board.At(i).Building == PrologueBuildingId.CommandPost)
                {
                    homeTile = i;
                    break;
                }
            }
            if (homeTile < 0) homeTile = 0;

            foreach (var node in SectorManager.Instance.Sectors[0].Nodes)
            {
                if (node == null || node.isExplored) continue;
                if (node.type == SectorNode.NodeType.Nexus) continue;
                if (!ctrl.TryGetTileIndex(node.position, out int tile)) continue;
                if (Dist(homeTile, tile) > 8) continue;
                return true;
            }
            return false;
        }

        public bool AnyRevealedNonFirstIce()
        {
            foreach (int idx in Board.SecondIceTiles)
            {
                var t = Board.At(idx);
                if (t.Revealed && t.DepositLeft > 0) return true;
            }
            foreach (var t in Board.Tiles)
            {
                if (!t.Revealed || t.DepositLeft <= 0) continue;
                if (t.Feature != PrologueFeature.Ice && t.Feature != PrologueFeature.Glacier) continue;
                if (Board.FirstIceTiles.Contains(t.Index)) continue;
                return true;
            }
            return false;
        }

        public int NewIceTile()
        {
            foreach (int idx in Board.SecondIceTiles)
            {
                var t = Board.At(idx);
                if (t.Revealed || t.DepositLeft <= 0) continue;
                return idx;
            }
            foreach (var t in Board.Tiles)
            {
                if (t.Revealed || t.DepositLeft <= 0) continue;
                if (t.Feature != PrologueFeature.Ice && t.Feature != PrologueFeature.Glacier) continue;
                if (Board.FirstIceTiles.Contains(t.Index)) continue;
                return t.Index;
            }
            return -1;
        }

        /// <summary>Step 4 target ice: unrevealed new pad, or already-revealed non-first ice.</summary>
        public int SecondIceTargetTile()
        {
            int hidden = NewIceTile();
            if (hidden >= 0) return hidden;
            foreach (int idx in Board.SecondIceTiles)
            {
                var t = Board.At(idx);
                if (!t.Revealed || t.DepositLeft <= 0) continue;
                return idx;
            }
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
            if (Board.SecondIceTiles.Count > 0)
            {
                foreach (int iceIdx in Board.SecondIceTiles)
                {
                    if (Board.At(iceIdx).DepositLeft <= 0) continue;
                    if (Dist(cell, iceIdx) <= PrologueData.HarvestRange) return true;
                }
                return false;
            }
            int ice = SecondIceTargetTile();
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

        /// <summary>
        /// What the crew is doing, and each prologue goal against the number it needs.
        /// extraActivity is a live job the board does not store, such as a survey in progress.
        /// </summary>
        public string StatsText(string extraActivity = null)
        {
            var sb = new StringBuilder();
            if (ContractCancelled)
                sb.AppendLine(ContractMessage);
            else if (PrologueStep >= PrologueData.PrologueSteps)
                sb.AppendLine("Prologue complete. The contract is held.");
            else
                PrologueGuidance.AppendGuideBlock(sb, this);

            sb.AppendLine("COLONISTS");
            sb.Append(Colonists).Append(" people · ");
            sb.AppendLine(ColonistsWorking ? "working" : "stopped");
            sb.Append("Satisfaction ").Append(Satisfaction).Append(" · Faith ").Append(Faith);
            if (!ColonistsWorking)
                sb.Append(" · work resumes at ").Append(PrologueData.WorkThreshold);
            sb.AppendLine();
            sb.AppendLine();

            sb.AppendLine("THIS WEEK");
            if (!string.IsNullOrEmpty(extraActivity))
                sb.AppendLine(extraActivity);
            if (Board.Sites.Count == 0 && string.IsNullOrEmpty(extraActivity))
                sb.AppendLine(ColonistsWorking ? "No build on the board." : "The crew is not working.");
            foreach (var site in Board.Sites)
            {
                string name = PrologueData.Get(site.Id).DisplayName;
                if (string.IsNullOrEmpty(name)) name = site.Id.ToString();
                string verb = ColonistsWorking ? "Building" : "Waiting on";
                sb.Append(verb).Append(' ').Append(name)
                    .Append(" · week ").Append(site.WeeksDone).Append(" of ").Append(site.WeeksTotal)
                    .AppendLine(" done");
            }
            AppendOxygenWork(sb);
            sb.AppendLine(CurrentGoalLine());
            sb.AppendLine();

            sb.AppendLine("GOALS");
            sb.AppendLine(GoalMark(0) + " Find ice");
            sb.AppendLine(GoalMark(1) + " Drill ice within 3");
            sb.AppendLine(GoalMark(2) + " Power the Atmosphere Processor");
            sb.Append(GoalMark(3)).Append(" Beds ").Append(TotalBeds()).Append("/20");
            if (ShuttleLandWeek >= 0 && !ShuttleLanded)
                sb.Append(" · shuttle in ").Append(Math.Max(0, ShuttleLandWeek - Week)).Append(" wk");
            else if (ShuttleLanded)
                sb.Append(" · shuttle landed");
            sb.AppendLine();
            sb.AppendLine(GoalMark(4) + " Second ice drill");
            sb.AppendLine(GoalMark(5) + " " + StormGoalLine());
            sb.AppendLine();

            int need = O2Need();
            double runway = O2Runway();
            string runwayStr = runway > 1000 ? "inf" : runway.ToString("0.0");
            sb.Append("Air ").Append(Oxygen).Append(" · need ").Append(need).Append("/wk · ").Append(runwayStr).AppendLine(" wk left");
            sb.Append("Ice ").Append(IceStock)
                .Append(" · Minerals ").Append(GetStock(PrologueResourceId.Minerals)).Append(" on hand");
            return sb.ToString();
        }

        void AppendOxygenWork(StringBuilder sb)
        {
            int processors = 0;
            int powered = 0;
            foreach (var t in Board.Tiles)
            {
                if (t.Building != PrologueBuildingId.Oxygen || t.Broken) continue;
                processors++;
                if (IsPowered(t.Index)) powered++;
            }
            if (processors == 0) return;
            var info = PrologueData.Get(PrologueBuildingId.Oxygen);
            if (powered == 0)
            {
                sb.AppendLine(SolarDarkened
                    ? "Atmosphere Processor has no power. Place a Windmill within 2 tiles."
                    : "Atmosphere Processor has no power. Add Solar or Wind within 2 tiles.");
                return;
            }
            sb.AppendLine(SolarDarkened
                ? "Storm power: Windmill is feeding the Atmosphere Processor."
                : "Camp power is feeding the Atmosphere Processor.");
            if (!ColonistsWorking)
                sb.AppendLine("Atmosphere Processor is idle. The crew is not running it.");
            else if (IceStock < info.IceConsumedPerWeek)
                sb.Append("Atmosphere Processor needs ").Append(info.IceConsumedPerWeek)
                    .Append(" ice. Stock is ").Append(IceStock).AppendLine(".");
            else
                sb.Append("Atmosphere Processor will make ").Append(info.OxygenProducedPerWeek)
                    .Append(" air from ").Append(info.IceConsumedPerWeek).AppendLine(" ice.");
        }

        string CurrentGoalLine()
        {
            switch (PrologueStep)
            {
                case 0: return "The crew needs a surveyed ice deposit.";
                case 1: return "The drill has to sit within 3 tiles of that ice.";
                case 2: return "The Atmosphere Processor has to be powered and fed ice.";
                case 3: return "The shuttle needs 20 beds. " + TotalBeds() + " are ready.";
                case 4: return "A second drill has to reach a new ice deposit.";
                case 5: return Oxygen > 0
                    ? "Keep the air above zero until the storm passes."
                    : "The air is gone.";
                default: return "The contract is held.";
            }
        }

        string StormGoalLine()
        {
            if (PrologueStep > 5) return "Held air through the storm";
            if (StormLandWeek < 0) return "Hold air through the storm";
            if (!DustStormActive && Week < StormLandWeek)
                return "Hold air through the storm · in " + (StormLandWeek - Week) + " wk";
            int held = Math.Max(0, Week - StormLandWeek);
            return "Hold air through the storm · week " + Math.Min(held, PrologueData.PrologueStormWeeks)
                + "/" + PrologueData.PrologueStormWeeks + " · air " + Oxygen;
        }

        string GoalMark(int step)
        {
            if (PrologueStep > step || PrologueStep >= PrologueData.PrologueSteps) return "[OK]";
            if (PrologueStep == step) return ">";
            return "·";
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
            return HasPowerPath(tile);
        }

        /// <summary>
        /// Build + haul weeks for a job. FasterBuild research −1 week (clamp ≥ 1 when &gt; 0).
        /// </summary>
        public int WeeksForJob(PrologueBuildingId id, int tile)
        {
            int weeks = PrologueData.Get(id).BuildWeeks
                + PrologueData.HaulWeeks(SupplyDistance(tile));
            return ApplyFasterBuild(weeks);
        }

        /// <summary>FasterBuild: −1 week when unlocked; leave 0 instant jobs alone; otherwise clamp ≥ 1.</summary>
        public int ApplyFasterBuild(int weeks)
        {
            if (weeks <= 0) return 0;
            if (Research != null && Research.IsUnlocked(PrologueResearchId.FasterBuild))
                return Math.Max(1, weeks - 1);
            return weeks;
        }

        /// <summary>
        /// Connected power BFS. Prologue: 2-tile reach by default (3 with Longer Power Reach).
        /// Camp powers consumers until the dust storm; during storm only
        /// non-solar generators count as sources.
        /// </summary>
        bool HasPowerPath(int startTile)
        {
            int reach = 2;
            if (Research != null && Research.IsUnlocked(PrologueResearchId.LongerPowerReach))
                reach = 3;
            var q = new Queue<int>();
            var seen = new HashSet<int>();
            q.Enqueue(startTile);
            seen.Add(startTile);
            while (q.Count > 0)
            {
                int cur = q.Dequeue();
                var curB = Board.At(cur).Building;
                if (curB == PrologueBuildingId.None || Board.At(cur).Broken) continue;
                if (IsEnergySource(cur)) return true;
                foreach (var t in Board.Tiles)
                {
                    if (t.Building == PrologueBuildingId.None || t.Broken) continue;
                    if (seen.Contains(t.Index)) continue;
                    if (Dist(cur, t.Index) > reach) continue;
                    // Under-construction sites do not conduct yet.
                    bool building = false;
                    foreach (var s in Board.Sites)
                        if (s.Tile == t.Index) { building = true; break; }
                    if (building) continue;
                    seen.Add(t.Index);
                    q.Enqueue(t.Index);
                }
            }
            return false;
        }

        bool IsEnergySource(int tile)
        {
            var b = Board.At(tile).Building;
            if (b == PrologueBuildingId.Solar && SolarDarkened)
                return false;
            if (b != PrologueBuildingId.None && PrologueData.Get(b).PowerOutput > 0)
                return true;
            // Camp is a virtual source until storm.
            return !SolarDarkened;
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

        /// <summary>Empty build hex beside ice (prefer adjacent) where an aquifer may sit (ignores mineral stock).</summary>
        public bool TryFindAquiferPadNearIce(int iceTile, out int padTile)
        {
            padTile = -1;
            if (iceTile < 0 || iceTile >= Board.Tiles.Count) return false;

            var ctrl = PrologueController.Instance;
            if (ctrl == null || !ctrl.TryGetCell(iceTile, out Vector2Int iceCell))
                return false;
            float iceY = ColonyTileGrid.SampleGroundY(ColonyTileGrid.CellToWorld(iceCell, 0f));

            int bestScore = int.MaxValue;
            for (int ring = 1; ring <= PrologueData.HarvestRange; ring++)
            {
                for (int i = 0; i < Board.Tiles.Count; i++)
                {
                    if (Dist(iceTile, i) != ring) continue;
                    if (!CanPlaceAquiferPad(i)) continue;
                    if (!ctrl.TryGetCell(i, out Vector2Int padCell)) continue;
                    float padY = ColonyTileGrid.SampleGroundY(ColonyTileGrid.CellToWorld(padCell, 0f));
                    int iceDelta = Mathf.RoundToInt(Mathf.Abs(padY - iceY) * 25f);
                    int localSlope = Mathf.RoundToInt(ColonyTileGrid.HexLocalSlopeMeters(padCell) * 40f);
                    int score = ring * 1000 + iceDelta * 2 + localSlope * 3;
                    if (score >= bestScore) continue;
                    bestScore = score;
                    padTile = i;
                }
            }
            return padTile >= 0;
        }

        /// <summary>Tile rules for an aquifer pad (no mineral check — hand UI shows cost).</summary>
        public bool CanPlaceAquiferPad(int tile)
        {
            var cell = Board.At(tile);
            if (cell.Building != PrologueBuildingId.None || SiteOnTile(tile)) return false;
            if (!Unlocked.Contains(PrologueBuildingId.Aquifer)) return false;
            if (cell.Feature == PrologueFeature.Ice || cell.Feature == PrologueFeature.Glacier) return false;
            if (!HasIceInHarvestRange(tile)) return false;
            if (PrologueStep >= 4 && !SecondIceDrill() && SecondIceTargetTile() >= 0 && !ReachesNewIce(tile))
                return false;
            return true;
        }

        public int NearestRevealedIceTile(int fromTile)
        {
            int best = -1;
            int bestD = int.MaxValue;
            foreach (var t in Board.Tiles)
            {
                if (!t.Revealed || t.DepositLeft <= 0) continue;
                if (t.Feature != PrologueFeature.Ice && t.Feature != PrologueFeature.Glacier) continue;
                int d = Dist(fromTile, t.Index);
                if (d >= bestD) continue;
                bestD = d;
                best = t.Index;
            }
            return best;
        }

        void SettleSites(List<string> log, bool advance)
        {
            for (int i = Board.Sites.Count - 1; i >= 0; i--)
            {
                var s = Board.Sites[i];
                int want = WeeksForJob(s.Id, s.Tile);
                if (want < s.WeeksTotal)
                    s.WeeksTotal = Math.Max(want, s.WeeksDone);
                if (advance) s.WeeksDone++;
                if (s.WeeksDone < s.WeeksTotal) continue;
                Board.At(s.Tile).Building = s.Id;
                log.Add($"Completed {PrologueData.Get(s.Id).DisplayName}");
                Board.Sites.RemoveAt(i);
            }
        }

        public List<string> EndWeek()
        {
            var log = new List<string>();
            if (ContractCancelled)
            {
                log.Add(ContractMessage);
                return log;
            }
            if (PrologueStep >= PrologueData.PrologueSteps)
            {
                Week++;
                log.Add("Prologue complete");
                return log;
            }

            bool crewWorking = ColonistsWorking;
            bool processorProducedThisWeek = false;
            if (!crewWorking)
                log.Add("Colonists stopped. Satisfaction is too low to work.");

            // 1) Advance sites when the crew is working. A closer finished building shortens the haul mid-job.
            SettleSites(log, advance: crewWorking);
            SettleSites(log, advance: false);

            if (crewWorking)
            {
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

                // 2b) Typed mines on uncovered deposits (individual stocks, not Materials).
                foreach (var (type, take) in mineSources)
                {
                    if (take == null) continue;
                    int gained = take(PrologueData.MineHarvestPerWeek);
                    if (gained <= 0) continue;
                    AddStock(type, gained);
                    log.Add($"+{gained} {PrologueResourceIdUtil.ToTypeName(type)} from mine");
                }

                // 3) Oxygen processors
                foreach (var t in Board.Tiles)
                {
                    if (t.Building != PrologueBuildingId.Oxygen || t.Broken) continue;
                    if (!IsPowered(t.Index)) { log.Add("Atmosphere Processor unpowered"); continue; }
                    var info = PrologueData.Get(PrologueBuildingId.Oxygen);
                    if (IceStock < info.IceConsumedPerWeek) { log.Add("Atmosphere Processor starved for ice"); continue; }
                    IceStock -= info.IceConsumedPerWeek;
                    Oxygen += info.OxygenProducedPerWeek;
                    processorProducedThisWeek = true;
                    log.Add($"+{info.OxygenProducedPerWeek} air. Colonists ran the Atmosphere Processor.");
                }
            }

            // 4) Breathe
            int need = O2Need();
            int breathed = Math.Min(need, Oxygen);
            Oxygen -= breathed;
            bool airCritical = breathed < need;
            if (airCritical) log.Add("AIR CRITICAL: not enough oxygen");

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

            // Camp→grid energy pool refills after storm flags (storm darkens camp gen).
            RefillEnergy();

            // 7) Research progresses each End Week (independent of crew work).
            Research?.TickWeek(log);

            // 8) Advance week
            Week++;

            ApplySatisfaction(airCritical);
            ApplyFaith(airCritical, processorProducedThisWeek);
            if (Satisfaction <= 0)
                CancelContract("The corporation cancelled the contract. Colonist satisfaction is too low.");
            else if (Faith <= 0 && !TryExtendContractForAirReserves(need, log))
                CancelContract("The corporation lost faith in this mission and cancelled the contract.");

            if (ContractCancelled)
            {
                log.Add(ContractMessage);
                stepMarked = PrologueStep;
                return log;
            }

            StoryCheck(log);
            stepMarked = PrologueStep;

            if (DustStormActive && Oxygen <= 0)
            {
                CancelContract("The corporation cancelled the contract. The mission ran out of air.");
                log.Add(ContractMessage);
            }
            return log;
        }

        void ApplySatisfaction(bool airCritical)
        {
            if (airCritical)
                Satisfaction -= PrologueData.AirCriticalSatisfactionLoss;
            if (ShuttleLanded && TotalBeds() < Colonists)
                Satisfaction -= PrologueData.UnhousedSatisfactionLoss;
            bool bedsCovered = !ShuttleLanded || TotalBeds() >= Colonists;
            if (!airCritical && bedsCovered)
                Satisfaction += PrologueData.NeedsMetSatisfactionGain;
            Satisfaction = Math.Clamp(Satisfaction, 0, 100);
        }

        void ApplyFaith(bool airCritical, bool processorProducedThisWeek)
        {
            bool advanced = PrologueStep != stepMarked
                || (PrologueStep < PrologueData.PrologueSteps && StepIsComplete(PrologueStep));
            if (advanced)
                Faith += PrologueData.StepAdvancedFaithGain;
            else if (HadSteadyWeek(airCritical, processorProducedThisWeek))
                Faith += PrologueData.SteadyWeekFaithGain;
            else
                Faith -= PrologueData.QuietWeekFaithLoss;

            int window = PrologueData.DeadlinePressureWeeks;
            if (ShuttleLandWeek >= 0 && !ShuttleLanded
                && ShuttleLandWeek - Week <= window
                && TotalBeds() < 20)
                Faith -= PrologueData.DeadlineFaithLoss;
            if (StormLandWeek >= 0
                && StormLandWeek - Week <= window
                && !PowerBesideOxygen())
                Faith -= PrologueData.DeadlineFaithLoss;
            Faith = Math.Clamp(Faith, 0, 100);
        }

        /// <summary>Weekly life-support win without a story beat — builds corporation faith back.</summary>
        bool HadSteadyWeek(bool airCritical, bool processorProducedThisWeek)
        {
            if (!ColonistsWorking || airCritical) return false;
            bool bedsOk = !ShuttleLanded || TotalBeds() >= Colonists;
            if (!bedsOk) return false;
            if (processorProducedThisWeek) return true;
            return O2Runway() >= 2.0;
        }

        /// <summary>Enough stored air + working crew: auditor extends instead of cancelling at 0 Faith.</summary>
        bool TryExtendContractForAirReserves(int weeklyNeed, List<string> log)
        {
            int reserve = PrologueData.AirReserveFaithExtensionThreshold(weeklyNeed);
            if (Oxygen < reserve || Satisfaction < PrologueData.WorkThreshold)
                return false;
            Faith = PrologueData.FaithRecoveryFloor;
            log.Add(
                $"Corporation holds the contract: {Oxygen} air in reserve (target {reserve}). "
                + $"Faith reset to {Faith} — keep life-support and storm objectives on track.");
            return true;
        }

        void CancelContract(string message)
        {
            ContractCancelled = true;
            ContractMessage = message;
        }

        public bool PowerBesideOxygen()
        {
            int reach = 2;
            foreach (var oxygen in Board.Tiles)
            {
                if (oxygen.Building != PrologueBuildingId.Oxygen || oxygen.Broken) continue;
                foreach (var plant in Board.Tiles)
                {
                    if (plant.Building == PrologueBuildingId.None || plant.Broken) continue;
                    if (plant.Building == PrologueBuildingId.Solar && SolarDarkened) continue;
                    if (PrologueData.Get(plant.Building).PowerOutput <= 0) continue;
                    if (Dist(oxygen.Index, plant.Index) <= reach) return true;
                }
            }
            return false;
        }

        bool StepIsComplete(int step) => step switch
        {
            0 => AnyRevealedIce(),
            1 => AnyCompleteAquiferReachingIce(),
            2 => AnyPoweredOxygen(),
            3 => TotalBeds() >= 20,
            4 => SecondIceDrill(),
            5 => StormLandWeek >= 0
                && Week >= StormLandWeek + PrologueData.PrologueStormWeeks
                && Oxygen > 0,
            _ => false
        };

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
                        if (StepIsComplete(0))
                        {
                            if (Board.FirstIceTiles.Count == 0)
                                PopulateFirstIceTiles();
                            Unlocked.Add(PrologueBuildingId.Aquifer);
                            PrologueStep = 1;
                            log.Add(PrologueGuidance.StepAdvancedNarration(PrologueStep));
                            advanced = true;
                        }
                        break;
                    case 1:
                        if (StepIsComplete(1))
                        {
                            Unlocked.Add(PrologueBuildingId.Oxygen);
                            PrologueStep = 2;
                            log.Add(PrologueGuidance.StepAdvancedNarration(PrologueStep));
                            advanced = true;
                        }
                        break;
                    case 2:
                        if (StepIsComplete(2))
                        {
                            Unlocked.Add(PrologueBuildingId.Habitat);
                            if (ShuttleLandWeek < 0)
                                ShuttleLandWeek = Week + PrologueData.PrologueShuttleWeeks;
                            PrologueStep = 3;
                            log.Add(PrologueGuidance.StepAdvancedNarration(PrologueStep));
                            advanced = true;
                        }
                        break;
                    case 3:
                        if (StepIsComplete(3))
                        {
                            PrologueStep = 4;
                            log.Add(PrologueGuidance.StepAdvancedNarration(PrologueStep));
                            // Ensure a second hidden ice pad exists for the drone objective.
                            PrologueController.Instance?.EnsureMinimumIceDeposits(2);
                            RefreshSecondIceTiles();
                            advanced = true;
                        }
                        break;
                    case 4:
                        if (StepIsComplete(4))
                        {
                            Unlocked.Add(PrologueBuildingId.Solar);
                            Unlocked.Add(PrologueBuildingId.WindTurbine);
                            // Mines are optional but costly — always leave room for storm power.
                            int windCost = PrologueData.MineralCost(PrologueBuildingId.WindTurbine);
                            int haveMinerals = GetStock(PrologueResourceId.Minerals);
                            if (haveMinerals < windCost)
                                AddStock(PrologueResourceId.Minerals, windCost - haveMinerals);
                            if (StormLandWeek < 0)
                                StormLandWeek = Week + PrologueData.PrologueStormWeeks;
                            PrologueStep = 5;
                            log.Add(PrologueGuidance.StepAdvancedNarration(PrologueStep));
                            advanced = true;
                        }
                        break;
                    case 5:
                        if (StepIsComplete(5))
                        {
                            DustStormActive = false;
                            PrologueStep = 6;
                            log.Add(PrologueGuidance.StepAdvancedNarration(PrologueStep));
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

        public bool HasIceInHarvestRange(int tile)
        {
            foreach (var t in Board.Tiles)
            {
                if (!t.Revealed || t.DepositLeft <= 0) continue;
                if (t.Feature != PrologueFeature.Ice && t.Feature != PrologueFeature.Glacier) continue;
                if (Dist(tile, t.Index) <= PrologueData.HarvestRange) return true;
            }
            return false;
        }

        public bool HasSurveyedIce() => AnyRevealedIce();

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

        public bool HasBuilding(PrologueBuildingId id)
        {
            foreach (var t in Board.Tiles)
            {
                if (t.Building == id && !t.Broken) return true;
            }
            foreach (var s in Board.Sites)
            {
                if (s.Id == id) return true;
            }
            return false;
        }

        bool TryGetOxygenProcessorTile(out int tile)
        {
            tile = -1;
            foreach (var t in Board.Tiles)
            {
                if (t.Building != PrologueBuildingId.Oxygen || t.Broken) continue;
                tile = t.Index;
                return true;
            }
            foreach (var s in Board.Sites)
            {
                if (s.Id != PrologueBuildingId.Oxygen) continue;
                tile = s.Tile;
                return true;
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
