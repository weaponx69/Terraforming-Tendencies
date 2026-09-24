using System.Collections.Generic;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    public enum PlacementLinkKind
    {
        Neighbor,
        SameTag,
        ClimatePair,
        Power,
        Anchor,
        Life,
        Geology
    }

    public readonly struct PlacementLink
    {
        public readonly BaseBuilding Building;
        public readonly PlacementLinkKind Kind;
        public readonly string Label;
        public readonly int ScoreBonus;

        public PlacementLink(BaseBuilding building, PlacementLinkKind kind, string label, int scoreBonus)
        {
            Building = building;
            Kind = kind;
            Label = label;
            ScoreBonus = scoreBonus;
        }
    }

    /// <summary>
    /// Combolands-style dry-run of placing a card on a hex: score estimate,
    /// terraforming rates/pulses, and per-neighbor combo roles for the placement halo.
    /// </summary>
    public sealed class PlacementComboPreview
    {
        public BuildingSO Building;
        public Vector2Int Cell;
        public Vector3 WorldPos;
        public string Tag;
        public int BaseScore;
        public int AdjScore;
        public int PowerBonus;
        public int EstimatedTotal;
        public float ClimateRateMult = 1f;
        public bool IsClimateTile;
        public float ProductionEfficiency = 1f;
        public bool WillBePowered = true;

        /// <summary>Expected generation after efficiency × combo (per spent week).</summary>
        public float TempRatePerSec;
        public float AtmosRatePerSec;
        public float WaterRatePerSec;

        /// <summary>Instant on-place climate (geology match + first climate-pair pulse).</summary>
        public float InstantTemp;
        public float InstantAtmos;
        public float InstantWater;

        /// <summary>Remaining sector/Act budget at this hover cell (what can still apply).</summary>
        public float RemainTemp;
        public float RemainAtmos;
        public float RemainWater;

        public readonly List<PlacementLink> Links = new List<PlacementLink>();

        public bool HasTerraformRates =>
            TempRatePerSec > 0.0001f || AtmosRatePerSec > 0.0001f || WaterRatePerSec > 0.0001f;

        public bool HasInstantTerraform =>
            InstantTemp > 0.0001f || InstantAtmos > 0.0001f || InstantWater > 0.0001f;

        public string SummaryLine
        {
            get
            {
                var parts = new List<string>();
                if (EstimatedTotal > 0) parts.Add($"+{EstimatedTotal} score");

                string rates = FormatClimateTriplet(TempRatePerSec, AtmosRatePerSec, WaterRatePerSec, perWeek: true);
                if (!string.IsNullOrEmpty(rates))
                {
                    string powerNote = WillBePowered ? "" : " @20%";
                    if (ClimateRateMult > 1.01f)
                        parts.Add($"{rates} ×{ClimateRateMult:0.00}{powerNote}");
                    else
                        parts.Add($"{rates}{powerNote}");
                }
                else if (IsClimateTile)
                {
                    parts.Add(ClimateRateMult > 1.01f
                        ? $"climate ×{ClimateRateMult:0.00}"
                        : "climate ×1.00");
                }

                string pulse = FormatClimateTriplet(InstantTemp, InstantAtmos, InstantWater, perWeek: false);
                if (!string.IsNullOrEmpty(pulse))
                    parts.Add($"on place {pulse}");

                if (Links.Count > 0) parts.Add($"{Links.Count} combo link{(Links.Count == 1 ? "" : "s")}");
                if (parts.Count == 0) parts.Add("No edge combo yet");
                return string.Join(" · ", parts);
            }
        }

        /// <summary>Second line under the score summary — remaining Act/sector climate headroom.</summary>
        public string ClimateBudgetLine
        {
            get
            {
                if (!HasTerraformRates && !HasInstantTerraform && !IsClimateTile)
                    return null;
                string left = FormatClimateTriplet(RemainTemp, RemainAtmos, RemainWater, perWeek: false);
                if (string.IsNullOrEmpty(left)) return "sector climate full";
                return $"left {left}";
            }
        }

        public static string FormatClimateTriplet(float temp, float atmos, float water, bool perWeek)
        {
            var bits = new List<string>(3);
            string suffix = perWeek ? "/wk" : "";
            if (temp > 0.0001f) bits.Add($"+{temp:0.##}C{suffix}");
            if (atmos > 0.0001f) bits.Add($"+{atmos:0.###}atm{suffix}");
            if (water > 0.0001f) bits.Add($"+{water:0.##}%{suffix}");
            return bits.Count == 0 ? null : string.Join(" ", bits);
        }

        public static Color ColorFor(PlacementLinkKind kind)
        {
            return kind switch
            {
                PlacementLinkKind.SameTag => new Color(0.35f, 0.95f, 0.55f, 0.55f),
                PlacementLinkKind.ClimatePair => new Color(0.95f, 0.55f, 0.2f, 0.55f),
                PlacementLinkKind.Power => new Color(1f, 0.92f, 0.25f, 0.55f),
                PlacementLinkKind.Anchor => new Color(0.55f, 0.75f, 1f, 0.55f),
                PlacementLinkKind.Life => new Color(0.55f, 0.95f, 0.85f, 0.55f),
                PlacementLinkKind.Geology => new Color(0.85f, 0.55f, 0.95f, 0.55f),
                _ => new Color(0.35f, 0.75f, 1f, 0.4f),
            };
        }
    }
}
