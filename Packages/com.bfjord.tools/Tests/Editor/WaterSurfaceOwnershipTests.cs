using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Bwork.Authoring.Editor.Tests
{
    public sealed class WaterSurfaceOwnershipTests
    {
        GameObject root,surface,foreign;
        Mesh mesh;
        Material material;
        MeshFilter filter;
        MeshRenderer renderer;
        [SetUp] public void Setup()
        {
            root=new GameObject("Water ownership test");
            surface=new GameObject("Surface",typeof(MeshFilter),typeof(MeshRenderer));surface.transform.SetParent(root.transform);
            mesh=new Mesh{name="Owned water test mesh"};mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.forward};mesh.triangles=new[]{0,2,1};
            material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            filter=surface.GetComponent<MeshFilter>();filter.sharedMesh=mesh;
            renderer=surface.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
        }
        [TearDown] public void Cleanup()
        {
            if(foreign!=null)Object.DestroyImmediate(foreign);
            if(root!=null)Object.DestroyImmediate(root);
            if(mesh!=null)Object.DestroyImmediate(mesh);
            if(material!=null)Object.DestroyImmediate(material);
        }
        [Test] public void SoleOwnedSurfaceCanRefresh()
        {Assert.DoesNotThrow(()=>ConnectedWaterCommand.ValidateSurfaceReferences(root.transform,filter,renderer));}
        [Test] public void DuplicateSurfaceRejectsWithoutChangingReferences()
        {
            foreign=Object.Instantiate(surface,root.transform);
            Assert.Throws<InvalidDataException>(()=>ConnectedWaterCommand.ValidateSurfaceReferences(root.transform,filter,renderer));
            Assert.That(filter.sharedMesh,Is.SameAs(mesh));Assert.That(renderer.sharedMaterial,Is.SameAs(material));
            Assert.That(foreign.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(mesh));
        }
        [TestCase("mesh")] [TestCase("material")] [TestCase("collider")]
        public void InactiveExternalReferenceRejectsBeforeRetiringAssets(string kind)
        {
            foreign=new GameObject("Unrelated retained object");foreign.SetActive(false);
            if(kind=="mesh")foreign.AddComponent<MeshFilter>().sharedMesh=mesh;
            else if(kind=="collider")foreign.AddComponent<MeshCollider>().sharedMesh=mesh;
            else foreign.AddComponent<MeshRenderer>().sharedMaterial=material;
            Assert.Throws<InvalidDataException>(()=>ConnectedWaterCommand.ValidateSurfaceReferences(root.transform,filter,renderer));
            Assert.That(filter.sharedMesh,Is.SameAs(mesh));Assert.That(renderer.sharedMaterial,Is.SameAs(material));
        }
    }
}
