// SPDX-License-Identifier: MIT
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Bwork.Authoring.Editor.Rocks
{
    /// <summary>Nine admitted silhouettes on retained terrain, with a separate finite ownership receipt.</summary>
    public static class RockShowcaseCommand
    {
        const string Group="BFjord Boulder Showcase [owned:bwork_rock_showcase:v1]",ReceiptFile="rock-showcase.json";
        public static readonly string[] VariantIds={"granite_boulder","river_stone","scree_cluster","stratified_outcrop","cliff_slab","upright_crag","coastal_outcrop","river_ledge","fractured_boulder_cluster"};
        static readonly string[] Profiles={"granite","wet","granite","sandstone","basalt","mossy","granite","mossy","basalt"};
        static readonly float[] Scales={1.05f,1.25f,1.0f,.8f,.76f,.8f,.8f,1.05f,1.0f};
        [Serializable] public sealed class Placement {public string id,label,profile;public Vector3 position;public float yaw,scale,groundRelief;
            public object Report()=>new{id,label,profile,position=Coordinates(position),yaw,scale,groundRelief};}
        [Serializable] sealed class Receipt
        {public int schemaVersion=1,seed;public float centerX,centerZ;public string scene,sourceHash,rockHash,labelHash;public Placement[] placements;public Vector3 eye,target;public float orthographicSize;}
        [CliCommand("bwork_rock_showcase","Place, label, view or remove a seeded nine-variant boulder comparison on existing terrain.",MainThreadRequired=true)]
        public static object Run([CliArg("action","prepare, setup, status, view or remove")]string action="status",
            [CliArg("seed","Repeatable scale, yaw and slight spacing variation")]int seed=20260916,
            [CliArg("centerX","Comparison center in terrain world metres")]float centerX=80,
            [CliArg("centerZ","Comparison center in terrain world metres")]float centerZ=145)
        {
            var terrain=ToolSandbox.RequireTerrain();var roots=ToolSandbox.Root.Cast<Transform>().Where(t=>t.name==Group).ToArray();
            if(roots.Length>1)throw new InvalidDataException("Duplicate boulder showcase root; preserve the scene.");
            var old=roots.SingleOrDefault();var prior=Read();
            if((old==null)!=(prior==null))throw new InvalidDataException("Boulder showcase root/receipt disagree; no content will be replaced.");
            if(action=="status")return new{applied=prior!=null,seed=prior?.seed,sourceHash=prior?.sourceHash,placements=Reports(prior?.placements)};
            if(prior!=null)Validate(old,prior);
            if(action=="view")
            {
                if(prior==null)throw new InvalidOperationException("Set up the boulder showcase before framing it.");
                var camera=ToolSandbox.Root.GetComponentInChildren<Camera>()??throw new InvalidOperationException("Sandbox camera is missing.");
                camera.transform.SetPositionAndRotation(prior.eye,Quaternion.LookRotation(prior.target-prior.eye,Vector3.up));camera.orthographic=prior.orthographicSize>0;if(camera.orthographic)camera.orthographicSize=prior.orthographicSize;else camera.fieldOfView=45;ToolSandbox.Save();
                return new{view="nine-boulder-comparison",seed=prior.seed,cameraPosition=Coordinates(prior.eye),cameraTarget=Coordinates(prior.target),orthographicSize=prior.orthographicSize};
            }
            if(action=="remove")
            {
                if(prior==null)return new{removed=false};
                var held=new WaterGeneration.HeldRoot(old);held.Park();
                try{ToolSandbox.RestoreText(ReceiptFile,null);ToolSandbox.Save();}
                catch{held.Restore();Write(prior);throw;}
                held.Release();ToolSandbox.Save();return new{removed=true,seed=prior.seed,terrainChanged=false};
            }
            if(action!="prepare"&&action!="setup")throw new ArgumentException("Unknown boulder showcase action.");
            var source=RockContract.Admit(ProjectContext.Current.rockSourceRoot);
            RockContract.Require(RockLibrary.Ready(source),"Build the admitted nine-variant rock library with bwork_rocks action=build-assets first.");
            var data=terrain.terrainData;var origin=terrain.transform.position;
            RockContract.Require(terrain.transform.rotation==Quaternion.identity&&terrain.transform.lossyScale==Vector3.one,"Showcase requires ordinary unrotated, unit-scale Terrain.");
            float Ground(float x,float z)
            {
                float u=(x-origin.x)/data.size.x,v=(z-origin.z)/data.size.z;
                if(u<0||u>1||v<0||v>1||data.IsHole(Mathf.Clamp((int)(u*data.holesResolution),0,data.holesResolution-1),Mathf.Clamp((int)(v*data.holesResolution),0,data.holesResolution-1)))return float.NaN;
                return origin.y+data.GetInterpolatedHeight(u,v);
            }
            var exclusions=SpatialExclusions.Create(ToolSandbox.Root);
            var placements=Plan(source.manifest.variants,seed,centerX,centerZ,Ground,(p,r)=>ToolSandbox.Excluded(p,r)||exclusions.Intersects(p,r));
            if(action=="prepare")return new{prepared=true,seed,sourceHash=source.hash,placements=Reports(placements),terrainChanged=false};
            var next=new Receipt{scene=ToolSandbox.ScenePath,seed=seed,centerX=centerX,centerZ=centerZ,sourceHash=source.hash,placements=placements};
            next.target=new Vector3(centerX,placements.Average(p=>p.position.y)+2.2f,centerZ);next.eye=next.target+new Vector3(-6,52,-24);next.orthographicSize=21;
            float eyeGround=Ground(next.eye.x,next.eye.z);if(float.IsFinite(eyeGround))next.eye.y=Mathf.Max(next.eye.y,eyeGround+3);
            GameObject pending=null;var previous=new WaterGeneration.HeldRoot(old);bool published=false;
            try
            {
                pending=new GameObject(Group);pending.SetActive(false);pending.transform.SetParent(ToolSandbox.Root,false);
                var boulders=new GameObject("Boulders");boulders.transform.SetParent(pending.transform,false);
                var labels=new GameObject("Labels");labels.transform.SetParent(pending.transform,false);
                var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")??throw new InvalidOperationException("Unity built-in label font is unavailable.");
                for(int i=0;i<placements.Length;i++)
                {
                    var p=placements[i];var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(RockLibrary.PrefabPath(source,p.id))??throw new InvalidDataException("Missing showcase prefab: "+p.id);
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,boulders.transform);instance.name=(i+1).ToString("00")+" "+p.id;
                    instance.transform.SetPositionAndRotation(p.position,Quaternion.Euler(0,p.yaw,0));instance.transform.localScale=Vector3.one*p.scale;
                    var material=RockLibrary.MaterialForVariant(source,p.id,p.profile)??throw new InvalidDataException("Missing variant-aware rock material.");
                    foreach(var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))renderer.sharedMaterials=Enumerable.Repeat(material,renderer.sharedMaterials.Length).ToArray();
                    foreach(var collider in instance.GetComponentsInChildren<Collider>(true))collider.enabled=false;
                    // Comparison distance must not cull small stones; only this instance changes thresholds.
                    var lod=instance.GetComponent<LODGroup>();if(lod==null)throw new InvalidDataException("Showcase prefab has no LOD group.");
                    var levels=lod.GetLODs();if(levels.Length!=3)throw new InvalidDataException("Showcase expects the admitted three LODs.");
                    for(int level=0;level<3;level++)levels[level].screenRelativeTransitionHeight=new[]{.025f,.01f,.001f}[level];lod.SetLODs(levels);
                    var label=new GameObject((i+1).ToString("00")+" label");label.transform.SetParent(labels.transform,false);
                    var labelPosition=new Vector3(p.position.x,0,centerZ+(i/3-1)*14-4.6f);labelPosition.y=Ground(labelPosition.x,labelPosition.z)+1.1f;
                    if(!float.IsFinite(labelPosition.y))throw new InvalidDataException("Label leaves the supported terrain patch; relocate the showcase.");
                    label.transform.SetPositionAndRotation(labelPosition,Quaternion.LookRotation(next.target-next.eye,Vector3.up));
                    var text=label.AddComponent<TextMesh>();text.font=font;text.fontSize=64;text.characterSize=.09f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=Color.white;
                    text.text=(i+1).ToString("00")+"  "+p.label+"\n"+p.profile+"  "+p.scale.ToString("F2",CultureInfo.InvariantCulture)+"x";
                    var labelRenderer=label.GetComponent<MeshRenderer>();labelRenderer.sharedMaterial=font.material;labelRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;labelRenderer.receiveShadows=false;
                }
                pending.SetActive(true);next.rockHash=WaterfallCommand.HierarchyFingerprint(boulders.transform);next.labelHash=LabelsFingerprint(labels.transform);
                previous.Park();published=true;Write(next);ToolSandbox.Save();
            }
            catch
            {
                if(pending!=null)Object.DestroyImmediate(pending);previous.Restore();if(published){if(prior==null)ToolSandbox.RestoreText(ReceiptFile,null);else Write(prior);}throw;
            }
            previous.Release();ToolSandbox.Save();return new{applied=true,seed,sourceHash=source.hash,placements=Reports(placements),terrainChanged=false,collidersEnabled=false};
        }
        static float[] Coordinates(Vector3 value)=>new[]{value.x,value.y,value.z};
        static object[] Reports(Placement[] placements)=>placements?.Select(p=>p.Report()).ToArray();
        public static Placement[] Plan(RockVariant[] variants,int seed,float centerX,float centerZ,Func<float,float,float> ground,Func<Vector3,float,bool> excluded)
        {
            if(!float.IsFinite(centerX)||!float.IsFinite(centerZ)||variants==null||ground==null||excluded==null)throw new ArgumentException("Finite showcase center and terrain queries required.");
            var random=new System.Random(seed);var output=new Placement[9];
            for(int i=0;i<9;i++)
            {
                var variant=variants.SingleOrDefault(v=>v.id==VariantIds[i])??throw new InvalidDataException("Showcase requires all nine admitted silhouettes: "+VariantIds[i]);
                float scale=Scales[i]*(.9f+(float)random.NextDouble()*.2f),yaw=(float)random.NextDouble()*360;
                float x=centerX+(i%3-1)*14+((float)random.NextDouble()-.5f),z=centerZ+(i/3-1)*14+((float)random.NextDouble()-.5f);
                float radius=new Vector2(variant.Size.x,variant.Size.z).magnitude*.5f*scale;
                float minimum=float.PositiveInfinity,maximum=float.NegativeInfinity;
                for(int n=0;n<9;n++)
                {
                    double angle=n*Math.PI/4;float px=x+(n==8?0:(float)Math.Cos(angle)*radius),pz=z+(n==8?0:(float)Math.Sin(angle)*radius),height=ground(px,pz);
                    if(!float.IsFinite(height))throw new InvalidDataException("Boulder comparison leaves valid terrain; relocate centerX/centerZ.");minimum=Mathf.Min(minimum,height);maximum=Mathf.Max(maximum,height);
                }
                if(maximum-minimum>Mathf.Min(1.2f,variant.Size.y*scale*.45f))throw new InvalidDataException("Boulder comparison patch is too uneven at "+variant.id+"; relocate centerX/centerZ.");
                var position=new Vector3(x,minimum-Mathf.Clamp(variant.Size.y*scale*.08f,.08f,.32f),z);
                if(excluded(position,radius+.6f))throw new InvalidDataException("Boulder comparison overlaps a protected road, water or structure footprint; relocate centerX/centerZ.");
                output[i]=new Placement{id=variant.id,label=variant.displayName,profile=Profiles[i],scale=scale,yaw=yaw,position=position,groundRelief=maximum-minimum};
            }
            return output;
        }
        static void Validate(Transform root,Receipt receipt)
        {
            if(receipt.schemaVersion!=1||receipt.scene!=ToolSandbox.ScenePath||receipt.placements?.Length!=9||root.childCount!=2||root.GetComponents<Component>().Length!=1||root.localPosition!=Vector3.zero||root.localScale!=Vector3.one||Quaternion.Angle(root.localRotation,Quaternion.identity)>.001f||!root.gameObject.activeSelf)throw new InvalidDataException("Showcase root or receipt was edited; preserve foreign content.");
            var rocks=root.Find("Boulders");var labels=root.Find("Labels");
            if(rocks==null||labels==null||rocks.childCount!=9||WaterfallCommand.HierarchyFingerprint(rocks)!=receipt.rockHash||LabelsFingerprint(labels)!=receipt.labelHash)throw new InvalidDataException("Showcase hierarchy, labels or references changed; replacement/removal refused.");
        }
        static string LabelsFingerprint(Transform root)
        {
            if(root==null||root.childCount!=9||root.GetComponents<Component>().Length!=1)throw new InvalidDataException("Unexpected showcase label hierarchy.");
            var text=new StringBuilder();
            void Number(float value){if(!float.IsFinite(value))throw new InvalidDataException("Nonfinite label value.");text.Append(Math.Round(value,4).ToString("F4",CultureInfo.InvariantCulture)).Append(';');}
            void TransformState(Transform t){text.Append(t.name).Append('|').Append(t.gameObject.activeSelf).Append('|');foreach(float f in new[]{t.localPosition.x,t.localPosition.y,t.localPosition.z,t.localRotation.x,t.localRotation.y,t.localRotation.z,t.localRotation.w,t.localScale.x,t.localScale.y,t.localScale.z})Number(f);}
            TransformState(root);
            foreach(Transform child in root)
            {
                var label=child.GetComponent<TextMesh>();var renderer=child.GetComponent<MeshRenderer>();
                if(child.childCount!=0||child.GetComponents<Component>().Length!=3||label==null||renderer==null||label.font==null||renderer.sharedMaterials.Length!=1||renderer.sharedMaterial!=label.font.material)throw new InvalidDataException("Foreign or edited showcase label components.");
                TransformState(child);text.Append(label.text).Append('|').Append(label.font.name).Append('|').Append(label.fontSize).Append('|').Append(label.anchor).Append('|').Append(label.alignment).Append('|').Append(label.fontStyle).Append('|').Append(renderer.enabled).Append(renderer.shadowCastingMode).Append(renderer.receiveShadows);
                Number(label.characterSize);Number(label.lineSpacing);Number(label.offsetZ);Number(label.color.r);Number(label.color.g);Number(label.color.b);Number(label.color.a);
            }
            using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-","").ToLowerInvariant();
        }
        static Receipt Read()
        {var asset=AssetDatabase.LoadAssetAtPath<TextAsset>(ToolSandbox.Generated+"/"+ReceiptFile);if(asset==null)return null;return JsonUtility.FromJson<Receipt>(asset.text)??throw new InvalidDataException("Invalid boulder showcase receipt.");}
        static void Write(Receipt receipt)=>ToolSandbox.Persist(new TextAsset(JsonUtility.ToJson(receipt,true)),ReceiptFile);
    }
}
