using System;
using System.IO;
using Bwork.Authoring.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Bwork.Authoring.Tests
{
    public sealed class WaterfallContractTests
    {
        [Test] public void OwnershipSealDetectsInjectedObjectsAndEditedTransforms()
        {
            var root=new GameObject("Waterfall Sample");
            try
            {
                var surface=new GameObject("Falling sheet",typeof(MeshFilter),typeof(MeshRenderer));surface.transform.SetParent(root.transform,false);
                string baseline=WaterfallCommand.HierarchyFingerprint(root.transform);
                var foreign=new GameObject("Foreign child");foreign.transform.SetParent(surface.transform,false);
                Assert.That(WaterfallCommand.HierarchyFingerprint(root.transform),Is.Not.EqualTo(baseline));
                Object.DestroyImmediate(foreign);
                Assert.That(WaterfallCommand.HierarchyFingerprint(root.transform),Is.EqualTo(baseline));
                surface.transform.localPosition=Vector3.up;
                Assert.That(WaterfallCommand.HierarchyFingerprint(root.transform),Is.Not.EqualTo(baseline));
                surface.transform.localPosition=Vector3.zero;
                surface.AddComponent<BoxCollider>();
                Assert.Throws<InvalidDataException>(()=>WaterfallCommand.HierarchyFingerprint(root.transform));
            }
            finally{Object.DestroyImmediate(root);}
        }

        [Test] public void FirstPublicationRollbackRemovesAnOrphanReceiptMeta()
        {
            string folder="__WaterfallReceiptTest-"+Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets",folder);string directory="Assets/"+folder,path=directory+"/receipt.json";
            try
            {
                File.WriteAllText(path,"{}");AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                Assert.That(File.Exists(path+".meta"),Is.True);
                File.Delete(path);
                var helper=typeof(WaterfallCommand).Assembly.GetType("Bwork.Authoring.Editor.WaterGeneration");
                helper.GetMethod("RestoreReceipt").Invoke(null,new object[]{path,null});
                Assert.That(File.Exists(path),Is.False);Assert.That(File.Exists(path+".meta"),Is.False);
            }
            finally{AssetDatabase.DeleteAsset(directory);}
        }

        [Test] public void InvalidMotionAndGeometryAreRejectedBeforeBuilding()
        {
            Assert.Throws<InvalidDataException>(()=>WaterfallCommand.Validate(new WaterfallRecipe{fallSpeed=float.NaN}));
            Assert.Throws<InvalidDataException>(()=>WaterfallCommand.Validate(new WaterfallRecipe{plungeWaveHeight=float.PositiveInfinity}));
            Assert.Throws<InvalidDataException>(()=>WaterfallCommand.Validate(new WaterfallRecipe{toe=new Vector3(90,25,418)}));
            Assert.Throws<InvalidDataException>(()=>WaterfallCommand.Validate(new WaterfallRecipe{fallSegments=int.MaxValue}));
        }

        [Test] public void WaterFlowsFromLevelFeederToAuthoredLanding()
        {
            var recipe=new WaterfallRecipe();var mesh=WaterfallCommand.BuildSheet(recipe);
            try
            {
                var vertices=mesh.vertices;var uv=mesh.uv2;int stride=recipe.acrossSegments+1,centre=recipe.acrossSegments/2;
                Assert.That(vertices[centre].y,Is.EqualTo(recipe.lip.y).Within(.0001));
                Assert.That(vertices[centre].z,Is.LessThan(recipe.lip.z));
                Assert.That(Vector3.Distance(vertices[vertices.Length-stride+centre],recipe.toe),Is.LessThan(.0001));
                for(int row=1;row<=recipe.fallSegments;row++)
                {
                    Assert.That(uv[row*stride+centre].y,Is.GreaterThan(uv[(row-1)*stride+centre].y));
                    Assert.That(vertices[row*stride+centre].y,Is.LessThanOrEqualTo(vertices[(row-1)*stride+centre].y));
                }
            }
            finally{Object.DestroyImmediate(mesh);}
        }

        [Test] public void BothMeshesReserveTheEntireReceivingWaveEnvelope()
        {
            var recipe=new WaterfallRecipe{plungeWaveHeight=1};
            foreach(var mesh in new[]{WaterfallCommand.BuildSheet(recipe),WaterfallCommand.BuildPlunge(recipe)})
            {
                try
                {
                    var tolerantBounds=mesh.bounds;tolerantBounds.Expand(.0002f);
                    foreach(var vertex in mesh.vertices)
                    {
                        Assert.That(tolerantBounds.Contains(vertex+Vector3.up*recipe.plungeWaveHeight),Is.True);
                        Assert.That(tolerantBounds.Contains(vertex-Vector3.up*recipe.plungeWaveHeight),Is.True);
                    }
                    var positions=mesh.vertices;var indices=mesh.triangles;
                    for(int i=0;i<indices.Length;i+=3)
                        Assert.That(Vector3.Cross(positions[indices[i+1]]-positions[indices[i]],positions[indices[i+2]]-positions[indices[i]]).sqrMagnitude,Is.GreaterThan(1e-10));
                }
                finally{Object.DestroyImmediate(mesh);}
            }
        }
    }
}
