using System.Collections.Generic;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>
    /// Prologue-only registry so survey / paint never calls FindObjectsByType per frame or per click.
    /// Built as marks spawn; cleared when marks despawn.
    /// </summary>
    public static class PrologueInteractableIndex
    {
        public static readonly List<PrologueDepositMark> DepositMarks = new(32);
        public static readonly List<PrologueIceMarkerClick> IceClicks = new(16);

        public static void Register(PrologueDepositMark mark)
        {
            if (mark != null && !DepositMarks.Contains(mark))
                DepositMarks.Add(mark);
        }

        public static void Unregister(PrologueDepositMark mark)
        {
            if (mark != null) DepositMarks.Remove(mark);
        }

        public static void Register(PrologueIceMarkerClick click)
        {
            if (click != null && !IceClicks.Contains(click))
                IceClicks.Add(click);
        }

        public static void Unregister(PrologueIceMarkerClick click)
        {
            if (click != null) IceClicks.Remove(click);
        }

        public static void Clear()
        {
            DepositMarks.Clear();
            IceClicks.Clear();
        }
    }
}