using System.Collections.Generic;
using System.Text;

namespace GameDevTV.RTS.Prologue
{
    public enum PrologueBriefingTone
    {
        Progress,
        Concern,
        Crisis
    }

    public struct PrologueWeekMemory
    {
        public int Satisfaction;
        public int Faith;
        public int Oxygen;
        public int Step;
        public bool Storm;
    }

    /// <summary>
    /// What a person says after End Week: what the crew did, what they need next,
    /// and whether the news is bad enough to stop the clock.
    /// </summary>
    public readonly struct PrologueWeekBriefing
    {
        public readonly string Speaker;
        public readonly string Role;
        public readonly string Speech;
        public readonly PrologueBriefingTone Tone;

        public PrologueWeekBriefing(string speaker, string role, string speech, PrologueBriefingTone tone)
        {
            Speaker = speaker;
            Role = role;
            Speech = speech;
            Tone = tone;
        }

        /// <summary>Mara at landing, before the first week. Names the first job.</summary>
        public static PrologueWeekBriefing Opening(PrologueColony colony)
        {
            string next = colony.ObjectiveText(out _, out _);
            string speech = PrologueGuidance.OpeningSpeech(colony);
            return new PrologueWeekBriefing("Mara Voss", "Colony foreman", speech, PrologueBriefingTone.Progress);
        }

        public static PrologueWeekBriefing Compose(
            PrologueColony colony,
            IReadOnlyList<string> log,
            PrologueWeekMemory before)
        {
            string next = colony.ContractCancelled
                ? "The contract is over."
                : colony.ObjectiveText(out _, out _);

            var did = new List<string>();
            var worry = new List<string>();
            bool crisis = colony.ContractCancelled;
            if (log != null)
            {
                for (int i = 0; i < log.Count; i++)
                {
                    string line = log[i];
                    if (string.IsNullOrEmpty(line)) continue;
                    if (line.Contains("cancelled the contract"))
                    {
                        crisis = true;
                        worry.Add(line);
                    }
                    else if (line.Contains("Colonists stopped"))
                        worry.Add("The crew stopped. Satisfaction is too low to keep terraforming.");
                    else if (line.Contains("AIR CRITICAL"))
                        worry.Add($"The air ran short. People breathed less than the {colony.O2Need()} the week required.");
                    else if (line.Contains("unpowered"))
                        worry.Add("The Atmosphere Processor had no power, so it made no air.");
                    else if (line.Contains("starved for ice"))
                        worry.Add("The Atmosphere Processor had no ice, so it made no air.");
                    else if (line.Contains("air. Colonists ran"))
                        did.Add(line.Replace(" Colonists ran the Atmosphere Processor.", " The crew ran the Atmosphere Processor."));
                    else if (line.StartsWith("Completed "))
                        did.Add("The crew finished the " + line.Substring("Completed ".Length) + ".");
                    else if (line.Contains("colonists landed"))
                        did.Add(line + ". They need beds and air.");
                    else if (line.Contains("from mine"))
                        did.Add("The mine crew brought in stock. " + line);
                    else if (line.Contains("Corporation holds the contract"))
                        did.Add(line);
                }
            }

            if (colony.DustStormActive && !before.Storm)
            {
                crisis = true;
                worry.Add("The dust storm is on the camp. Camp power is dark. A Windmill within 2 tiles of the Atmosphere Processor has to run or the air stops.");
            }
            if (!crisis && colony.Faith < before.Faith)
                worry.Add($"The corporation's faith fell to {colony.Faith}. " + PrologueGuidance.FaithRecoveryHint(colony));
            if (!crisis && colony.Satisfaction < before.Satisfaction)
                worry.Add($"Colonist satisfaction fell to {colony.Satisfaction}.");

            PrologueBriefingTone tone = crisis
                ? PrologueBriefingTone.Crisis
                : worry.Count > 0 ? PrologueBriefingTone.Concern : PrologueBriefingTone.Progress;

            string speaker;
            string role;
            if (tone == PrologueBriefingTone.Crisis)
            {
                speaker = "Auditor Kline";
                role = "Corporation";
            }
            else if (tone == PrologueBriefingTone.Concern)
            {
                speaker = "Ivo Pell";
                role = "Colonist";
            }
            else
            {
                speaker = "Mara Voss";
                role = "Colony foreman";
            }

            var speech = new StringBuilder();
            speech.Append("Week ").Append(colony.Week).Append(". ");
            if (tone == PrologueBriefingTone.Progress)
            {
                if (did.Count == 0)
                    speech.Append("The crew held the camp. Nothing new moved the air. ");
                else
                {
                    for (int i = 0; i < did.Count; i++)
                    {
                        speech.Append(did[i]);
                        if (!did[i].EndsWith(".")) speech.Append('.');
                        speech.Append(' ');
                    }
                }
                speech.Append("Next: ").Append(next);
            }
            else if (tone == PrologueBriefingTone.Concern)
            {
                for (int i = 0; i < worry.Count; i++)
                {
                    speech.Append(worry[i]);
                    if (!worry[i].EndsWith(".")) speech.Append('.');
                    speech.Append(' ');
                }
                speech.Append("What we need next: ").Append(next);
            }
            else
            {
                if (colony.ContractCancelled && !string.IsNullOrEmpty(colony.ContractMessage))
                    speech.Append(colony.ContractMessage).Append(" The mission is over.");
                else if (worry.Count > 0)
                    speech.Append(worry[0]).Append(' ');
                else
                    speech.Append("This week the mission slipped. ");
                if (!colony.ContractCancelled)
                    speech.Append("What we need next: ").Append(next);
            }

            return new PrologueWeekBriefing(speaker, role, speech.ToString().Trim(), tone);
        }
    }
}
