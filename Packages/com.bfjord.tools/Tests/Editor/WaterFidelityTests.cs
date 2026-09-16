using System;
using System.IO;
using Bwork.Authoring.WaterSandbox;
using NUnit.Framework;
using UnityEngine;

namespace Bwork.Authoring.Editor.Tests
{
    public sealed class WaterFidelityTests
    {
        [TestCase(0,1)]
        [TestCase(0,-1)]
        [TestCase(1,0)]
        [TestCase(-1,0)]
        [TestCase(.6f,.8f)]
        public void StreakFeaturesTravelDownstreamWithoutAcrossDrift(float x,float z)
        {
            var flow=new Vector2(x,z)*1.7f;
            var p=new Vector2(137,49);
            foreach(float speed in new[]{.28f,1.05f,2.4f})
            {
                var before=WaterFidelity.CurrentCoordinates(p,flow,2,speed);
                var moving=WaterFidelity.CurrentCoordinates(p+flow*(.25f*speed),flow,2.25f,speed);
                Assert.That(Vector2.Distance(before,moving),Is.LessThan(.00001f));
                var stationary=WaterFidelity.CurrentCoordinates(p,flow,2.25f,speed);
                Assert.That(stationary.x,Is.EqualTo(before.x).Within(.00001f));
                Assert.That(stationary.y,Is.LessThan(before.y));
            }
        }

        [Test]
        public void CurrentMappingKeepsLongAxisDownstreamAtMetricScale()
        {
            foreach(var flow in new[]{Vector2.right,Vector2.up,new Vector2(-.6f,.8f)})
            {
                var across=new Vector2(flow.y,-flow.x);
                var origin=WaterFidelity.CurrentCoordinates(Vector2.zero,flow,0,1);
                var width=WaterFidelity.CurrentCoordinates(across*8,flow,0,1)-origin;
                var length=WaterFidelity.CurrentCoordinates(flow*24,flow,0,1)-origin;
                Assert.That(Vector2.Distance(width,Vector2.right),Is.LessThan(.000001f));
                Assert.That(Vector2.Distance(length,Vector2.up),Is.LessThan(.000001f));
            }
        }

        [Test]
        public void CrestSharpeningFitsExistingDisplacementEnvelope()
        {
            for(int shape=0;shape<=10;shape++)
                for(int phase=0;phase<1000;phase++)
                    Assert.That(Mathf.Abs(WaterFidelity.SharpenedWave(phase*Mathf.PI/500,shape/10f)),Is.LessThanOrEqualTo(1.000001f));
            Assert.That(WaterFidelity.SharpenedWave(Mathf.PI/2,1),Is.EqualTo(1).Within(.000001f));
        }

        [Test]
        public void InvalidProfileValuesAreRejectedBeforePublication()
        {
            foreach(string field in new[]{"riverCurrentStrength","riverStreakScale","riverTurbulence","oceanSurfaceStrength",
                "oceanWaveSharpness","oceanShoreFoam","oceanBreakerStrength","oceanBeachDepth","oceanSwashSpeed","oceanSwashDepthSpacing"})
            {
                var recipe=new ConnectedWaterRecipe();
                typeof(ConnectedWaterRecipe).GetField(field).SetValue(recipe,float.NaN);
                Assert.Throws<ArgumentException>(()=>WaterFidelity.ValidateAppearance(recipe),field);
                typeof(ConnectedWaterRecipe).GetField(field).SetValue(recipe,99f);
                Assert.Throws<ArgumentException>(()=>WaterFidelity.ValidateAppearance(recipe),field);
            }
        }

