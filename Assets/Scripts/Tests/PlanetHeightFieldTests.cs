using GameDevTV.RTS.Environment;
using NUnit.Framework;
using UnityEngine;

namespace GameDevTV.RTS.Tests
{
    public class PlanetHeightFieldTests
    {
        Texture2D MakeRampHeightmap()
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.SetPixels(new[]
            {
                Color.black, Color.white,
                Color.black, Color.white
            });
            t.Apply(false, false);
            return t;
        }

        PlanetConfig MakeConfig(Texture2D hm, float amp = 10f, float flatR = 0f)
        {
            var c = ScriptableObject.CreateInstance<PlanetConfig>();
            c.MapWidth = 10;
            c.MapHeight = 10;
            c.Heightmap = hm;
            c.HeightAmplitude = amp;
            c.LandingFlatRadius = flatR;
            c.HeightMultiplier = 5f;
            c.NoiseScale = 20f;
            return c;
        }

        [Test]
        public void SampleWorldY_WithHeightmap_HigherOnBrightSide()
        {
            var hm = MakeRampHeightmap();
            var cfg = MakeConfig(hm, amp: 10f, flatR: 0f);
            var p = PlanetTerrainBuilder.MakeParams(cfg, cellSize: 1f, landingLocal: new Vector3(5f, 0f, 5f));

            float yLow = PlanetTerrainBuilder.SampleWorldY(0.25f, 5f, in p);
            float yHigh = PlanetTerrainBuilder.SampleWorldY(9.75f, 5f, in p);
            Assert.Greater(yHigh, yLow + 2f);
        }

        [Test]
        public void SampleWorldY_LandingFlatten_NearZeroAtCenter()
        {
            var hm = MakeRampHeightmap();
            var cfg = MakeConfig(hm, amp: 20f, flatR: 8f);
            var landing = new Vector3(5f, 0f, 5f);
            var p = PlanetTerrainBuilder.MakeParams(cfg, 1f, landing);
            float y = PlanetTerrainBuilder.SampleWorldY(landing.x, landing.z, in p);
            Assert.That(y, Is.EqualTo(0f).Within(0.05f));
        }

        [Test]
        public void SampleWorldY_NullHeightmap_FallsBackWithoutThrowing()
        {
            var cfg = MakeConfig(null, amp: 10f, flatR: 0f);
            var p = PlanetTerrainBuilder.MakeParams(cfg, 1f, new Vector3(5f, 0f, 5f));
            float y = PlanetTerrainBuilder.SampleWorldY(3f, 4f, in p);
            Assert.That(float.IsNaN(y), Is.False);
        }
    }
}
