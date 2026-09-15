using System;
using System.Linq;
using Bwork.WorldAuthoring;
using Bwork.FjordCoast.Junctions;
using NUnit.Framework;
using UnityEngine;

namespace Bwork.Authoring.Editor.Tests
{
    public sealed class RoadDetailPresentationTests
    {
        [Test] public void ImportedBoundsRetainRootAxisConversionAndMetreScale()
        {
            var model=new GameObject("Imported root mesh");var mesh=new Mesh();
            try
            {
                model.transform.localRotation=Quaternion.Euler(-90,0,0);
                var filter=model.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
                mesh.vertices=new[]{new Vector3(-.375f,-1.2f,0),new Vector3(.375f,1.2f,.94f)};
                var method=typeof(RoadDetailPresentation).GetMethod("ImportedBounds",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                Assert.That(method,Is.Not.Null);
                var bounds=(Bounds)method.Invoke(null,new object[]{filter});
                Assert.That(bounds.min.y,Is.EqualTo(0).Within(.00001));
                Assert.That(bounds.size.x,Is.EqualTo(.75f).Within(.00001));
                Assert.That(bounds.size.y,Is.EqualTo(.94f).Within(.00001));
                Assert.That(bounds.size.z,Is.EqualTo(2.4f).Within(.00001));
            }
            finally {UnityEngine.Object.DestroyImmediate(model);UnityEngine.Object.DestroyImmediate(mesh);}
        }

        [Test] public void ImportedMeshBakeRetainsRootAndChildTransformsWithoutChangingSource()
        {
            var model=new GameObject("Imported model");var child=new GameObject("Mesh node");
            var mesh=new Mesh();Mesh baked=null;
            try
            {
                model.transform.localRotation=Quaternion.Euler(-90,0,0);model.transform.localScale=Vector3.one*2;
                child.transform.SetParent(model.transform,false);child.transform.localPosition=Vector3.forward;
                var filter=child.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
                var original=new[]{Vector3.zero,Vector3.right,Vector3.up};mesh.vertices=original;
                mesh.normals=new[]{Vector3.forward,Vector3.forward,Vector3.forward};
                mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up};mesh.triangles=new[]{0,1,2};
                var method=typeof(RoadDetailPresentation).GetMethod("BakeImportedMesh",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                Assert.That(method,Is.Not.Null);baked=(Mesh)method.Invoke(null,new object[]{filter});
                var expected=new[]{new Vector3(0,2,0),new Vector3(2,2,0),new Vector3(0,2,-2)};
                for(int i=0;i<3;i++)
                {
                    Assert.That(Vector3.Distance(baked.vertices[i],expected[i]),Is.LessThan(.00001));
                    Assert.That(Vector3.Distance(baked.normals[i],Vector3.up),Is.LessThan(.00001));
                }
                CollectionAssert.AreEqual(original,mesh.vertices);CollectionAssert.AreEqual(mesh.triangles,baked.triangles);
                Assert.That(baked,Is.Not.SameAs(mesh));Assert.That(baked.tangents.Length,Is.EqualTo(3));
                Assert.That(baked.bounds.min.y,Is.EqualTo(2).Within(.00001));
            }
            finally {UnityEngine.Object.DestroyImmediate(model);UnityEngine.Object.DestroyImmediate(mesh);if(baked!=null)UnityEngine.Object.DestroyImmediate(baked);}
        }

        static SandboxRoadResult Straight(float length=180) => new SandboxRoadResult { Paths = new[] {
            new SandboxPath { Source=new SandboxRoad{Id="road",SourceId="test-road",Width=8,Surface="asphalt"},
                Points=Enumerable.Range(0,(int)length+1).Select(z=>new V3(0,0,z)).ToArray(),
                Stations=Enumerable.Range(0,(int)length+1).Select(z=>(float)z).ToArray(),FirstVisible=0,LastVisible=(int)length }
        }};

        [Test] public void DressingIsDeterministicOutsideTheVergeAndPreservesSourceGeometry()
        {
            var roads=Straight();var before=roads.Paths[0].Points.ToArray();
            var first=RoadDetailPresentation.Plan(roads,(x,z)=>10,(p,r)=>false);
            var second=RoadDetailPresentation.Plan(roads,(x,z)=>10,(p,r)=>false);
            Assert.That(first.Length,Is.GreaterThan(3));Assert.That(first.Length,Is.EqualTo(second.Length));
            Assert.That(first.Any(p=>p.assetId=="VergeWall_A"),Is.True);
            Assert.That(first.Any(p=>p.assetId=="Delineator_A"),Is.True);
            for(int i=0;i<first.Length;i++)
            {
                Assert.That(first[i].position,Is.EqualTo(second[i].position));
                Assert.That(first[i].position.y,Is.EqualTo(9.94f).Within(.00001));
                Assert.That(Math.Abs(first[i].position.x),Is.GreaterThan(6.8f+.3f));
            }
            CollectionAssert.AreEqual(before,roads.Paths[0].Points);
        }
        [Test] public void ProtectedSpansAndOtherRoadsOmitDressing()
        {
            var roads=Straight();roads.Paths[0].Source.Spans=new[]{new SandboxSpan{Id="bridge",Kind="bridge",StartMeters=35,EndMeters=115}};
            var plan=RoadDetailPresentation.Plan(roads,(x,z)=>0,(p,r)=>false);
            Assert.That(plan.All(p=>p.station<25.8f||p.station>124.2f),Is.True);
            var crossing=Straight().Paths[0];crossing.Points=crossing.Points.Select(p=>new V3(7.4f,0,p.Z)).ToArray();
            roads.Paths=roads.Paths.Concat(new[]{crossing}).ToArray();
            Assert.That(RoadDetailPresentation.Plan(roads,(x,z)=>0,(p,r)=>false).Where(p=>p.sourceId=="test-road"&&p.assetId=="VergeWall_A").All(p=>p.position.x>8),Is.True);
        }
        [Test] public void MissingGroundWaterAndUnevenSupportAreRejected()
        {
            var roads=Straight();
            Assert.That(RoadDetailPresentation.Plan(roads,(x,z)=>float.NaN,(p,r)=>false),Is.Empty);
            Assert.That(RoadDetailPresentation.Plan(roads,(x,z)=>0,(p,r)=>true),Is.Empty);
            var plan=RoadDetailPresentation.Plan(roads,(x,z)=>x>7.1f?1:0,(p,r)=>false);
            Assert.That(plan.Any(p=>p.assetId=="VergeWall_A"),Is.False,"A center sample alone must not admit a wall across a support step.");
        }
        [Test] public void BoundedCountAndSupportPlaneAreRetained()
        {
            var roads=Straight(20000);
            var plan=RoadDetailPresentation.Plan(roads,(x,z)=>z*.12f+x*.08f,(p,r)=>false);
            Assert.That(plan.Length,Is.LessThanOrEqualTo(RoadDetailPresentation.MaximumPlacements));
            var wall=plan.First(p=>p.assetId=="VergeWall_A");
            var forward=wall.rotation*Vector3.forward;
            Assert.That(forward.y/forward.z,Is.EqualTo(.12f).Within(.001));
        }

        [Test] public void ExactWaterTrianglesPreserveDryLandInsideTheirCombinedBounds()
        {
            var root=new GameObject("Exclusion test");
            try
            {
                var index=SpatialExclusions.Create(root.transform);
                // Exercise the same triangle index without adding AssetDatabase/configuration side
                // effects to a geometry unit test. Production mesh admission remains unchanged.
                var triangle=typeof(SpatialExclusions).GetNestedType("Triangle",System.Reflection.BindingFlags.NonPublic);
                var add=typeof(SpatialExclusions).GetMethod("Add",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new[]{triangle},null);
                foreach(float center in new[]{-10f,10f})
                {
                    var a=new Vector3(center-2,0,-2);var b=new Vector3(center+2,0,-2);var c=new Vector3(center+2,0,2);var d=new Vector3(center-2,0,2);
                    foreach(var corners in new[]{new object[]{a,b,c},new object[]{a,c,d}})
                        add.Invoke(index,new[]{Activator.CreateInstance(triangle,corners)});
                }
                Assert.That(index.Intersects(Vector3.zero,.24f),Is.False,"Dry ground within the combined renderer bounds must remain eligible.");
                Assert.That(index.Intersects(new Vector3(-10,0,0),.24f),Is.True);
                Assert.That(index.Intersects(new Vector3(10,0,0),.24f),Is.True);
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test] public void ExcludingPriorRoadsPreservesDefaultGuardAndRejectsUnknownGroupNames()
        {
            var root=new GameObject("Exclusion test");var road=new GameObject("Roads");road.transform.SetParent(root.transform);road.AddComponent<MeshFilter>();
            try
            {
                Assert.Throws<InvalidOperationException>(()=>SpatialExclusions.Create(root.transform));
                Assert.That(SpatialExclusions.Create(root.transform,excludedGroups:new[]{"Roads"}).TriangleCount,Is.Zero);
                Assert.Throws<ArgumentException>(()=>SpatialExclusions.Create(root.transform,excludedGroups:new[]{"foreign"}));
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test] public void ReceiptSealRejectsForeignChildrenAndChangedTransformsBeforeDeletion()
        {
            var root=new GameObject("Roads");var owned=new GameObject("Owned node");owned.transform.SetParent(root.transform,false);
            try
            {
                string fingerprint=RoadHierarchyOwnership.Fingerprint(root.transform);
                RoadHierarchyOwnership.Validate(root.transform,fingerprint,Array.Empty<string>());
                owned.transform.localPosition=Vector3.up;
                Assert.Throws<System.IO.InvalidDataException>(()=>RoadHierarchyOwnership.Validate(root.transform,fingerprint,Array.Empty<string>()));
                owned.transform.localPosition=Vector3.zero;
                var foreign=new GameObject("User prop");foreign.transform.SetParent(root.transform,false);
                Assert.Throws<System.IO.InvalidDataException>(()=>RoadHierarchyOwnership.Validate(root.transform,fingerprint,Array.Empty<string>()));
                Assert.That(foreign,Is.Not.Null);
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
