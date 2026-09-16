using System;
using Bwork.FjordCoast.Editor;
using NUnit.Framework;
using UnityEngine;
namespace Bwork.Authoring.Editor.Tests
{
    public sealed class FoliageGroundingTests
    {
        [Test] public void WideRootCollarIsBuriedBelowTheDownhillFootprint()
        {
            var root=new FoliageGrounding.Profile(.8f,.04f,12);
            var fit=FoliageGrounding.Fit(root,new Vector3(10,5,10),1,r=>5-(10-r.xMin)*.7f);
            Assert.That(fit.HasValue,Is.True);
            Assert.That(fit.Value.y+root.bottom,Is.LessThan(5-.8f*.7f));
            Assert.That(fit.Value.x,Is.EqualTo(10));Assert.That(fit.Value.z,Is.EqualTo(10));
        }
        [Test] public void AlreadyBuriedRootsAreNeverRaised()
        {
            var root=new FoliageGrounding.Profile(.5f,-.45f,10);
            var fit=FoliageGrounding.Fit(root,new Vector3(10,5,10),1,r=>4.9f);
            Assert.That(fit.Value.y,Is.EqualTo(5));
        }
        [Test] public void UnsupportedCliffOrHoleRejectsInsteadOfSinkingTheWholeTree()
        {
            var root=new FoliageGrounding.Profile(1,0,10);
            Assert.That(FoliageGrounding.Fit(root,new Vector3(10,5,10),1,r=>1),Is.Null);
            Assert.That(FoliageGrounding.Fit(root,new Vector3(10,5,10),1,r=>null),Is.Null);
        }
        [Test] public void TerrainSupportIncludesCellCornersAndRejectsHoles()
        {
            var heights=new float[,]{{.3f,.3f,.3f},{.3f,0,.3f},{.3f,.3f,.3f}};
            var holes=new bool[,]{{true,true},{true,true}};
            var support=new FoliageGrounding.TerrainSupport(heights,holes,Vector3.zero,new Vector3(2,10,2));
            Assert.That(support.Minimum(new Rect(.8f,.8f,.4f,.4f)),Is.EqualTo(0));
            Assert.That(support.Minimum(new Rect(-.1f,.8f,.4f,.4f)),Is.Null);
            holes[0,0]=false;
            support=new FoliageGrounding.TerrainSupport(heights,holes,Vector3.zero,new Vector3(2,10,2));
            Assert.That(support.Minimum(new Rect(.8f,.8f,.4f,.4f)),Is.Null);
        }
        [Test] public void TinyRootsOnCoarsePlanarCellsUseOnlyTheirActualFootprint()
        {
            var coarse=new FoliageGrounding.TerrainSupport(new float[,]{{0,1},{0,1}},new bool[,]{{true}},Vector3.zero,new Vector3(2,1,2));
            var fine=new FoliageGrounding.TerrainSupport(new float[,]{{0,.5f,1},{0,.5f,1},{0,.5f,1}},new bool[,]{{true,true},{true,true}},Vector3.zero,new Vector3(2,1,2));
            var root=new FoliageGrounding.Profile(.1f,0,1);var position=new Vector3(1,.5f,1);
            var fit=FoliageGrounding.Fit(root,position,1,coarse.Minimum);
            Assert.That(fit.HasValue,Is.True,"A safe small root must not be rejected using a distant cell corner.");
            Assert.That(fit.Value.y,Is.EqualTo(.41f).Within(.000001f));
            Assert.That(FoliageGrounding.Fit(root,position,1,fine.Minimum).Value.y,Is.EqualTo(fit.Value.y).Within(.000001f));
        }
        [Test] public void ScaledRootsAndTranslatedTerrainKeepMetricContact()
        {
            var support=new FoliageGrounding.TerrainSupport(new float[,]{{0,1},{0,1}},new bool[,]{{true}},new Vector3(10,4,20),new Vector3(2,1,2));
            var fit=FoliageGrounding.Fit(new FoliageGrounding.Profile(.1f,.05f,1),new Vector3(11,4.5f,21),2,support.Minimum);
            Assert.That(fit.HasValue,Is.True);Assert.That(fit.Value.y,Is.EqualTo(4.26f).Within(.000001f));
        }
        [Test] public void ClippedDiagonalEdgesContributeWithoutOutsideCellMinima()
        {
            var support=new FoliageGrounding.TerrainSupport(new float[,]{{0,1},{1,0}},new bool[,]{{true}},Vector3.zero,Vector3.one);
            // The minimum of the first footprint is on a clipped cell diagonal,
            // not at a rectangle corner. The second never reaches either zero-height corner.
            Assert.That(support.Minimum(new Rect(.2f,.3f,.5f,.3f)),Is.EqualTo(0).Within(.000001f));
            Assert.That(support.Minimum(new Rect(.1f,.7f,.1f,.1f)),Is.EqualTo(.5f).Within(.000001f));
        }
        [TestCase(false)][TestCase(true)] public void TreeRootProfileDoesNotUseCanopyWidth(bool discardCpuReadability)
        {
            var tree=new GameObject("tree");var mesh=new Mesh();
            try
            {
                mesh.vertices=new[]{new Vector3(-.3f,-.2f,0),new Vector3(.3f,-.2f,0),new Vector3(-6,10,0),new Vector3(6,10,0)};
                mesh.triangles=new[]{0,2,1,1,2,3};
                if(discardCpuReadability)mesh.UploadMeshData(true);
                tree.AddComponent<MeshFilter>().sharedMesh=mesh;
                var root=FoliageGrounding.FromPrefab(tree);
                Assert.That(root.radius,Is.LessThan(.4f));Assert.That(root.height,Is.EqualTo(10.2f).Within(.001));
            }
            finally{UnityEngine.Object.DestroyImmediate(tree);UnityEngine.Object.DestroyImmediate(mesh);}
        }
        [Test] public void RootFitCannotMoveAPlacementPastItsExclusions()
        {
            var species=new FjordBulkScatter.Species{id="tree",prefabKey="tree",weight=1,radius=1,minimumScale=1,maximumScale=1};
            var recipe=new FjordBulkScatter.Recipe{id="grounding",seed=7,area=new Rect(0,0,10,10),maximumCount=1,densityPerHectare=1000,minimumSpacing=1,minimumHeight=-10,maximumHeight=10,maximumSlopeDegrees=40,species=new[]{species}};
            Assert.Throws<InvalidOperationException>(()=>FjordBulkScatter.Plan(recipe,p=>0,p=>Vector3.up,(p,r)=>false,null,(s,p,k)=>p+Vector3.right));
        }
    }
}