        [Test]
        public void SavedRecipesWithoutNewFieldsKeepLegacyDefaults()
        {
            var recipe=new ConnectedWaterRecipe();JsonUtility.FromJsonOverwrite("{\"schemaVersion\":1}",recipe);
            WaterFidelity.ValidateAppearance(recipe);
            Assert.That(recipe.riverCurrentStrength,Is.EqualTo(.22f));
            Assert.That(recipe.oceanSurfaceStrength,Is.EqualTo(.32f));
            Assert.That(recipe.oceanWaveSharpness,Is.Zero);
            Assert.That(recipe.oceanShoreFoam,Is.Zero);
            Assert.That(recipe.oceanBreakerStrength,Is.Zero);
            Assert.That(WaterFidelity.PatternOffset(recipe.waterPatternSeed),Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void AppearanceRefreshRetainsTopologyAndDisplacementEnvelope()
        {
            var installed=new ConnectedWaterRecipe();
            JsonUtility.FromJsonOverwrite(File.ReadAllText(ToolSandbox.SamplePath("connected-water.json")),installed);
            var requested=new ConnectedWaterRecipe();
            JsonUtility.FromJsonOverwrite(File.ReadAllText(ToolSandbox.SamplePath("water-fidelity-high.json")),requested);
            var nodes=installed.nodes;var reaches=installed.reaches;
            float oceanHeight=installed.oceanWaveHeight,riverHeight=installed.riverWaveHeight,cell=installed.cellSize;
            string topology=JsonUtility.ToJson(installed.nodes[0]);
            requested.oceanWaveHeight=1;requested.cellSize=4;requested.nodes=null;
            WaterFidelity.CopyAppearance(installed,requested);
            Assert.That(installed.nodes,Is.SameAs(nodes));Assert.That(installed.reaches,Is.SameAs(reaches));
            Assert.That(JsonUtility.ToJson(installed.nodes[0]),Is.EqualTo(topology));
            Assert.That(installed.oceanWaveHeight,Is.EqualTo(oceanHeight));
            Assert.That(installed.riverWaveHeight,Is.EqualTo(riverHeight));Assert.That(installed.cellSize,Is.EqualTo(cell));
            Assert.That(installed.flowSpeed,Is.EqualTo(requested.flowSpeed));
            Assert.That(installed.oceanWaveSharpness,Is.EqualTo(requested.oceanWaveSharpness));
            Assert.That(installed.oceanWaveDirection,Is.EqualTo(requested.oceanWaveDirection));
            Assert.DoesNotThrow(()=>new ConnectedWaterField(installed));
        }

        [Test]
        public void StrongerOceanRefreshChangesOnlyWeightsAndConservativeBounds()
        {
            var recipe=new ConnectedWaterRecipe();
            JsonUtility.FromJsonOverwrite(File.ReadAllText(ToolSandbox.SamplePath("connected-water.json")),recipe);
            var before=new ConnectedWaterField(recipe).BuildMesh();
            Mesh after=null;
            try
            {
                recipe.oceanWaveHeight=.95f;recipe.oceanBlendDistance=8;
                after=new ConnectedWaterField(recipe).BuildMesh();
                CollectionAssert.AreEqual(before.vertices,after.vertices);
                CollectionAssert.AreEqual(before.triangles,after.triangles);
                Assert.That(after.bounds.size.y-before.bounds.size.y,Is.EqualTo(1).Within(.0001f));
                Assert.That(after.colors,Is.Not.EqualTo(before.colors));
            }
            finally{UnityEngine.Object.DestroyImmediate(before);if(after!=null)UnityEngine.Object.DestroyImmediate(after);}
        }

        [Test]
        public void OceanDirectionCannotBeZeroOrNonfinite()
        {
            var r=new ConnectedWaterRecipe{ oceanWaveDirection=Vector2.zero };
            Assert.Throws<ArgumentException>(()=>WaterFidelity.ValidateAppearance(r));
            r.oceanWaveDirection=new Vector2(float.PositiveInfinity,1);
            Assert.Throws<ArgumentException>(()=>WaterFidelity.ValidateAppearance(r));
        }

        [Test]
        public void PatternSeedIsRepeatableAndDoesNotTouchGlobalRandom()
        {
            var state=UnityEngine.Random.state;
            var a=WaterFidelity.PatternOffset(50391);
            Assert.That(a,Is.EqualTo(WaterFidelity.PatternOffset(50391)));
            Assert.That(a,Is.Not.EqualTo(WaterFidelity.PatternOffset(50392)));
            Assert.That(a.x,Is.InRange(0f,1f));Assert.That(a.y,Is.InRange(0f,1f));
            Assert.That(UnityEngine.Random.state,Is.EqualTo(state));
        }

        [Test]
        public void BundledFlowPresetsHaveIncreasingSpeedAndAeration()
        {
            float lastSpeed=0,lastFoam=0,lastTurbulence=0;
            foreach(string name in new[]{"low","medium","high"})
            {
                var recipe=new ConnectedWaterRecipe();
                JsonUtility.FromJsonOverwrite(File.ReadAllText(ToolSandbox.SamplePath("water-fidelity-"+name+".json")),recipe);
                WaterFidelity.ValidateAppearance(recipe);
                Assert.That(recipe.flowSpeed,Is.GreaterThan(lastSpeed));
                Assert.That(recipe.riverCurrentStrength,Is.GreaterThan(lastFoam));
                Assert.That(recipe.riverTurbulence,Is.GreaterThan(lastTurbulence));
                Assert.That(recipe.oceanBreakerStrength,Is.GreaterThan(.8f));
                lastSpeed=recipe.flowSpeed;lastFoam=recipe.riverCurrentStrength;lastTurbulence=recipe.riverTurbulence;
            }
        }
    }
}
