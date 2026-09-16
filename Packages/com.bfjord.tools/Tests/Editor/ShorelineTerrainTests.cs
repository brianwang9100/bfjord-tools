using System;
using Bwork.Authoring.WaterSandbox;
using NUnit.Framework;
using UnityEngine;

namespace Bwork.Authoring.Editor.Tests
{
    public sealed class ShorelineTerrainTests
    {
        readonly TerrainPaintProfile profile = new TerrainPaintProfile { palette = "shoreline" };

        [Test]
        public void DryAndWetSandShareOneMatchedScanAtItsPhysicalScale()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                material.SetTexture("_BaseMap", Texture2D.whiteTexture);
                material.SetTexture("_BumpMap", Texture2D.whiteTexture);
                var surfaces = TerrainMaterialBank.Resolve("shoreline", new[] { material,material,material,material },
                    new[] { Texture2D.whiteTexture,Texture2D.whiteTexture,Texture2D.whiteTexture,Texture2D.whiteTexture }, new[] { 1f,1f,1f,1f });
                Assert.That(surfaces.Length, Is.EqualTo(4));
                Assert.That(surfaces[1].color, Is.SameAs(surfaces[2].color));
                Assert.That(surfaces[1].normal, Is.SameAs(surfaces[2].normal));
                Assert.That(surfaces[1].mask, Is.SameAs(surfaces[2].mask));
                Assert.That(surfaces[1].tileMeters, Is.EqualTo(2f));
                Assert.That(surfaces[2].tileMeters, Is.EqualTo(2f));
                Assert.That(surfaces[1].color.width, Is.EqualTo(2048));
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }

        [Test]
        public void OceanRectangleUsesSignedDistanceIncludingCorners()
        {
            var ocean = new WaterNode { kind = "ocean", position = new Vector3(10, 3, 20), radius = new Vector2(5, 8) };
            Assert.That(TerrainPresentation.OceanSignedDistance(new Vector2(10,20), ocean), Is.EqualTo(-5));
            Assert.That(TerrainPresentation.OceanSignedDistance(new Vector2(18,32), ocean), Is.EqualTo(5).Within(.001f));
            Assert.That(TerrainPresentation.OceanSignedDistance(Vector2.zero, null), Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void SandRequiresBothOceanProximityAndLowElevation()
        {
            foreach (var input in new[] { new Vector2(80,0), new Vector2(0,20), new Vector2(-80,0), new Vector2(0,-20) })
            {
                var w = TerrainPresentation.ShorelineWeights(0, input.y, input.x, .5f, .5f, profile);
                Assert.That(w.x, Is.EqualTo(1).Within(.0001f));
                Assert.That(w.y+w.z, Is.Zero);
            }
            var missing = TerrainPresentation.ShorelineWeights(0, 0, float.PositiveInfinity, .5f, .5f, profile);
            Assert.That(missing.x, Is.EqualTo(1));
        }

        [Test]
        public void LowShoreIsWetUpperBeachDryAndCliffsRock()
        {
            var wet = TerrainPresentation.ShorelineWeights(0, .1f, .1f, .5f, .5f, profile);
            var dry = TerrainPresentation.ShorelineWeights(0, 1.8f, 8, .5f, .5f, profile);
            var cliff = TerrainPresentation.ShorelineWeights(70, 0, 0, .5f, .5f, profile);
            Assert.That(wet.z, Is.GreaterThan(.95f));
            Assert.That(dry.y, Is.GreaterThan(.5f));
            Assert.That(dry.z, Is.Zero);
            Assert.That(cliff.w, Is.GreaterThan(.99f));
            for (int d = -40; d <= 40; d++)
            {
                var w = TerrainPresentation.ShorelineWeights(13, .5f, d, .3f, .7f, profile);
                Assert.That(w.x+w.y+w.z+w.w, Is.EqualTo(1).Within(.0001f));
                for (int i = 0; i < 4; i++) Assert.That(w[i], Is.InRange(0,1));
            }
        }

        [Test]
        public void DampMaterialStaysNearWaterlineLeavingDryPaleUpperBeach()
        {
            var low = TerrainPresentation.ShorelineWeights(0,.1f,.1f,.5f,.5f,profile);
            var upper = TerrainPresentation.ShorelineWeights(0,.8f,.1f,.5f,.5f,profile);
            var inland = TerrainPresentation.ShorelineWeights(0,.1f,4,.5f,.5f,profile);
            Assert.That(low.z, Is.GreaterThan(.95f));
            Assert.That(upper.z, Is.Zero);
            Assert.That(upper.y, Is.EqualTo(1));
            Assert.That(inland.z, Is.Zero);
            Assert.That(inland.y, Is.EqualTo(1));
        }

        [Test]
        public void VisualOceanApronWetsOnlyLowBeachWithinItsOuterReach()
        {
            // The showcase's actual sea/terrain contact is 11.4 m outside the
            // recipe rectangle, inside its enabled 18 m visual swash apron.
            var contact = TerrainPresentation.ShorelineWeights(0,.1f,11.4f,.5f,.5f,profile,18);
            var upper = TerrainPresentation.ShorelineWeights(0,.8f,11.4f,.5f,.5f,profile,18);
            var beyond = TerrainPresentation.ShorelineWeights(0,.1f,22,.5f,.5f,profile,18);
            var interior = TerrainPresentation.ShorelineWeights(0,.1f,-11.4f,.5f,.5f,profile,18);
            Assert.That(contact.z, Is.GreaterThan(.95f));
            Assert.That(upper.z, Is.Zero);
            Assert.That(upper.y, Is.EqualTo(1));
            Assert.That(beyond.z, Is.Zero);
            Assert.That(interior.z, Is.Zero, "An outward visual apron must not expand the inward gate.");
            for (int d=-30; d<=30; d++)
                Assert.That(TerrainPresentation.ShorelineWeights(0,.1f,d,.5f,.5f,profile,0),
                    Is.EqualTo(TerrainPresentation.ShorelineWeights(0,.1f,d,.5f,.5f,profile)));
        }

        [Test]
        public void BeachInteriorStaysSandBeforeItsSoftOuterTransition()
        {
            var interior = TerrainPresentation.ShorelineWeights(0,3,12,.5f,.5f,profile);
            Assert.That(interior.x, Is.Zero, "Multiplying fades from zero leaked leaf litter across the beach interior.");
            Assert.That(interior.y, Is.EqualTo(1));
            var transition = TerrainPresentation.ShorelineWeights(0,4.5f,20,.5f,.5f,profile);
            Assert.That(transition.y, Is.InRange(.01f,.99f));
            Assert.That(transition.x+transition.y+transition.z+transition.w, Is.EqualTo(1).Within(.0001f));
        }

        [Test]
        public void ShorelineBoundsAreValidatedWithoutChangingLegacyDefaults()
        {
            TerrainPresentation.Validate(profile);
            Assert.That(new TerrainPaintProfile().palette, Is.EqualTo("temperate"));
            Assert.Throws<ArgumentException>(() => TerrainPresentation.Validate(new TerrainPaintProfile { shorelineWidthMeters = float.NaN }));
            Assert.Throws<ArgumentException>(() => TerrainPresentation.Validate(new TerrainPaintProfile { shorelineWetHeightMeters = 7 }));
        }
    }
}
