using System.Collections.Generic;
using UnityEngine;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Authored building bodies for the themed prefabs that used to be a Unity primitive.
    /// Each mesh sits on y = 0 and stays inside a hex (about 4.5 m across).
    /// Submeshes: 0 hull, 1 steel, 2 pit, 3 glass, 4 trim.
    /// </summary>
    public static class ColonyBuildingMeshLibrary
    {
        public const int Hull = 0;
        public const int Steel = 1;
        public const int Pit = 2;
        public const int Glass = 3;
        public const int Trim = 4;

        public static Mesh Build(string buildingName)
        {
            var d = new Draft();
            switch (buildingName)
            {
                case "Deep-Core Mining Laser": Laser(d); break;
                case "Basalt Strip-Mine": StripMine(d); break;
                case "Iron Ore Extractor": Iron(d); break;
                case "Gas Well": GasWell(d); break;
                case "GHG Factory": Factory(d); break;
                case "Carbon Dioxide Import Laser": ImportLaser(d); break;
                case "Atmospheric Condenser": Condenser(d); break;
                case "Urban Green Commons": Commons(d); break;
                case "Subterranean Apartment Block": Apartments(d); break;
                case "Subglacial Water Extractor": Subglacial(d); break;
                case "Solar Greenhouse": Greenhouse(d); break;
                case "Sector Command Center": SectorCommand(d); break;
                case "Methanogenic Microbe Spreader": Spreader(d); break;
                case "Magnetic Shield Generator": Shield(d); break;
                case "Lichen Nursery": Nursery(d); break;
                case "Lava Tube Outpost": LavaTube(d); break;
                case "Habitat": HabitatDomes(d); break;
                case "Oxygen Processor": OxygenProcessor(d); break;
                case "Inflatable Bio-Dome": Dome(d, 1.55f, true); break;
                case "Greenery Dome": Dome(d, 1.25f, false); break;
                case "Geothermal Generator": Geothermal(d); break;
                case "Genetically Modified Algae Spreader": Algae(d); break;
                case "Biosphere Center": Biosphere(d); break;
                default: return null;
            }
            return d.ToMesh(buildingName);
        }

        static void Laser(Draft d)
        {
            d.Disc(Vector3.zero, 2.15f, 0.04f, 24, Hull);
            d.Slope(0.04f, 1.85f, 0.42f, 1.15f, 20, Pit);
            d.Disc(new Vector3(0f, 0.42f, 0f), 1.15f, 0f, 16, Pit);
            d.Box(new Vector3(0f, 0.5f, 1.35f), new Vector3(2.4f, 0.08f, 0.16f), Steel);
            d.Box(new Vector3(0f, 0.5f, -1.35f), new Vector3(2.4f, 0.08f, 0.16f), Steel);
            d.Box(new Vector3(-1.15f, 0.7f, 0f), new Vector3(0.12f, 1.2f, 0.12f), Steel);
            d.Box(new Vector3(1.15f, 0.7f, 0f), new Vector3(0.12f, 1.2f, 0.12f), Steel);
            d.Box(new Vector3(0f, 1.28f, 0f), new Vector3(2.5f, 0.1f, 0.16f), Steel);
            d.Box(new Vector3(0f, 1.05f, 0f), new Vector3(0.28f, 0.22f, 0.28f), Trim);
            d.Cylinder(new Vector3(0f, 0.72f, 0f), 0.035f, 0.55f, 6, Vector3.up, Trim);
        }

        static void StripMine(Draft d)
        {
            d.Box(new Vector3(0f, 0.06f, 0f), new Vector3(4.2f, 0.12f, 3.2f), Hull);
            d.Box(new Vector3(0f, 0.28f, 0f), new Vector3(2.6f, 0.36f, 1.5f), Pit);
            d.Box(new Vector3(0f, 0.55f, 1.15f), new Vector3(3.2f, 0.12f, 0.35f), Steel);
            d.Box(new Vector3(1.35f, 0.42f, 1.15f), new Vector3(0.4f, 0.4f, 0.4f), Steel);
            d.Cylinder(new Vector3(-1.15f, 0.55f, 0.15f), 0.55f, 0.18f, 12, Vector3.forward, Steel);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                d.Box(new Vector3(-1.15f + Mathf.Cos(a) * 0.48f, 0.55f + Mathf.Sin(a) * 0.48f, 0.15f),
                    new Vector3(0.16f, 0.22f, 0.22f), Trim);
            }
            d.Box(new Vector3(1.5f, 0.35f, -1.15f), new Vector3(0.9f, 0.45f, 0.7f), Hull);
        }

        static void Iron(Draft d)
        {
            d.Disc(Vector3.zero, 2.1f, 0.04f, 20, Hull);
            d.Slope(0.04f, 1.7f, 0.38f, 0.85f, 16, Pit);
            d.Disc(new Vector3(0f, 0.38f, 0f), 0.85f, 0f, 12, Pit);
            d.Box(new Vector3(1.35f, 0.55f, 0f), new Vector3(0.7f, 0.9f, 0.9f), Steel);
            d.Beam(new Vector3(1.05f, 0.95f, 0f), new Vector3(-0.15f, 1.35f, 0f), 0.08f, Steel);
            d.Beam(new Vector3(-0.15f, 1.35f, 0f), new Vector3(-0.15f, 0.45f, 0f), 0.04f, Trim);
            d.Box(new Vector3(-1.25f, 0.4f, 0.9f), new Vector3(0.7f, 0.55f, 0.7f), Hull);
        }

        static void GasWell(Draft d)
        {
            d.Box(new Vector3(0f, 0.05f, 0f), new Vector3(3.4f, 0.1f, 2.6f), Hull);
            d.Cylinder(new Vector3(-0.35f, 0.45f, 0f), 0.28f, 0.7f, 10, Vector3.up, Steel);
            d.Box(new Vector3(-0.35f, 0.85f, 0f), new Vector3(0.7f, 0.12f, 0.16f), Trim);
            d.Box(new Vector3(-0.35f, 0.7f, 0.35f), new Vector3(0.16f, 0.16f, 0.45f), Trim);
            d.Cylinder(new Vector3(0.85f, 0.32f, 0f), 0.28f, 1.3f, 12, Vector3.right, Steel);
            d.Cylinder(new Vector3(0.15f, 0.55f, -0.85f), 0.08f, 0.9f, 6, Vector3.up, Steel);
            d.Box(new Vector3(1.45f, 0.4f, 0f), new Vector3(0.45f, 0.55f, 0.55f), Hull);
        }

        static void Factory(Draft d)
        {
            d.Box(new Vector3(0f, 0.55f, 0f), new Vector3(3.4f, 1.0f, 2.2f), Hull);
            d.Box(new Vector3(0f, 1.12f, 0f), new Vector3(3.6f, 0.16f, 2.4f), Steel);
            d.Cylinder(new Vector3(-0.9f, 1.7f, 0.4f), 0.16f, 1.0f, 8, Vector3.up, Steel);
            d.Cylinder(new Vector3(0.7f, 1.55f, -0.35f), 0.12f, 0.7f, 8, Vector3.up, Steel);
            d.Box(new Vector3(1.55f, 0.35f, 0.9f), new Vector3(0.5f, 0.55f, 0.7f), Trim);
            d.Box(new Vector3(0f, 0.45f, 1.15f), new Vector3(1.1f, 0.7f, 0.08f), Glass);
        }

        static void ImportLaser(Draft d)
        {
            d.Disc(Vector3.zero, 2.05f, 0.04f, 20, Hull);
            d.Ring(0.08f, 1.15f, 1.55f, 20, Steel);
            d.Box(new Vector3(0f, 0.55f, -1.15f), new Vector3(1.3f, 0.7f, 0.7f), Hull);
            d.Beam(new Vector3(-0.7f, 0.35f, -0.7f), new Vector3(0.15f, 1.15f, 0.35f), 0.07f, Steel);
            d.Beam(new Vector3(0.7f, 0.35f, -0.7f), new Vector3(0.15f, 1.15f, 0.35f), 0.07f, Steel);
            d.Disc(new Vector3(0.35f, 1.2f, 0.55f), 0.42f, 0f, 12, Trim);
            d.Box(new Vector3(1.2f, 0.35f, 0.85f), new Vector3(0.7f, 0.5f, 0.55f), Steel);
        }

        static void Condenser(Draft d)
        {
            d.Box(new Vector3(0f, 0.05f, 0f), new Vector3(3.8f, 0.1f, 2.4f), Hull);
            for (int i = 0; i < 7; i++)
            {
                float x = -1.35f + i * 0.45f;
                d.Box(new Vector3(x, 0.7f, 0f), new Vector3(0.08f, 1.15f, 1.5f), Steel);
            }
            d.Box(new Vector3(0f, 0.35f, 0.95f), new Vector3(3.0f, 0.16f, 0.16f), Trim);
            d.Cylinder(new Vector3(1.55f, 0.45f, -0.7f), 0.32f, 0.7f, 10, Vector3.up, Steel);
        }

        static void Commons(Draft d)
        {
            d.Box(new Vector3(0f, 0.04f, 0f), new Vector3(4.0f, 0.08f, 3.2f), Hull);
            d.Box(new Vector3(-1.15f, 0.22f, -0.7f), new Vector3(0.9f, 0.28f, 0.9f), Pit);
            d.Box(new Vector3(1.15f, 0.22f, -0.7f), new Vector3(0.9f, 0.28f, 0.9f), Pit);
            d.Box(new Vector3(0f, 0.22f, 0.85f), new Vector3(1.4f, 0.28f, 0.7f), Pit);
            d.Box(new Vector3(-1.15f, 0.42f, -0.7f), new Vector3(0.55f, 0.16f, 0.55f), Trim);
            d.Box(new Vector3(1.15f, 0.42f, -0.7f), new Vector3(0.55f, 0.16f, 0.55f), Trim);
            d.Box(new Vector3(0f, 0.42f, 0.85f), new Vector3(0.9f, 0.16f, 0.4f), Trim);
            d.Box(new Vector3(-1.3f, 0.85f, 0.2f), new Vector3(0.08f, 1.4f, 0.08f), Steel);
            d.Box(new Vector3(1.3f, 0.85f, 0.2f), new Vector3(0.08f, 1.4f, 0.08f), Steel);
            d.Box(new Vector3(0f, 1.55f, 0.2f), new Vector3(2.8f, 0.06f, 1.6f), Steel);
        }

        static void Apartments(Draft d)
        {
            d.Box(new Vector3(0f, 0.15f, 0f), new Vector3(3.6f, 0.3f, 2.8f), Hull);
            d.Box(new Vector3(0f, 0.7f, -0.15f), new Vector3(3.2f, 0.85f, 2.2f), Steel);
            d.Box(new Vector3(0f, 1.18f, -0.15f), new Vector3(3.4f, 0.14f, 2.4f), Hull);
            d.Box(new Vector3(0f, 0.15f, 1.35f), new Vector3(1.1f, 0.2f, 0.9f), Pit);
            for (int row = 0; row < 2; row++)
            for (int col = 0; col < 3; col++)
            {
                d.Box(new Vector3(-0.9f + col * 0.9f, 0.5f + row * 0.42f, 0.96f),
                    new Vector3(0.38f, 0.22f, 0.06f), Glass);
            }
        }

        static void Subglacial(Draft d)
        {
            d.Disc(Vector3.zero, 1.9f, 0.05f, 18, Hull);
            d.Ring(0.12f, 0.7f, 1.15f, 16, Glass);
            d.Box(new Vector3(0f, 0.55f, -1.05f), new Vector3(1.2f, 0.85f, 0.9f), Steel);
            d.Cylinder(new Vector3(0f, 0.35f, 0.15f), 0.12f, 0.5f, 8, Vector3.up, Steel);
            d.Cylinder(new Vector3(1.15f, 0.4f, 0.4f), 0.32f, 0.7f, 10, Vector3.up, Steel);
            d.Beam(new Vector3(0.3f, 0.4f, -0.5f), new Vector3(1.05f, 0.45f, 0.3f), 0.08f, Trim);
        }

        static void Greenhouse(Draft d)
        {
            d.Box(new Vector3(0f, 0.08f, 0f), new Vector3(3.8f, 0.16f, 2.2f), Hull);
            d.Box(new Vector3(0f, 0.7f, 0f), new Vector3(3.4f, 1.05f, 1.7f), Glass);
            d.Box(new Vector3(0f, 0.7f, 0f), new Vector3(3.5f, 0.06f, 0.06f), Steel);
            d.Box(new Vector3(0f, 0.7f, 0f), new Vector3(0.06f, 1.1f, 1.75f), Steel);
            d.Box(new Vector3(-1.15f, 0.7f, 0f), new Vector3(0.05f, 1.05f, 1.7f), Steel);
            d.Box(new Vector3(1.15f, 0.7f, 0f), new Vector3(0.05f, 1.05f, 1.7f), Steel);
            d.Box(new Vector3(0f, 1.25f, 0f), new Vector3(3.5f, 0.08f, 1.8f), Steel);
            d.Box(new Vector3(0f, 1.32f, 0f), new Vector3(2.4f, 0.06f, 0.7f), Trim);
        }

        static void SectorCommand(Draft d)
        {
            d.Box(new Vector3(0f, 0.08f, 0f), new Vector3(4.0f, 0.16f, 3.0f), Hull);
            d.Box(new Vector3(-0.4f, 0.7f, 0f), new Vector3(2.2f, 1.1f, 1.8f), Steel);
            d.Box(new Vector3(1.15f, 0.45f, 0.35f), new Vector3(1.1f, 0.6f, 1.3f), Hull);
            d.Box(new Vector3(-0.4f, 0.7f, 0.92f), new Vector3(1.2f, 0.4f, 0.06f), Glass);
            d.Cylinder(new Vector3(1.35f, 1.15f, -0.7f), 0.06f, 1.1f, 6, Vector3.up, Steel);
            d.Disc(new Vector3(1.55f, 1.55f, -0.55f), 0.28f, 0f, 10, Trim);
        }

        static void Spreader(Draft d)
        {
            d.Box(new Vector3(0f, 0.28f, 0.35f), new Vector3(2.6f, 0.35f, 1.3f), Steel);
            d.Box(new Vector3(-1.15f, 0.18f, 0.85f), new Vector3(1.1f, 0.22f, 0.28f), Pit);
            d.Box(new Vector3(-1.15f, 0.18f, -0.15f), new Vector3(1.1f, 0.22f, 0.28f), Pit);
            d.Box(new Vector3(0.9f, 0.18f, 0.85f), new Vector3(0.8f, 0.22f, 0.28f), Pit);
            d.Box(new Vector3(0.9f, 0.18f, -0.15f), new Vector3(0.8f, 0.22f, 0.28f), Pit);
            d.Cylinder(new Vector3(-0.2f, 0.75f, 0.35f), 0.42f, 1.5f, 12, Vector3.right, Hull);
            d.Box(new Vector3(0.3f, 0.55f, -0.85f), new Vector3(2.4f, 0.08f, 0.08f), Trim);
            d.Box(new Vector3(-0.7f, 0.4f, -0.85f), new Vector3(0.08f, 0.25f, 0.08f), Trim);
            d.Box(new Vector3(1.2f, 0.4f, -0.85f), new Vector3(0.08f, 0.25f, 0.08f), Trim);
        }

        static void Shield(Draft d)
        {
            d.Disc(Vector3.zero, 2.0f, 0.06f, 20, Hull);
            d.Ring(0.55f, 1.05f, 1.35f, 24, Steel);
            d.Ring(0.42f, 1.15f, 1.25f, 24, Steel);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                d.Box(new Vector3(Mathf.Cos(a) * 1.2f, 0.35f, Mathf.Sin(a) * 1.2f),
                    new Vector3(0.35f, 0.45f, 0.28f), Trim, a);
            }
            d.Cylinder(new Vector3(0f, 0.4f, 0f), 0.22f, 0.55f, 10, Vector3.up, Glass);
        }

        static void Nursery(Draft d)
        {
            d.Box(new Vector3(0f, 0.05f, 0f), new Vector3(3.6f, 0.1f, 2.6f), Hull);
            d.Box(new Vector3(-0.7f, 0.4f, 0f), new Vector3(1.4f, 0.12f, 1.6f), Steel);
            d.Box(new Vector3(0.9f, 0.4f, 0f), new Vector3(1.4f, 0.12f, 1.6f), Steel);
            d.Box(new Vector3(-0.7f, 0.5f, 0f), new Vector3(1.15f, 0.1f, 1.3f), Trim);
            d.Box(new Vector3(0.9f, 0.5f, 0f), new Vector3(1.15f, 0.1f, 1.3f), Trim);
            d.Box(new Vector3(0f, 0.85f, -0.95f), new Vector3(3.0f, 0.08f, 0.5f), Steel);
            d.Box(new Vector3(-1.4f, 0.5f, -0.95f), new Vector3(0.08f, 0.7f, 0.08f), Steel);
            d.Box(new Vector3(1.4f, 0.5f, -0.95f), new Vector3(0.08f, 0.7f, 0.08f), Steel);
        }

        static void LavaTube(Draft d)
        {
            d.Box(new Vector3(0f, 0.06f, 0.4f), new Vector3(3.2f, 0.12f, 2.4f), Hull);
            d.Arch(new Vector3(0f, 0.12f, -0.2f), 1.15f, 1.6f, 10, Pit);
            d.Box(new Vector3(0f, 0.9f, 0.7f), new Vector3(1.5f, 1.5f, 0.16f), Steel);
            d.Box(new Vector3(0f, 0.55f, 0.78f), new Vector3(0.7f, 0.9f, 0.08f), Glass);
            d.Box(new Vector3(1.2f, 0.4f, 0.9f), new Vector3(0.7f, 0.55f, 0.6f), Hull);
        }

        static void HabitatDomes(Draft d)
        {
            // One hex is about 4.3 m from center to center. Stay inside that so a
            // neighbor does not sit inside the dome.
            var main = new Vector3(-0.38f, 0f, 0f);
            var annex = new Vector3(1.13f, 0f, 0f);
            d.Disc(Vector3.zero, 1.87f, 0.05f, 28, Hull);
            d.Ring(0.08f, 1.70f, 1.85f, 28, Steel);
            HabitatShell(d, main, 1.16f, 0.50f, 24, 8, false, 0f);
            HabitatShell(d, annex, 0.65f, 0.40f, 20, 6, true, 0f);
            d.Cylinder((main + annex) * 0.5f + new Vector3(0f, 0.26f, 0f), 0.26f, 0.57f, 12, Vector3.right, Steel);
            d.Cylinder(main + new Vector3(0f, 1.74f, 0f), 0.03f, 0.32f, 6, Vector3.up, Trim);
            d.Box(main + new Vector3(0.07f, 1.88f, 0f), new Vector3(0.18f, 0.04f, 0.1f), Trim);
        }

        static void OxygenProcessor(Draft d)
        {
            // Plant hall plus two oxygen tanks. Footprint stays inside one hex.
            d.Disc(Vector3.zero, 1.75f, 0.05f, 24, Hull);
            d.Box(new Vector3(0f, 0.55f, 0.05f), new Vector3(1.7f, 0.95f, 1.15f), Steel);
            d.Box(new Vector3(0f, 0.62f, 0.64f), new Vector3(0.7f, 0.42f, 0.06f), Glass);
            d.Box(new Vector3(0f, 1.08f, 0.05f), new Vector3(1.75f, 0.08f, 1.2f), Trim);
            d.Cylinder(new Vector3(-1.05f, 0.85f, -0.15f), 0.34f, 1.55f, 14, Vector3.up, Glass);
            d.Cylinder(new Vector3(1.05f, 0.85f, -0.15f), 0.34f, 1.55f, 14, Vector3.up, Glass);
            d.Cylinder(new Vector3(-1.05f, 1.68f, -0.15f), 0.38f, 0.08f, 14, Vector3.up, Steel);
            d.Cylinder(new Vector3(1.05f, 1.68f, -0.15f), 0.38f, 0.08f, 14, Vector3.up, Steel);
            d.Box(new Vector3(0f, 1.45f, -0.15f), new Vector3(1.7f, 0.1f, 0.12f), Steel);
            d.Cylinder(new Vector3(0f, 1.35f, -0.85f), 0.16f, 1.5f, 10, Vector3.up, Trim);
            d.Box(new Vector3(0.85f, 0.35f, 0.55f), new Vector3(0.45f, 0.28f, 0.4f), Pit);
        }

        static void HabitatShell(Draft d, Vector3 origin, float radius, float skirt, int slices, int stacks, bool door, float doorYaw)
        {
            d.Cylinder(origin + new Vector3(0f, skirt * 0.5f, 0f), radius * 0.96f, skirt, slices, Vector3.up, Steel);
            d.Hemisphere(origin + new Vector3(0f, skirt, 0f), radius, slices, Mathf.Max(stacks, 12), Hull);
            d.Cylinder(origin + new Vector3(0f, skirt, 0f), radius + 0.06f, 0.1f, slices, Vector3.up, Trim);

            int windows = door ? 4 : 6;
            for (int i = 0; i < windows; i++)
            {
                float a = (Mathf.PI * 2f * i) / windows + 0.55f;
                float blocked = door ? doorYaw : 0f;
                if (Facing(a, blocked, door ? 50f : 38f))
                    continue;
                if (door && Facing(a, doorYaw + Mathf.PI, 46f))
                    continue;
                const float phi = 1.05f;
                var normal = new Vector3(Mathf.Cos(a) * Mathf.Cos(phi), Mathf.Sin(phi), Mathf.Sin(a) * Mathf.Cos(phi));
                var center = origin + new Vector3(0f, skirt, 0f) + normal * (radius + 0.02f);
                float size = radius * (door ? 0.28f : 0.32f);
                // Thin axis of the box is local Z. Yaw turns that axis onto the window's outward direction.
                float yaw = Mathf.Atan2(normal.x, normal.z);
                d.Box(center, new Vector3(size, size * 0.72f, 0.06f), Glass, yaw);
            }

            if (!door) return;
            var outDir = new Vector3(Mathf.Cos(doorYaw), 0f, Mathf.Sin(doorYaw));
            var doorCenter = origin + outDir * (radius * 0.9f) + new Vector3(0f, skirt * 0.72f, 0f);
            float doorYawBox = Mathf.Atan2(outDir.x, outDir.z);
            d.Box(doorCenter, new Vector3(radius * 0.72f, skirt * 1.35f, 0.08f), Steel, doorYawBox);
            d.Box(doorCenter + outDir * 0.04f + new Vector3(0f, -0.02f, 0f), new Vector3(radius * 0.4f, skirt * 1.05f, 0.05f), Pit, doorYawBox);
            d.Box(origin + outDir * (radius + radius * 0.28f) + new Vector3(0f, 0.05f, 0f), new Vector3(radius * 0.7f, 0.06f, radius * 0.5f), Hull, doorYawBox);
        }

        static bool Facing(float angle, float target, float degrees)
        {
            return Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, target * Mathf.Rad2Deg)) < degrees;
        }

        static void Dome(Draft d, float radius, bool airlock)
        {
            d.Disc(Vector3.zero, radius + 0.45f, 0.05f, 20, Hull);
            d.Ring(0.12f, radius - 0.08f, radius + 0.18f, 20, Steel);
            d.Hemisphere(new Vector3(0f, 0.12f, 0f), radius, 16, 8, Glass);
            if (airlock)
                d.Box(new Vector3(0f, 0.4f, radius * 0.75f), new Vector3(0.7f, 0.7f, 0.55f), Steel);
            else
            {
                d.Box(new Vector3(-0.7f, 0.12f, 0.85f), new Vector3(0.7f, 0.12f, 0.4f), Trim);
                d.Box(new Vector3(0.75f, 0.12f, 0.4f), new Vector3(0.5f, 0.12f, 0.7f), Trim);
            }
        }

        static void Geothermal(Draft d)
        {
            d.Box(new Vector3(0f, 0.06f, 0f), new Vector3(3.6f, 0.12f, 2.4f), Hull);
            d.Box(new Vector3(-0.35f, 0.55f, 0f), new Vector3(2.0f, 0.9f, 1.4f), Steel);
            d.Cylinder(new Vector3(1.15f, 0.7f, 0f), 0.55f, 0.35f, 14, Vector3.right, Steel);
            d.Box(new Vector3(1.38f, 0.7f, 0.7f), new Vector3(0.08f, 0.9f, 0.16f), Trim);
            d.Box(new Vector3(1.38f, 1.15f, 0f), new Vector3(0.08f, 0.16f, 0.9f), Trim);
            d.Box(new Vector3(1.38f, 0.7f, -0.7f), new Vector3(0.08f, 0.9f, 0.16f), Trim);
            d.Cylinder(new Vector3(-1.15f, 0.85f, 0.55f), 0.1f, 0.8f, 6, Vector3.up, Trim);
        }

        static void Algae(Draft d)
        {
            d.Disc(new Vector3(-0.85f, 0.08f, 0.15f), 0.75f, 0f, 14, Glass);
            d.Disc(new Vector3(0.55f, 0.08f, -0.45f), 0.65f, 0f, 14, Glass);
            d.Disc(new Vector3(0.7f, 0.08f, 0.7f), 0.5f, 0f, 12, Glass);
            d.Ring(0.1f, 0.62f, 0.78f, 14, Steel);
            d.Box(new Vector3(-0.2f, 0.4f, -1.15f), new Vector3(1.3f, 0.55f, 0.7f), Hull);
            d.Beam(new Vector3(-0.2f, 0.55f, -0.75f), new Vector3(0.7f, 0.35f, 0.55f), 0.06f, Trim);
        }

        static void Biosphere(Draft d)
        {
            d.Box(new Vector3(0f, 0.05f, 0f), new Vector3(4.0f, 0.1f, 3.0f), Hull);
            d.Box(new Vector3(0f, 0.55f, 0f), new Vector3(1.6f, 0.9f, 1.3f), Steel);
            d.Box(new Vector3(0f, 0.55f, 0.68f), new Vector3(0.8f, 0.4f, 0.06f), Glass);
            d.Hemisphere(new Vector3(-1.25f, 0.1f, 0.15f), 0.75f, 12, 6, Glass);
            d.Hemisphere(new Vector3(1.25f, 0.1f, -0.2f), 0.6f, 12, 6, Glass);
            d.Box(new Vector3(-0.55f, 0.25f, 0.1f), new Vector3(0.7f, 0.3f, 0.35f), Hull);
            d.Box(new Vector3(0.7f, 0.25f, -0.05f), new Vector3(0.55f, 0.28f, 0.35f), Hull);
        }

        sealed class Draft
        {
            readonly List<Vector3> verts = new();
            readonly List<Vector3> normals = new();
            readonly List<int>[] tris = { new(), new(), new(), new(), new() };

            public void Box(Vector3 center, Vector3 size, int sub, float yaw = 0f)
            {
                int start = verts.Count;
                Vector3 h = size * 0.5f;
                Vector3[] faces = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                foreach (var n in faces)
                {
                    Vector3 tangent = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                    Vector3 bitangent = Vector3.Cross(n, tangent).normalized;
                    tangent = Vector3.Cross(bitangent, n).normalized;
                    Vector3 faceCenter = center + Vector3.Scale(n, h);
                    Vector3 tu = Vector3.Scale(tangent, h);
                    Vector3 tv = Vector3.Scale(bitangent, h);
                    int i = verts.Count;
                    verts.Add(faceCenter - tu - tv);
                    verts.Add(faceCenter + tu - tv);
                    verts.Add(faceCenter + tu + tv);
                    verts.Add(faceCenter - tu + tv);
                    for (int k = 0; k < 4; k++) normals.Add(n);
                    tris[sub].Add(i); tris[sub].Add(i + 2); tris[sub].Add(i + 1);
                    tris[sub].Add(i); tris[sub].Add(i + 3); tris[sub].Add(i + 2);
                }
                if (Mathf.Abs(yaw) < 0.001f) return;
                var rot = Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f);
                for (int i = start; i < verts.Count; i++)
                {
                    verts[i] = center + rot * (verts[i] - center);
                    normals[i] = rot * normals[i];
                }
            }

            public void Cylinder(Vector3 center, float radius, float height, int segments, Vector3 axis, int sub)
            {
                axis.Normalize();
                Quaternion align = Quaternion.FromToRotation(Vector3.up, axis);
                float half = height * 0.5f;
                int ring = verts.Count;
                for (int i = 0; i < segments; i++)
                {
                    float a = (Mathf.PI * 2f * i) / segments;
                    Vector3 radial = align * new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                    verts.Add(center + axis * half + radial);
                    verts.Add(center - axis * half + radial);
                    normals.Add(radial.normalized);
                    normals.Add(radial.normalized);
                }
                for (int i = 0; i < segments; i++)
                {
                    int i0 = ring + i * 2;
                    int i1 = ring + ((i + 1) % segments) * 2;
                    tris[sub].Add(i0); tris[sub].Add(i1); tris[sub].Add(i0 + 1);
                    tris[sub].Add(i1); tris[sub].Add(i1 + 1); tris[sub].Add(i0 + 1);
                }
                Cap(center + axis * half, axis, radius, segments, align, sub);
                Cap(center - axis * half, -axis, radius, segments, align, sub);
            }

            public void Beam(Vector3 a, Vector3 b, float thickness, int sub)
            {
                Vector3 dir = b - a;
                float len = dir.magnitude;
                if (len < 0.001f) return;
                int start = verts.Count;
                Box((a + b) * 0.5f, new Vector3(thickness, len, thickness), sub);
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, dir / len);
                Vector3 mid = (a + b) * 0.5f;
                for (int i = start; i < verts.Count; i++)
                {
                    verts[i] = mid + rot * (verts[i] - mid);
                    normals[i] = rot * normals[i];
                }
            }

            public void Disc(Vector3 center, float radius, float y, int segments, int sub)
            {
                Cap(center + Vector3.up * y, Vector3.up, radius, segments, Quaternion.identity, sub);
            }

            public void Ring(float y, float inner, float outer, int segments, int sub)
            {
                int ring = verts.Count;
                for (int i = 0; i < segments; i++)
                {
                    float a = (Mathf.PI * 2f * i) / segments;
                    float c = Mathf.Cos(a);
                    float s = Mathf.Sin(a);
                    verts.Add(new Vector3(c * outer, y, s * outer));
                    verts.Add(new Vector3(c * inner, y, s * inner));
                    normals.Add(Vector3.up);
                    normals.Add(Vector3.up);
                }
                for (int i = 0; i < segments; i++)
                {
                    int i0 = ring + i * 2;
                    int i1 = ring + ((i + 1) % segments) * 2;
                    tris[sub].Add(i0); tris[sub].Add(i0 + 1); tris[sub].Add(i1);
                    tris[sub].Add(i1); tris[sub].Add(i0 + 1); tris[sub].Add(i1 + 1);
                }
            }

            public void Slope(float yTop, float rTop, float yBottom, float rBottom, int segments, int sub)
            {
                int ring = verts.Count;
                for (int i = 0; i < segments; i++)
                {
                    float a = (Mathf.PI * 2f * i) / segments;
                    float c = Mathf.Cos(a);
                    float s = Mathf.Sin(a);
                    Vector3 top = new Vector3(c * rTop, yTop, s * rTop);
                    Vector3 bot = new Vector3(c * rBottom, yBottom, s * rBottom);
                    Vector3 tangent = new Vector3(-s, 0f, c);
                    Vector3 n = Vector3.Cross(tangent, bot - top).normalized;
                    if (n.y < 0f) n = -n;
                    verts.Add(top);
                    verts.Add(bot);
                    normals.Add(n);
                    normals.Add(n);
                }
                for (int i = 0; i < segments; i++)
                {
                    int i0 = ring + i * 2;
                    int i1 = ring + ((i + 1) % segments) * 2;
                    tris[sub].Add(i0); tris[sub].Add(i0 + 1); tris[sub].Add(i1);
                    tris[sub].Add(i1); tris[sub].Add(i0 + 1); tris[sub].Add(i1 + 1);
                }
            }

            public void Hemisphere(Vector3 center, float radius, int slices, int stacks, int sub)
            {
                int start = verts.Count;
                int cols = slices;
                for (int y = 0; y <= stacks; y++)
                {
                    float phi = (y / (float)stacks) * Mathf.PI * 0.5f;
                    float rr = Mathf.Cos(phi) * radius;
                    float yy = Mathf.Sin(phi) * radius;
                    for (int x = 0; x <= cols; x++)
                    {
                        float theta = (x / (float)cols) * Mathf.PI * 2f;
                        Vector3 p = center + new Vector3(Mathf.Cos(theta) * rr, yy, Mathf.Sin(theta) * rr);
                        verts.Add(p);
                        normals.Add((p - center).normalized);
                    }
                }
                int stride = cols + 1;
                for (int y = 0; y < stacks; y++)
                for (int x = 0; x < cols; x++)
                {
                    int i = start + y * stride + x;
                    tris[sub].Add(i);
                    tris[sub].Add(i + stride);
                    tris[sub].Add(i + 1);
                    tris[sub].Add(i + 1);
                    tris[sub].Add(i + stride);
                    tris[sub].Add(i + stride + 1);
                }
            }

            public void Arch(Vector3 center, float radius, float length, int segments, int sub)
            {
                int ring = verts.Count;
                float half = length * 0.5f;
                for (int i = 0; i <= segments; i++)
                {
                    float a = Mathf.PI * i / segments;
                    float c = Mathf.Cos(a);
                    float s = Mathf.Sin(a);
                    Vector3 radial = new Vector3(c * radius, s * radius, 0f);
                    Vector3 n = new Vector3(c, s, 0f);
                    verts.Add(center + radial + new Vector3(0f, 0f, half));
                    verts.Add(center + radial + new Vector3(0f, 0f, -half));
                    normals.Add(n);
                    normals.Add(n);
                }
                for (int i = 0; i < segments; i++)
                {
                    int i0 = ring + i * 2;
                    int i1 = ring + (i + 1) * 2;
                    tris[sub].Add(i0); tris[sub].Add(i1); tris[sub].Add(i0 + 1);
                    tris[sub].Add(i1); tris[sub].Add(i1 + 1); tris[sub].Add(i0 + 1);
                }
            }

            void Cap(Vector3 center, Vector3 normal, float radius, int segments, Quaternion align, int sub)
            {
                int hub = verts.Count;
                verts.Add(center);
                normals.Add(normal);
                int rim = verts.Count;
                for (int i = 0; i < segments; i++)
                {
                    float a = (Mathf.PI * 2f * i) / segments;
                    verts.Add(center + align * new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
                    normals.Add(normal);
                }
                bool flip = Vector3.Dot(normal, align * Vector3.up) < 0f;
                for (int i = 0; i < segments; i++)
                {
                    int a = rim + i;
                    int b = rim + (i + 1) % segments;
                    if (flip) { tris[sub].Add(hub); tris[sub].Add(a); tris[sub].Add(b); }
                    else { tris[sub].Add(hub); tris[sub].Add(b); tris[sub].Add(a); }
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(verts);
                mesh.SetNormals(normals);
                int count = 0;
                for (int i = 0; i < tris.Length; i++)
                    if (tris[i].Count > 0) count = i + 1;
                mesh.subMeshCount = Mathf.Max(1, count);
                for (int i = 0; i < mesh.subMeshCount; i++)
                    mesh.SetTriangles(tris[i], i);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
