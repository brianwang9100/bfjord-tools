using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Bwork.Authoring.Editor.Tests
{
    public sealed class TerrainMaterialBankTests
    {
        [Test]
        public void OldPaintRecipesRetainTemperatePalette()
        {
            var profile = JsonUtility.FromJson<TerrainPaintProfile>("{\"schemaVersion\":1,\"seed\":731}");
            Assert.That(profile.palette, Is.EqualTo("temperate"));
            TerrainPresentation.Validate(profile);
            Assert.Throws<ArgumentException>(() => TerrainPresentation.Validate(new TerrainPaintProfile { palette = "unknown" }));
        }

        [TestCase("ForestLitter")]
        [TestCase("CoastalShingle")]
        [TestCase("RockFace")]
        public void BankHasMatchedColorNormalAndLinearMask(string surface)
        {
            foreach (var suffix in new[] { "Color", "Normal", "Mask" })
            {
                string path = "Assets/BFjord/TerrainDetail/Surfaces/" + surface + "_" + suffix + ".png";
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(texture, Is.Not.Null, "Install material-bank sample assets first.");
                Assert.That(texture.width, Is.EqualTo(1024));
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.sRGBTexture, Is.EqualTo(suffix == "Color"));
                Assert.That(importer.mipmapEnabled, Is.True);
                Assert.That(importer.textureType, Is.EqualTo(suffix == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default));
            }
        }
    }
}
