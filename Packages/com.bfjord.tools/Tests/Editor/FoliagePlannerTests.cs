using System;
using System.Linq;
using Bwork.FjordCoast.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Bwork.Authoring.Editor.Tests
{
    public sealed class FoliagePlannerTests
    {
        static FjordBulkScatter.Recipe Recipe() => new FjordBulkScatter.Recipe
        {
            id = "cluster-test", area = new Rect(0, 0, 80, 80), seed = 412,
            maximumCount = 120, densityPerHectare = 120, minimumSpacing = 1,
            minimumHeight = -2, maximumHeight = 20, minimumSlopeDegrees = 0, maximumSlopeDegrees = 30,
            clusterCount = 4, clusterRadius = 18,
            species = new[] { new FjordBulkScatter.Species { id = "fern", prefabKey = "fern", weight = 1,
                radius = .4f, minimumScale = .8f, maximumScale = 1.2f, groundOffsetMeters = -.03f } }
        };

        [Test] public void ClusteredPlanningIsDeterministicAndKeepsFootprintsApart()
        {
            var recipe = Recipe();
            var first = FjordBulkScatter.Plan(recipe, p => 4, p => Vector3.up, (p, r) => false);
            var second = FjordBulkScatter.Plan(recipe, p => 4, p => Vector3.up, (p, r) => false);
            Assert.That(first.placements.Length, Is.GreaterThan(30));
            Assert.That(first.placements.Select(p => p.position), Is.EqualTo(second.placements.Select(p => p.position)));
            foreach (var p in first.placements)
            {
                Assert.That(p.position.y, Is.EqualTo(4 - .03f * p.scale).Within(.00001));
                Assert.That(recipe.area.Contains(new Vector2(p.position.x, p.position.z)), Is.True);
            }
            for (int i = 0; i < first.placements.Length; i++)
                for (int j = 0; j < i; j++)
                {
                    var a = first.placements[i]; var b = first.placements[j];
                    var delta = new Vector2(a.position.x - b.position.x, a.position.z - b.position.z);
                    Assert.That(delta.magnitude, Is.GreaterThanOrEqualTo(Mathf.Max(recipe.minimumSpacing, a.supportRadius + b.supportRadius) - .0001f));
                }
        }

        [Test] public void NeighborFootprintsExcludeNewInstancesAcrossBatches()
        {
            var recipe = Recipe(); recipe.clusterCount = 0;
            var species = new FjordBulkScatter.Species { id = "tree", prefabKey = "tree", radius = 15 };
            var neighbor = new FjordBulkScatter.Placement(species, new Vector3(40, 4, 40), 0, 1);
            var result = FjordBulkScatter.Plan(recipe, p => 4, p => Vector3.up, (p, r) => false, new[] { neighbor });
            foreach (var p in result.placements)
                Assert.That(new Vector2(p.position.x - 40, p.position.z - 40).magnitude, Is.GreaterThanOrEqualTo(15 + p.supportRadius));
            Assert.That(result.rejectedSpacing, Is.GreaterThan(0));
        }

        [Test] public void TerrainAndOwnedExclusionsRemainAuthoritativeForClusters()
        {
            var result = FjordBulkScatter.Plan(Recipe(), p => p.x < 20 ? (float?)null : 4,
                p => p.z < 20 ? Vector3.right : Vector3.up, (p, r) => p.x > 55 - r);
            Assert.That(result.placements.All(p => p.position.x >= 20 && p.position.x + p.supportRadius <= 55 && p.position.z >= 20), Is.True);
        }

        [Test] public void SurfaceAlignmentPreservesPlacementRandomStreamAndYawOnTheTangent()
        {
            var recipe = Recipe();
            var normal = new Vector3(.3f, 1, -.2f).normalized;
            var upright = FjordBulkScatter.Plan(recipe, p => 4, p => normal, (p, r) => false);
            recipe.species[0].alignToSurface = true;
            var aligned = FjordBulkScatter.Plan(recipe, p => 4, p => normal * 3, (p, r) => false);
            var repeated = FjordBulkScatter.Plan(recipe, p => 4, p => normal * 3, (p, r) => false);
            Assert.That(aligned.placements.Length, Is.GreaterThan(0));
            Assert.That(aligned.placements.Select(p => p.position), Is.EqualTo(upright.placements.Select(p => p.position)));
            Assert.That(aligned.placements.Select(p => p.scale), Is.EqualTo(upright.placements.Select(p => p.scale)));
            Assert.That(aligned.placements.Select(p => p.rotationDegrees), Is.EqualTo(upright.placements.Select(p => p.rotationDegrees)));
            Assert.That(aligned.placements.Select(p => p.rotation), Is.EqualTo(repeated.placements.Select(p => p.rotation)));
            foreach (var p in upright.placements)
                Assert.That(Vector3.Distance(p.rotation * Vector3.up, Vector3.up), Is.LessThan(.00001f));
            foreach (var p in aligned.placements)
            {
                Assert.That(Vector3.Distance(p.rotation * Vector3.up, normal), Is.LessThan(.00001f));
                var tangentForward = Quaternion.Inverse(Quaternion.FromToRotation(Vector3.up, normal)) * (p.rotation * Vector3.forward);
                var oldForward = Quaternion.Euler(0, p.rotationDegrees, 0) * Vector3.forward;
                Assert.That(Vector3.Distance(tangentForward, oldForward), Is.LessThan(.00001f));
            }
        }

        [Test] public void FlatSurfaceAlignmentHasTheLegacyRotation()
        {
            var recipe = Recipe();
            var legacy = FjordBulkScatter.Plan(recipe, p => 4, p => Vector3.up, (p, r) => false);
            recipe.species[0].alignToSurface = true;
            var aligned = FjordBulkScatter.Plan(recipe, p => 4, p => Vector3.up, (p, r) => false);
            Assert.That(aligned.placements.Select(p => p.rotation), Is.EqualTo(legacy.placements.Select(p => p.rotation)));
        }

        [Test] public void RecipeAlignmentSurvivesJsonAndLegacyDefaultsRemainUpright()
        {
            const string legacy = "{\"species\":[{\"id\":\"litter\",\"prefabKey\":\"bfjord:OakBirchLeafLitter_A\"}]}";
            var recipe = JsonUtility.FromJson<FoliageCommand.Recipe>(legacy);
            Assert.That(recipe.PlannerRecipe().species[0].alignToSurface, Is.False);
            recipe.species[0].alignToSurface = true;
            var replay = JsonUtility.FromJson<FoliageCommand.Recipe>(JsonUtility.ToJson(recipe));
            Assert.That(replay.PlannerRecipe().species[0].alignToSurface, Is.True);
        }

        [TestCase("bfjord:MatureOak_A")]
        [TestCase("bfjord:SilverBirch_B")]
        [TestCase("Assets/Bwork/ThirdParty/PolyHaven/Prefabs/pine_sapling_small_b.prefab")]
        public void CanopyAlignmentIsRejectedBeforePlanning(string key)
        {
            var recipe = new FoliageCommand.Recipe
            {
                species = new[] { new FoliageCommand.Species { id = "canopy", prefabKey = key, alignToSurface = true } }
            };
            Assert.Throws<ArgumentException>(() => recipe.PlannerRecipe());
            recipe.species[0].alignToSurface = false;
            Assert.DoesNotThrow(() => recipe.PlannerRecipe());
        }

        [Test] public void GroundSamplingRejectsTerrainHolesAndKeepsFarEdgesInBounds()
        {
            var data = new TerrainData { heightmapResolution = 33, size = new Vector3(32, 10, 32) };
            try
            {
                int n = data.holesResolution;
                var holes = new bool[n, n];
                for (int z = 0; z < n; z++) for (int x = 0; x < n; x++) holes[z, x] = true;
                holes[n / 2, n / 2] = false;
                data.SetHoles(0, 0, holes);
                var origin = new Vector3(10, 4, 20);
                Assert.That(FoliageCommand.SampleGround(data, origin, new Vector2(26.5f, 36.5f)), Is.Null);
                Assert.That(FoliageCommand.SampleGround(data, origin, new Vector2(10, 20)), Is.EqualTo(4));
                Assert.That(FoliageCommand.SampleGround(data, origin, new Vector2(42, 52)), Is.EqualTo(4));
                Assert.That(FoliageCommand.SampleGround(data, origin, new Vector2(42.01f, 52)), Is.Null);
                Assert.That(FoliageCommand.SampleGround(data, origin, new Vector2(float.NaN, 20)), Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(data); }
        }

        [Test] public void InvalidClusterOrNeighborDataFailsBeforePlanning()
        {
            var recipe = Recipe(); recipe.clusterRadius = float.NaN;
            Assert.Throws<ArgumentException>(() => FjordBulkScatter.Plan(recipe, p => 4, p => Vector3.up, (p, r) => false));
        }
    }
}
