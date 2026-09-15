using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Bwork.Authoring.WaterSandbox;
using Object = UnityEngine.Object;

namespace Bwork.Authoring.Editor
{
    public static class WaterCommand
    {
        const string GroupName = "Water Sample";
        static string ReceiptPath => ToolSandbox.Generated + "/water-edit.json";
        [Serializable] sealed class Receipt { public string recipeHash; public TerrainPatchChange patch; public string[] assets, cleanupAssets; public bool removed; }
        sealed class Prepared : IDisposable
        {
            public RiverRecipe recipe;
            public string hash;
            public Mesh river, ocean;
            public float[,] original, current, carved;
            public TerrainPatchChange transition, ownership;
            public void Dispose() { if (river != null) Object.DestroyImmediate(river); if (ocean != null) Object.DestroyImmediate(ocean); }
        }

        [CliCommand("bwork_water", "Prepare, apply, inspect or remove the bounded river/ocean sample.", MainThreadRequired = true)]
        public static object Run(
            [CliArg("action", "prepare, apply, status, remove")] string action = "status",
            [CliArg("recipePath", "JSON path; empty uses the packaged meander sample")] string recipePath = "")
        {
            var terrain = ToolSandbox.RequireTerrain();
            if (action == "status")
            {
                var receipt = ReadReceipt();
                return new { installed = ToolSandbox.Root.Find(GroupName) != null, recipeHash = receipt?.recipeHash,
                    changedCells = receipt?.patch?.Count ?? 0, removed = receipt?.removed ?? false,
                    cleanupPending = receipt == null ? 0 : (receipt.removed ? receipt.assets.Concat(receipt.cleanupAssets) : receipt.cleanupAssets)
                        .Count(p => File.Exists(p) || File.Exists(p+".meta")), scope = "Sandbox river and ocean; confluence unions and waterfall authoring remain separate." };
            }
            if (action == "remove") return Remove(terrain);
            if (action != "prepare" && action != "apply") throw new ArgumentException("Unknown water action.");
            if (ToolSandbox.Root.Find("Connected Water Sample") != null || File.Exists(ToolSandbox.Generated + "/connected-water-edit.json"))
                throw new InvalidOperationException("Remove the connected water sample with bwork_water_connected action=remove first.");
            using (var prepared = Prepare(terrain, recipePath))
            {
                if (action == "prepare") return new { recipeHash = prepared.hash, riverVertices = prepared.river.vertexCount,
                    oceanVertices = prepared.ocean.vertexCount, changedCells = prepared.ownership.Count, applied = false };
                Apply(terrain, prepared);
                return new { recipeHash = prepared.hash, changedCells = prepared.ownership.Count, applied = true,
                    group = GroupName, scope = "Carved meander and ocean material samples; final mouth blending remains a separate review." };
            }
        }

        static Receipt ReadReceipt()
        {
            if (!File.Exists(ReceiptPath)) return null;
            var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(ReceiptPath));
            if (receipt == null || receipt.patch == null || string.IsNullOrEmpty(receipt.recipeHash))
                throw new InvalidDataException("Invalid water terrain receipt; preserve it for recovery.");
            receipt.assets=WaterGeneration.Owned(receipt.assets,receipt.recipeHash,false);
            receipt.cleanupAssets=WaterGeneration.Validate(receipt.cleanupAssets,false);
            return receipt;
        }

