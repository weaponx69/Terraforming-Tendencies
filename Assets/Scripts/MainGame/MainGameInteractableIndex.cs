using System.Collections.Generic;

namespace GameDevTV.RTS.MainGame
{
    /// <summary>
    /// MainGame-only registry so survey / paint never calls FindObjectsByType per frame or per click.
    /// Built as marks spawn; cleared when marks despawn.
    /// </summary>
    public static class MainGameInteractableIndex
    {
        public static readonly List<MainGameDepositMark> DepositMarks = new(32);
        public static readonly List<MainGameIceMarkerClick> IceClicks = new(16);

        public static void Register(MainGameDepositMark mark)
        {
            if (mark != null && !DepositMarks.Contains(mark))
                DepositMarks.Add(mark);
        }

        public static void Unregister(MainGameDepositMark mark)
        {
            if (mark != null) DepositMarks.Remove(mark);
        }

        public static void Register(MainGameIceMarkerClick click)
        {
            if (click != null && !IceClicks.Contains(click))
                IceClicks.Add(click);
        }

        public static void Unregister(MainGameIceMarkerClick click)
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