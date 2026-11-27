using System;

namespace GameDevTV.RTS.MainGame
{
    /// <summary>
    /// Individual MainGame resource stocks (<see cref="MainGameResourceId.Minerals"/> is shown as Materials in UI).
    /// Ice stays on <see cref="MainGameColony.IceStock"/>; these cover mineable deposits.
    /// </summary>
    public enum MainGameResourceId
    {
        Minerals = 0,
        Gas = 1,
        Iron = 2,
        Regolith = 3
    }

    public static class MainGameResourceIdUtil
    {
        public static readonly MainGameResourceId[] All =
        {
            MainGameResourceId.Minerals,
            MainGameResourceId.Gas,
            MainGameResourceId.Iron,
            MainGameResourceId.Regolith
        };

        public static bool TryParse(string typeName, out MainGameResourceId id)
        {
            id = default;
            if (string.IsNullOrEmpty(typeName)) return false;
            if (typeName.Equals("Minerals", StringComparison.OrdinalIgnoreCase)
                || typeName.Equals("Materials", StringComparison.OrdinalIgnoreCase))
            {
                id = MainGameResourceId.Minerals;
                return true;
            }
            if (typeName.Equals("Gas", StringComparison.OrdinalIgnoreCase))
            {
                id = MainGameResourceId.Gas;
                return true;
            }
            if (typeName.Equals("Iron", StringComparison.OrdinalIgnoreCase))
            {
                id = MainGameResourceId.Iron;
                return true;
            }
            if (typeName.Equals("Regolith", StringComparison.OrdinalIgnoreCase))
            {
                id = MainGameResourceId.Regolith;
                return true;
            }
            return false;
        }

        public static string ToTypeName(MainGameResourceId id) => id switch
        {
            MainGameResourceId.Minerals => "Materials",
            MainGameResourceId.Gas => "Gas",
            MainGameResourceId.Iron => "Iron",
            MainGameResourceId.Regolith => "Regolith",
            _ => id.ToString()
        };
    }
}
