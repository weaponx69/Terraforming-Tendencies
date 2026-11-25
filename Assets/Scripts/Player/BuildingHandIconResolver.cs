using System.Collections.Generic;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>Building hand icons: SO art, then Resources/BuildingHandIcons/* (1024 PPU card style).</summary>
    public static class BuildingHandIconResolver
    {
        static readonly Dictionary<string, Sprite> cache = new();

        public static Sprite Resolve(BuildingSO building)
        {
            if (building == null) return null;
            if (building.Icon != null) return building.Icon;

            string name = building.Name ?? building.name ?? "";
            if (string.IsNullOrEmpty(name)) return null;
            if (cache.TryGetValue(name, out var hit) && hit != null) return hit;

            string slug = Slug(name);
            var sprite = Resources.Load<Sprite>($"BuildingHandIcons/{slug}");
            if (sprite != null)
                cache[name] = sprite;
            return sprite;
        }

        static string Slug(string name)
        {
            var chars = new List<char>(name.Length);
            bool lastSpace = false;
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c))
                {
                    chars.Add(char.ToLowerInvariant(c));
                    lastSpace = false;
                }
                else if (!lastSpace && chars.Count > 0)
                    lastSpace = true;
            }

            return new string(chars.ToArray())
                .Replace(" ", "")
                .Replace("-", "");
        }
    }
}