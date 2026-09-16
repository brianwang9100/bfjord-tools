// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace Bwork.Authoring.Editor
{
    /// <summary>Metric paint clipped from retained road triangles. No terrain, route or collider ownership.</summary>
    public static class RoadMarkings
    {
        public const string Group="Road lane markings";
        public const int Version=1;
        public const float Lift=.012f;
        public sealed class Road {public string id,surface;public float width;}
        public sealed class Geometry {public Vector3[] vertices,normals;public Vector2[] uv;public int[][] triangles;}
        struct Point {public Vector3 position,normal;public Vector2 coordinate;}
        public static Geometry Clip(Vector3[] vertices,Vector3[] normals,Vector2[] coordinates,int[] triangles,float width,string surface)
        {
            var positions=new List<Vector3>();var outputNormals=new List<Vector3>();var uv=new List<Vector2>();var indices=new[]{new List<int>(),new List<int>()};
            Geometry Finish()=>new Geometry{vertices=positions.ToArray(),normals=outputNormals.ToArray(),uv=uv.ToArray(),triangles=indices.Select(t=>t.ToArray()).ToArray()};
            if(surface!="asphalt")return Finish();
            if(vertices==null||vertices.Length==0||vertices.Length>200000||normals?.Length!=vertices.Length||coordinates?.Length!=vertices.Length||triangles==null||triangles.Length%3!=0||triangles.Length>1200000||!Finite(width)||width<3||width>20)throw new InvalidDataException("Bounded retained road mesh with metric UV2 required.");
            if(triangles.Any(i=>i<0||i>=vertices.Length)||vertices.Any(v=>!Finite(v.x)||!Finite(v.y)||!Finite(v.z))||normals.Any(v=>!Finite(v.sqrMagnitude)||v.sqrMagnitude<.5f)||coordinates.Any(v=>!Finite(v.x)||!Finite(v.y)))throw new InvalidDataException("Nonfinite or invalid retained road arrays.");
            float first=coordinates.Min(v=>v.y)+.6f,last=coordinates.Max(v=>v.y)-.6f;
            if(last<=first)return Finish();
            // ±0.14m centers with 0.12m paint gives a 0.16m unpainted center gap.
            var bands=new[]{(-.20f,-.08f,0),(.08f,.20f,0),(-width/2+.22f,-width/2+.34f,1),(width/2-.34f,width/2-.22f,1)};
            for(int i=0;i<triangles.Length;i+=3)
            {
                var original=new List<Point>(3);
                for(int k=0;k<3;k++)
                {int index=triangles[i+k];original.Add(new Point{position=vertices[index],normal=normals[index],coordinate=new Vector2(coordinates[index].x*width/8,coordinates[index].y)});}
                foreach(var band in bands)
                {
                    var polygon=Cut(Cut(Cut(Cut(original,0,band.Item1,true),0,band.Item2,false),1,first,true),1,last,false);
                    for(int k=1;k<polygon.Count-1;k++)
                    {
                        var a=polygon[0];var b=polygon[k];var c=polygon[k+1];
                        if(Vector3.Cross(b.position-a.position,c.position-a.position).sqrMagnitude<1e-14f)continue;
                        foreach(var p in new[]{a,b,c})
                        {indices[band.Item3].Add(positions.Count);positions.Add(p.position+p.normal.normalized*Lift);outputNormals.Add(p.normal.normalized);uv.Add(p.coordinate);}
                    }
                }
                if(positions.Count>400000)throw new InvalidDataException("Road marking vertex budget exceeded.");
            }
            return Finish();
        }
        static List<Point> Cut(List<Point> input,int axis,float bound,bool greater)
        {
            var output=new List<Point>();if(input.Count==0)return output;
            float Value(Point p)=>axis==0?p.coordinate.x:p.coordinate.y;
            bool Inside(Point p)=>greater?Value(p)>=bound:Value(p)<=bound;
            var previous=input[input.Count-1];bool before=Inside(previous);
            foreach(var current in input)
            {
                bool after=Inside(current);
                if(before!=after)
                {float t=(bound-Value(previous))/(Value(current)-Value(previous));output.Add(new Point{position=Vector3.LerpUnclamped(previous.position,current.position,t),normal=Vector3.LerpUnclamped(previous.normal,current.normal,t),coordinate=Vector2.LerpUnclamped(previous.coordinate,current.coordinate,t)});}
                if(after)output.Add(current);previous=current;before=after;
            }
            return output;
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public static bool Current(bool enabled,int version,bool recorded)=>enabled?recorded&&version==Version:!recorded;
        public static GameObject Build(Transform roads,Road[] definitions,string generation,List<string> assets)
        {
            if(definitions==null||definitions.Length>32||definitions.Any(r=>r==null||string.IsNullOrEmpty(r.id)||r.id==Group)||definitions.Select(r=>r.id).Distinct().Count()!=definitions.Length)throw new InvalidDataException("Bounded unique recorded road definitions required.");
            // Only explicit road IDs are admitted: junction-* patches are deliberately never painted.
            var planned=new List<(string id,Geometry geometry)>();
            foreach(var road in definitions.Where(r=>r.surface=="asphalt"))
            {
                var matches=roads.Cast<Transform>().Where(t=>t.name==road.id).ToArray();
                if(matches.Length!=1)throw new InvalidDataException("Recorded asphalt road is absent or ambiguous: "+road.id);
                var filter=matches[0].GetComponent<MeshFilter>();var mesh=filter==null?null:filter.sharedMesh;
                if(mesh==null||!mesh.isReadable||mesh.subMeshCount!=3)throw new InvalidDataException("Readable owned road surface required for markings.");
                var geometry=Clip(mesh.vertices,mesh.normals,mesh.uv2,mesh.GetTriangles(0).Concat(mesh.GetTriangles(1)).ToArray(),road.width,road.surface);
                if(geometry.vertices.Length==0)continue;
                var matrix=roads.worldToLocalMatrix*filter.transform.localToWorldMatrix;var normalMatrix=matrix.inverse.transpose;
                for(int i=0;i<geometry.vertices.Length;i++){geometry.vertices[i]=matrix.MultiplyPoint3x4(geometry.vertices[i]);geometry.normals[i]=normalMatrix.MultiplyVector(geometry.normals[i]).normalized;}
                planned.Add((road.id,geometry));
            }
            if(planned.Count==0)return null;
            var group=new GameObject(Group);group.SetActive(false);
            try
            {
                var materials=new Material[2];
                for(int i=0;i<2;i++)
                {
                    var material=CreatePaintMaterial(i);
                    materials[i]=Persist(material,generation+"-paint-"+i+".asset",assets);
                }
                foreach(var item in planned)
                {
                    var g=item.geometry;var mesh=new Mesh{name=item.id+" lane markings",indexFormat=g.vertices.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                    mesh.vertices=g.vertices;mesh.normals=g.normals;mesh.uv=g.uv;mesh.subMeshCount=2;mesh.SetTriangles(g.triangles[0],0);mesh.SetTriangles(g.triangles[1],1);mesh.RecalculateBounds();
                    var saved=Persist(mesh,generation+"-paint-mesh-"+assets.Count+".asset",assets);
                    var child=new GameObject(item.id+" lane markings");child.transform.SetParent(group.transform,false);child.AddComponent<MeshFilter>().sharedMesh=saved;
                    var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterials=materials;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
                }
                return group;
            }
            catch{Object.DestroyImmediate(group);throw;}
        }
        public static Material CreatePaintMaterial(int index)
        {
            if(index<0||index>1)throw new ArgumentOutOfRangeException(nameof(index));
            var shader=Shader.Find("Universal Render Pipeline/Lit")??throw new InvalidOperationException("URP Lit is required for road paint.");
            var material=new Material(shader){name=index==0?"Road center yellow":"Road outer white",enableInstancing=true};
            material.SetColor("_BaseColor",index==0?new Color(1,.68f,.025f):new Color(.88f,.89f,.84f));
            material.SetFloat("_Smoothness",.22f);material.SetFloat("_Metallic",0);
            // Run the same public URP initialization used by its later Editor/import callback
            // before persistence and receipt sealing, including legacy color, tags and passes.
            BaseShaderGUI.SetMaterialKeywords(material,LitGUI.SetMaterialKeywords);
            return material;
        }
        static T Persist<T>(T value,string filename,List<string> assets) where T:Object
        {assets.Add(ToolSandbox.Generated+"/"+filename);return ToolSandbox.Persist(value,filename);}
        /// <summary>Admit only a byte-exact proof of URP's delayed initialization of the two v1 paint materials.</summary>
        public static bool ProvesLegacyPaintInitialization(string[] paths,string[] markingAssets,string expectedHash)
        {
            if(markingAssets==null||string.IsNullOrEmpty(expectedHash))return false;
            var materials=markingAssets.Where(p=>Regex.IsMatch(Path.GetFileName(p),@"^roads-[a-f0-9]{32}-paint-[01]\.asset$")).ToArray();
            if(materials.Length!=2||materials.Any(p=>!paths.Contains(p)))return false;
            try{return AssetFingerprint(paths,false,materials)==expectedHash;}
            catch(InvalidDataException){return false;}
        }
        public static string BeforeKnownLitInitialization(string text)
        {
            const string tag="  stringTagMap:\n    RenderType: Opaque\n",passes="  disabledShaderPasses:\n  - MOTIONVECTORS\n";
            if(!text.Contains(tag)||!text.Contains(passes))return null;
            var color=Regex.Match(text,@"(?m)^    - _Color: (\{[^\r\n]+\})$");
            var baseColor=Regex.Match(text,@"(?m)^    - _BaseColor: (\{[^\r\n]+\})$");
            // URP's color read/write roundtrip serialized the original white preset's blue
            // channel one float ULP lower. Admit only that observed value, never a tolerance.
            if(!color.Success||!baseColor.Success)return null;
            bool knownWhiteRoundtrip=baseColor.Groups[1].Value=="{r: 0.88, g: 0.89, b: 0.84, a: 1}"&&color.Groups[1].Value=="{r: 0.88, g: 0.89, b: 0.8399999, a: 1}";
            if(color.Groups[1].Value!=baseColor.Groups[1].Value&&!knownWhiteRoundtrip)return null;
            text=text.Replace(tag,"  stringTagMap: {}\n").Replace(passes,"  disabledShaderPasses: []\n");
            return Regex.Replace(text,@"(?m)^    - _Color: \{[^\r\n]+\}$","    - _Color: {r: 1, g: 1, b: 1, a: 1}");
        }
        public static string AssetFingerprint(string[] paths,bool saveNew=false) => AssetFingerprint(paths,saveNew,null);
        static string AssetFingerprint(string[] paths,bool saveNew,string[] legacyPaintMaterials)
        {
            using var hash=SHA256.Create();
            foreach(string path in paths.OrderBy(p=>p,StringComparer.Ordinal))
            {
                if(!path.StartsWith(ToolSandbox.Generated+"/roads-",StringComparison.Ordinal)||Path.GetExtension(path)!=".asset")throw new InvalidDataException("Unowned road asset in receipt.");
                var asset=AssetDatabase.LoadMainAssetAtPath(path)??throw new InvalidDataException("Owned road asset missing: "+path);
                if(saveNew)AssetDatabase.SaveAssetIfDirty(asset);
                else if(EditorUtility.IsDirty(asset))throw new InvalidDataException("Owned road asset has unsaved edits; preserve it before changing road appearance.");
                var name=Encoding.UTF8.GetBytes(path);hash.TransformBlock(name,0,name.Length,name,0);var bytes=File.ReadAllBytes(path);
                if(legacyPaintMaterials!=null&&legacyPaintMaterials.Contains(path))
                {
                    string previous=BeforeKnownLitInitialization(Encoding.UTF8.GetString(bytes));
                    if(previous==null)throw new InvalidDataException("Road paint does not match the known URP initialization transition.");
                    bytes=Encoding.UTF8.GetBytes(previous);
                }
                hash.TransformBlock(bytes,0,bytes.Length,bytes,0);
            }
            hash.TransformFinalBlock(Array.Empty<byte>(),0,0);return BitConverter.ToString(hash.Hash).Replace("-","").ToLowerInvariant();
        }
    }
}
