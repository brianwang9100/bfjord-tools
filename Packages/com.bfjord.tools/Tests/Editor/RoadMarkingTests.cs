using System;
using System.Collections;
using UnityEngine.TestTools;
using UnityEditor.Rendering.Universal.ShaderGUI;
using System.Linq;
using System.IO;
using Bwork.WorldAuthoring;
using Newtonsoft.Json.Linq;
using UnityEditor;
using NUnit.Framework;
using UnityEngine;
namespace Bwork.Authoring.Editor.Tests
{
    public sealed class RoadMarkingTests
    {
        static RoadMarkings.Geometry Paint(string surface="asphalt",float width=8)
        {
            var vertices=new[]{new Vector3(-width/2,-.16f,0),new Vector3(width/2,.16f,0),new Vector3(-width/2,1.84f,20),new Vector3(width/2,2.16f,20)};
            var normal=new Vector3(-.04f,1,-.1f).normalized;
            return RoadMarkings.Clip(vertices,Enumerable.Repeat(normal,4).ToArray(),new[]{new Vector2(-4,0),new Vector2(4,0),new Vector2(-4,20),new Vector2(4,20)},new[]{0,2,1,1,2,3},width,surface);
        }
        [Test] public void PaintIsMetricAndFollowsTheRetainedTriangleSurface()
        {
            var paint=Paint();Assert.That(paint.vertices,Is.Not.Empty);
            var normal=new Vector3(-.04f,1,-.1f).normalized;
            foreach(var vertex in paint.vertices)
            {var contact=vertex-normal*RoadMarkings.Lift;Assert.That(contact.y,Is.EqualTo(.04f*contact.x+.1f*contact.z).Within(.00001f));Assert.That(contact.z,Is.InRange(.5999f,19.4001f));}
            foreach(int index in paint.triangles[0])Assert.That(Mathf.Abs(paint.uv[index].x),Is.InRange(.0799f,.2001f));
            foreach(int index in paint.triangles[1])Assert.That(Mathf.Abs(paint.uv[index].x),Is.InRange(3.6599f,3.7801f));
        }
        [Test] public void JunctionArmsStopBeforeTheirTrimmedMouths()
        {
            var roads=SandboxRoadAuthoring.Build(SandboxRoadExample.Recipe(false),(x,z)=>0);
            foreach(var path in roads.Paths)
            {
                var mesh=roads.Meshes.Single(m=>m.Id==path.Source.Id);var normals=RoadPresentation.FittedNormals(new[]{mesh})[0];
                var painted=RoadMarkings.Clip(mesh.Vertices.Select(v=>new Vector3(v.Position.X,v.Position.Y,v.Position.Z)).ToArray(),normals,
                    mesh.Vertices.Select(v=>new Vector2(v.Mask.X*8/mesh.Width,v.Mask.Y)).ToArray(),mesh.Triangles[0].Concat(mesh.Triangles[1]).ToArray(),mesh.Width,mesh.Surface);
                foreach(var coordinate in painted.uv)Assert.That(coordinate.y,Is.InRange(path.Stations[path.FirstVisible]+.599f,path.Stations[path.LastVisible]-.599f));
            }
            Assert.That(roads.Meshes.Any(m=>m.Id.StartsWith("junction-")),Is.True,"The fixture must actually contain a trimmed junction.");
        }
        [Test,Explicit("Root integration only: run on the saved assembled sandbox after roads and river edits.")]
        public void AssembledRefreshIsIdempotentAndPreservesTerrainAndBaseRoadAssets()
        {
            var terrain=ToolSandbox.RequireTerrain();var root=ToolSandbox.Root.Find("Roads");
            if(root==null)Assert.Ignore("Requires the assembled sandbox road fixture.");
            string receiptPath=ToolSandbox.Generated+"/road-edit.json";var original=JObject.Parse(File.ReadAllText(receiptPath));
            if(!JObject.Parse((string)original["recipeJson"])["roads"].Any(r=>(string)r["surface"]=="asphalt"))Assert.Ignore("Requires an asphalt arm.");
            bool enabled=(bool?)original["markings"]??false;
            var data=terrain.terrainData;var heights=data.GetHeights(0,0,data.heightmapResolution,data.heightmapResolution);var holes=data.GetHoles(0,0,data.holesResolution,data.holesResolution);
            var baseMeshes=root.Cast<Transform>().Where(t=>t.name!=RoadMarkings.Group).SelectMany(t=>t.GetComponentsInChildren<MeshFilter>(true)).ToDictionary(f=>f, f=>f.sharedMesh);
            try
            {
                RoadCommand.Run("remove-markings");RoadCommand.Run("refresh-markings");
                var group=root.Find(RoadMarkings.Group);Assert.That(group,Is.Not.Null);Assert.That(group.GetComponentsInChildren<Collider>(true),Is.Empty);
                var first=File.ReadAllText(receiptPath);var unchanged=JObject.FromObject(RoadCommand.Run("refresh-markings"));
                Assert.That((bool)unchanged["unchanged"],Is.True);Assert.That(File.ReadAllText(receiptPath),Is.EqualTo(first));
                var foreign=new GameObject("User road prop");foreign.transform.SetParent(root,false);
                try {Assert.Throws<InvalidDataException>(()=>RoadCommand.Run("refresh-markings"));Assert.That(foreign,Is.Not.Null);}
                finally {UnityEngine.Object.DestroyImmediate(foreign);}
                group.localPosition=Vector3.up;
                try {Assert.Throws<InvalidDataException>(()=>RoadCommand.Run("remove-markings"));}
                finally {group.localPosition=Vector3.zero;}
                RoadCommand.Run("remove-markings");Assert.That(root.Find(RoadMarkings.Group),Is.Null);
                foreach(var item in baseMeshes)Assert.That(item.Key.sharedMesh,Is.SameAs(item.Value));
                Assert.That(JToken.DeepEquals(JObject.Parse(File.ReadAllText(receiptPath))["change"],original["change"]),Is.True);
                CollectionAssert.AreEqual(heights,data.GetHeights(0,0,data.heightmapResolution,data.heightmapResolution));
                CollectionAssert.AreEqual(holes,data.GetHoles(0,0,data.holesResolution,data.holesResolution));
            }
            finally {RoadCommand.Run(enabled?"refresh-markings":"remove-markings");}
        }
        [TestCase("gravel")][TestCase("dirt")]
        public void NonAsphaltSurfacesHaveNoMarkings(string surface)=>Assert.That(Paint(surface).vertices,Is.Empty);
        [Test] public void SameGeometryProducesExactlyTheSameMarkings()
        {var first=Paint();var second=Paint();CollectionAssert.AreEqual(first.vertices,second.vertices);CollectionAssert.AreEqual(first.triangles[0],second.triangles[0]);CollectionAssert.AreEqual(first.triangles[1],second.triangles[1]);}
        [Test] public void LegacyGeneratedMeshStemsRequireExactRetainedRecipeNamesAndOrder()
        {
            var recipe=SandboxRoadExample.Recipe(false);
            var assets=Enumerable.Range(0,4).Select(i=>"Assets/Generated/roads-a669427ea92643ed988099221f90588b-"+i+".asset").ToArray();
            var names=RoadHierarchyOwnership.LegacyNames(recipe,assets);
            for(int i=0;i<3;i++)Assert.That(names[assets[i]],Is.EqualTo(recipe.Roads[i].Id));
            Assert.That(names[assets[3]],Is.EqualTo("junction-hill-junction"));
            string stem=Path.GetFileNameWithoutExtension(assets[0]);
            Assert.That(RoadHierarchyOwnership.LegacyMeshNameMatches(recipe.Roads[0].Id,stem,assets[0],names),Is.True);
            Assert.That(RoadHierarchyOwnership.LegacyMeshNameMatches("User renamed road",stem,assets[0],names),Is.False);
            Assert.That(RoadHierarchyOwnership.LegacyMeshNameMatches(recipe.Roads[1].Id,stem,assets[0],names),Is.False);
            Assert.That(RoadHierarchyOwnership.LegacyMeshNameMatches(recipe.Roads[0].Id,"User renamed mesh",assets[0],names),Is.False);
            Assert.That(RoadHierarchyOwnership.LegacyMeshNameMatches(recipe.Roads[0].Id,stem,assets[0],null),Is.False);
            (assets[0],assets[1])=(assets[1],assets[0]);
            Assert.Throws<InvalidDataException>(()=>RoadHierarchyOwnership.LegacyNames(recipe,assets));
        }
        [UnityTest] public IEnumerator NewPaintBytesSurviveDeferredUrpInitialization()
        {
            string directory="Assets/RoadPaintInitializationTest-"+Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets",Path.GetFileName(directory));
            Material material=null;
            try
            {
                for(int index=0;index<2;index++)
                {
                    string path=directory+"/paint-"+index+".asset";
                    material=RoadMarkings.CreatePaintMaterial(index);
                    AssetDatabase.CreateAsset(material,path);AssetDatabase.SaveAssetIfDirty(material);
                    byte[] before=File.ReadAllBytes(path);
                    // The same later validator which changed the published v1 material must be a no-op.
                    BaseShaderGUI.SetMaterialKeywords(material,LitGUI.SetMaterialKeywords);
                    EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
                    AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                    yield return null;
                    AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<Material>(path));
                    CollectionAssert.AreEqual(before,File.ReadAllBytes(path),"Paint preset "+index);
                }
            }
            finally{AssetDatabase.DeleteAsset(directory);if(material!=null&&!EditorUtility.IsPersistent(material))UnityEngine.Object.DestroyImmediate(material);}
        }
        [Test] public void LegacyUrpProofPreservesEveryAuthoringField()
        {
            const string original="Material:\n  stringTagMap: {}\n  disabledShaderPasses: []\n    - _BaseColor: {r: 1, g: 0.68, b: 0.025, a: 1}\n    - _Color: {r: 1, g: 1, b: 1, a: 1}\n    - _Smoothness: 0.22\n";
            string normalized=original.Replace("  stringTagMap: {}\n","  stringTagMap:\n    RenderType: Opaque\n")
                .Replace("  disabledShaderPasses: []\n","  disabledShaderPasses:\n  - MOTIONVECTORS\n")
                .Replace("_Color: {r: 1, g: 1, b: 1, a: 1}","_Color: {r: 1, g: 0.68, b: 0.025, a: 1}");
            Assert.That(RoadMarkings.BeforeKnownLitInitialization(normalized),Is.EqualTo(original));
            Assert.That(RoadMarkings.BeforeKnownLitInitialization(normalized.Replace("_Smoothness: 0.22","_Smoothness: 0.8")),Is.Not.EqualTo(original));
            Assert.That(RoadMarkings.BeforeKnownLitInitialization(normalized.Replace("_Color: {r: 1, g: 0.68","_Color: {r: 0, g: 0.68")),Is.Null);
            Assert.That(RoadMarkings.BeforeKnownLitInitialization(normalized.Replace("RenderType: Opaque","RenderType: Transparent")),Is.Null);
            Assert.That(RoadMarkings.BeforeKnownLitInitialization(normalized+"  foreign: 1\n"),Is.Not.EqualTo(original));
            string white=normalized.Replace("{r: 1, g: 0.68, b: 0.025, a: 1}","{r: 0.88, g: 0.89, b: 0.84, a: 1}")
                .Replace("_Color: {r: 0.88, g: 0.89, b: 0.84, a: 1}","_Color: {r: 0.88, g: 0.89, b: 0.8399999, a: 1}");
            Assert.That(RoadMarkings.BeforeKnownLitInitialization(white),Is.EqualTo(original.Replace("_BaseColor: {r: 1, g: 0.68, b: 0.025, a: 1}","_BaseColor: {r: 0.88, g: 0.89, b: 0.84, a: 1}")));
            Assert.That(RoadMarkings.BeforeKnownLitInitialization(white.Replace("b: 0.8399999","b: 0.8399998")),Is.Null);
        }
        [Test] public void NarrowRoadKeepsPaintWidthAndEdgeInsetInMetres()
        {var paint=Paint(width:4);foreach(int i in paint.triangles[1])Assert.That(Mathf.Abs(paint.uv[i].x),Is.InRange(1.6599f,1.7801f));}
    }
}
