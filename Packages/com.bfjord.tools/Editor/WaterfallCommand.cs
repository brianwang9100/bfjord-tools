using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Unity.Pipeline.Commands;
using Bwork.Authoring.Editor.Rocks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Bwork.Authoring.Editor
{
    [Serializable]
    public sealed class WaterfallRecipe
    {
        public int schemaVersion=1;
        public Vector3 lip=new Vector3(90,18,410),toe=new Vector3(90,.83f,418);
        public float width=7,plungeWidth=16,plungeLength=23,fallSpeed=7,opacity=.86f;
        public int acrossSegments=16,fallSegments=40;
        public bool rockBackdrop=true;
        public float plungeWaveHeight=.45f,plungeWaveLength=28,plungeWaveSpeed=.65f;
    }

    /// <summary>Finite visual cascade. No terrain, route, current simulation or native state ownership.</summary>
    public static class WaterfallCommand
    {
        const string GroupName="Waterfall Sample";
        static readonly string[] BackdropIds={"granite_boulder","granite_boulder","upright_crag","granite_boulder","river_stone","upright_crag","granite_boulder","river_stone"};
        static readonly string[] LegacyBackdropIds={"cliff_slab","stratified_outcrop","stratified_outcrop","granite_boulder","river_stone","stratified_outcrop","granite_boulder","river_stone"};
        static string ReceiptPath=>ToolSandbox.Generated+"/waterfall-edit.json";
        [Serializable] sealed class Receipt
        {
            public int schemaVersion=1;
            public string generation,sourceHash,recipeJson,rockSourceHash,hierarchyHash;
            public string[] assets,cleanupAssets,backdropPrefabIds;
            public bool removed;
        }

        [CliCommand("bwork_waterfall","Prepare, replace, remove or view a bounded animated waterfall; capture water at an exact animation time.",MainThreadRequired=true)]
        public static object Run(
            [CliArg("action","prepare, apply, status, remove, view, capture")]string action="status",
            [CliArg("recipePath","Optional JSON; empty uses the original coastal waterfall fixture")]string recipePath="",
            [CliArg("capture","Optional view screenshot basename; required for capture")]string capture="",
            [CliArg("timeSeconds","Optional exact nonnegative animation time for capture; empty uses live time")]string timeSeconds="")
        {
            ToolSandbox.RequireTerrain();
            if(action=="capture")return new{image=Capture(capture,timeSeconds),animationTime=timeSeconds};
            var prior=ReadReceipt();var group=ToolSandbox.Root.Find(GroupName);
            if(action=="status")return new{installed=group!=null,sourceHash=prior?.sourceHash,removed=prior?.removed??false,
                cleanupPending=prior==null?0:(prior.removed?prior.assets.Concat(prior.cleanupAssets):prior.cleanupAssets).Count(Exists),
                terrainChanged=false};
            VerifyOwnedGroup(prior,group);
            if(action=="remove")return Remove(prior,group);
            if(action=="view")
            {
                if(prior==null||prior.removed||group==null)throw new InvalidOperationException("Apply the waterfall before viewing it.");
                var r=Parse(prior.recipeJson);var forward=Forward(r);var right=Vector3.Cross(Vector3.up,forward);
                var camera=ToolSandbox.Root.GetComponentInChildren<Camera>();
                camera.orthographic=false;camera.fieldOfView=48;
                camera.transform.position=r.toe+forward*24+right*23+Vector3.up*Mathf.Max(10,(r.lip.y-r.toe.y)*.45f);
                camera.transform.LookAt(Vector3.Lerp(r.lip,r.toe,.57f));
                return new{image=string.IsNullOrEmpty(capture)?null:Capture(capture,timeSeconds),lip=new[]{r.lip.x,r.lip.y,r.lip.z},toe=new[]{r.toe.x,r.toe.y,r.toe.z}};
            }
            if(action!="prepare"&&action!="apply")throw new ArgumentException("Unknown waterfall action.");
            if(prior?.removed==true)throw new InvalidOperationException("Finish pending waterfall removal first.");
            string path=string.IsNullOrWhiteSpace(recipePath)?ToolSandbox.SamplePath("waterfall.json"):Path.GetFullPath(recipePath);
            ProjectContext.RejectLinks(path);
            if(!File.Exists(path)||new FileInfo(path).Length>16384)throw new InvalidDataException("Waterfall recipe must be at most 16 KiB.");
            string json=File.ReadAllText(path);var recipe=Parse(json);
            var shader=Shader.Find("Bwork/Sandbox/Waterfall");
            if(shader==null||!shader.isSupported)throw new InvalidOperationException("Import the waterfall shader first.");
            var maps=RequireMaps(shader);
            var rocks=recipe.rockBackdrop?RockContract.Admit(ProjectContext.Current.rockSourceRoot):null;
            if(rocks!=null&&!RockLibrary.Ready(rocks))throw new InvalidOperationException("Run bwork_rocks action=build-assets for the configured rock source before preparing the rock-backed waterfall.");
            Mesh sheet=null,plunge=null;
            try
            {
                sheet=BuildSheet(recipe);plunge=BuildPlunge(recipe);
                int vertices=sheet.vertexCount+plunge.vertexCount,triangles=(sheet.triangles.Length+plunge.triangles.Length)/3;
                if(action=="prepare")return new{applied=false,vertices,triangles,terrainChanged=false,lip=new[]{recipe.lip.x,recipe.lip.y,recipe.lip.z},toe=new[]{recipe.toe.x,recipe.toe.y,recipe.toe.z}};
                var receipt=new Receipt{generation=Guid.NewGuid().ToString("N"),recipeJson=json,sourceHash=Hash(json+(rocks?.hash??""),shader,maps),rockSourceHash=rocks?.hash};
                receipt.assets=Paths(receipt.generation);receipt.backdropPrefabIds=recipe.rockBackdrop?(string[])BackdropIds.Clone():Array.Empty<string>();
                receipt.cleanupAssets=(prior?.assets??Array.Empty<string>()).Concat(prior?.cleanupAssets??Array.Empty<string>()).Where(Exists).Distinct().ToArray();
                ValidatePaths(receipt.cleanupAssets);
                Apply(recipe,shader,maps,rocks,ref sheet,ref plunge,receipt,group);
                return new{applied=true,vertices,triangles,terrainChanged=false,sourceHash=receipt.sourceHash};
            }
            finally{if(sheet!=null)Object.DestroyImmediate(sheet);if(plunge!=null)Object.DestroyImmediate(plunge);}
        }

        static WaterfallRecipe Parse(string json)
        {
            var r=new WaterfallRecipe();JsonUtility.FromJsonOverwrite(json,r);Validate(r);return r;
        }
        public static void Validate(WaterfallRecipe r)
        {
            if(r==null||r.schemaVersion!=1)throw new InvalidDataException("Waterfall schemaVersion must be 1.");
            foreach(var p in new[]{r.lip,r.toe})
            {Range(p.x,32,480,"x");Range(p.z,32,480,"z");Range(p.y,-10,150,"height");}
            Range(r.lip.y-r.toe.y,2,60,"drop");
            Range(Vector2.Distance(new Vector2(r.lip.x,r.lip.z),new Vector2(r.toe.x,r.toe.z)),1,50,"horizontal run");
            Range(r.width,1,20,"width");Range(r.plungeWidth,r.width,40,"plungeWidth");Range(r.plungeLength,4,40,"plungeLength");
            Range(r.plungeWaveHeight,0,1,"plungeWaveHeight");Range(r.plungeWaveLength,4,80,"plungeWaveLength");Range(r.plungeWaveSpeed,0,3,"plungeWaveSpeed");
            Range(r.fallSpeed,.1f,20,"fallSpeed");Range(r.opacity,.1f,1,"opacity");
            if(r.acrossSegments<2||r.acrossSegments>32||r.fallSegments<4||r.fallSegments>128)
                throw new InvalidDataException("Waterfall subdivisions exceed the finite authoring budget.");
            var centre=r.toe+Forward(r)*(r.plungeLength*.13f);
            float radius=Mathf.Sqrt(r.plungeWidth*r.plungeWidth+r.plungeLength*r.plungeLength)*.5f;
            if(centre.x-radius<0||centre.z-radius<0||centre.x+radius>512||centre.z+radius>512)
                throw new InvalidDataException("Plunge foam must stay inside the 512m fixture.");
        }
        static void Range(float value,float min,float max,string name)
        {if(float.IsNaN(value)||float.IsInfinity(value)||value<min||value>max)throw new InvalidDataException("Waterfall "+name+" is outside its finite range.");}
        static Vector3 Forward(WaterfallRecipe r)=>new Vector3(r.toe.x-r.lip.x,0,r.toe.z-r.lip.z).normalized;

        public static Mesh BuildSheet(WaterfallRecipe r)
        {
            Validate(r);int stride=r.acrossSegments+1;
            var positions=new Vector3[stride*(r.fallSegments+1)];var uv=new Vector2[positions.Length];var metres=new Vector2[positions.Length];
            var forward=Forward(r);var right=Vector3.Cross(Vector3.up,forward);Vector3 last=r.lip-forward*1.6f;float length=0;
            for(int y=0;y<=r.fallSegments;y++)
            {
                float v=y/(float)r.fallSegments;
                float fall=Mathf.Clamp01((v-.15f)/.85f);
                var centre=v<.15f?r.lip-forward*(1.6f*(1-v/.15f)):Vector3.Lerp(r.lip,r.toe,fall);
                centre.y=Mathf.Lerp(r.lip.y,r.toe.y,Mathf.Pow(fall,1.65f));
                length+=Vector3.Distance(last,centre);last=centre;
                for(int x=0;x<stride;x++)
                {
                    int i=y*stride+x;float u=x/(float)r.acrossSegments;
                    float lateral=(u-.5f)*r.width*(1+.08f*v);
                    positions[i]=centre+right*lateral;uv[i]=new Vector2(u,v);metres[i]=new Vector2(lateral,length);
                }
            }
            return Expand(Grid("Original waterfall sheet",positions,uv,metres,r.acrossSegments,r.fallSegments),r.plungeWaveHeight);
        }
        public static Mesh BuildPlunge(WaterfallRecipe r)
        {
            Validate(r);const int divisions=16;int stride=divisions+1;
            var positions=new Vector3[stride*stride];var uv=new Vector2[positions.Length];var metres=new Vector2[positions.Length];
            var forward=Forward(r);var right=Vector3.Cross(Vector3.up,forward);var centre=r.toe+forward*(r.plungeLength*.13f);
            for(int y=0;y<stride;y++)for(int x=0;x<stride;x++)
            {
                int i=y*stride+x;var t=new Vector2(x/(float)divisions,y/(float)divisions);
                var p=new Vector2((t.x-.5f)*r.plungeWidth,(t.y-.5f)*r.plungeLength);
                positions[i]=centre+right*p.x+forward*p.y;uv[i]=t;metres[i]=p;
            }
            return Expand(Grid("Original waterfall plunge foam",positions,uv,metres,divisions,divisions),r.plungeWaveHeight);
        }
        static Mesh Expand(Mesh mesh,float height) {var bounds=mesh.bounds;bounds.Expand(new Vector3(0,height*2,0));mesh.bounds=bounds;return mesh;}
        static Mesh Grid(string name,Vector3[] positions,Vector2[] uv,Vector2[] metres,int columns,int rows)
        {
            var triangles=new int[columns*rows*6];int k=0,stride=columns+1;
            for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
            {
                int a=y*stride+x;triangles[k++]=a;triangles[k++]=a+stride;triangles[k++]=a+1;
                triangles[k++]=a+1;triangles[k++]=a+stride;triangles[k++]=a+stride+1;
            }
            var mesh=new Mesh{name=name,vertices=positions,uv=uv,uv2=metres,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static string Physical(Object asset)
        {
            string path=AssetDatabase.GetAssetPath(asset);var package=UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
            return package==null?Path.GetFullPath(path):Path.Combine(package.resolvedPath,path.Substring(package.assetPath.Length+1));
        }
        static Texture2D[] RequireMaps(Shader shader)
        {
            string root=Path.GetDirectoryName(Path.GetDirectoryName(AssetDatabase.GetAssetPath(shader))).Replace('\\','/');
            return new[]{"waterfall-motion.png","water-foam.png"}.Select(name=>
            {
                string path=root+"/Textures/Water/"+name;var map=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(map==null||importer==null||importer.sRGBTexture||!importer.mipmapEnabled||importer.wrapMode!=TextureWrapMode.Repeat||
                    importer.textureType!=TextureImporterType.Default||importer.maxTextureSize>512)
                    throw new InvalidDataException("Waterfall requires its linear repeating mipmapped map: "+path);
                return map;
            }).ToArray();
        }
        static string Hash(string json,Shader shader,Texture2D[] maps)
        {
            using var sha=SHA256.Create();var text=new StringBuilder(json).Append(File.ReadAllText(Physical(shader)));
            foreach(var map in maps)text.Append(Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(Physical(map)))));
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-","").ToLowerInvariant();
        }
        static Material Material(Shader shader,Texture2D[] maps,WaterfallRecipe r,bool plunge)
        {
            var m=new Material(shader){name=plunge?"Original plunge whitewater":"Original falling water"};
            m.SetTexture("_WaterfallMap",maps[0]);m.SetTexture("_FoamMap",maps[1]);m.SetFloat("_FallSpeed",r.fallSpeed);
            m.SetFloat("_PlungeWaveHeight",r.plungeWaveHeight);m.SetFloat("_PlungeWaveLength",r.plungeWaveLength);m.SetFloat("_PlungeWaveSpeed",r.plungeWaveSpeed);
            m.SetFloat("_Opacity",r.opacity);m.SetFloat("_Plunge",plunge?1:0);return m;
        }
        static void AddSurface(GameObject parent,string name,Mesh mesh,Material material)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent.transform,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        static GameObject AddBackdrop(GameObject parent,WaterfallRecipe r,RockSource source)
        {
            var root=new GameObject("Original rock backdrop");root.transform.SetParent(parent.transform,false);
            var forward=Forward(r);var right=Vector3.Cross(Vector3.up,forward);
            float drop=r.lip.y-r.toe.y,baseHeight=r.toe.y-3;
            var rotation=Quaternion.LookRotation(forward,Vector3.up);
            GameObject Place(string id,Vector3 centre,Vector3 size,float yaw,string profile)
            {
                var variant=source.manifest.variants.Single(v=>v.id==id);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(RockLibrary.PrefabPath(source,id));
                if(prefab==null)throw new InvalidDataException("Missing immutable backdrop rock: "+id);
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root.transform);
                instance.transform.localPosition=centre;instance.transform.localRotation=rotation*Quaternion.Euler(0,yaw,0);
                instance.transform.localScale=new Vector3(size.x/variant.Size.x,size.y/variant.Size.y,size.z/variant.Size.z);
                var material=AssetDatabase.LoadAssetAtPath<Material>(RockLibrary.MaterialPath(source,profile));
                foreach(var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
                    renderer.sharedMaterials=Enumerable.Repeat(material,renderer.sharedMaterials.Length).ToArray();
                foreach(var collider in instance.GetComponentsInChildren<Collider>(true))collider.enabled=false;
                return instance;
            }
            var cliff=r.lip-forward*4.7f;cliff.y=baseHeight;
            Place("granite_boulder",cliff,new Vector3(r.width*2.25f,drop+2.8f,10),-7,"granite");
            var ledge=r.lip-forward*1.45f;ledge.y=r.lip.y-3.5f;
            var cap=Place("granite_boulder",ledge,new Vector3(r.width*1.6f,3.45f,5.8f),0,"wet");
            for(int side=-1;side<=1;side+=2)
            {
                var shoulder=r.lip+right*(side*r.width*.92f)-forward*2;shoulder.y=baseHeight-.3f;
                Place("upright_crag",shoulder,new Vector3(r.width*.9f,drop*(side<0?.74f:.62f)+3,7),side*19,"granite");
                var foot=r.toe+right*(side*r.width*.64f)-forward*1.5f;foot.y=r.toe.y-1.7f;
                Place("granite_boulder",foot,new Vector3(4.8f,3.2f,4.1f),side*21,"wet");
                var pebble=r.toe+right*(side*r.width*.96f)+forward*2.6f;pebble.y=r.toe.y-.9f;
                Place("river_stone",pebble,new Vector3(3.5f,1.55f,2.7f),side*34,"wet");
            }
            return cap;
        }
        static void FitFeeder(Mesh sheet,WaterfallRecipe recipe,GameObject cap)
        {
            // One authoring-time projection against the visible source LOD; no collider or per-frame mesh work.
            var support=cap.transform.Find("LOD0");
            if(support==null)throw new InvalidDataException("The waterfall cap has no admitted LOD0 support surface.");
            var triangles=new List<(Vector3 a,Vector3 b,Vector3 c)>();
            foreach(var filter in support.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=filter.sharedMesh;var vertices=mesh.vertices;var indices=mesh.triangles;
                var matrix=cap.transform.parent.parent.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                for(int i=0;i<indices.Length;i+=3)triangles.Add((matrix.MultiplyPoint3x4(vertices[indices[i]]),matrix.MultiplyPoint3x4(vertices[indices[i+1]]),matrix.MultiplyPoint3x4(vertices[indices[i+2]])));
            }
            float Top(Vector3 p)
            {
                float highest=float.NegativeInfinity;
                foreach(var t in triangles)
                {
                    float d=(t.b.z-t.c.z)*(t.a.x-t.c.x)+(t.c.x-t.b.x)*(t.a.z-t.c.z);
                    if(Mathf.Abs(d)<1e-7f)continue;
                    float a=((t.b.z-t.c.z)*(p.x-t.c.x)+(t.c.x-t.b.x)*(p.z-t.c.z))/d;
                    float b=((t.c.z-t.a.z)*(p.x-t.c.x)+(t.a.x-t.c.x)*(p.z-t.c.z))/d,c=1-a-b;
                    if(a>=-.0001f&&b>=-.0001f&&c>=-.0001f)highest=Mathf.Max(highest,a*t.a.y+b*t.b.y+c*t.c.y);
                }
                if(float.IsNegativeInfinity(highest))throw new InvalidDataException("The authored waterfall feeder escaped its rock support.");
                return highest+.045f;
            }
            var positions=sheet.vertices;var uv=sheet.uv;var metres=sheet.uv2;int stride=recipe.acrossSegments+1;
            var right=Vector3.Cross(Vector3.up,Forward(recipe));
            for(int column=0;column<stride;column++)
            {
                float u=column/(float)recipe.acrossSegments;
                float lip=Top(recipe.lip+right*((u-.5f)*recipe.width*1.012f));
                float distance=0;
                for(int row=0;row<=recipe.fallSegments;row++)
                {
                    int i=row*stride+column;float v=uv[i].y;
                    if(v<=.15f)positions[i].y=Top(positions[i]);
                    else positions[i].y=Mathf.Lerp(lip,recipe.toe.y,Mathf.Pow((v-.15f)/.85f,1.65f));
                    if(row>0)distance+=Vector3.Distance(positions[i],positions[i-stride]);
                    metres[i].y=distance;
                }
            }
            sheet.vertices=positions;sheet.uv2=metres;sheet.RecalculateNormals();sheet.RecalculateBounds();Expand(sheet,recipe.plungeWaveHeight);
        }
        static void Apply(WaterfallRecipe recipe,Shader shader,Texture2D[] maps,RockSource rocks,ref Mesh sheet,ref Mesh plunge,Receipt receipt,Transform priorGroup)
        {
            var held=new WaterGeneration.HeldRoot(priorGroup);var bytes=WaterGeneration.Snapshot(ReceiptPath);var created=new List<string>();
            var pending=new GameObject(GroupName+" Pending");pending.transform.SetParent(ToolSandbox.Root,false);pending.SetActive(false);
            bool publishing=false,saving=false;
            try
            {
                if(rocks!=null)FitFeeder(sheet,recipe,AddBackdrop(pending,recipe,rocks));
                var savedSheet=WaterGeneration.Create(sheet,receipt.assets[0],created);sheet=null;
                var savedPlunge=WaterGeneration.Create(plunge,receipt.assets[1],created);plunge=null;
                var sheetMaterial=WaterGeneration.Create(Material(shader,maps,recipe,false),receipt.assets[2],created);
                var plungeMaterial=WaterGeneration.Create(Material(shader,maps,recipe,true),receipt.assets[3],created);
                AddSurface(pending,"Falling sheet",savedSheet,sheetMaterial);AddSurface(pending,"Plunge whitewater",savedPlunge,plungeMaterial);
                pending.transform.Find("Original rock backdrop")?.SetAsLastSibling();
                held.Park();pending.name=GroupName;pending.SetActive(true);
                receipt.hierarchyHash=HierarchyFingerprint(pending.transform);
                publishing=true;WaterGeneration.Publish(ReceiptPath,JsonUtility.ToJson(receipt,true));saving=true;ToolSandbox.Save();
            }
            catch(Exception error)
            {
                var errors=new List<Exception>{error};WaterGeneration.Attempt(()=>Object.DestroyImmediate(pending),errors);WaterGeneration.Attempt(held.Restore,errors);
                if(publishing)WaterGeneration.Attempt(()=>WaterGeneration.RestoreReceipt(ReceiptPath,bytes),errors);
                if(saving)WaterGeneration.Attempt(ToolSandbox.Save,errors);
                if(errors.Count==1)WaterGeneration.Attempt(()=>Cleanup(created.ToArray()),errors);
                else errors.Add(new IOException("Preserve waterfall generation assets until rollback recovery: "+string.Join(", ",created)));
                throw new AggregateException("Waterfall publication failed; prior state restored where possible.",errors);
            }
            held.Release();Cleanup(receipt.cleanupAssets);
        }
        static object Remove(Receipt receipt,Transform group)
        {
            if(receipt==null)return new{removed=false};
            if(!receipt.removed)
            {
                var bytes=WaterGeneration.Snapshot(ReceiptPath);var held=new WaterGeneration.HeldRoot(group);bool saving=false;
                try{held.Park();receipt.removed=true;WaterGeneration.Publish(ReceiptPath,JsonUtility.ToJson(receipt,true));saving=true;ToolSandbox.Save();}
                catch(Exception error)
                {
                    var errors=new List<Exception>{error};WaterGeneration.Attempt(held.Restore,errors);
                    WaterGeneration.Attempt(()=>WaterGeneration.RestoreReceipt(ReceiptPath,bytes),errors);if(saving)WaterGeneration.Attempt(ToolSandbox.Save,errors);
                    throw new AggregateException("Waterfall removal failed; prior state restored where possible.",errors);
                }
                held.Release();
            }
            Cleanup(receipt.assets.Concat(receipt.cleanupAssets).Distinct().ToArray());WaterGeneration.RestoreReceipt(ReceiptPath,null);return new{removed=true};
        }
        static string[] Paths(string key)=>new[]{"Sheet.asset","Plunge.asset","Sheet.mat","Plunge.mat"}.Select(s=>ToolSandbox.Generated+"/Waterfall-"+key+"-"+s).ToArray();
        static bool Exists(string path)=>File.Exists(path)||File.Exists(path+".meta");
        static void ValidatePaths(string[] paths)
        {
            if(paths==null||paths.Length>128||paths.Distinct().Count()!=paths.Length)throw new InvalidDataException("Invalid waterfall asset ownership.");
            foreach(var path in paths)
            {
                string prefix=ToolSandbox.Generated+"/Waterfall-";
                if(path==null||!path.StartsWith(prefix,StringComparison.Ordinal)||path.Length<prefix.Length+33)throw new InvalidDataException("Invalid waterfall asset path.");
                string key=path.Substring(prefix.Length,32);
                if(key.Any(c=>!"0123456789abcdef".Contains(c))||!Paths(key).Contains(path))throw new InvalidDataException("Waterfall path escaped its finite generation.");
                ProjectContext.RejectLinks(Path.GetFullPath(path));
            }
        }
        static Receipt ReadReceipt()
        {
            if(!File.Exists(ReceiptPath))return null;
            ProjectContext.RejectLinks(Path.GetFullPath(ReceiptPath));
            if(new FileInfo(ReceiptPath).Length>65536)throw new InvalidDataException("Waterfall receipt exceeds its finite budget.");
            var r=JsonUtility.FromJson<Receipt>(File.ReadAllText(ReceiptPath));
            if(r==null||r.schemaVersion!=1||r.generation==null||r.generation.Length!=32||r.generation.Any(c=>!"0123456789abcdef".Contains(c))||
                r.sourceHash==null||r.sourceHash.Length!=64||r.sourceHash.Any(c=>!"0123456789abcdef".Contains(c)))throw new InvalidDataException("Invalid waterfall receipt.");
            ValidatePaths(r.assets);ValidatePaths(r.cleanupAssets);
            if(!r.assets.SequenceEqual(Paths(r.generation))||r.assets.Intersect(r.cleanupAssets).Any())throw new InvalidDataException("Waterfall receipt asset identity differs.");
            var recipe=Parse(r.recipeJson);
            if(r.backdropPrefabIds==null)r.backdropPrefabIds=recipe.rockBackdrop?(string[])LegacyBackdropIds.Clone():Array.Empty<string>();
            if(recipe.rockBackdrop&&!(r.backdropPrefabIds.SequenceEqual(BackdropIds)||r.backdropPrefabIds.SequenceEqual(LegacyBackdropIds))||!recipe.rockBackdrop&&r.backdropPrefabIds.Length!=0)
                throw new InvalidDataException("Unexpected recorded backdrop composition.");
            if(!Digest(r.hierarchyHash)||recipe.rockBackdrop&&!Digest(r.rockSourceHash)||!recipe.rockBackdrop&&!string.IsNullOrEmpty(r.rockSourceHash))
                throw new InvalidDataException("Waterfall ownership seal is missing or invalid; preserve this generation for recovery.");
            return r;
        }
        static bool Digest(string text)=>text!=null&&text.Length==64&&text.All(c=>"0123456789abcdef".Contains(c));
        static void VerifyOwnedGroup(Receipt receipt,Transform group)
        {
            if(receipt==null&&group==null)return;
            if(receipt==null||receipt.removed&&group!=null||!receipt.removed&&group==null)throw new InvalidDataException("Waterfall group and receipt differ; preserve them for recovery.");
            if(receipt.removed)return;
            var recipe=Parse(receipt.recipeJson);
            if(group.childCount!=(recipe.rockBackdrop?3:2)||group.GetComponents<Component>().Length!=1)
                throw new InvalidDataException("Waterfall contains unexpected root objects or components.");
            string[] names={"Falling sheet","Plunge whitewater"};
            for(int i=0;i<2;i++)
            {
                var surface=group.GetChild(i);var filter=surface.GetComponent<MeshFilter>();var renderer=surface.GetComponent<MeshRenderer>();
                if(surface.name!=names[i]||surface.childCount!=0||surface.GetComponents<Component>().Length!=3||filter==null||renderer==null||
                    AssetDatabase.GetAssetPath(filter.sharedMesh)!=receipt.assets[i]||renderer.sharedMaterials.Length!=1||
                    AssetDatabase.GetAssetPath(renderer.sharedMaterial)!=receipt.assets[i+2])
                    throw new InvalidDataException("Waterfall surfaces differ from their exact owned assets.");
            }
            if(recipe.rockBackdrop)
            {
                var backdrop=group.GetChild(2);
                if(backdrop.name!="Original rock backdrop"||backdrop.childCount!=8||backdrop.GetComponents<Component>().Length!=1)
                    throw new InvalidDataException("Waterfall backdrop hierarchy differs from its owned fixture.");
                string prefix=ToolSandbox.Generated+"/RockLibrary/"+receipt.rockSourceHash+"/";
                string[] ids=receipt.backdropPrefabIds;
                for(int i=0;i<ids.Length;i++)
                {
                    var rock=backdrop.GetChild(i).gameObject;
                    if(!PrefabUtility.IsAnyPrefabInstanceRoot(rock)||PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(rock)!=prefix+ids[i]+".prefab")
                        throw new InvalidDataException("Waterfall backdrop prefab differs from its recorded immutable rock source.");
                }
            }
            if(HierarchyFingerprint(group)!=receipt.hierarchyHash)
                throw new InvalidDataException("Waterfall hierarchy, transforms or component references were edited; preserve them instead of replacing/removing the fixture.");
        }
        /// <summary>Stable installed-object seal; no external source files or volatile instance IDs.</summary>
        public static string HierarchyFingerprint(Transform root)
        {
            if(root==null)throw new ArgumentNullException(nameof(root));
            var nodes=root.GetComponentsInChildren<Transform>(true);
            if(nodes.Length>512)throw new InvalidDataException("Waterfall hierarchy exceeds the finite fixture budget.");
            var ordinals=nodes.Select((node,index)=>(node,index)).ToDictionary(p=>p.node,p=>p.index);
            var text=new StringBuilder();
            void Add(string value){value=value??"";text.Append(value.Length).Append(':').Append(value).Append(';');}
            void Number(float value)
            {
                if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidDataException("Nonfinite waterfall component value.");
                // Unity scene float serialization can round the final bits; tolerate less than 0.1mm.
                Add(Math.Round(value,4,MidpointRounding.AwayFromZero).ToString("F4",CultureInfo.InvariantCulture));
            }
            void Vector(Vector3 v){Number(v.x);Number(v.y);Number(v.z);}
            void Asset(Object value)
            {
                if(value==null){Add("null");return;}
                if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value,out string guid,out long local))
                    throw new InvalidDataException("Waterfall contains a transient or foreign non-asset reference.");
                Add(guid);Add(local.ToString(CultureInfo.InvariantCulture));Add(AssetDatabase.GetAssetPath(value));
            }
            foreach(var node in nodes)
            {
                Add(node==root?"root":ordinals[node.parent].ToString(CultureInfo.InvariantCulture));
                Add(node.name);Add(node.gameObject.activeSelf.ToString());Add(node.childCount.ToString(CultureInfo.InvariantCulture));
                Vector(node.localPosition);Vector(node.localScale);var rotation=node.localRotation;
                Number(rotation.x);Number(rotation.y);Number(rotation.z);Number(rotation.w);
                Asset(PrefabUtility.GetCorrespondingObjectFromSource(node.gameObject));
                foreach(var component in node.GetComponents<Component>())
                {
                    if(component==null)throw new InvalidDataException("Waterfall contains a missing script.");
                    Add(component.GetType().FullName);
                    if(component is Transform)continue;
                    if(component is MeshFilter filter)Asset(filter.sharedMesh);
                    else if(component is MeshRenderer renderer)
                    {
                        Add(renderer.enabled.ToString());Add(((int)renderer.shadowCastingMode).ToString(CultureInfo.InvariantCulture));
                        Add(renderer.receiveShadows.ToString());Add(renderer.sharedMaterials.Length.ToString(CultureInfo.InvariantCulture));
                        foreach(var material in renderer.sharedMaterials)Asset(material);
                    }
                    else if(component is MeshCollider collider)
                    {Asset(collider.sharedMesh);Add(collider.enabled.ToString());Add(collider.convex.ToString());Add(collider.isTrigger.ToString());}
                    else if(component is LODGroup lod)
                    {
                        Add(lod.enabled.ToString());Add(((int)lod.fadeMode).ToString(CultureInfo.InvariantCulture));Number(lod.size);Vector(lod.localReferencePoint);
                        foreach(var level in lod.GetLODs())
                        {
                            Number(level.screenRelativeTransitionHeight);Number(level.fadeTransitionWidth);Add(level.renderers.Length.ToString(CultureInfo.InvariantCulture));
                            foreach(var lodRenderer in level.renderers)
                            {
                                if(lodRenderer==null||!ordinals.TryGetValue(lodRenderer.transform,out int ordinal))throw new InvalidDataException("Waterfall LOD references foreign geometry.");
                                Add(ordinal.ToString(CultureInfo.InvariantCulture));
                            }
                        }
                    }
                    else throw new InvalidDataException("Unexpected waterfall component: "+component.GetType().Name);
                }
            }
            using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-","").ToLowerInvariant();
        }
        static void Cleanup(string[] paths)
        {
            ValidatePaths(paths);var errors=new List<Exception>();
            foreach(string path in paths)WaterGeneration.Attempt(()=>{if(Exists(path)&&!AssetDatabase.DeleteAsset(path))throw new IOException("Could not remove waterfall asset: "+path);},errors);
            if(errors.Count>0)throw new AggregateException("Waterfall cleanup incomplete; retained receipt allows retry.",errors);
        }
        static string Capture(string name,string timeSeconds)
        {
            if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("A capture basename is required.");
            if(string.IsNullOrWhiteSpace(timeSeconds))return ToolSandbox.Capture(name);
            if(!float.TryParse(timeSeconds,NumberStyles.Float,CultureInfo.InvariantCulture,out float time)||float.IsNaN(time)||float.IsInfinity(time)||time<0||time>600)
                throw new ArgumentException("Animation time must be finite within 0–600 seconds.");
            var snapshots=new List<(MeshRenderer renderer,MaterialPropertyBlock block)>();
            try
            {
                foreach(var renderer in ToolSandbox.Root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if(renderer.sharedMaterial==null||!renderer.sharedMaterial.HasProperty("_AnimationTime"))continue;
                    var before=new MaterialPropertyBlock();renderer.GetPropertyBlock(before);snapshots.Add((renderer,before));
                    var current=new MaterialPropertyBlock();renderer.GetPropertyBlock(current);current.SetFloat("_AnimationTime",time);renderer.SetPropertyBlock(current);
                }
                if(snapshots.Count==0)throw new InvalidOperationException("No animated water material is present for capture.");
                return ToolSandbox.Capture(name);
            }
            finally{foreach(var entry in snapshots)if(entry.renderer!=null)entry.renderer.SetPropertyBlock(entry.block);}
        }
    }
}
