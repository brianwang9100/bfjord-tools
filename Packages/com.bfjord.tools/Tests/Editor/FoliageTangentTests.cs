using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
namespace Bwork.Authoring.Editor.Tests
{
    public sealed class FoliageTangentTests
    {
        [TestCase(1f)] [TestCase(-1f)]
        public void CoordinateBakeRebuildsParallelImportedTangentsFromFinalGeometry(float mirror)
        {
            var mesh = new Mesh {
                vertices = new[] { Vector3.zero, Vector3.forward, Vector3.right },
                normals = new[] { Vector3.up, Vector3.up, Vector3.up },
                uv = new[] { Vector2.zero, Vector2.up, Vector2.right },
                tangents = new[] { new Vector4(0,1,0,1),new Vector4(0,1,0,1),new Vector4(0,1,0,1) },
                triangles = new[] { 0,1,2 }
            };
            try
            {
                var matrix=Matrix4x4.TRS(new Vector3(3,1,2),Quaternion.Euler(25,17,8),new Vector3(mirror*2,1,.75f));
                typeof(FoliagePresentation).GetMethod("BakeToRoot",BindingFlags.NonPublic|BindingFlags.Static)
                    .Invoke(null,new object[]{mesh,matrix,"regression"});
                Assert.That(mesh.tangents.Length,Is.EqualTo(3));
                for(int i=0;i<3;i++)
                {
                    var t=mesh.tangents[i];var v=new Vector3(t.x,t.y,t.z);
                    Assert.That(v.magnitude,Is.EqualTo(1).Within(.001f));
                    Assert.That(Vector3.Dot(v,mesh.normals[i]),Is.EqualTo(0).Within(.001f));
                    Assert.That(Mathf.Abs(t.w),Is.EqualTo(1));
                }
                var indices=mesh.triangles;var positions=mesh.vertices;
                Assert.That(Vector3.Dot(Vector3.Cross(positions[indices[1]]-positions[indices[0]],positions[indices[2]]-positions[indices[0]]),mesh.normals[0]),Is.GreaterThan(0));
            }
            finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}
