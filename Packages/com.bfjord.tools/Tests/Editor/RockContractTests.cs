using System;
using System.IO;
using System.Linq;
using Bwork.Authoring.Editor.Rocks;
using Bwork.Authoring.WaterSandbox;
using Bwork.FjordCoast.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Bwork.Authoring.Editor.Tests
{
    public sealed class RockContractTests
    {
        static RockManifest Manifest() => new RockManifest { schemaVersion = 1, license = "CC0-1.0",
            materials = new[] { new RockMaterialSource { id = "rock", baseColorPath = "Textures/color.jpg", normalPath = "Textures/normal.jpg", metallicSmoothnessPath = "Textures/mask.png" } },
            variants = new[] { new RockVariant { id = "stone", boundsSize = new[] { 2f, 1f, 2f }, colliderPath = "Models/collider.fbx", lods = new[] {
                new RockLOD { path = "Models/0.fbx", triangles = 2000, screenRelativeHeight = .5f },
                new RockLOD { path = "Models/1.fbx", triangles = 700, screenRelativeHeight = .2f },
                new RockLOD { path = "Models/2.fbx", triangles = 200, screenRelativeHeight = .06f } } } } };
        static RockRecipe Recipe() => new RockRecipe { id = "test-rocks", area = new RockArea { x = 0, z = 0, width = 100, depth = 100 },
            species = new[] { new RockSpecies { id = "stone", weight = 1, minimumScale = .5f, maximumScale = 1.5f } }, densityPerHectare = 60, maximumCount = 60 };

        [Test] public void BakedMaterialSourcesRemainExplicitAndLegacyDefaultsRemainValid()
        {
            var legacy = Manifest();
            Assert.That(legacy.variants[0].materialId, Is.EqualTo("rock"));
            Assert.DoesNotThrow(() => RockContract.ValidateManifest(legacy));
            var baked = new RockMaterialSource { id = "coastal_outcrop", baseColorPath = "Textures/coast.png", normalPath = "Textures/coast-normal.png", metallicSmoothnessPath = "Textures/mask.png" };
            legacy.materials = legacy.materials.Append(baked).ToArray();
            legacy.variants[0].materialId = baked.id;
            Assert.DoesNotThrow(() => RockContract.ValidateManifest(legacy));
            legacy.variants[0].materialId = "missing";
            Assert.Throws<InvalidDataException>(() => RockContract.ValidateManifest(legacy));
            legacy.variants[0].materialId = baked.id;
            legacy.materials = legacy.materials.Append(baked).ToArray();
            Assert.Throws<InvalidDataException>(() => RockContract.ValidateManifest(legacy));
        }

        [Test] public void MaterialProfileCannotChangePlacementStructure()
        {
            var r = Recipe(); var m = Manifest();
            var a = FjordBulkScatter.Plan(RockContract.PlannerRecipe(r, m), _ => 0, _ => Vector3.up, (_, __) => false);
            r.materialProfile = "wet";
            var b = FjordBulkScatter.Plan(RockContract.PlannerRecipe(r, m), _ => 0, _ => Vector3.up, (_, __) => false);
            Assert.That(a.placements.Length, Is.GreaterThan(0));
            Assert.That(b.placements.Select(p => (p.position, p.rotationDegrees, p.scale)), Is.EqualTo(a.placements.Select(p => (p.position, p.rotationDegrees, p.scale))));
        }
        [Test] public void SizeBiasChangesWeightsWithoutEscapingSpeciesScaleRange()
        {
            var r = Recipe(); r.sizeDistribution = "small-biased";
            var planner = RockContract.PlannerRecipe(r, Manifest());
            Assert.That(planner.species.Length, Is.EqualTo(3));
            Assert.That(planner.species[0].weight, Is.GreaterThan(planner.species[2].weight));
            Assert.That(planner.species.Min(s => s.minimumScale), Is.EqualTo(.5f));
            Assert.That(planner.species.Max(s => s.maximumScale), Is.EqualTo(1.5f));
            Assert.That(planner.species.All(s => s.prefabKey == "stone"), Is.True);
        }
        [Test] public void TraversalDuplicateJsonAndInvalidLodsFailAdmission()
        {
            Assert.Throws<InvalidDataException>(() => RockContract.FileAt("/tmp", "../escape.fbx"));
            Assert.Throws<Newtonsoft.Json.JsonReaderException>(() => RockContract.Parse<RockRecipe>("{\"id\":\"a\",\"id\":\"b\"}"));
            var m = Manifest(); m.variants[0].lods[1].triangles = 2100;
            Assert.Throws<InvalidDataException>(() => RockContract.ValidateManifest(m));
        }
        [Test] public void InvalidWaterAndFootprintParametersAreRejected()
        {
            var r = Recipe(); r.orientation = "flow";
            Assert.Throws<InvalidDataException>(() => RockContract.PlannerRecipe(r, Manifest()));
            r.waterMode = "shallow"; r.maximumWaterDepth = float.NaN;
            Assert.Throws<InvalidDataException>(() => RockContract.PlannerRecipe(r, Manifest()));
            r.maximumWaterDepth = .6f; r.species[0].maximumScale = 100;
            Assert.Throws<InvalidDataException>(() => RockContract.PlannerRecipe(r, Manifest()));
        }
        [Test] public void PlannerRespectsFullTiltEnvelopeAndExclusionFootprints()
        {
            var r = Recipe(); var m = Manifest();
            var planner = RockContract.PlannerRecipe(r, m);
            Assert.That(planner.species[0].radius, Is.GreaterThanOrEqualTo(m.variants[0].Size.magnitude * .5f));
            var plan = FjordBulkScatter.Plan(planner, _ => 0, _ => Vector3.up, (p, radius) => p.x - radius < 50);
            Assert.That(plan.placements.All(p => p.position.x - p.supportRadius >= 50), Is.True);
            Assert.That(plan.rejectedExclusion, Is.GreaterThan(0));
        }

        [Test] public void RiverRocksRejectLakeAndOceanShoreBandsEvenWhereShadingWeightsAreZero()
        {
            WaterReach Reach(string id, string from, string to, float start, float end) => new WaterReach
            {
                id = id, from = from, to = to, knots = new[] {
                    new RiverKnot { position = new Vector3(0, 0, start), handleIn = new Vector3(0, 0, start - 10),
                        handleOut = new Vector3(0, 0, Mathf.Lerp(start, end, 1f / 3)), width = 8 },
                    new RiverKnot { position = new Vector3(0, 0, end), handleIn = new Vector3(0, 0, Mathf.Lerp(start, end, 2f / 3)),
                        handleOut = new Vector3(0, 0, end + 10), width = 8 } }
            };
            var field = new ConnectedWaterField(new ConnectedWaterRecipe
            {
                nodes = new[] {
                    new WaterNode { id = "source", kind = "source", position = Vector3.zero, radius = new Vector2(4, 4) },
                    new WaterNode { id = "lake", kind = "lake", position = new Vector3(0, 0, 100), radius = new Vector2(20, 20) },
                    new WaterNode { id = "mouth", kind = "mouth", position = new Vector3(0, 0, 200), radius = new Vector2(4, 4) },
                    new WaterNode { id = "ocean", kind = "ocean", position = new Vector3(0, 0, 230), radius = new Vector2(50, 40) } },
                reaches = new[] { Reach("upper", "source", "lake", 0, 100), Reach("lower", "lake", "mouth", 100, 200) }
            });
            foreach (var point in new[] { new Vector2(21, 100), new Vector2(51, 230) })
            {
                var sample = field.SampleForBank(point, 14);
                Assert.That(sample.Lake + sample.Ocean, Is.Zero);
                Assert.That(field.IsNearLakeOrOcean(point, 14), Is.True);
            }
            foreach (var point in new[] { new Vector2(19.9f, 100), new Vector2(49.9f, 230) })
            {
                var sample = field.SampleForBank(point, 14);
                Assert.That(sample.Lake + sample.Ocean, Is.LessThan(.1f));
                Assert.That(field.IsNearLakeOrOcean(point, 0), Is.True);
            }
            Assert.That(field.IsNearLakeOrOcean(new Vector2(30, 100), 10), Is.True);
            Assert.That(field.IsNearLakeOrOcean(new Vector2(30.1f, 100), 10), Is.False);
            Assert.That(field.IsNearLakeOrOcean(new Vector2(7, 45), 14), Is.False);
            Assert.Throws<ArgumentException>(() => field.IsNearLakeOrOcean(Vector2.zero, float.NaN));
        }
    }
}
