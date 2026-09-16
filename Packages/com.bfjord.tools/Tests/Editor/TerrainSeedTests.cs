using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Bwork.Authoring.Editor.Tests
{
    public sealed class TerrainSeedTests
    {
        static readonly string[] Shapes = { "ridge", "basin", "mesa", "eroded-ridge", "coastal-bluff" };

        [TestCaseSource(nameof(Shapes))]
        public void IdenticalSeedReproducesStampAndAnotherSeedChangesIt(string shape)
        {
            var first = TerrainCommand.Example(shape, 51915).Operations[0].Stamp.Values;
            var same = TerrainCommand.Example(shape, 51915).Operations[0].Stamp.Values;
            var other = TerrainCommand.Example(shape, 51916).Operations[0].Stamp.Values;
            Assert.That(same, Is.EqualTo(first));
            Assert.That(other.SequenceEqual(first), Is.False);
        }

        [TestCaseSource(nameof(Shapes))]
        public void AllSeedExtremesRemainFiniteNormalizedAndKeepTheLegacyStampBorder(string shape)
        {
            var legacy = TerrainCommand.Example(shape, 0).Operations[0].Stamp.Values;
            foreach (int seed in new[] { int.MinValue, -1, 1, 51915, int.MaxValue })
            {
                var stamp = TerrainCommand.Example(shape, seed).Operations[0].Stamp;
                Assert.That(stamp.Width, Is.EqualTo(65));
                Assert.That(stamp.Height, Is.EqualTo(65));
                Assert.That(stamp.Values.All(value => float.IsFinite(value) && value >= 0 && value <= 1), Is.True);
                for (int i = 0; i < 65; i++)
                {
                    Assert.That(stamp.Values[i], Is.EqualTo(legacy[i]));
                    Assert.That(stamp.Values[64 * 65 + i], Is.EqualTo(legacy[64 * 65 + i]));
                    Assert.That(stamp.Values[i * 65], Is.EqualTo(legacy[i * 65]));
                    Assert.That(stamp.Values[i * 65 + 64], Is.EqualTo(legacy[i * 65 + 64]));
                }
            }
        }

        // Samples from the original unseeded formulas, at (14,19), (32,32), (40,44), (52,25).
        [TestCase("ridge", .03431962f, .61142918f, .26388260f, .04527900f)]
        [TestCase("basin", .42059595f, 0f, .24791416f, .41833102f)]
        [TestCase("mesa", .21029976f, 1f, .76021152f, .27596919f)]
        [TestCase("eroded-ridge", .02948742f, .60319079f, .20629909f, .04458411f)]
        [TestCase("coastal-bluff", .54696507f, .88517071f, .33691207f, .02085930f)]
        public void SeedZeroRetainsLegacyDefaultAndReliefSamples(string shape, float a, float b, float c, float d)
        {
            var recipe = TerrainCommand.Example(shape, 0);
            var values = recipe.Operations[0].Stamp.Values;
            Assert.That(values, Is.EqualTo(TerrainCommand.Example(shape).Operations[0].Stamp.Values));
            var actual = new[] { values[19 * 65 + 14], values[32 * 65 + 32], values[44 * 65 + 40], values[25 * 65 + 52] };
            Assert.That(actual, Is.EqualTo(new[] { a, b, c, d }).Within(.000002f));
            Assert.That(recipe.Operations[0].CenterWorldX, Is.EqualTo(356));
            Assert.That(recipe.Operations[0].CenterWorldZ, Is.EqualTo(170));
            Assert.That(recipe.Operations[0].RotationDegrees, Is.EqualTo(-22));
        }

        [Test]
        public void SeededRecipeDoesNotConsumeOrResetGlobalUnityRandomState()
        {
            var original = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(91);
                float expected = UnityEngine.Random.value;
                UnityEngine.Random.InitState(91);
                TerrainCommand.Example("eroded-ridge", -451);
                Assert.That(UnityEngine.Random.value, Is.EqualTo(expected));
            }
            finally { UnityEngine.Random.state = original; }
        }

        [Test]
        public void CustomRecipeSeedFailsBeforeAccessingSceneOrRecipeFile()
        {
            Assert.Throws<ArgumentException>(() => TerrainCommand.Run("prepare", "unused-custom-recipe.json", "ridge", 12));
            Assert.Throws<ArgumentException>(() => TerrainCommand.Run("paint", "", "ridge", 12));
        }
    }
}
