using System;
using System.Text;

namespace GameDevTV.RTS.MainGame
{
    /// <summary>
    /// Player-facing contract guide: what to do now, how to win the step, and what meters can fail the run.
    /// </summary>
    public static class MainGameGuidance
    {
        public static void AppendGuideBlock(StringBuilder sb, MainGameColony colony)
        {
            if (colony == null || sb == null) return;
            if (colony.ContractCancelled || colony.MainGameStep >= MainGameData.MainGameSteps)
                return;

            sb.AppendLine("GUIDE");
            sb.Append("To finish this step: ").AppendLine(StepWinLine(colony));
            string watch = WatchLine(colony);
            if (!string.IsNullOrEmpty(watch))
                sb.AppendLine(watch);
            sb.AppendLine("Loop: play a card → place on ground → End Week (builds & life-support tick).");
            sb.AppendLine();
        }

        public static string ActionHint(MainGameColony colony, out MainGameBuildingId suggest, out bool wantDrone)
        {
            suggest = MainGameBuildingId.None;
            wantDrone = false;
            if (colony == null) return "Play a card from the hand, click the ground, then End Week.";

            string objective = colony.ObjectiveText(out suggest, out wantDrone);
            if (wantDrone)
            {
                return "DO THIS: Play Survey Team → paint toward yellow ? → green [OK]. "
                    + "Stuck? Orbital Scan (camp energy) pings the nearest target.";
            }

            return colony.MainGameStep switch
            {
                0 => objective,
                1 => "DO THIS: Play Water Ice Aquifer → place within 3 tiles of ice → End Week until built.",
                2 => "DO THIS: Place Atmosphere Processor → End Week. Camp power runs it until you add Solar later.",
                3 => "DO THIS: Place Habitat Domes until beds ≥ 20 before shuttle lands (check HUD beds line).",
                4 => objective.Contains("Survey", StringComparison.OrdinalIgnoreCase)
                    ? "DO THIS: Survey Team toward new ice, then Aquifer beside that deposit."
                    : "DO THIS: Second Ice Aquifer within 3 tiles of the new ice — End Week to finish.",
                5 when !colony.HasBuilding(MainGameBuildingId.WindTurbine)
                    && !HasSolarBesideOxygen(colony)
                    => "DO THIS: Windmill or Solar within 2 tiles of Atmosphere Processor (wind works in storm).",
                5 => "DO THIS: End Week each week — processor makes AIR; keep Faith & Satisfaction up through the storm.",
                _ => objective
            };
        }

        public static string StepWinLine(MainGameColony colony)
        {
            if (colony == null) return "";
            return colony.MainGameStep switch
            {
                0 => "Reveal ice on the map (Survey paint + OK).",
                1 => "Finish an Ice Aquifer that can reach ice.",
                2 => "Atmosphere Processor finished and powered.",
                3 => "20 beds ready before shuttle arrival.",
                4 => "Second aquifer finished on new ice.",
                5 => "Storm: keep AIR > 0 for 2 weeks; stockpile helps Faith recover (+6 steady weeks).",
                _ => "Hold the corporation contract."
            };
        }

        public static string WatchLine(MainGameColony colony)
        {
            if (colony == null || colony.ContractCancelled) return "";

            var sb = new StringBuilder();
            int need = colony.O2Need();
            int reserve = MainGameData.AirReserveFaithExtensionThreshold(need);
            double runway = colony.O2Runway();

            if (colony.Faith < 35)
                sb.Append("Faith low — steady life-support weeks restore it; at 0, ")
                    .Append(reserve).Append("+ AIR may extend the contract. ");
            else if (colony.Faith < 55)
                sb.Append("Faith falling on quiet weeks — run the processor and avoid deadline penalties. ");

            if (colony.Satisfaction < MainGameData.WorkThreshold)
                sb.Append("Crew STOPPED — Satisfaction under ").Append(MainGameData.WorkThreshold)
                    .Append(" (builds & air production pause). ");
            else if (colony.Satisfaction < MainGameData.WorkThreshold + 15)
                sb.Append("Keep Satisfaction above ").Append(MainGameData.WorkThreshold)
                    .Append(" so the crew keeps working. ");

            if (colony.MainGameStep >= 4 && runway < 2.0 && runway < 1000)
                sb.Append($"AIR runway under 2 weeks ({runway:0.0} wk) — power processor & ice. ");

            if (colony.DustStormActive)
                sb.Append("STORM: Solar off — Windmill must power the processor. ");

            if (colony.StormLandWeek >= 0 && !colony.DustStormActive
                && colony.StormLandWeek - colony.Week <= MainGameData.DeadlinePressureWeeks
                && !colony.PowerBesideOxygen())
                sb.Append("Storm soon — place Wind/Solar beside processor or Faith drops. ");

            return sb.Length > 0 ? "WATCH: " + sb.ToString().Trim() : "";
        }

        public static string OpeningSpeech(MainGameColony colony)
        {
            string next = colony?.ObjectiveText(out _, out _) ?? "find ice.";
            return
                "The lander is down. I'm Mara Voss, colony foreman. The corporation scores this contract on "
                + "Faith (trust), colonist Satisfaction (work stops below " + MainGameData.WorkThreshold + "), "
                + "and AIR stored each week — not just whether you survive one crisis.\n\n"
                + "Play cards to place buildings, then End Week so construction and life-support advance. "
                + "Yellow hints under the hand tell you the one job that matters.\n\n"
                + "First objective: " + next;
        }

        public static string FaithRecoveryHint(MainGameColony colony)
        {
            if (colony == null) return "";
            int reserve = MainGameData.AirReserveFaithExtensionThreshold(colony.O2Need());
            return "Steady weeks (+AIR, crew working) raise Faith again. With "
                + reserve + "+ AIR in reserve, the auditor may extend the contract instead of pulling it.";
        }

        public static string StepAdvancedNarration(int newStep)
        {
            return newStep switch
            {
                1 => "Ice found. The corporation unlocked Ice Aquifer cards.",
                2 => "First drill online. Build and power the Atmosphere Processor.",
                3 => "Air plant ready. House the shuttle crew — 20 beds total.",
                4 => "Shuttle window handled. Survey and drill the second ice field.",
                5 => "Second drill done. Storm incoming: storm power, stock AIR, protect Faith.",
                6 => "Storm survived. Contract complete.",
                _ => "Objective updated."
            };
        }

        static bool HasSolarBesideOxygen(MainGameColony colony)
        {
            foreach (var t in colony.Board.Tiles)
            {
                if (t.Building != MainGameBuildingId.Solar || t.Broken) continue;
                foreach (var o in colony.Board.Tiles)
                {
                    if (o.Building != MainGameBuildingId.Oxygen || o.Broken) continue;
                    if (colony.Dist(t.Index, o.Index) <= 2) return true;
                }
            }
            return false;
        }
    }
}