        static Prepared Prepare(Terrain terrain, string path)
        {
            path = string.IsNullOrWhiteSpace(path) ? ToolSandbox.SamplePath("meander.json") : Path.GetFullPath(path);
            var json = File.ReadAllText(path); var recipe = JsonUtility.FromJson<RiverRecipe>(json);
            if (recipe == null || recipe.knots == null) throw new InvalidDataException("A river recipe is required.");
            Range(recipe.widthScale,.25f,4,"widthScale"); Range(recipe.bedDepth,.3f,8,"bedDepth");
            Range(recipe.bankFalloff,4,60,"bankFalloff"); Range(recipe.flowSpeed,0,4,"flowSpeed");
            Range(recipe.waveLength,4,80,"waveLength"); Range(recipe.waveSpeed,0,3,"waveSpeed");
            Range(recipe.oceanWaveHeight,0,1,"oceanWaveHeight"); Range(recipe.oceanWaveLength,4,80,"oceanWaveLength");
            Range(recipe.oceanWaveSpeed,0,3,"oceanWaveSpeed");
            foreach (var knot in recipe.knots)
            {
                if (knot == null) throw new InvalidDataException("River knot is missing.");
                knot.width *= recipe.widthScale;
            }
            var result = new Prepared { recipe = recipe };
            try
            {
                using (var sha = SHA256.Create()) result.hash = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json))).Replace("-", "").ToLowerInvariant();
                var data = terrain.terrainData; int n = data.heightmapResolution;
                result.current = data.GetHeights(0,0,n,n);
                var prior = ReadReceipt();
                if (prior?.removed == true) throw new InvalidOperationException("Complete the pending water removal with action=remove first.");
                if (ToolSandbox.Root.Find(GroupName) != null && prior == null)
                    throw new InvalidDataException("Water group has no terrain restoration receipt; preserve the scene for recovery.");
                result.original = prior?.patch.RestoredCopy(terrain) ?? (float[,])result.current.Clone();
                result.river = BworkRiverRibbon.Build(recipe);
                if (result.river.vertexCount/(recipe.crossSegments+1)>4096)
                    throw new ArgumentException("The sandbox river exceeds the finite carving row budget.");
                result.ocean = BworkRiverRibbon.BuildOcean(new Vector3(256,.8f,438),new Vector2(540,220),2,recipe.oceanWaveHeight);
                result.carved = Carve(terrain,result.original,result.river,recipe);
                result.transition = TerrainPatchChange.Create(terrain,result.current,result.carved);
                result.ownership = TerrainPatchChange.Create(terrain,result.original,result.carved);
                return result;
            }
            catch { result.Dispose(); throw; }
        }

        static void Range(float value,float min,float max,string field)
        { if (!float.IsFinite(value) || value < min || value > max) throw new ArgumentOutOfRangeException(field); }

        static float[,] Carve(Terrain terrain,float[,] original,Mesh river,RiverRecipe recipe)
        {
            var result = (float[,])original.Clone(); var vertices = river.vertices;
            int stride = recipe.crossSegments+1, rows = vertices.Length/stride;
            var centers = new Vector3[rows]; var widths = new float[rows];
            for (int i=0;i<rows;i++)
            {
                centers[i]=(vertices[i*stride]+vertices[i*stride+stride-1])*.5f;
                widths[i]=Vector3.Distance(vertices[i*stride],vertices[i*stride+stride-1])*.5f;
            }
            var origin=terrain.transform.position;var size=terrain.terrainData.size;int n=original.GetLength(0);
            var bounds=river.bounds;bounds.Expand(new Vector3(recipe.bankFalloff*2,0,recipe.bankFalloff*2));
            int x0=Mathf.Clamp(Mathf.FloorToInt((bounds.min.x-origin.x)/size.x*(n-1)),0,n-1);
            int x1=Mathf.Clamp(Mathf.CeilToInt((bounds.max.x-origin.x)/size.x*(n-1)),0,n-1);
            int z0=Mathf.Clamp(Mathf.FloorToInt((bounds.min.z-origin.z)/size.z*(n-1)),0,n-1);
            int z1=Mathf.Clamp(Mathf.CeilToInt((bounds.max.z-origin.z)/size.z*(n-1)),0,n-1);
            // Finite local channel lowering; no terrain is raised to manufacture an aqueduct.
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
            {
                var point=new Vector2(origin.x+x*size.x/(n-1),origin.z+z*size.z/(n-1));
                float best=float.PositiveInfinity,level=0,radius=0;
                for(int i=0;i<rows-1;i++)
                {
                    var a=new Vector2(centers[i].x,centers[i].z);var b=new Vector2(centers[i+1].x,centers[i+1].z);var delta=b-a;
                    float t=Mathf.Clamp01(Vector2.Dot(point-a,delta)/delta.sqrMagnitude);
                    float distance=Vector2.Distance(point,a+t*delta);
                    if(distance>=best)continue;
                    best=distance;level=Mathf.Lerp(centers[i].y,centers[i+1].y,t);radius=Mathf.Lerp(widths[i],widths[i+1],t);
                }
                if(best>radius+recipe.bankFalloff)continue;
                float before=origin.y+original[z,x]*size.y,target;
                if(best<=radius)
                {
                    float fraction=best/radius;
                    target=level-recipe.bedDepth*(1-fraction*fraction);
                }
                else target=Mathf.Lerp(level,before,Mathf.SmoothStep(0,1,(best-radius)/recipe.bankFalloff));
                result[z,x]=Mathf.Clamp01((Mathf.Min(before,target)-origin.y)/size.y);
            }
            return result;
        }

        static Material WaterMaterial(RiverRecipe recipe,bool ocean)
        {
            var shader=Shader.Find("Bwork/Sandbox/Wave Water") ?? throw new InvalidOperationException("The sandbox wave shader is not imported.");
            if(!shader.isSupported)throw new InvalidOperationException("The sandbox wave shader is unsupported; inspect shader compilation errors.");
            var material=new Material(shader){name=ocean?"Ocean waves":"River flow"};
            material.SetColor("_BaseColor",ocean?new Color(.035f,.095f,.125f):new Color(.045f,.135f,.135f));
            material.SetColor("_ShallowColor",ocean?new Color(.11f,.21f,.23f):new Color(.15f,.26f,.23f));
            material.SetFloat("_WaveHeight",ocean?recipe.oceanWaveHeight:recipe.maximumWaveHeight);
            material.SetFloat("_WaveLength",ocean?recipe.oceanWaveLength:recipe.waveLength);
            material.SetFloat("_WaveSpeed",ocean?recipe.oceanWaveSpeed:recipe.waveSpeed);
            material.SetFloat("_FlowSpeed",ocean ? .3f : recipe.flowSpeed);
            material.SetFloat("_NormalStrength",ocean ? .035f : .05f);
            material.SetFloat("_FoamStrength",ocean ? .1f : .2f);material.SetFloat("_Smoothness",ocean ? .9f : .88f);
            material.SetFloat("_UseDepth",1);
            return material;
        }

        static void Apply(Terrain terrain,Prepared prepared)
        {
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var cameras=ToolSandbox.Root.GetComponentsInChildren<Camera>(true);
            if(pipeline==null||!pipeline.supportsCameraDepthTexture||cameras.Length==0||cameras.Any(c=>!c.GetUniversalAdditionalCameraData().requiresDepthTexture))
                throw new InvalidOperationException("Water depth shading requires the sandbox URP depth copy and depth-enabled cameras.");
            var prior=ReadReceipt();var priorBytes=WaterGeneration.Snapshot(ReceiptPath);
            var held=new WaterGeneration.HeldRoot(ToolSandbox.Root.Find(GroupName));
            var paths=WaterGeneration.Paths(Guid.NewGuid().ToString("N"),false);var created=new List<string>();
            var receipt=new Receipt{recipeHash=prepared.hash,patch=prepared.ownership,assets=paths,
                cleanupAssets=WaterGeneration.Obsolete(prior?.assets,prior?.cleanupAssets,false)};
            var pending=new GameObject(GroupName+" Pending");pending.SetActive(false);pending.transform.SetParent(ToolSandbox.Root,false);
            bool changed=false,publicationAttempted=false,saveAttempted=false;
            try
            {
                var river=WaterGeneration.Create(prepared.river,paths[0],created);prepared.river=null;
                var ocean=WaterGeneration.Create(prepared.ocean,paths[1],created);prepared.ocean=null;
                var riverMaterial=WaterGeneration.Create(WaterMaterial(prepared.recipe,false),paths[2],created);
                var oceanMaterial=WaterGeneration.Create(WaterMaterial(prepared.recipe,true),paths[3],created);
                Add(pending.transform,"Carved meander",river,riverMaterial);Add(pending.transform,"Ocean wave sample",ocean,oceanMaterial);
                held.Park();prepared.transition.Apply(terrain);changed=true;
                pending.name=GroupName;pending.SetActive(true);
                publicationAttempted=true;WaterGeneration.Publish(ReceiptPath,JsonUtility.ToJson(receipt,true));
                saveAttempted=true;ToolSandbox.Save();
            }
            catch(Exception error)
            {
                var errors=new List<Exception>{error};
                if(changed)WaterGeneration.Attempt(()=>prepared.transition.Restore(terrain),errors);
                WaterGeneration.Attempt(()=>Object.DestroyImmediate(pending),errors);WaterGeneration.Attempt(held.Restore,errors);
                if(publicationAttempted)WaterGeneration.Attempt(()=>WaterGeneration.RestoreReceipt(ReceiptPath,priorBytes),errors);
                if(saveAttempted)WaterGeneration.Attempt(ToolSandbox.Save,errors);
                if(errors.Count==1)WaterGeneration.Attempt(()=>WaterGeneration.Cleanup(created.ToArray(),false),errors);
                else errors.Add(new IOException("Rollback is incomplete; preserve the attempted generation for recovery: "+string.Join(", ",created)));
                throw new AggregateException("Water publication failed; rollback errors are included when present.",errors);
            }
            held.Release();WaterGeneration.Cleanup(receipt.cleanupAssets,false);
        }

        static void Add(Transform parent,string name,Mesh mesh,Material material)
        {
            var child=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));child.transform.SetParent(parent,false);
            child.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=child.GetComponent<MeshRenderer>();
            renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        static object Remove(Terrain terrain)
        {
            var receipt=ReadReceipt();var group=ToolSandbox.Root.Find(GroupName);
            if(receipt==null)
            {
                if(group!=null)throw new InvalidDataException("Water group has no restoration receipt; preserve the scene for recovery.");
                return new {removed=false,changedCells=0};
            }
            var obsolete=WaterGeneration.Obsolete(receipt.assets,receipt.cleanupAssets,false);
            if(!receipt.removed)
            {
                var priorBytes=WaterGeneration.Snapshot(ReceiptPath);var held=new WaterGeneration.HeldRoot(group);
                bool changed=false,publicationAttempted=false,saveAttempted=false;
                try
                {
                    held.Park();receipt.patch.Restore(terrain);changed=true;
                    receipt.removed=true;publicationAttempted=true;WaterGeneration.Publish(ReceiptPath,JsonUtility.ToJson(receipt,true));
                    saveAttempted=true;ToolSandbox.Save();
                }
                catch(Exception error)
                {
                    var errors=new List<Exception>{error};if(changed)WaterGeneration.Attempt(()=>receipt.patch.Apply(terrain),errors);
                    WaterGeneration.Attempt(held.Restore,errors);
                    if(publicationAttempted)WaterGeneration.Attempt(()=>WaterGeneration.RestoreReceipt(ReceiptPath,priorBytes),errors);
                    if(saveAttempted)WaterGeneration.Attempt(ToolSandbox.Save,errors);
                    throw new AggregateException("Water removal failed; rollback errors are included when present.",errors);
                }
                held.Release();
            }
            else if(group!=null)throw new InvalidDataException("Removed water receipt still has a live group; preserve the scene for recovery.");
            WaterGeneration.Cleanup(obsolete,false);WaterGeneration.RestoreReceipt(ReceiptPath,null);
            return new {removed=true,changedCells=receipt.patch.Count};
        }
    }
    // Shared only by the two water wrappers; recipes and Terrain ownership stay in those commands.
    internal static class WaterGeneration
    {
        public static string[] Paths(string key,bool connected) => connected
            ? new[]{ToolSandbox.Generated+"/ConnectedWater-"+key+".asset",ToolSandbox.Generated+"/ConnectedWater-"+key+".mat"}
            : new[]{ToolSandbox.Generated+"/WaterRiver-"+key+".asset",ToolSandbox.Generated+"/WaterOcean-"+key+".asset",
                ToolSandbox.Generated+"/WaterRiver-"+key+".mat",ToolSandbox.Generated+"/WaterOcean-"+key+".mat"};

        public static string[] Owned(string[] recorded,string hash,bool connected)
        {
            if(hash==null||hash.Length!=64||hash.Any(c=>!"0123456789abcdef".Contains(c)))throw new InvalidDataException("Invalid water recipe hash.");
            // Legacy receipts predate asset ownership; only their exact former generated names are admitted.
            var paths=recorded==null?Paths(hash.Substring(0,16),connected):recorded;
            if(paths.Length!=(connected?2:4))throw new InvalidDataException("Incomplete water generation assets.");
            return Validate(paths,connected);
        }
        public static string[] Validate(string[] paths,bool connected)
        {
            paths=paths??Array.Empty<string>();
            if(paths.Length>128||paths.Distinct(StringComparer.Ordinal).Count()!=paths.Length)throw new InvalidDataException("Invalid water cleanup ownership.");
            foreach(string path in paths)
            {
                if(path==null||!path.StartsWith(ToolSandbox.Generated+"/",StringComparison.Ordinal)||path.Contains('\\'))throw new InvalidDataException("Water asset escaped its generated directory.");
                string name=path.Substring(ToolSandbox.Generated.Length+1),extension=Path.GetExtension(name);
                string prefix=connected?"ConnectedWater-":name.StartsWith("WaterRiver-",StringComparison.Ordinal)?"WaterRiver-":"WaterOcean-";
                if(Path.GetFileName(name)!=name||!name.StartsWith(prefix,StringComparison.Ordinal)||(extension!=".asset"&&extension!=".mat"))throw new InvalidDataException("Unexpected owned water asset path.");
                string key=name.Substring(prefix.Length,name.Length-prefix.Length-extension.Length);
                if((key.Length!=16&&key.Length!=32)||key.Any(c=>!"0123456789abcdef".Contains(c)))throw new InvalidDataException("Unexpected water generation identity.");
            }
            return paths;
        }
        public static string[] Obsolete(string[] assets,string[] cleanup,bool connected) => Validate(
            (assets??Array.Empty<string>()).Concat(cleanup??Array.Empty<string>()).Distinct(StringComparer.Ordinal)
            .Where(p=>File.Exists(p)||File.Exists(p+".meta")).ToArray(),connected);

        public static T Create<T>(T value,string path,List<string> created) where T:Object
        {
            if(File.Exists(path)||File.Exists(path+".meta")||AssetDatabase.LoadMainAssetAtPath(path)!=null)
            {Object.DestroyImmediate(value);throw new IOException("Water generation path already exists: "+path);}
            created.Add(path); // CreateAsset may write bytes before it reports an import failure.
            try{AssetDatabase.CreateAsset(value,path);return value;}
            catch{if(value!=null&&!AssetDatabase.Contains(value))Object.DestroyImmediate(value);throw;}
        }
        public static void Cleanup(string[] paths,bool connected)
        {
            var errors=new List<Exception>();
            foreach(string path in Validate(paths,connected))Attempt(()=>
            {
                if((File.Exists(path)||File.Exists(path+".meta"))&&!AssetDatabase.DeleteAsset(path))throw new IOException("Could not delete owned water asset: "+path);
            },errors);
            if(errors.Count>0)throw new AggregateException("Water generation cleanup failed; the receipt retains obsolete paths for retry.",errors);
        }
        public static byte[] Snapshot(string path)=>File.Exists(path)?File.ReadAllBytes(path):null;
        public static void Publish(string path,string json)=>RestoreReceipt(path,Encoding.UTF8.GetBytes(json+"\n"));
        public static void RestoreReceipt(string path,byte[] bytes)
        {
            if(bytes==null)
            {
                if(!File.Exists(path)&&!File.Exists(path+".meta"))return;
                if(!AssetDatabase.DeleteAsset(path))
                {File.Delete(path);File.Delete(path+".meta");AssetDatabase.Refresh();}
            }
            else{File.WriteAllBytes(path,bytes);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);}
        }
        public static void Attempt(Action action,List<Exception> errors)
        {try{action();}catch(Exception error){errors.Add(error);}}

        public sealed class HeldRoot
        {
            readonly GameObject root;
            readonly string name;
            readonly bool active;
            readonly Dictionary<Object,HideFlags> flags=new Dictionary<Object,HideFlags>();
            public HeldRoot(Transform transform)
            {
                if(transform==null)return;root=transform.gameObject;name=root.name;active=root.activeSelf;
                foreach(var child in root.GetComponentsInChildren<Transform>(true))
                {
                    flags.Add(child.gameObject,child.gameObject.hideFlags);
                    foreach(var component in child.GetComponents<Component>())if(component!=null)flags.Add(component,component.hideFlags);
                }
            }
            public void Park()
            {
                if(root==null)return;root.SetActive(false);root.name=name+" Previous";
                // Every retained object is excluded from the new saved scene, but remains available for rollback.
                foreach(var item in flags)item.Key.hideFlags=item.Value|HideFlags.DontSaveInEditor;
            }
            public void Restore()
            {
                if(root==null)return;foreach(var item in flags)if(item.Key!=null)item.Key.hideFlags=item.Value;
                root.name=name;root.SetActive(active);
            }
            public void Release(){if(root!=null)Object.DestroyImmediate(root);}
        }
    }
}
