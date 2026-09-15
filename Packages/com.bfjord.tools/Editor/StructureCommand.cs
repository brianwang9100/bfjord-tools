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
using Object=UnityEngine.Object;

namespace Bwork.Authoring.Editor
{
    public static class StructureCommand
    {
        const string Group="Structures",ReceiptFile="structure-edit.json";
        [CliCommand("bwork_structures","Prepare, apply, remove or inspect original bridges and tunnels with owned Terrain holes.",MainThreadRequired=true)]
        public static object Run([CliArg("action","prepare, apply, remove, status")]string action="status",
            [CliArg("recipePath","Bounded JSON recipe, masonry-example for current-terrain tunnel, or omitted for legacy samples")]string recipePath="")
        {
            var terrain=ToolSandbox.RequireTerrain();var previous=Read();var old=ToolSandbox.Root.Find(Group);
            if(action=="status")return new{applied=previous!=null,structures=previous?.count??0,changedHoleCells=previous?.holes.Count??0,changedHeightCells=previous?.heights?.Count??0,parts=old==null?0:old.childCount};
            if((previous==null)!=(old==null))throw new InvalidOperationException("Structure root/receipt disagree; restore their saved checkpoint.");
            if(previous!=null)
            {
                if(string.IsNullOrEmpty(previous.hierarchyHash))TunnelDetail.ValidateLegacy(old,previous.assets);
                else if(TunnelDetail.Fingerprint(old)!=previous.hierarchyHash)throw new InvalidOperationException("Structure hierarchy was edited; preserve foreign children or changed references before replacement/removal.");
            }
            if(action=="remove")
            {
                if(previous==null)return new{removed=false};previous.holes.RestoredCopy(terrain);previous.heights?.RestoredCopy(terrain);
                previous.holes.Restore(terrain);previous.heights?.Restore(terrain);Object.DestroyImmediate(old.gameObject);
                foreach(string asset in previous.assets)Delete(asset);AssetDatabase.DeleteAsset(ToolSandbox.Generated+"/"+ReceiptFile);ToolSandbox.Save();return new{removed=true,restoredHoleCells=previous.holes.Count};
            }
            if(action!="prepare"&&action!="apply")throw new ArgumentException("Unknown structure action.");
            var data=terrain.terrainData;var origin=terrain.transform.position;var size=data.size;int n=data.heightmapResolution;
            int fullDetail=Mathf.RoundToInt(Mathf.Log(n/17,2));
            if(terrain.heightmapMinimumLODSimplification!=fullDetail||terrain.heightmapMaximumLOD!=0||!terrain.ignoreQualitySettings)
                throw new InvalidOperationException("Structure approach conformance requires the sandbox full-detail Terrain setting (the road tool establishes it).");
            var currentHeights=data.GetHeights(0,0,n,n);var heights=previous?.heights==null?(float[,])currentHeights.Clone():previous.heights.RestoredCopy(terrain);
            float Ground(float x,float z)
            {
                float u=Mathf.Clamp((x-origin.x)/size.x*(n-1),0,n-1),v=Mathf.Clamp((z-origin.z)/size.z*(n-1),0,n-1);int x0=(int)u,z0=(int)v,x1=Math.Min(x0+1,n-1),z1=Math.Min(z0+1,n-1);
                return origin.y+size.y*Mathf.Lerp(Mathf.Lerp(heights[z0,x0],heights[z0,x1],u-x0),Mathf.Lerp(heights[z1,x0],heights[z1,x1],u-x0),v-z0);
            }
            var recipe=recipePath=="masonry-example"?MasonryExample(Ground):string.IsNullOrEmpty(recipePath)?Example(Ground):LoadRecipe(recipePath);
            if(recipe.structures==null||recipe.structures.Length<1||recipe.structures.Length>8||recipe.structures.Select(s=>s?.id).Distinct().Count()!=recipe.structures.Length)throw new ArgumentException("One to eight uniquely identified structures required.");
            if(size.x!=size.z||size.x>2048)throw new ArgumentException("A bounded square sandbox Terrain is required.");
            float cellMargin=(float)Math.Sqrt(Math.Pow(size.x/data.holesResolution,2)+Math.Pow(size.z/data.holesResolution,2))*.5f;
            var placed=recipe.structures.Select(s=>{var spec=s.ToSpec();spec.HoleCellMargin=cellMargin;return StructureGeometry.Build(spec,Ground,origin.x,origin.z,size.x);}).ToArray();
            var portalModel=placed.Any(p=>p.Spec.PortalStyle=="masonry")?TunnelDetail.Read():null;
            int h=data.holesResolution;var current=data.GetHoles(0,0,h,h);var baseline=previous==null?(bool[,])current.Clone():previous.holes.RestoredCopy(terrain);var after=(bool[,])baseline.Clone();
            float margin=(float)Math.Sqrt(Math.Pow(size.x/h,2)+Math.Pow(size.z/h,2))*.5f;
            foreach(var item in placed.Where(p=>p.Spec.Kind=="tunnel"))
            {
                float reach=item.Spec.Width/2+item.Spec.Thickness+margin;
                int x0=Math.Max(0,(int)Math.Floor((item.Path.Min(p=>p.X)-reach-origin.x)/size.x*h)),x1=Math.Min(h-1,(int)Math.Ceiling((item.Path.Max(p=>p.X)+reach-origin.x)/size.x*h));
                int z0=Math.Max(0,(int)Math.Floor((item.Path.Min(p=>p.Z)-reach-origin.z)/size.z*h)),z1=Math.Min(h-1,(int)Math.Ceiling((item.Path.Max(p=>p.Z)+reach-origin.z)/size.z*h));
                for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++){float wx=origin.x+(x+.5f)*size.x/h,wz=origin.z+(z+.5f)*size.z/h;if(item.CutHole(wx,wz,Ground(wx,wz),margin))after[z,x]=false;}
            }
            var approachPatch=new SandboxEarthworkPatch(new SandboxRoadRecipe{MinX=origin.x,MinZ=origin.z,Size=size.x},Ground);
            foreach(var item in placed)foreach(var face in item.ApproachEarthwork.Faces)approachPatch.Add(face.a,face.b,face.c,item.Spec.Width);
            var terrainFit=SandboxTerrainConformance.Fit(approachPatch,heights,origin.x,origin.y,origin.z,size.x,size.y,size.z);
            var nextHeights=terrainFit.Heights;
            var ownedHeights=TerrainPatchChange.Create(terrain,heights,nextHeights);var heightTransition=TerrainPatchChange.Create(terrain,currentHeights,nextHeights);
            var holes=TerrainHoleChange.Create(terrain,baseline,after);if(action=="prepare")return Report(placed,holes.Count,false,ownedHeights.Count,terrainFit);
            var materials=new Dictionary<string,Material>();var assets=new List<string>();GameObject pending=null;bool applied=false,heightApplied=false,wrote=false;
            var transition=TerrainHoleChange.Create(terrain,current,after);string generation="structures-"+Guid.NewGuid().ToString("N");
            try
            {
                pending=new GameObject("Structures preparing");pending.SetActive(false);pending.transform.SetParent(ToolSandbox.Root,false);
                foreach(var style in placed.SelectMany(p=>p.Parts.Select(m=>m.Material)).Distinct())
                {
                    var material=style.StartsWith("tunnel-",StringComparison.Ordinal)?TunnelDetail.Material(style,generation,assets):StructureMaterial(style,recipe.concreteMaterialPath);
                    string filename=generation+"-"+style+".mat";materials.Add(style,ToolSandbox.Persist(material,filename));assets.Add(ToolSandbox.Generated+"/"+filename);
                }
                int index=0;
                foreach(var item in placed)foreach(var part in item.Parts)
                {
                    var mesh=new Mesh{name=part.Name,indexFormat=part.Vertices.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                    mesh.SetVertices(part.Vertices.Select(p=>new Vector3(p.X,p.Y,p.Z)).ToArray());mesh.SetUVs(0,part.UV.Select(p=>new Vector2(p.X,p.Y)).ToArray());mesh.SetTriangles(part.Triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
                    string filename=generation+"-"+(index++)+".asset";mesh=ToolSandbox.Persist(mesh,filename);assets.Add(ToolSandbox.Generated+"/"+filename);
                    var go=new GameObject(part.Name);go.transform.SetParent(pending.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial=materials[part.Material];go.AddComponent<MeshCollider>().sharedMesh=mesh;
                }
                if(portalModel!=null)foreach(var item in placed.Where(p=>p.Spec.PortalStyle=="masonry"))TunnelDetail.Add(portalModel,item,pending.transform,materials["tunnel-stone"],generation,assets);
                heightTransition.Apply(terrain);heightApplied=true;transition.Apply(terrain);applied=true;
                terrainFit.MinimumClearance=SandboxTerrainConformance.Validate(approachPatch,data.GetHeights(0,0,n,n),origin.x,origin.y,origin.z,size.x,size.y,size.z);
                ownedHeights=TerrainPatchChange.Create(terrain,heights,data.GetHeights(0,0,n,n));
                pending.name=Group;pending.SetActive(true);
                wrote=true;Write(new Receipt{hierarchyHash=TunnelDetail.Fingerprint(pending.transform),holes=holes,heights=ownedHeights,assets=assets.ToArray(),count=placed.Length,recipe=JsonUtility.ToJson(recipe)});
                if(old!=null){old.name="Structures previous";old.gameObject.SetActive(false);}pending.name=Group;pending.SetActive(true);ToolSandbox.Save();
            }
            catch
            {
                if(pending!=null)Object.DestroyImmediate(pending);if(old!=null){old.name=Group;old.gameObject.SetActive(true);}
                if(applied)transition.Restore(terrain);if(heightApplied)heightTransition.Restore(terrain);if(wrote){if(previous!=null)Write(previous);else AssetDatabase.DeleteAsset(ToolSandbox.Generated+"/"+ReceiptFile);}
                foreach(string asset in assets)Delete(asset);throw;
            }
            if(old!=null)Object.DestroyImmediate(old.gameObject);if(previous!=null)foreach(string asset in previous.assets)Delete(asset);ToolSandbox.Save();return Report(placed,holes.Count,true,ownedHeights.Count,terrainFit);
        }
        static Material StructureMaterial(string style,string concretePath)
        {
            if(style=="concrete")
            {
                var source=string.IsNullOrEmpty(concretePath)?null:AssetDatabase.LoadAssetAtPath<Material>(concretePath);
                if(source!=null)return new Material(source){name="Structure concrete"};
                if(!string.IsNullOrEmpty(concretePath)&&concretePath!="Assets/Generated/FjordReview/Tunnel Concrete.mat")throw new InvalidOperationException("Configured concrete material is missing: "+concretePath);
                var shader=Shader.Find("Universal Render Pipeline/Lit")??throw new InvalidOperationException("Missing URP Lit shader.");
                var plain=new Material(shader){name="Structure plain concrete"};plain.SetColor("_BaseColor",new Color(.63f,.62f,.58f));plain.SetFloat("_Smoothness",.18f);plain.SetFloat("_Metallic",0);return plain;
            }
            string name=style=="asphalt"?"Asphalt":style=="stone"?"Gravel":"Ground";
            var original=AssetDatabase.LoadAssetAtPath<Material>(ProjectContext.Material(name))??throw new InvalidOperationException("Missing reviewed material: "+name);
            return new Material(original){name="Structure "+style};
        }
        static object Report(PlacedStructure[] placed,int holes,bool applied,int heightCells,SandboxTerrainFit terrainFit)=>new
        {applied,structures=placed.Length,bridges=placed.Count(p=>p.Spec.Kind=="bridge"),tunnels=placed.Count(p=>p.Spec.Kind=="tunnel"),
            parts=placed.Sum(p=>p.Parts.Length),vertices=placed.Sum(p=>p.Parts.Sum(m=>m.Vertices.Length)),triangles=placed.Sum(p=>p.Parts.Sum(m=>m.Triangles.Length/3)),
            maximumGradePercent=placed.Max(p=>p.MaximumGradePercent),changedHoleCells=holes,changedHeightCells=heightCells,
            approachMinimumClearance=terrainFit.MinimumClearance,approachBeforeMinimumClearance=terrainFit.BeforeMinimumClearance,approachMaximumAdditionalCut=terrainFit.MaximumAdditionalCut,
            clearances=placed.Where(p=>p.Spec.Kind=="bridge").Select(p=>new{id=p.Spec.Id,minimumSampledUndersideClearance=p.MinimumUndersideClearance}).ToArray(),
            sourceIds=placed.Select(p=>p.Spec.SourceId).ToArray()};
        static Receipt Read()
        {
            var asset=AssetDatabase.LoadAssetAtPath<TextAsset>(ToolSandbox.Generated+"/"+ReceiptFile);if(asset==null)return null;
            var receipt=JsonUtility.FromJson<Receipt>(asset.text);if(receipt==null||receipt.holes==null||receipt.assets==null)throw new InvalidDataException("Invalid structure receipt.");return receipt;
        }
        static void Write(Receipt receipt)=>ToolSandbox.Persist(new TextAsset(JsonUtility.ToJson(receipt,true)),ReceiptFile);
        static void Delete(string path){if(!path.StartsWith(ToolSandbox.Generated+"/structures-",StringComparison.Ordinal)||!(path.EndsWith(".asset",StringComparison.Ordinal)||path.EndsWith(".mat",StringComparison.Ordinal)||path.EndsWith(".png",StringComparison.Ordinal)))throw new InvalidDataException("Unowned structure asset in receipt.");AssetDatabase.DeleteAsset(path);}
        static Recipe LoadRecipe(string path){var file=new FileInfo(path);if(!file.Exists||file.Length>1024*1024)throw new ArgumentException("Existing recipe up to 1MiB required.");return JsonUtility.FromJson<Recipe>(File.ReadAllText(path))??throw new ArgumentException("Invalid structure recipe.");}
        static Recipe MasonryExample(Func<float,float,float> ground)
        {var specs=StructureGeometry.Example(ground).Where(s=>s.Kind=="tunnel").ToArray();foreach(var spec in specs)spec.PortalStyle="masonry";return new Recipe{structures=specs.Select(Item.From).ToArray()};}
        static Recipe Example(Func<float,float,float> ground)=>new Recipe{structures=StructureGeometry.Example(ground).Select(Item.From).ToArray()};
        [Serializable] sealed class Receipt {public TerrainHoleChange holes;public TerrainPatchChange heights;public string[] assets;public int count;public string recipe,hierarchyHash;}
        [Serializable] sealed class Recipe {public Item[] structures;public string concreteMaterialPath="Assets/Generated/FjordReview/Tunnel Concrete.mat";}
        [Serializable] sealed class Item
        {
            public string id,sourceId,kind,style="concrete",portalStyle="none";public Vector3[] controls;public float width=8,clearance=5,thickness=.5f,parapetHeight=1.1f,supportSpacing=20,portalDepth=2,minimumCover=.5f,approachLength=40;
            public bool hasWaterLevel=false;public float waterLevel=0;
            public static Item From(StructureSpec s)=>new Item{id=s.Id,sourceId=s.SourceId,kind=s.Kind,style=s.Style,portalStyle=s.PortalStyle,controls=s.Controls.Select(p=>new Vector3(p.X,p.Y,p.Z)).ToArray(),width=s.Width,clearance=s.Clearance,thickness=s.Thickness,parapetHeight=s.ParapetHeight,supportSpacing=s.SupportSpacing,portalDepth=s.PortalDepth,minimumCover=s.MinimumCover,approachLength=s.ApproachLength,hasWaterLevel=s.WaterLevel.HasValue,waterLevel=s.WaterLevel??0};
            public StructureSpec ToSpec()=>new StructureSpec{Id=id,SourceId=sourceId,Kind=kind,Style=style,PortalStyle=portalStyle,Controls=controls?.Select(p=>new V3(p.x,p.y,p.z)).ToArray(),Width=width,Clearance=clearance,Thickness=thickness,ParapetHeight=parapetHeight,SupportSpacing=supportSpacing,PortalDepth=portalDepth,MinimumCover=minimumCover,ApproachLength=approachLength,WaterLevel=hasWaterLevel?(float?)waterLevel:null};
        }
    }
}
