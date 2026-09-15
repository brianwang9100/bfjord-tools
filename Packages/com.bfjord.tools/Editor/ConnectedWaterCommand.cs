using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Bwork.Authoring.WaterSandbox;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace Bwork.Authoring.Editor
{
    public static class ConnectedWaterCommand
    {
        const string GroupName="Connected Water Sample";
        static string ReceiptPath=>ToolSandbox.Generated+"/connected-water-edit.json";
        [Serializable] sealed class Receipt {public int schemaVersion=1;public string recipeHash,recipeJson;public TerrainPatchChange patch;public string[] assets,cleanupAssets;public bool removed;}

        [CliCommand("bwork_water_connected","Author one connected tributary/lake/delta/ocean sandbox surface.",MainThreadRequired=true)]
        public static object Run(
            [CliArg("action","prepare, apply, refresh-appearance, status, remove")]string action="status",
            [CliArg("recipePath","JSON path; empty uses the connected-water package sample")]string recipePath="")
        {
            var terrain=ToolSandbox.RequireTerrain();var prior=ReadReceipt();
            if(action=="status")return new{installed=ToolSandbox.Root.Find(GroupName)!=null,recipeHash=prior?.recipeHash,changedCells=prior?.patch.Count??0,removed=prior?.removed??false,
                cleanupPending=prior==null?0:(prior.removed?prior.assets.Concat(prior.cleanupAssets):prior.cleanupAssets)
                    .Count(p=>File.Exists(p)||File.Exists(p+".meta"))};
            if((action=="apply"||action=="remove")&&Rocks.RockCommand.HasRiverRocks())
                throw new InvalidOperationException("River rocks depend on this water. Use bwork_river_scene for the bundled river batches; remove custom water-aware bwork_rocks batches before changing their water.");
            if(action=="remove")return Remove(terrain,prior);
            if(action=="refresh-appearance")return RefreshAppearance(prior,recipePath);
            if(action!="prepare"&&action!="apply")throw new ArgumentException("Unknown connected-water action.");
            if(prior?.removed==true)throw new InvalidOperationException("Complete pending connected-water removal with action=remove first.");
            if(ToolSandbox.Root.Find("Water Sample")!=null||File.Exists(ToolSandbox.Generated+"/water-edit.json"))
                throw new InvalidOperationException("Remove the original water sample with bwork_water action=remove before applying the connected sample.");
            if(ToolSandbox.Root.Find(GroupName)!=null&&prior==null)throw new InvalidDataException("Connected water has no restoration receipt; preserve the scene for recovery.");
            string path=string.IsNullOrWhiteSpace(recipePath)?ToolSandbox.SamplePath("connected-water.json"):Path.GetFullPath(recipePath);
            string json=File.ReadAllText(path);var recipe=ParseRecipe(json);
            var field=new ConnectedWaterField(recipe);var shader=RequireShader();var maps=RequireMaps(shader);Mesh mesh=null;
            try
            {
                mesh=field.BuildMesh();int n=terrain.terrainData.heightmapResolution;
                var current=terrain.terrainData.GetHeights(0,0,n,n);var original=prior?.patch.RestoredCopy(terrain)??(float[,])current.Clone();
                var carved=field.Carve(terrain,original);var ownership=TerrainPatchChange.Create(terrain,original,carved);
                int vertices=mesh.vertexCount,triangles=mesh.triangles.Length/3;
                if(action=="prepare")return new{applied=false,nodes=recipe.nodes.Length,reaches=recipe.reaches.Length,vertices,triangles,changedCells=ownership.Count};
                string hash=AppearanceHash(json,shader,maps);
                Apply(terrain,field,ref mesh,TerrainPatchChange.Create(terrain,current,carved),new Receipt{recipeHash=hash,recipeJson=json,patch=ownership});
                return new{applied=true,nodes=recipe.nodes.Length,reaches=recipe.reaches.Length,vertices,triangles,changedCells=ownership.Count,
                    surface="One welded river/lake/delta/ocean union; no stacked water meshes."};
            }
            finally{if(mesh!=null)Object.DestroyImmediate(mesh);}
        }
        /// <summary>Construct once per authoring operation; legacy receipts have no reconstructable field.</summary>
        public static ConnectedWaterField ActiveField()
        {
            var receipt=ReadReceipt();
            return receipt==null||receipt.removed||string.IsNullOrEmpty(receipt.recipeJson)||ToolSandbox.Root.Find(GroupName)==null
                ?null:new ConnectedWaterField(ParseRecipe(receipt.recipeJson));
        }
        static ConnectedWaterRecipe ParseRecipe(string json)
        {
            var recipe=new ConnectedWaterRecipe();
            JsonUtility.FromJsonOverwrite(json,recipe);
            return recipe;
        }
        static string AppearanceHash(string json,Shader shader,Texture2D[] maps)
        {
            string inputs=json+"\n"+File.ReadAllText(PhysicalAssetPath(shader));
            foreach(var map in maps)using(var sha=SHA256.Create())inputs+="\n"+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(PhysicalAssetPath(map))));
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(inputs))).Replace("-","").ToLowerInvariant();
        }
        static object RefreshAppearance(Receipt receipt,string recipePath)
        {
            var group=ToolSandbox.Root.Find(GroupName);
            if(receipt==null||receipt.removed||group==null||string.IsNullOrEmpty(receipt.recipeJson))
                throw new InvalidOperationException("Apply connected water before refreshing its appearance.");
            var installed=ParseRecipe(receipt.recipeJson);
            string path=string.IsNullOrWhiteSpace(recipePath)?ToolSandbox.SamplePath("connected-water.json"):Path.GetFullPath(recipePath);
            var requested=ParseRecipe(File.ReadAllText(path));
            // Only shading properties may change without rebuilding mesh bounds and bank gradients.
            foreach(string name in new[]{"flowSpeed","normalStrength","foamStrength","rippleTileSize","detailTileSize","detailStrength",
                "smoothness","oceanSmoothness","depthColorDistance","shallowOpacity","deepOpacity","shoreFadeDepth",
                "foamWidth","foamTileSize","foamCutoff","crestFoamStrength","shallowColor","deepColor","oceanShallowColor","oceanDeepColor"})
            {
                var field=typeof(ConnectedWaterRecipe).GetField(name);field.SetValue(installed,field.GetValue(requested));
            }
            _=new ConnectedWaterField(installed); // Validate values; no mesh generation, carving or painting.
            var shader=RequireShader();var maps=RequireMaps(shader);
            var renderer=group.GetComponentInChildren<MeshRenderer>();
            var material=AssetDatabase.LoadAssetAtPath<Material>(receipt.assets[1]);
            if(renderer==null||material==null||renderer.sharedMaterial!=material)
                throw new InvalidDataException("Connected-water material differs from its owned receipt.");
            string json=JsonUtility.ToJson(installed,true),hash=AppearanceHash(json,shader,maps);
            var priorBytes=WaterGeneration.Snapshot(ReceiptPath);var snapshot=new Material(material);var updated=Material(shader,installed);
            bool saveAttempted=false;
            try
            {
                material.shader=shader;material.CopyPropertiesFromMaterial(updated);EditorUtility.SetDirty(material);
                receipt.recipeJson=json;receipt.recipeHash=hash;
                WaterGeneration.Publish(ReceiptPath,JsonUtility.ToJson(receipt,true));
                saveAttempted=true;ToolSandbox.Save();
            }
            catch(Exception error)
            {
                var errors=new List<Exception>{error};
                WaterGeneration.Attempt(()=>{material.shader=snapshot.shader;material.CopyPropertiesFromMaterial(snapshot);EditorUtility.SetDirty(material);},errors);
                WaterGeneration.Attempt(()=>WaterGeneration.RestoreReceipt(ReceiptPath,priorBytes),errors);
                if(saveAttempted)WaterGeneration.Attempt(ToolSandbox.Save,errors);
                throw new AggregateException("Water appearance refresh failed; the original material and receipt were restored where possible.",errors);
            }
            finally{Object.DestroyImmediate(snapshot);Object.DestroyImmediate(updated);}
            return new{refreshed=true,recipeHash=hash,meshRebuilt=false,terrainChanged=false};
        }
        static Shader RequireShader()
        {
            var shader=Shader.Find("Bwork/Sandbox/Connected Wave Water");
            if(shader==null||!shader.isSupported)throw new InvalidOperationException("Import and compile the connected wave shader first.");
            return shader;
        }
        static string PhysicalAssetPath(Object asset)
        {
            string path=AssetDatabase.GetAssetPath(asset);
            var package=UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
            return package==null?Path.GetFullPath(path):Path.Combine(package.resolvedPath,path.Substring(package.assetPath.Length+1));
        }
        static Texture2D[] RequireMaps(Shader shader)
        {
            string root=Path.GetDirectoryName(Path.GetDirectoryName(AssetDatabase.GetAssetPath(shader))).Replace('\\','/');
            return new[]{"water-ripple-normal.png","water-detail-normal.png","water-foam.png","river-motion.png","ocean-motion.png"}.Select(name=>
            {
                string path=root+"/Textures/Water/"+name;
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(texture==null||importer==null||importer.sRGBTexture||!importer.mipmapEnabled||importer.textureType!=TextureImporterType.Default||
                    importer.wrapMode!=TextureWrapMode.Repeat||importer.maxTextureSize>1024)
                    throw new InvalidDataException("Water requires the bounded linear RGB, repeating mipmapped package texture: "+path);
                return texture;
            }).ToArray();
        }
        static Receipt ReadReceipt()
        {
            if(!File.Exists(ReceiptPath))return null;
            var receipt=JsonUtility.FromJson<Receipt>(File.ReadAllText(ReceiptPath));
            if(receipt==null||receipt.schemaVersion!=1||receipt.patch==null||string.IsNullOrEmpty(receipt.recipeHash))
                throw new InvalidDataException("Invalid connected-water restoration receipt.");
            receipt.assets=WaterGeneration.Owned(receipt.assets,receipt.recipeHash,true);
            receipt.cleanupAssets=WaterGeneration.Validate(receipt.cleanupAssets,true);
            return receipt;
        }
        static void Apply(Terrain terrain,ConnectedWaterField field,ref Mesh mesh,TerrainPatchChange transition,Receipt receipt)
        {
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var cameras=ToolSandbox.Root.GetComponentsInChildren<Camera>(true);
            if(pipeline==null||!pipeline.supportsCameraDepthTexture||cameras.Length==0||cameras.Any(c=>!c.GetUniversalAdditionalCameraData().requiresDepthTexture))
                throw new InvalidOperationException("The connected shader requires the sandbox camera's current URP depth copy.");
            var shader=RequireShader();
            var prior=ReadReceipt();var priorBytes=WaterGeneration.Snapshot(ReceiptPath);var paintSnapshot=new TerrainPresentation.Snapshot(terrain);
            var held=new WaterGeneration.HeldRoot(ToolSandbox.Root.Find(GroupName));
            receipt.assets=WaterGeneration.Paths(Guid.NewGuid().ToString("N"),true);
            receipt.cleanupAssets=WaterGeneration.Obsolete(prior?.assets,prior?.cleanupAssets,true);
            var created=new List<string>();var pending=new GameObject(GroupName+" Pending");
            pending.SetActive(false);pending.transform.SetParent(ToolSandbox.Root,false);
            bool changed=false,publicationAttempted=false,saveAttempted=false;
            try
            {
                var savedMesh=WaterGeneration.Create(mesh,receipt.assets[0],created);mesh=null;
                var material=WaterGeneration.Create(Material(shader,field.Recipe),receipt.assets[1],created);
                var surface=new GameObject("Tributaries lake delta and ocean",typeof(MeshFilter),typeof(MeshRenderer));surface.transform.SetParent(pending.transform,false);
                surface.GetComponent<MeshFilter>().sharedMesh=savedMesh;
                var renderer=surface.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
                held.Park();transition.Apply(terrain);changed=true;pending.name=GroupName;pending.SetActive(true);
                TerrainPresentation.Paint(terrain,field);
                publicationAttempted=true;WaterGeneration.Publish(ReceiptPath,JsonUtility.ToJson(receipt,true));
                saveAttempted=true;ToolSandbox.Save();
            }
            catch(Exception error)
            {
                var errors=new List<Exception>{error};if(changed)WaterGeneration.Attempt(()=>transition.Restore(terrain),errors);
                WaterGeneration.Attempt(()=>paintSnapshot.Restore(terrain),errors);
                WaterGeneration.Attempt(()=>Object.DestroyImmediate(pending),errors);WaterGeneration.Attempt(held.Restore,errors);
                if(publicationAttempted)WaterGeneration.Attempt(()=>WaterGeneration.RestoreReceipt(ReceiptPath,priorBytes),errors);
                if(saveAttempted)WaterGeneration.Attempt(ToolSandbox.Save,errors);
                if(errors.Count==1)WaterGeneration.Attempt(()=>WaterGeneration.Cleanup(created.ToArray(),true),errors);
                else errors.Add(new IOException("Rollback is incomplete; preserve the attempted generation for recovery: "+string.Join(", ",created)));
                throw new AggregateException("Connected-water publication failed; rollback errors are included when present.",errors);
            }
            held.Release();WaterGeneration.Cleanup(receipt.cleanupAssets,true);
        }
        static Material Material(Shader shader,ConnectedWaterRecipe r)
        {
            var maps=RequireMaps(shader);
            var material=new Material(shader){name="Connected river lake and ocean"};
            material.SetTexture("_RippleNormal",maps[0]);material.SetTexture("_DetailNormal",maps[1]);material.SetTexture("_FoamMap",maps[2]);
            material.SetTexture("_RiverMotionMap",maps[3]);material.SetTexture("_OceanMotionMap",maps[4]);
            material.SetColor("_BaseColor",r.deepColor);material.SetColor("_ShallowColor",r.shallowColor);
            material.SetColor("_OceanBaseColor",r.oceanDeepColor);material.SetColor("_OceanShallowColor",r.oceanShallowColor);
            material.SetFloat("_LakeWaveHeight",r.lakeWaveHeight);material.SetFloat("_LakeWaveLength",r.lakeWaveLength);material.SetFloat("_LakeWaveSpeed",r.lakeWaveSpeed);
            material.SetFloat("_RippleTileSize",r.rippleTileSize);material.SetFloat("_DetailTileSize",r.detailTileSize);material.SetFloat("_DetailStrength",r.detailStrength);
            material.SetFloat("_Smoothness",r.smoothness);material.SetFloat("_OceanSmoothness",r.oceanSmoothness);material.SetFloat("_DepthColorDistance",r.depthColorDistance);
            material.SetFloat("_ShallowOpacity",r.shallowOpacity);material.SetFloat("_DeepOpacity",r.deepOpacity);material.SetFloat("_ShoreFadeDepth",r.shoreFadeDepth);
            material.SetFloat("_FoamWidth",r.foamWidth);material.SetFloat("_FoamTileSize",r.foamTileSize);material.SetFloat("_FoamCutoff",r.foamCutoff);material.SetFloat("_CrestFoamStrength",r.crestFoamStrength);
            material.SetFloat("_WaveHeight",r.riverWaveHeight);material.SetFloat("_WaveLength",r.riverWaveLength);material.SetFloat("_WaveSpeed",r.riverWaveSpeed);
            material.SetFloat("_OceanWaveHeight",r.oceanWaveHeight);material.SetFloat("_OceanWaveLength",r.oceanWaveLength);material.SetFloat("_OceanWaveSpeed",r.oceanWaveSpeed);
            material.SetFloat("_FlowSpeed",r.flowSpeed);material.SetFloat("_NormalStrength",r.normalStrength);material.SetFloat("_FoamStrength",r.foamStrength);material.SetFloat("_UseDepth",1);
            return material;
        }
        static object Remove(Terrain terrain,Receipt receipt)
        {
            var group=ToolSandbox.Root.Find(GroupName);
            if(receipt==null)
            {
                if(group!=null)throw new InvalidDataException("Connected water has no restoration receipt.");
                return new{removed=false,changedCells=0};
            }
            var obsolete=WaterGeneration.Obsolete(receipt.assets,receipt.cleanupAssets,true);
            if(!receipt.removed)
            {
                var priorBytes=WaterGeneration.Snapshot(ReceiptPath);var paintSnapshot=new TerrainPresentation.Snapshot(terrain);var held=new WaterGeneration.HeldRoot(group);
                bool changed=false,publicationAttempted=false,saveAttempted=false;
                try
                {
                    held.Park();receipt.patch.Restore(terrain);changed=true;receipt.removed=true;
                    TerrainPresentation.Paint(terrain);
                    publicationAttempted=true;WaterGeneration.Publish(ReceiptPath,JsonUtility.ToJson(receipt,true));
                    saveAttempted=true;ToolSandbox.Save();
                }
                catch(Exception error)
                {
                    var errors=new List<Exception>{error};if(changed)WaterGeneration.Attempt(()=>receipt.patch.Apply(terrain),errors);
                    WaterGeneration.Attempt(()=>paintSnapshot.Restore(terrain),errors);
                    WaterGeneration.Attempt(held.Restore,errors);
                    if(publicationAttempted)WaterGeneration.Attempt(()=>WaterGeneration.RestoreReceipt(ReceiptPath,priorBytes),errors);
                    if(saveAttempted)WaterGeneration.Attempt(ToolSandbox.Save,errors);
                    throw new AggregateException("Connected-water removal failed; rollback errors are included when present.",errors);
                }
                held.Release();
            }
            else if(group!=null)throw new InvalidDataException("Removed connected-water receipt still has a live group; preserve the scene for recovery.");
            WaterGeneration.Cleanup(obsolete,true);WaterGeneration.RestoreReceipt(ReceiptPath,null);
            return new{removed=true,changedCells=receipt.patch.Count};
        }
    }
}
