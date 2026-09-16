using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Bwork.Authoring.Editor.Tests
{
    public sealed class TerrainAntiTilingTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void GeneratedSurfaceAndBasemapPassesCompileBeforeAssignment(bool heightBlend)
        {
            // An import-only check missed the HLSL reserved local name "point".
            // Prepare now synchronously compiles the actual surface/basemap paths.
            var shader = TerrainAntiTiling.Prepare(heightBlend);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
        }

        [Test]
        public void AppearanceControlsAreBoundedAndIndependentOfPaintWeights()
        {
            var original = new TerrainPaintProfile();
            var changed = new TerrainPaintProfile { appearanceSeed = int.MinValue, macroVariation = .3f,
                stochasticCellTiles = 5, macroScaleMeters = 80 };
            TerrainPresentation.Validate(changed);
            Assert.That(TerrainPresentation.Weights(37, 51, .2f, .63f, .4f, .1f, changed),
                Is.EqualTo(TerrainPresentation.Weights(37, 51, .2f, .63f, .4f, .1f, original)));
            changed.stochasticCellTiles = float.NaN;
            Assert.Throws<ArgumentException>(() => TerrainPresentation.Validate(changed));
            changed.stochasticCellTiles = 3;
            changed.macroVariation = .51f;
            Assert.Throws<ArgumentException>(() => TerrainPresentation.Validate(changed));
        }

        [Test]
        public void LegacyRecipeRetainsPaletteAndCanSelectExactNativeSampling()
        {
            var profile = JsonUtility.FromJson<TerrainPaintProfile>("{\"schemaVersion\":1,\"palette\":\"woodland\",\"antiTiling\":false}");
            TerrainPresentation.Validate(profile);
            Assert.That(profile.palette, Is.EqualTo("woodland"));
            Assert.That(profile.antiTiling, Is.False);
        }

        [Test]
        public void AdapterPreservesNativeGeometryAndReplacesOnlyAlignedSurfaceSamples()
        {
            var sources = TerrainAntiTiling.ReadVerifiedSources();
            var adapted = TerrainAntiTiling.BuildSources(sources, "Assets/TestTerrainShader", "test", "// sampling helper");
            string passes = adapted["TerrainLitPasses.hlsl"];
            foreach (string name in new[] { "ClipHoles(IN.uvMainAndLM.xy)", "TerrainInstancing(v.positionOS, v.normalOS, v.texcoord)",
                "HeightBasedSplatModify(splatControl, masks)", "SAMPLE_TEXTURE2D(_Control, sampler_Control, splatUV)" })
                Assert.That(passes, Does.Contain(name));
            foreach (int i in Enumerable.Range(0, 4))
            {
                Assert.That(passes, Does.Contain("BFjordTerrainColor(TEXTURE2D_ARGS(_Splat" + i));
                Assert.That(passes, Does.Contain("BFjordTerrainNormal(TEXTURE2D_ARGS(_Normal" + i));
                Assert.That(passes, Does.Contain("BFjordTerrainData(TEXTURE2D_ARGS(_Mask" + i));
                Assert.That(passes, Does.Not.Contain("SAMPLE_TEXTURE2D(_Splat" + i));
                Assert.That(passes, Does.Not.Contain("SAMPLE_TEXTURE2D(_Mask" + i));
            }
            Assert.That(adapted["TerrainLitDepthNormalsPass.hlsl"], Does.Contain("Assets/TestTerrainShader/TerrainLitPasses.hlsl"));
            Assert.That(adapted["TerrainLitBasemapGen.shader"], Does.Contain("BFjordTerrainData(TEXTURE2D_ARGS(_Mask0"));
            Assert.That(adapted["TerrainLit.shader"], Does.Contain("BFjord/Terrain/Stochastic Basemap/test"));
            Assert.That(adapted["TerrainLit.shader"], Does.Contain("Name \"ShadowCaster\""));
        }

        [Test]
        public void ChangedUrpDependencyFailsBeforeGeneratingAReplacement()
        {
            var sources = TerrainAntiTiling.ReadVerifiedSources();
            sources["TerrainLitPasses.hlsl"] += "\n// changed dependency\n";
            Assert.Throws<InvalidOperationException>(() => TerrainAntiTiling.ValidateSources(sources));
        }
    }
}
