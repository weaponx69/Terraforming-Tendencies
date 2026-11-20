using System;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>
    /// Individual Prologue resource stocks (not collapsed into Materials).
    /// Ice stays on <see cref="PrologueColony.IceStock"/>; these cover mineable deposits.
    /// </summary>
    public enum PrologueResourceId
    {
        Minerals = 0,
        Gas = 1,
        Iron = 2,
        Regolith = 3
    }

    public static class PrologueResourceIdUtil
    {
        public static readonly PrologueResourceId[] All =
        {
            PrologueResourceId.Minerals,
            PrologueResourceId.Gas,
            PrologueResourceId.Iron,
            PrologueResourceId.Regolith
        };

        public static bool TryParse(string typeName, out PrologueResourceId id)
        {
            id = default;
            if (string.IsNullOrEmpty(typeName)) return false;
            if (typeName.Equals("Minerals", StringComparison.OrdinalIgnoreCase))
            {
                id = PrologueResourceId.Minerals;
                return true;
            }
            if (typeName.Equals("Gas", StringComparison.OrdinalIgnoreCase))
            {
                id = PrologueResourceId.Gas;
                return true;
            }
            if (typeName.Equals("Iron", StringComparison.OrdinalIgnoreCase))
            {
                id = PrologueResourceId.Iron;
                return true;
            }
            if (typeName.Equals("Regolith", StringComparison.OrdinalIgnoreCase))
            {
                id = PrologueResourceId.Regolith;
                return true;
            }
            return false;
        }

        public static string ToTypeName(PrologueResourceId id) => id switch
        {
            PrologueResourceId.Minerals => "Minerals",
            PrologueResourceId.Gas => "Gas",
            PrologueResourceId.Iron => "Iron",
            PrologueResourceId.Regolith => "Regolith",
            _ => id.ToString()
        };
    }
}
