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
            [CliArg("action", "prepare, apply, remove, status, view-dressing, refresh-markings, remove-markings, retry-marking-cleanup")] string action = "status",
            [CliArg("recipePath", "Optional JSON recipe path; omitted uses the original hill junction example")] string recipePath = "",
            [CliArg("fourArms", "Use four arms in the built-in example")] bool fourArms = false,
            [CliArg("dressing", "Add original stone verge fragments and posts; explicit opt-in on each apply")] bool dressing = false,
            [CliArg("markings", "Double yellow center and white edges on asphalt; default true. Refresh changes appearance only.")] bool markings = true)
        {
            var terrain=ToolSandbox.RequireTerrain();var prior=ReadReceipt();var old=ToolSandbox.Root.Find(Group);
            if(action=="view-dressing")return RoadDetailPresentation.View(terrain);
            if(action=="status")return new{applied=prior!=null,objects=old==null?0:old.childCount,roads=prior?.roadCount??0,changedHeightCells=prior?.change.Count??0,gradePercent=prior?.gradePercent??0,dressing=prior?.dressing??false,dressingInstances=prior?.dressingInstances??0,markings=prior?.markings??false,markingRoads=prior?.markingObjects??0,markingCleanupPending=prior?.markingCleanupAssets.Length??0};
            if(action=="retry-marking-cleanup")
            {
                if(prior==null||old==null)throw new InvalidOperationException("Marking cleanup requires the retained road root and receipt.");
                ValidateOwned(old,prior);CleanupMarkings(prior);
                return new{markingCleanupPending=prior.markingCleanupAssets.Length};
            }
            if(action=="refresh-markings"||action=="remove-markings")
            {
                if(!string.IsNullOrEmpty(recipePath))throw new ArgumentException("Marking refresh uses the retained road receipt; recipePath is only for road construction.");
                return RefreshMarkings(prior,old,action=="refresh-markings"&&markings);
            }
            if(prior!=null&&prior.ownsTerrainLOD&&(terrain.heightmapMinimumLODSimplification!=prior.fittedMinimumLOD||terrain.heightmapMaximumLOD!=0||!terrain.ignoreQualitySettings))
                throw new InvalidOperationException("Road-owned Terrain LOD settings changed; restore the road checkpoint before replacement or removal.");
            if(prior!=null&&old!=null&&(action=="apply"||action=="remove")){ValidateOwned(old,prior);CleanupMarkings(prior);}
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
            float FittedGround(float x,float z)
            {
                float u=(x-origin.x)/size.x,v=(z-origin.z)/size.z;
                if(u<0||v<0||u>1||v>1)return float.NaN;
                if(data.IsHole(Math.Min(data.holesResolution-1,(int)(u*data.holesResolution)),Math.Min(data.holesResolution-1,(int)(v*data.holesResolution))))return float.NaN;
                float fx=u*(resolution-1),fz=v*(resolution-1);int x0=(int)fx,z0=(int)fz,x1=Math.Min(x0+1,resolution-1),z1=Math.Min(z0+1,resolution-1);
                return origin.y+size.y*Mathf.Lerp(Mathf.Lerp(after[z0,x0],after[z0,x1],fx-x0),Mathf.Lerp(after[z1,x0],after[z1,x1],fx-x0),fz-z0);
            }
            if(dressing)RoadDetailPresentation.RequireSources();
            var detailPlan=dressing?RoadDetailPresentation.Plan(result,FittedGround,RoadDetailPresentation.ExistingExclusions(ToolSandbox.Root)):Array.Empty<RoadDetailPresentation.Placement>();
            if(action=="prepare")return Report(result,owned.Count,false,fit,fullDetail,detailPlan.Length,markings);
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
                RoadDetailPresentation.Build(pending.transform,detailPlan,generation,createdAssets);
                int markingStart=createdAssets.Count;
                var paint=markings?RoadMarkings.Build(pending.transform,MarkingRoads(RecipeData.From(recipe)),generation,createdAssets):null;
                var markingAssets=createdAssets.Skip(markingStart).ToArray();
                if(paint!=null){paint.transform.SetParent(pending.transform,false);paint.SetActive(true);}
                terrain.heightmapMaximumLOD=0;terrain.heightmapMinimumLODSimplification=fullDetail;terrain.ignoreQualitySettings=true;
                if(terrain.heightmapMinimumLODSimplification!=fullDetail||terrain.heightmapMaximumLOD!=0)throw new InvalidOperationException("Full-detail sandbox Terrain setting was not retained.");
                EditorUtility.SetDirty(terrain);
                transition.Apply(terrain);terrainApplied=true;
                fit.MinimumClearance=SandboxTerrainConformance.Validate(result.Earthwork,data.GetHeights(0,0,resolution,resolution),origin.x,origin.y,origin.z,size.x,size.y,size.z);
                // Store actual post-write values, including Terrain's height quantization.
                owned=TerrainPatchChange.Create(terrain,baseline,data.GetHeights(0,0,resolution,resolution));
                var receipt=new Receipt{markings=markings,markingVersion=markings?RoadMarkings.Version:0,markingObjects=paint==null?0:paint.transform.childCount,markingAssets=markingAssets,assetHash=RoadMarkings.AssetFingerprint(createdAssets.ToArray(),true),ownsTerrainLOD=true,dressing=dressing,dressingInstances=detailPlan.Length,hierarchyHash=RoadHierarchyOwnership.Fingerprint(pending.transform),
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
            ToolSandbox.Save();return Report(result,owned.Count,true,fit,fullDetail,detailPlan.Length,markings);
        }
        static RoadMarkings.Road[] MarkingRoads(RecipeData recipe)
        {
            if(recipe?.roads==null)throw new InvalidDataException("Retained road recipe is missing.");
            return recipe.roads.Select(r=>new RoadMarkings.Road{id=r.id,surface=r.surface,width=r.width}).ToArray();
        }
        static void ValidateOwned(Transform root,Receipt receipt)
        {
            var legacyNames=string.IsNullOrEmpty(receipt.hierarchyHash)?RoadHierarchyOwnership.LegacyNames(
                JsonUtility.FromJson<RecipeData>(receipt.recipeJson)?.ToRecipe(),receipt.meshAssets):null;
            RoadHierarchyOwnership.Validate(root,receipt.hierarchyHash,receipt.meshAssets,legacyNames);
            string actual=RoadMarkings.AssetFingerprint(receipt.meshAssets);
            if(!string.IsNullOrEmpty(receipt.assetHash)&&actual!=receipt.assetHash)
            {
                if(receipt.markingVersion!=1||!RoadMarkings.ProvesLegacyPaintInitialization(receipt.meshAssets,receipt.markingAssets,receipt.assetHash))
                    throw new InvalidDataException("Owned road asset bytes changed; preserve the edited generation before replacement/removal.");
                // All original asset bytes are proven by reversing only the documented URP
                // initialization delta; accept its initialized form, never arbitrary edited assets.
                receipt.assetHash=actual;WriteReceipt(receipt);
            }
        }
        static object RefreshMarkings(Receipt prior,Transform roads,bool enabled)
        {
            if(prior==null||roads==null)throw new InvalidOperationException("Marking refresh requires an existing owned Roads root and receipt.");
            ValidateOwned(roads,prior);CleanupMarkings(prior);
            var previousAssets=prior.markingAssets??Array.Empty<string>();var previous=roads.Find(RoadMarkings.Group);
            if(previousAssets.Any(p=>!prior.meshAssets.Contains(p)||!Path.GetFileName(p).Contains("-paint-"))||prior.markingObjects<0||(prior.markingObjects>0)!=(previous!=null)||(previousAssets.Length>0)!=(previous!=null)||previous!=null&&!prior.markings)throw new InvalidDataException("Road marking root/receipt disagree; preserve their checkpoint.");
            if(RoadMarkings.Current(enabled,prior.markingVersion,prior.markings))return new{unchanged=true,markings=enabled,markingRoads=prior.markingObjects,terrainChanges=false,roadGeometryChanges=false};
            var recipe=JsonUtility.FromJson<RecipeData>(prior.recipeJson);var definitions=MarkingRoads(recipe);
            var next=JsonUtility.FromJson<Receipt>(JsonUtility.ToJson(prior));var created=new List<string>();GameObject pending=null;
            var held=new WaterGeneration.HeldRoot(previous);int previousIndex=previous==null?0:previous.GetSiblingIndex();
            var previousPosition=previous==null?Vector3.zero:previous.localPosition;var previousRotation=previous==null?Quaternion.identity:previous.localRotation;var previousScale=previous==null?Vector3.one:previous.localScale;
            bool detached=false,published=false;
            try
            {
                if(enabled)pending=RoadMarkings.Build(roads,definitions,"roads-"+Guid.NewGuid().ToString("N"),created);
                if(previous!=null){held.Park();previous.SetParent(null,true);detached=true;}
                if(pending!=null){pending.transform.SetParent(roads,false);pending.SetActive(true);}
                next.markings=enabled;next.markingVersion=enabled?RoadMarkings.Version:0;next.markingObjects=pending==null?0:pending.transform.childCount;next.markingAssets=created.ToArray();
                next.meshAssets=prior.meshAssets.Except(previousAssets).Concat(created).ToArray();
                // Publish the retired generation before deletion; a failed cleanup remains retryable.
                next.markingCleanupAssets=previousAssets;
                next.assetHash=RoadMarkings.AssetFingerprint(next.meshAssets,true);next.hierarchyHash=RoadHierarchyOwnership.Fingerprint(roads);
                published=true;WriteReceipt(next);ToolSandbox.Save();
            }
            catch
            {
                if(pending!=null)Object.DestroyImmediate(pending);
                if(detached){previous.SetParent(roads,false);previous.SetLocalPositionAndRotation(previousPosition,previousRotation);previous.localScale=previousScale;previous.SetSiblingIndex(previousIndex);held.Restore();}
                if(published)WriteReceipt(prior);
                foreach(string path in created)DeleteOwnedMesh(path);
                throw;
            }
            held.Release();CleanupMarkings(next);ToolSandbox.Save();
            return new{unchanged=false,markings=enabled,markingRoads=next.markingObjects,terrainChanges=false,roadGeometryChanges=false};
        }
        static void CleanupMarkings(Receipt receipt)
        {
            if(receipt.markingCleanupAssets.Length==0)return;
            ValidateMarkingCleanup(receipt);
            foreach(string path in receipt.markingCleanupAssets)
            {
                if(!File.Exists(path)&&!File.Exists(path+".meta"))continue;
                if(!AssetDatabase.DeleteAsset(path))
                    throw new IOException("Road marking cleanup is incomplete; retained paths can be retried with action=retry-marking-cleanup: "+path);
            }
            // Keep the original list durable until every deletion succeeds. Already deleted paths
            // are harmless on retry after a partial failure or receipt-publication failure.
            var next=JsonUtility.FromJson<Receipt>(JsonUtility.ToJson(receipt));
            next.markingCleanupAssets=Array.Empty<string>();WriteReceipt(next);
            receipt.markingCleanupAssets=Array.Empty<string>();
        }
        static void ValidateMarkingCleanup(Receipt receipt)
        {
            receipt.markingCleanupAssets??=Array.Empty<string>();
            if(receipt.markingCleanupAssets.Length>128||receipt.markingCleanupAssets.Any(path=>
                string.IsNullOrEmpty(path)||path.Contains("..")||path.Contains('\\')||
                Path.GetDirectoryName(path)?.Replace('\\','/')!=ToolSandbox.Generated||
                !Path.GetFileName(path).StartsWith("roads-",StringComparison.Ordinal)||
                !Path.GetFileName(path).Contains("-paint-")||Path.GetExtension(path)!=".asset"||receipt.meshAssets.Contains(path)))
                throw new InvalidDataException("Invalid or still-active road marking cleanup path; preserve the receipt.");
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
        static object Report(SandboxRoadResult result,int changed,bool applied,SandboxTerrainFit fit,int fullDetail,int dressingInstances=0,bool markings=true)=>new
        {applied,dressingInstances,markings,markingRoads=markings?result.Paths.Count(p=>p.Source.Surface=="asphalt"):0,roads=result.Paths.Length,junctions=result.Meshes.Length-result.Paths.Length,vertices=result.Meshes.Sum(m=>m.Vertices.Length),
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
            if(receipt==null||receipt.change==null||receipt.meshAssets==null)throw new InvalidDataException("Invalid road edit receipt.");ValidateMarkingCleanup(receipt);return receipt;
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
        {public TerrainPatchChange change;public string[] meshAssets,sources;public string recipeJson,hierarchyHash,assetHash;public string[] markingAssets=Array.Empty<string>(),markingCleanupAssets=Array.Empty<string>();public bool markings;public int markingVersion,markingObjects;public int roadCount;public double gradePercent;
            public bool dressing;public int dressingInstances;
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
