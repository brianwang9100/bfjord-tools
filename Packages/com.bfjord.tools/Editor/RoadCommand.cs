using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bwork.FjordCoast.Junctions;
using Bwork.WorldAuthoring;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Bwork.Authoring.Editor
{
    public static class RoadCommand
    {
        const string Group = "Roads", ReceiptFile = "road-edit.json";
        [CliCommand("bwork_roads", "Prepare, apply, remove or inspect bounded sandbox roads with owned terrain rollback.", MainThreadRequired = true)]
        public static object Run(
            [CliArg("action", "prepare, apply, remove, status")] string action = "status",
            [CliArg("recipePath", "Optional JSON recipe path; omitted uses the original hill junction example")] string recipePath = "",
            [CliArg("fourArms", "Use four arms in the built-in example")] bool fourArms = false)
        {
            var terrain=ToolSandbox.RequireTerrain();var prior=ReadReceipt();var old=ToolSandbox.Root.Find(Group);
            if(action=="status")return new{applied=prior!=null,objects=old==null?0:old.childCount,roads=prior?.roadCount??0,changedHeightCells=prior?.change.Count??0,gradePercent=prior?.gradePercent??0};
            if(prior!=null&&prior.ownsTerrainLOD&&(terrain.heightmapMinimumLODSimplification!=prior.fittedMinimumLOD||terrain.heightmapMaximumLOD!=0||!terrain.ignoreQualitySettings))
                throw new InvalidOperationException("Road-owned Terrain LOD settings changed; restore the road checkpoint before replacement or removal.");
            if(action=="remove")return Remove(terrain,prior,old);
            if(action!="prepare"&&action!="apply")throw new ArgumentException("Unknown road action.");
            if((prior==null)!=(old==null))throw new InvalidOperationException("Road root/receipt disagree; restore their saved checkpoint before replacement.");
            var recipe=string.IsNullOrEmpty(recipePath)?SandboxRoadExample.Recipe(fourArms):ReadRecipe(recipePath);
            var data=terrain.terrainData;int resolution=data.heightmapResolution;
            var current=data.GetHeights(0,0,resolution,resolution);
            var baseline=prior==null?(float[,])current.Clone():prior.change.RestoredCopy(terrain);
            var origin=terrain.transform.position;var size=data.size;
            if(recipe.MinX<origin.x||recipe.MinZ<origin.z||recipe.MinX+recipe.Size>origin.x+size.x||recipe.MinZ+recipe.Size>origin.z+size.z)
                throw new ArgumentException("Road recipe must fit the target Terrain square.");
            float Ground(float x,float z)
            {
                float u=Mathf.Clamp((x-origin.x)/size.x*(resolution-1),0,resolution-1),v=Mathf.Clamp((z-origin.z)/size.z*(resolution-1),0,resolution-1);
                int x0=Mathf.FloorToInt(u),z0=Mathf.FloorToInt(v),x1=Math.Min(x0+1,resolution-1),z1=Math.Min(z0+1,resolution-1);
                return origin.y+size.y*Mathf.Lerp(Mathf.Lerp(baseline[z0,x0],baseline[z0,x1],u-x0),Mathf.Lerp(baseline[z1,x0],baseline[z1,x1],u-x0),v-z0);
            }
            var result=SandboxRoadAuthoring.Build(recipe,Ground);
            var fit=SandboxTerrainConformance.Fit(result.Earthwork,baseline,origin.x,origin.y,origin.z,size.x,size.y,size.z);
            var after=fit.Heights;
            // This small authoring sandbox certifies the stored grid, not arbitrary coarser LODs.
            // Unity's Inspector uses log2(resolution / 17) as its maximum simplification limit.
            int fullDetail=Mathf.RoundToInt(Mathf.Log(resolution/17,2));
            int previousMinimum=terrain.heightmapMinimumLODSimplification,previousMaximum=terrain.heightmapMaximumLOD;
            bool previousIgnoreQuality=terrain.ignoreQualitySettings;
            var owned=TerrainPatchChange.Create(terrain,baseline,after);
            if(action=="prepare")return Report(result,owned.Count,false,fit,fullDetail);
            // All geometry and cell work is complete before assets or live heights change.
            var materialSet=RoadPresentation.Materials();var normals=RoadPresentation.FittedNormals(result.Meshes);
            var paintSnapshot=new TerrainPresentation.Snapshot(terrain);var createdAssets=new List<string>();GameObject pending=null;
            var transition=TerrainPatchChange.Create(terrain,current,after);bool terrainApplied=false,receiptWritten=false;
            try
            {
                pending=new GameObject("Roads preparing");pending.SetActive(false);pending.transform.SetParent(ToolSandbox.Root,false);
                string generation="roads-"+Guid.NewGuid().ToString("N");
                for(int i=0;i<result.Meshes.Length;i++)
                {
                    var source=result.Meshes[i];var mesh=Mesh(source,normals[i]);string filename=generation+"-"+i+".asset";
                    var saved=ToolSandbox.Persist(mesh,filename);createdAssets.Add(ToolSandbox.Generated+"/"+filename);
                    var go=new GameObject(source.Id);go.transform.SetParent(pending.transform,false);
                    go.AddComponent<MeshFilter>().sharedMesh=saved;
                    var renderer=go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials=materialSet[source.Surface];
                    renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                    go.AddComponent<MeshCollider>().sharedMesh=saved;
                }
                terrain.heightmapMaximumLOD=0;terrain.heightmapMinimumLODSimplification=fullDetail;terrain.ignoreQualitySettings=true;
                if(terrain.heightmapMinimumLODSimplification!=fullDetail||terrain.heightmapMaximumLOD!=0)throw new InvalidOperationException("Full-detail sandbox Terrain setting was not retained.");
                EditorUtility.SetDirty(terrain);
                transition.Apply(terrain);terrainApplied=true;
                fit.MinimumClearance=SandboxTerrainConformance.Validate(result.Earthwork,data.GetHeights(0,0,resolution,resolution),origin.x,origin.y,origin.z,size.x,size.y,size.z);
                // Store actual post-write values, including Terrain's height quantization.
                owned=TerrainPatchChange.Create(terrain,baseline,data.GetHeights(0,0,resolution,resolution));
                var receipt=new Receipt{ownsTerrainLOD=true,
                    originalMinimumLOD=prior!=null&&prior.ownsTerrainLOD?prior.originalMinimumLOD:previousMinimum,
                    originalMaximumLOD=prior!=null&&prior.ownsTerrainLOD?prior.originalMaximumLOD:previousMaximum,
                    originalIgnoreQuality=prior!=null&&prior.ownsTerrainLOD?prior.originalIgnoreQuality:previousIgnoreQuality,
                    fittedMinimumLOD=fullDetail,change=owned,meshAssets=createdAssets.ToArray(),roadCount=result.Paths.Length,gradePercent=result.MaximumGradePercent,
                    sources=result.Paths.Select(p=>p.Source.SourceId).ToArray(),recipeJson=JsonUtility.ToJson(RecipeData.From(recipe))};
                receiptWritten=true;WriteReceipt(receipt);
                if(old!=null){old.name="Roads previous";old.gameObject.SetActive(false);}
                pending.name=Group;pending.SetActive(true);ToolSandbox.PaintTerrain(terrain);ToolSandbox.Save();
            }
            catch
            {
                if(pending!=null)Object.DestroyImmediate(pending);
                if(old!=null){old.name=Group;old.gameObject.SetActive(true);}
                if(terrainApplied)transition.Restore(terrain);
                paintSnapshot.Restore(terrain);
                terrain.heightmapMinimumLODSimplification=previousMinimum;terrain.heightmapMaximumLOD=previousMaximum;terrain.ignoreQualitySettings=previousIgnoreQuality;EditorUtility.SetDirty(terrain);
                if(receiptWritten){if(prior!=null)WriteReceipt(prior);else AssetDatabase.DeleteAsset(ToolSandbox.Generated+"/"+ReceiptFile);}
                foreach(string asset in createdAssets)AssetDatabase.DeleteAsset(asset);
                throw;
            }
            if(old!=null)Object.DestroyImmediate(old.gameObject);
            if(prior!=null)foreach(string asset in prior.meshAssets)DeleteOwnedMesh(asset);
            ToolSandbox.Save();return Report(result,owned.Count,true,fit,fullDetail);
        }
        static object Remove(Terrain terrain,Receipt receipt,Transform root)
        {
            if(receipt==null&&root==null)return new{removed=false};
            if(receipt==null||root==null)throw new InvalidOperationException("Road root/receipt disagree; no removal performed.");
            var paintSnapshot=new TerrainPresentation.Snapshot(terrain);
            receipt.change.Restore(terrain);
            try { ToolSandbox.PaintTerrain(terrain); }
            catch { receipt.change.Apply(terrain);paintSnapshot.Restore(terrain);throw; }
            if(receipt.ownsTerrainLOD){terrain.heightmapMinimumLODSimplification=receipt.originalMinimumLOD;terrain.heightmapMaximumLOD=receipt.originalMaximumLOD;terrain.ignoreQualitySettings=receipt.originalIgnoreQuality;EditorUtility.SetDirty(terrain);}
            Object.DestroyImmediate(root.gameObject);
            foreach(string asset in receipt.meshAssets)DeleteOwnedMesh(asset);
            AssetDatabase.DeleteAsset(ToolSandbox.Generated+"/"+ReceiptFile);ToolSandbox.Save();return new{removed=true,restoredHeightCells=receipt.change.Count};
        }
        static object Report(SandboxRoadResult result,int changed,bool applied,SandboxTerrainFit fit,int fullDetail)=>new
        {applied,roads=result.Paths.Length,junctions=result.Meshes.Length-result.Paths.Length,vertices=result.Meshes.Sum(m=>m.Vertices.Length),
            triangles=result.Meshes.Sum(m=>m.Triangles.Take(3).Sum(t=>t.Length/3)),gradePercent=result.MaximumGradePercent,surfaceGradePercent=result.MaximumSurfaceGradePercent,changedHeightCells=changed,
            minimumTerrainClearanceMeters=fit.MinimumClearance,priorMinimumTerrainClearanceMeters=fit.BeforeMinimumClearance,
            maximumAdditionalCutMeters=fit.MaximumAdditionalCut,terrainConstraintVertices=fit.ConstraintVertices,terrainFullDetailSimplification=fullDetail,
            sourceIds=result.Paths.Select(p=>p.Source.SourceId).ToArray(),spans=result.Paths.Sum(p=>p.Source.Spans.Length)};
        static Mesh Mesh(SandboxMesh source,Vector3[] normals)
        {
            var mesh=new Mesh{name=source.Id,indexFormat=source.Vertices.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(source.Vertices.Select(v=>new Vector3(v.Position.X,v.Position.Y,v.Position.Z)).ToArray());
            mesh.SetNormals(normals);
            mesh.SetUVs(0,source.Vertices.Select(v=>new Vector2(v.UV.X,v.UV.Y)).ToArray());
            mesh.SetUVs(1,source.Vertices.Select(v=>new Vector2(v.Mask.X*8/source.Width,v.Mask.Y)).ToArray());
            // The outer earthwork band remains in conformance/ownership, while the
            // classified Terrain supplies its visible surface and collision. Rendering a
            // uniform grass mesh there would hide the terrain's slope/bank paint.
            mesh.subMeshCount=3;for(int i=0;i<3;i++)mesh.SetTriangles(source.Triangles[i],i);
            mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
        static Receipt ReadReceipt()
        {
            var source=AssetDatabase.LoadAssetAtPath<TextAsset>(ToolSandbox.Generated+"/"+ReceiptFile);
            if(source==null)return null;var receipt=JsonUtility.FromJson<Receipt>(source.text);
            if(receipt==null||receipt.change==null||receipt.meshAssets==null)throw new InvalidDataException("Invalid road edit receipt.");return receipt;
        }
        static void WriteReceipt(Receipt receipt)=>ToolSandbox.Persist(new TextAsset(JsonUtility.ToJson(receipt,true)),ReceiptFile);
        static void DeleteOwnedMesh(string path)
        {if(path.StartsWith(ToolSandbox.Generated+"/roads-",StringComparison.Ordinal)&&Path.GetExtension(path)==".asset")AssetDatabase.DeleteAsset(path);else throw new InvalidDataException("Receipt contains an unowned road asset.");}
        static SandboxRoadRecipe ReadRecipe(string path)
        {
            var file=new FileInfo(path);if(!file.Exists||file.Length>1024*1024)throw new ArgumentException("Existing road recipe up to 1MiB required.");
            var data=JsonUtility.FromJson<RecipeData>(File.ReadAllText(path));if(data==null||data.roads==null)throw new ArgumentException("Road recipe requires roads.");return data.ToRecipe();
        }
        [Serializable] sealed class Receipt
        {public TerrainPatchChange change;public string[] meshAssets,sources;public string recipeJson;public int roadCount;public double gradePercent;
            public bool ownsTerrainLOD,originalIgnoreQuality;public int originalMinimumLOD,originalMaximumLOD,fittedMinimumLOD;}
        [Serializable] sealed class RecipeData
        {
            public float minX,minZ,size=512,sampleSpacing=2,maximumGrade=.24f;public RoadData[] roads;
            public SandboxRoadRecipe ToRecipe()=>new SandboxRoadRecipe{MinX=minX,MinZ=minZ,Size=size,SampleSpacing=sampleSpacing,MaximumGrade=maximumGrade,Roads=roads.Select(r=>r.ToRoad()).ToArray()};
            public static RecipeData From(SandboxRoadRecipe r)=>new RecipeData{minX=r.MinX,minZ=r.MinZ,size=r.Size,sampleSpacing=r.SampleSpacing,maximumGrade=r.MaximumGrade,roads=r.Roads.Select(RoadData.From).ToArray()};
        }
        [Serializable] sealed class RoadData
        {
            public string id,sourceId,startNode,endNode,surface="asphalt";public float width=8;public bool useControlHeights;
            public Vector3[] controls;public SpanData[] spans=Array.Empty<SpanData>();
            public SandboxRoad ToRoad()=>new SandboxRoad{Id=id,SourceId=sourceId,StartNode=startNode,EndNode=endNode,Surface=surface,Width=width,UseControlHeights=useControlHeights,
                Controls=controls?.Select(v=>new V3(v.x,v.y,v.z)).ToArray(),Spans=spans?.Select(s=>new SandboxSpan{Id=s.id,Kind=s.kind,StartMeters=s.startMeters,EndMeters=s.endMeters}).ToArray()};
            public static RoadData From(SandboxRoad r)=>new RoadData{id=r.Id,sourceId=r.SourceId,startNode=r.StartNode,endNode=r.EndNode,surface=r.Surface,width=r.Width,useControlHeights=r.UseControlHeights,
                controls=r.Controls.Select(v=>new Vector3(v.X,v.Y,v.Z)).ToArray(),spans=r.Spans.Select(s=>new SpanData{id=s.Id,kind=s.Kind,startMeters=s.StartMeters,endMeters=s.EndMeters}).ToArray()};
        }
        [Serializable] sealed class SpanData {public string id,kind;public float startMeters,endMeters;}
    }
}
