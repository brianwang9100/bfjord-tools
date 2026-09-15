using System;
using System.Linq;
using Bwork.WorldAuthoring;
using Bwork.FjordCoast.Junctions;
using Bwork.FjordCoast.TerrainAuthoring;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace Bwork.Authoring.Editor.Tests
{
    public sealed class TerrainRoadPresentationTests
    {
        [TestCase(false, 1f)]
        [TestCase(false, 4f)]
        [TestCase(true, 1f)]
        [TestCase(true, 4f)]
        public void HillJunctionKeepsExactPathsWithoutSteepPavementFans(bool fourArms, float spacing)
        {
            var recipe = SandboxRoadExample.Recipe(fourArms); recipe.SampleSpacing = spacing;
            var result = SandboxRoadAuthoring.Build(recipe, SandboxRoadExample.Ground);
            Assert.That(result.MaximumSurfaceGradePercent, Is.LessThanOrEqualTo(30.002));
            foreach (var path in result.Paths) foreach (var point in path.Points)
            {
                bool covered = false;
                foreach (var mesh in result.Meshes) foreach (var indices in mesh.Triangles.Take(2))
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        var a = mesh.Vertices[indices[i]].Position; var b = mesh.Vertices[indices[i+1]].Position; var c = mesh.Vertices[indices[i+2]].Position;
                        double Area(V3 p, V3 q, V3 r) => ((double)q.X-p.X)*(r.Z-p.Z)-((double)q.Z-p.Z)*(r.X-p.X);
                        double area = Area(a,b,c);
                        Assert.That(area, Is.LessThan(-1e-8), "Every pavement face must retain upward projected orientation.");
                        double u = Area(point,b,c)/area, v = Area(a,point,c)/area, w = 1-u-v;
                        if (Math.Min(u, Math.Min(v,w)) < -1e-6) continue;
                        Assert.That(a.Y*u+b.Y*v+c.Y*w, Is.EqualTo(point.Y).Within(.003), "Fitted centerline must remain on the collision surface.");
                        covered = true;
                    }
                Assert.That(covered, Is.True, "No gap may open around a preserved centerpath.");
            }
        }

        [TestCase(1f, false)]
        [TestCase(0f, true)]
        public void PavementAdmissionRejectsSteepOrFoldedFaces(float rise, bool reverse)
        {
            Vertex At(float x,float y,float z) => new Vertex(new V3(x,y,z),new V3(0,1,0),new V2(x,z),new V2(0,0));
            var mesh = new SandboxMesh { Vertices = new[] { At(0,0,0), At(0,rise,1), At(1,0,0) },
                Triangles = new[] { reverse ? new[] { 0,2,1 } : new[] { 0,1,2 }, Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>() } };
            Assert.Throws<ArgumentException>(() => SandboxRoadAuthoring.ValidatePavement(new[] { mesh }));
        }

        [Test]
        public void DerivedPaintIsNormalizedAndSteepLandFavorsRock()
        {
            var profile = new TerrainPaintProfile();
            for (int slope = 0; slope <= 85; slope += 5)
            for (int height = -20; height <= 120; height += 20)
            {
                var weights = TerrainPresentation.Weights(slope,height,.2f,.6f,.3f,.7f,profile);
                Assert.That(weights.x+weights.y+weights.z+weights.w, Is.EqualTo(1).Within(1e-6));
                for (int i=0;i<4;i++) Assert.That(weights[i], Is.InRange(0f,1f));
            }
            Assert.That(TerrainPresentation.Weights(65,100,0,.5f,.5f,0,profile).w, Is.GreaterThan(.95f));
            Assert.That(TerrainPresentation.Weights(0,10,0,.3f,.5f,0,profile).x, Is.GreaterThan(.95f));
        }

        [Test]
        public void BankPaintAddsDepositedMaterialAndRejectsInvalidProfiles()
        {
            var profile = new TerrainPaintProfile();
            Vector4 dry = TerrainPresentation.Weights(5,10,0,.3f,.5f,0,profile);
            Vector4 bank = TerrainPresentation.Weights(5,10,0,.3f,.5f,1,profile);
            Assert.That(bank.z, Is.GreaterThan(dry.z));
            profile.patchScaleMeters = float.NaN;
            Assert.Throws<ArgumentException>(()=>TerrainPresentation.Validate(profile));
        }

        [Test]
        public void PaintRollbackRestoresPersistentLayerPropertiesAndAlphamaps()
        {
            string folder = "Assets/BfjordTerrainSnapshotTest-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            var data = new TerrainData { heightmapResolution = 33, alphamapResolution = 16, size = new Vector3(32, 20, 32) };
            GameObject owner = null;
            var replacement = new TerrainLayer { name = "Replacement", tileSize = new Vector2(29, 31), normalScale = .1f, smoothness = .8f };
            try
            {
                var texture = new Texture2D(2, 2);
                AssetDatabase.CreateAsset(texture, folder + "/original.asset");
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(texture, out string textureGuid, out long textureFileId), Is.True);
                var first = new TerrainLayer { name = "Original", diffuseTexture = texture, tileSize = new Vector2(3, 5),
                    tileOffset = new Vector2(7, 11), normalScale = .7f, smoothness = .15f };
                var second = new TerrainLayer { name = "Second", tileSize = new Vector2(13, 17) };
                // Main asset names must agree with their filenames: a forced import
                // can normalize the name independently of the rollback under test.
                string firstPath = folder + "/Original.terrainlayer";
                AssetDatabase.CreateAsset(first, firstPath);
                AssetDatabase.CreateAsset(second, folder + "/Second.terrainlayer");
                AssetDatabase.SaveAssets();
                data.terrainLayers = new[] { first, second };
                var map = new float[16, 16, 2];
                for (int z = 0; z < 16; z++) for (int x = 0; x < 16; x++)
                { map[z,x,0] = x / 15f; map[z,x,1] = 1-map[z,x,0]; }
                data.SetAlphamaps(0, 0, map);
                var expected = data.GetAlphamaps(0, 0, 16, 16);
                owner = Terrain.CreateTerrainGameObject(data);
                var terrain = owner.GetComponent<Terrain>();
                var snapshot = new TerrainPresentation.Snapshot(terrain);

                // Reproduce Paint's in-place persistent-asset mutation, then simulate
                // subsequent palette/map changes before the operation fails to save.
                EditorUtility.CopySerialized(replacement, first);
                EditorUtility.SetDirty(first);
                data.terrainLayers = new[] { second, first };
                for (int z = 0; z < 16; z++) for (int x = 0; x < 16; x++)
                { map[z,x,0] = 0; map[z,x,1] = 1; }
                data.SetAlphamaps(0, 0, map);
                snapshot.Restore(terrain);
                Assert.That(first.name, Is.EqualTo("Original"), "Rollback must restore the name before import can normalize it.");
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(firstPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                var restored = AssetDatabase.LoadAssetAtPath<TerrainLayer>(firstPath);
                Assert.That(data.terrainLayers[0], Is.SameAs(first));
                Assert.That(data.terrainLayers[1], Is.SameAs(second));
                Assert.That(restored.name, Is.EqualTo("Original"));
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(restored.diffuseTexture, out string restoredGuid, out long restoredFileId), Is.True);
                Assert.That(restoredGuid, Is.EqualTo(textureGuid));
                Assert.That(restoredFileId, Is.EqualTo(textureFileId));
                Assert.That(restored.tileSize, Is.EqualTo(new Vector2(3, 5)));
                Assert.That(restored.tileOffset, Is.EqualTo(new Vector2(7, 11)));
                Assert.That(restored.normalScale, Is.EqualTo(.7f).Within(1e-6));
                Assert.That(restored.smoothness, Is.EqualTo(.15f).Within(1e-6));
                var actual = data.GetAlphamaps(0, 0, 16, 16);
                for (int z = 0; z < 16; z++) for (int x = 0; x < 16; x++) for (int layer = 0; layer < 2; layer++)
                    Assert.That(actual[z,x,layer], Is.EqualTo(expected[z,x,layer]).Within(1e-6));
            }
            finally
            {
                if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(data);
                UnityEngine.Object.DestroyImmediate(replacement);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [TestCase("ridge")]
        [TestCase("basin")]
        [TestCase("mesa")]
        public void BuiltInStampsKeepProtectedRoadAndWaterCells(string shape)
        {
            const int n=65;
            var heights=new float[n,n]; var mask=new float[n,n];
            for(int z=0;z<n;z++)for(int x=0;x<n;x++){heights[z,x]=.3f;mask[z,x]=x==44||z==24?0:1;}
            var metrics=new TerrainPatchMetrics{TerrainOriginY=-20,TerrainSizeX=512,TerrainSizeY=180,TerrainSizeZ=512,HeightmapResolution=n};
            var output=FjordTerrainPatchEditor.Apply(heights,metrics,mask,TerrainCommand.Example(shape));
            bool changed=false;
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                if(mask[z,x]==0)Assert.That(output[z,x],Is.EqualTo(heights[z,x]));
                else changed |= output[z,x]!=heights[z,x];
            }
            Assert.That(changed,Is.True);
        }
    }
}
