using System;
using System.Linq;
using Bwork.FjordCoast.Junctions;
using NUnit.Framework;
using UnityEngine;
namespace Bwork.Authoring.Editor.Tests
{
    public sealed class TunnelDetailTests
    {
        static StructureSpec Spec(bool east=false)=>new StructureSpec{Id="t",SourceId="original",Kind="tunnel",PortalStyle="masonry",Width=8,Clearance=5.5f,
            Controls=east?new[]{new V3(160,10,180),new V3(240,10,180)}:new[]{new V3(180,10,160),new V3(180,10,240)}};
        [Test] public void MasonryDimensionsAreRejectedRatherThanStretchingTheAsset()
        {var spec=Spec();spec.Width=9;Assert.Throws<ArgumentException>(()=>StructureGeometry.Build(spec,(x,z)=>10));}
        [Test] public void LiningUvSpanSurvivesNinetyDegreeRotation()
        {
            var a=StructureGeometry.Build(Spec(),(x,z)=>10).Parts.Single(p=>p.Name.EndsWith("arched lining"));
            var b=StructureGeometry.Build(Spec(true),(x,z)=>10).Parts.Single(p=>p.Name.EndsWith("arched lining"));
            Assert.That(a.UV.Max(v=>v.X)-a.UV.Min(v=>v.X),Is.GreaterThan(10));
            Assert.That(b.UV.Max(v=>v.X)-b.UV.Min(v=>v.X),Is.EqualTo(a.UV.Max(v=>v.X)-a.UV.Min(v=>v.X)).Within(.001));
            Assert.That(b.UV.Max(v=>v.Y),Is.EqualTo(a.UV.Max(v=>v.Y)).Within(.001));
        }
        [Test] public void ForeignChildChangesOwnedHierarchyFingerprint()
        {
            var root=new GameObject("Structures");
            try {var before=TunnelDetail.Fingerprint(root.transform);var child=new GameObject("Owner content");child.transform.SetParent(root.transform,false);Assert.That(TunnelDetail.Fingerprint(root.transform),Is.Not.EqualTo(before));}
            finally {UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test] public void PortalFrameFacesIntoBothEndsWithoutScaling()
        {
            var structure=StructureGeometry.Build(Spec(true),(x,z)=>10);
            TunnelDetail.Frame(structure,true,out var entry,out var er);TunnelDetail.Frame(structure,false,out var exit,out var xr);
            Assert.That(entry.x,Is.LessThan(160));Assert.That(exit.x,Is.GreaterThan(240));
            Assert.That(Vector3.Dot(er*Vector3.forward,Vector3.right),Is.GreaterThan(.99));
            Assert.That(Vector3.Dot(xr*Vector3.forward,Vector3.left),Is.GreaterThan(.99));
        }
    }
}
