using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace Bwork.Authoring.Editor
{
    /// <summary>Original fixed-size masonry facade; retained generated copies survive source removal.</summary>
    public static class TunnelDetail
    {
        const string Source="Assets/BFjord/TunnelDetail/";
        [Serializable] public sealed class Model {public int schemaVersion;public float width,clearance;public Level[] lods;}
        [Serializable] public sealed class Level {public Vector3[] vertices,normals;public Vector2[] uv;public int[] triangles;}
        public static Model Read()
        {
            string path=Source+"MasonryPortal.json";var file=new FileInfo(path);
            if(!file.Exists||file.Length>12*1024*1024)throw new InvalidDataException("Install the original TunnelDetail catalog assets before requesting masonry portals.");
            var model=JsonUtility.FromJson<Model>(File.ReadAllText(path));
            if(model==null||model.schemaVersion!=1||model.width!=8||model.clearance!=5.5f||model.lods==null||model.lods.Length!=3)throw new InvalidDataException("Invalid masonry portal dimensions/LOD contract.");
            foreach(var level in model.lods)
            {
                if(level?.vertices==null||level.vertices.Length<3||level.vertices.Length>60000||level.normals?.Length!=level.vertices.Length||level.uv?.Length!=level.vertices.Length||level.triangles==null||level.triangles.Length%3!=0||level.triangles.Length>90000)throw new InvalidDataException("Invalid bounded portal mesh arrays.");
                if(level.vertices.Any(v=>!Finite(v.x)||!Finite(v.y)||!Finite(v.z)||Math.Abs(v.x)>8||v.y<-.4f||v.y>8||Math.Abs(v.z)>2)||level.normals.Any(n=>!Finite(n.sqrMagnitude)||Math.Abs(n.sqrMagnitude-1)>.01f)||level.uv.Any(v=>!Finite(v.x)||!Finite(v.y))||level.triangles.Any(t=>t<0||t>=level.vertices.Length))throw new InvalidDataException("Invalid portal position, normal, UV or triangle bounds.");
                for(int i=0;i<level.triangles.Length;i+=3)
                {
                    int a=level.triangles[i],b=level.triangles[i+1],c=level.triangles[i+2];var cross=Vector3.Cross(level.vertices[b]-level.vertices[a],level.vertices[c]-level.vertices[a]);
                    if(cross.sqrMagnitude<1e-14f||Vector3.Dot(cross.normalized,level.normals[a])<.5f)throw new InvalidDataException("Degenerate or inward portal triangle.");
                }
            }
            if(model.lods[0].triangles.Length<=model.lods[1].triangles.Length||model.lods[1].triangles.Length<=model.lods[2].triangles.Length)throw new InvalidDataException("Portal LODs must reduce geometry.");
            foreach(string name in new[]{"Stone_BaseColor.png","Stone_Normal.png","Stone_Mask.png","Lining_BaseColor.png"})
                if(!File.Exists(Source+name)||new FileInfo(Source+name).Length>2*1024*1024)throw new InvalidDataException("Missing/bounded original tunnel texture required: "+name);
            return model;
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public static void Frame(PlacedStructure item,bool entrance,out Vector3 position,out Quaternion rotation)
        {
            var path=item.Path;int end=entrance?0:path.Length-1,adjacent=entrance?1:path.Length-2;
            var p=path[end];var q=path[adjacent];var inward=new Vector3(q.X-p.X,0,q.Z-p.Z).normalized;
            float reach=item.Spec.Width/2+item.Spec.Thickness+2*item.Spec.HoleCellMargin+1;
            position=new Vector3(p.X,p.Y,p.Z)-inward*reach;rotation=Quaternion.LookRotation(inward,Vector3.up);
        }
        public static Material Material(string style,string generation,List<string> assets)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit")??throw new InvalidOperationException("Missing URP Lit shader.");
            var material=new Material(shader){name=style,enableInstancing=true};
            material.SetTexture("_BaseMap",Copy(style=="tunnel-stone"?"Stone_BaseColor.png":"Lining_BaseColor.png",false,generation,assets));
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.2f);
            if(style=="tunnel-stone")
            {
                material.SetTexture("_BumpMap",Copy("Stone_Normal.png",true,generation,assets));material.SetFloat("_BumpScale",.55f);material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_MetallicGlossMap",Copy("Stone_Mask.png",false,generation,assets));material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            return material;
        }
        static Texture2D Copy(string name,bool normal,string generation,List<string> assets)
        {
            string path=ToolSandbox.Generated+"/"+generation+"-"+name;
            if(!assets.Contains(path))
            {
                // Record the path before import so rollback can clean a partial import.
                assets.Add(path);File.Copy(Source+name,path,false);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=AssetImporter.GetAtPath(path) as TextureImporter??throw new InvalidDataException("Texture import failed: "+path);
                importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=!normal&&!name.Contains("Mask");
                importer.wrapMode=TextureWrapMode.Repeat;importer.mipmapEnabled=true;importer.maxTextureSize=512;importer.anisoLevel=4;importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path)??throw new InvalidDataException("Missing imported tunnel texture.");
        }
        public static void Add(Model model,PlacedStructure item,Transform parent,Material material,string generation,List<string> assets)
        {
            var meshes=new Mesh[3];
            for(int i=0;i<3;i++)
            {
                var source=model.lods[i];var mesh=new Mesh{name="Original masonry portal LOD"+i,indexFormat=source.vertices.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                mesh.vertices=source.vertices;mesh.normals=source.normals;mesh.uv=source.uv;mesh.triangles=source.triangles;mesh.RecalculateTangents();mesh.RecalculateBounds();
                string filename=generation+"-portal-"+assets.Count+".asset";meshes[i]=ToolSandbox.Persist(mesh,filename);assets.Add(ToolSandbox.Generated+"/"+filename);
            }
            foreach(bool entrance in new[]{true,false})
            {
                var root=new GameObject(item.Spec.Id+(entrance?" entry masonry":" exit masonry"));root.transform.SetParent(parent,false);
                Frame(item,entrance,out var position,out var rotation);root.transform.SetPositionAndRotation(position,rotation);
                var levels=new LOD[3];
                for(int i=0;i<3;i++)
                {var child=new GameObject("Masonry LOD"+i);child.transform.SetParent(root.transform,false);child.AddComponent<MeshFilter>().sharedMesh=meshes[i];var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;levels[i]=new LOD(new[]{.24f,.09f,.015f}[i],new Renderer[]{renderer});}
                var group=root.AddComponent<LODGroup>();group.SetLODs(levels);group.RecalculateBounds();
            }
        }
        // Reuses the package's finite hierarchy/reference fingerprint, including LOD membership.
        public static string Fingerprint(Transform root)=>WaterfallCommand.HierarchyFingerprint(root);
        public static void ValidateLegacy(Transform root,string[] assets)
        {
            if(root.GetComponents<Component>().Length!=1)throw new InvalidDataException("Foreign structure root component.");
            foreach(Transform child in root)
            {
                var filter=child.GetComponent<MeshFilter>();var renderer=child.GetComponent<MeshRenderer>();var collider=child.GetComponent<MeshCollider>();
                if(child.childCount!=0||child.GetComponents<Component>().Length!=4||filter==null||renderer==null||collider==null||filter.sharedMesh!=collider.sharedMesh||!assets.Contains(AssetDatabase.GetAssetPath(filter.sharedMesh))||renderer.sharedMaterials.Any(m=>!assets.Contains(AssetDatabase.GetAssetPath(m))))throw new InvalidDataException("Legacy structure hierarchy contains foreign or edited content; preserve its checkpoint.");
            }
        }
    }
}
