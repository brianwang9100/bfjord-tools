using System;
using NUnit.Framework;
using UnityEngine;

namespace Bwork.Authoring.Editor.Tests
{
    public sealed class FoliageWindPreviewTests
    {
        [TearDown] public void RestoreLiveWind() => FoliagePresentation.WindFrame(-1);

        [Test] public void FixedCaptureUsesExplicitTimeAndLiveRestoresEngineTime()
        {
            FoliagePresentation.WindFrame(1.5f);
            Assert.That(Shader.GetGlobalVector("_BFjordWindPreview"), Is.EqualTo(new Vector4(1, 1.5f, 0, 0)));
            FoliagePresentation.WindFrame(-1);
            Assert.That(Shader.GetGlobalVector("_BFjordWindPreview"), Is.EqualTo(Vector4.zero));
        }

        [TestCase(-2)] [TestCase(61)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidCaptureTimeDoesNotReplaceCurrentWind(float seconds)
        {
            FoliagePresentation.WindFrame(.5f);
            Assert.Throws<ArgumentOutOfRangeException>(() => FoliagePresentation.WindFrame(seconds));
            Assert.That(Shader.GetGlobalVector("_BFjordWindPreview"), Is.EqualTo(new Vector4(1, .5f, 0, 0)));
        }
    }
}
