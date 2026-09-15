using System;
using Bwork.FjordCoast.TerrainAuthoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Bwork.Authoring.Editor.Tests
{
    public sealed class TerrainFidelityTests
    {
        [Test]
        public void BankDepositsVaryCompositionWithoutLosingCoverage()
        {
            var profile = new TerrainPaintProfile();
            var fine = TerrainPresentation.Weights(5, 10, 0, .3f, 0, 1, profile);
            var stone = TerrainPresentation.Weights(5, 10, 0, .3f, 1, 1, profile);
            Assert.That(fine.y, Is.GreaterThan(stone.y));
            Assert.That(stone.z, Is.GreaterThan(fine.z));
            Assert.That(fine.y + fine.z, Is.GreaterThan(.95f));
            Assert.That(stone.y + stone.z, Is.GreaterThan(.95f));
        }

        [TestCase("eroded-ridge")]
        [TestCase("coastal-bluff")]
        public void DetailedStampPreservesProtectedCellsAndInput(string shape)
        {
            const int n = 65;
            var original = new float[n, n]; var mask = new float[n, n];
            for (int z = 0; z < n; z++) for (int x = 0; x < n; x++)
            { original[z, x] = .3f; mask[z, x] = x == 44 || z == 24 ? 0 : 1; }
            var metrics = new TerrainPatchMetrics { TerrainOriginY = -20, TerrainSizeX = 512,
                TerrainSizeY = 180, TerrainSizeZ = 512, HeightmapResolution = n };
            var output = FjordTerrainPatchEditor.Apply(original, metrics, mask, TerrainCommand.Example(shape));
            bool changed = false;
            for (int z = 0; z < n; z++) for (int x = 0; x < n; x++)
            {
                Assert.That(original[z, x], Is.EqualTo(.3f));
                Assert.That(output[z, x], Is.InRange(0f, 1f));
                if (mask[z, x] == 0) Assert.That(output[z, x], Is.EqualTo(original[z, x]));
                else changed |= output[z, x] != original[z, x];
            }
            Assert.That(changed, Is.True);
        }

        [Test]
        public void MaterialRollbackRestoresHeightBlendAndAssignedTemplate()
        {
            var data = new TerrainData { heightmapResolution = 33, size = new Vector3(32, 20, 32) };
            var owner = Terrain.CreateTerrainGameObject(data);
            var material = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
            try
            {
                var terrain = owner.GetComponent<Terrain>();
                terrain.materialTemplate = material;
                material.SetFloat("_HeightTransition", .27f);
                material.DisableKeyword("_TERRAIN_BLEND_HEIGHT");
                var snapshot = new TerrainPresentation.Snapshot(terrain);
                material.SetFloat("_HeightTransition", .05f);
                material.EnableKeyword("_TERRAIN_BLEND_HEIGHT");
                terrain.materialTemplate = null;
                snapshot.Restore(terrain);
                Assert.That(terrain.materialTemplate, Is.SameAs(material));
                Assert.That(material.GetFloat("_HeightTransition"), Is.EqualTo(.27f).Within(1e-6));
                Assert.That(material.IsKeywordEnabled("_TERRAIN_BLEND_HEIGHT"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(data);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void InstalledTerrainMasksUseLinearData()
        {
            foreach (string layer in new[] { "Meadow", "Soil", "Talus", "Outcrop" })
            {
                string path = "Assets/BFjord/TerrainDetail/" + layer + "_TerrainMask.png";
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(texture, Is.Not.Null, "Install the current sample asset catalog before this integration check.");
                Assert.That(texture.width, Is.EqualTo(512));
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.sRGBTexture, Is.False);
                Assert.That(importer.mipmapEnabled, Is.True);
                Assert.That(importer.alphaIsTransparency, Is.False);
            }
        }

        [Test]
        public void FidelityControlsRejectNonfiniteOrOutOfBoundsValues()
        {
            Assert.Throws<ArgumentException>(() => TerrainPresentation.Validate(new TerrainPaintProfile { heightTransition = 0 }));
            Assert.Throws<ArgumentException>(() => TerrainPresentation.Validate(new TerrainPaintProfile { bankBreakup = 1 }));
            Assert.Throws<ArgumentException>(() => TerrainPresentation.Validate(new TerrainPaintProfile { patchWarpMeters = float.NaN }));
        }
    }
}
