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

        [Test] public void InvalidClusterOrNeighborDataFailsBeforePlanning()
        {
            var recipe = Recipe(); recipe.clusterRadius = float.NaN;
            Assert.Throws<ArgumentException>(() => FjordBulkScatter.Plan(recipe, p => 4, p => Vector3.up, (p, r) => false));
        }
    }
}
