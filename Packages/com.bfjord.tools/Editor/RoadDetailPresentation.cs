// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Bwork.WorldAuthoring;
using Object = UnityEngine.Object;

namespace Bwork.Authoring.Editor
{
    /// <summary>Optional, deterministic verge dressing. It never changes road vertices or owns collision.</summary>
    public static class RoadDetailPresentation
    {
        public const string SourceRoot = "Assets/BFjord/RoadDetail";
        public const int MaximumPlacements = 384;
        public sealed class Placement
        {
            public string assetId, sourceId;
            public float station;
            public Vector3 position;
            public Quaternion rotation;
            public float radius;
        }

        // A ground callback returns NaN outside the retained Terrain or inside holes. A support
        // plane uses the full wall footprint, not its center alone; uneven supports are omitted.
        public static Placement[] Plan(SandboxRoadResult roads, Func<float,float,float> ground,
            Func<Vector3,float,bool> excluded)
        {
            if (roads?.Paths == null || ground == null || excluded == null) throw new ArgumentNullException();
            var result = new List<Placement>();
            foreach (var path in roads.Paths)
            {
                float start = path.Stations[path.FirstVisible] + 10;
                float end = path.Stations[path.LastVisible] - 10;
                for (float station = start; station <= end && result.Count < MaximumPlacements; station += 36)
                {
                    // Small separated retaining fragments supply scale without fencing every route.
                    for (int section = 0; section < 3; section++)
                        Add(path, station + section * 2.52f, 1, "VergeWall_A", 1.24f, .40f);
                    if (path.Source.Surface == "asphalt")
                        Add(path, station + 12, -1, "Delineator_A", .10f, .10f);
                }
            }
            return result.ToArray();

            void Add(SandboxPath path, float station, int side, string id, float halfLength, float halfWidth)
            {
                if (result.Count >= MaximumPlacements || station > path.Stations[path.LastVisible] - 10) return;
                if (path.Source.Spans.Any(s => station >= s.StartMeters - 8 - halfLength && station <= s.EndMeters + 8 + halfLength)) return;
                int i = Array.FindIndex(path.Stations, s => s >= station);
                if (i <= path.FirstVisible || i > path.LastVisible) return;
                var a = path.Points[i-1]; var b = path.Points[i];
                float t = (station-path.Stations[i-1])/(path.Stations[i]-path.Stations[i-1]);
                var center = new Vector3(Mathf.Lerp(a.X,b.X,t),0,Mathf.Lerp(a.Z,b.Z,t));
                var forward = new Vector3(b.X-a.X,0,b.Z-a.Z).normalized;
                var right = Vector3.Cross(Vector3.up,forward);
                // Existing rendered verge ends at 0.85*width; keep even the nearest stone outside it.
                center += right * side * (path.Source.Width*.85f + halfWidth + .28f);
                float radius = Mathf.Sqrt(halfLength*halfLength+halfWidth*halfWidth);
                // Every other route and all endpoint mouths are protected, including hidden spans.
                foreach (var other in roads.Paths)
                {
                    foreach (int endpoint in new[] { other.FirstVisible,other.LastVisible })
                    {
                        var p = other.Points[endpoint];
                        if (Vector2.Distance(new Vector2(center.x,center.z),new Vector2(p.X,p.Z)) < other.Source.Width + radius) return;
                    }
                    if (other == path) continue;
                    for (int k=1;k<other.Points.Length;k++)
                    {
                        var p = other.Points[k-1]; var q = other.Points[k];
                        if (SegmentDistance(new Vector2(center.x,center.z),new Vector2(p.X,p.Z),new Vector2(q.X,q.Z)) < other.Source.Width*.85f+radius+.3f) return;
                    }
                }
                // Neighboring modules deliberately nearly join. Use their actual oriented footprint
                // samples below, rather than treating a 2.4m wall as a 2.4m-wide disk for spacing.
                if (result.Any(p => p.assetId != id && Vector2.Distance(new Vector2(p.position.x,p.position.z),new Vector2(center.x,center.z)) < p.radius+radius+.2f)) return;
                float h = ground(center.x,center.z);
                float f = ground(center.x+forward.x*halfLength,center.z+forward.z*halfLength);
                float back = ground(center.x-forward.x*halfLength,center.z-forward.z*halfLength);
                float r = ground(center.x+right.x*halfWidth,center.z+right.z*halfWidth);
                float l = ground(center.x-right.x*halfWidth,center.z-right.z*halfWidth);
                if (!Finite(h) || !Finite(f) || !Finite(back) || !Finite(r) || !Finite(l)) return;
                float forwardSlope=(f-back)/(2*halfLength),rightSlope=(r-l)/(2*halfWidth);
                if (Mathf.Abs(forwardSlope)>.32f || Mathf.Abs(rightSlope)>.55f) return;
                var normal=(Vector3.up-forward*forwardSlope-right*rightSlope).normalized;
                var rotation=id=="Delineator_A" ? Quaternion.LookRotation(forward,Vector3.up) : Quaternion.LookRotation(forward+Vector3.up*forwardSlope,normal);
                // Check support after rotation so pitched ends and rolled sides are sampled where
                // the actual asset base lies. Six centimetres of embedment hides admitted residuals.
                for (int z=-2;z<=2;z++) for (int x=-1;x<=1;x++)
                {
                    var offset=rotation*new Vector3(x*halfWidth,0,z*halfLength*.5f);
                    float sample=ground(center.x+offset.x,center.z+offset.z);
                    if (!Finite(sample) || Mathf.Abs(sample-(h+offset.y))>.055f) return;
                    if (excluded(new Vector3(center.x+offset.x,sample,center.z+offset.z),.24f)) return;
                }
                center.y=h-.06f;
                result.Add(new Placement{assetId=id,sourceId=path.Source.SourceId,station=station,position=center,rotation=rotation,radius=radius});
            }
        }

        // The shared exact triangle index keeps dry terrain between separate lake/river bodies
        // eligible. The prior road generation is explicitly omitted during replacement. AABBs are
        // used only for other scenery (e.g. trees), never a combined connected-water renderer.
        public static Func<Vector3,float,bool> ExistingExclusions(Transform root)
        {
            var exact=SpatialExclusions.Create(root,excludedGroups:new[]{"Roads"});
            var known=new[]{"Roads","Roads preparing","Water Sample","Connected Water Sample","Structure Sample","Structures","Bwork Structures [owned:bwork_structures:v1]"};
            var bounds = root.Cast<Transform>().Where(t=>!known.Contains(t.name))
                .SelectMany(t=>t.GetComponentsInChildren<Renderer>(true)).Select(r=>r.bounds).ToArray();
            return (p,radius)=>exact.Intersects(p,radius) || bounds.Any(b => p.x>=b.min.x-radius && p.x<=b.max.x+radius && p.z>=b.min.z-radius && p.z<=b.max.z+radius);
        }

        public static object View(Terrain terrain)
        {
            var roads=ToolSandbox.Root.Find("Roads");
            var detail=roads==null?null:roads.Find("Road verge dressing");
            var wall=detail==null?null:detail.Cast<Transform>().FirstOrDefault(t=>t.name.StartsWith("VergeWall_A ",StringComparison.Ordinal));
            if(wall==null)throw new InvalidOperationException("Apply roads with dressing=true on supported terrain before using view-dressing.");
            var camera=ToolSandbox.Root.GetComponentInChildren<Camera>();
            if(camera==null)throw new InvalidOperationException("Sandbox camera is missing.");
            var position=wall.position-wall.forward*7-wall.right*4.5f;
            position.y=terrain.SampleHeight(position)+terrain.transform.position.y+1.8f;
            Physics.SyncTransforms();
            foreach(var collider in roads.GetComponentsInChildren<MeshCollider>())
            {
                var ray=new Ray(new Vector3(position.x,collider.bounds.max.y+2,position.z),Vector3.down);
                if(collider.Raycast(ray,out var hit,collider.bounds.size.y+4))position.y=Mathf.Max(position.y,hit.point.y+1.8f);
            }
            camera.transform.position=position;camera.transform.LookAt(wall.position+wall.forward*1.3f+Vector3.up*.5f);
            camera.orthographic=false;camera.fieldOfView=53;
            return new{view="road-verge-detail",cameraPosition=new[]{position.x,position.y,position.z},asset=wall.name};
        }

        public static void RequireSources()
        {
            foreach(string id in new[]{"VergeWall_A","Delineator_A"}) for(int lod=0;lod<3;lod++)
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>($"{SourceRoot}/Models/{id}_LOD{lod}.fbx") ??
                    throw new InvalidOperationException("Import the original RoadDetail catalog before requesting dressing.");
                var filters=source.GetComponentsInChildren<MeshFilter>(true);
                if(filters.Length!=1 || filters[0].sharedMesh==null || !filters[0].sharedMesh.isReadable)
                    throw new InvalidOperationException("Road detail requires one readable mesh per admitted LOD.");
                var mesh=filters[0].sharedMesh;
                var bounds=ImportedBounds(filters[0]);
                bool wall=id=="VergeWall_A";
                if(mesh.subMeshCount!=(wall?1:3) || bounds.min.y<-.01f || bounds.min.y>.015f ||
                    bounds.size.y<(wall?.8f:1.04f) || bounds.size.y>(wall?1.05f:1.08f) ||
                    bounds.size.x>(wall?.79f:.15f) || bounds.size.z>(wall?2.48f:.22f) ||
                    bounds.size.x<(wall?.6f:.12f) || bounds.size.z<(wall?2.3f:.18f))
                    throw new InvalidOperationException($"Road detail {id} LOD{lod} dimensions, pivot or material slots differ from the admitted metre-scale asset contract: bounds {bounds}, submeshes {mesh.subMeshCount}.");
            }
            foreach(string name in new[]{"VergeStone_BaseColor","VergeStone_Normal"})
                if(AssetDatabase.LoadAssetAtPath<Texture2D>(SourceRoot+"/Textures/"+name+".png")==null)
                    throw new InvalidOperationException("Road detail texture is missing: "+name);
        }

        public static void Build(Transform parent, Placement[] plan, string generation, List<string> ownedAssets)
        {
            if(plan.Length==0)return;
            var shader=Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit is required for road dressing.");
            var materials=new Dictionary<string,Material>();
            Material Material(string id)
            {
                if(materials.TryGetValue(id,out var existing))return existing;
                var m=new Material(shader){name=id};
                m.SetFloat("_Smoothness",id=="DelineatorAmber"?.24f:.16f);
                m.SetColor("_BaseColor",id=="DelineatorIvory"?new Color(.68f,.665f,.60f):id=="DelineatorBlack"?new Color(.025f,.030f,.028f):id=="DelineatorAmber"?new Color(.85f,.19f,.018f):Color.white);
                if(id=="VergeStone")
                {
                    m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(SourceRoot+"/Textures/VergeStone_BaseColor.png") ?? throw new InvalidOperationException("Missing verge stone color map."));
                    m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(SourceRoot+"/Textures/VergeStone_Normal.png") ?? throw new InvalidOperationException("Missing verge stone normal map."));
                    m.SetFloat("_BumpScale",.55f);m.EnableKeyword("_NORMALMAP");
                }
                string file=generation+"-detail-"+id+".asset";
                var saved=ToolSandbox.Persist(m,file);ownedAssets.Add(ToolSandbox.Generated+"/"+file);materials.Add(id,saved);return saved;
            }
            var library=new Dictionary<string,(Mesh mesh,Material[] materials)[]>();
            foreach(string id in plan.Select(p=>p.assetId).Distinct())
            {
                var lods=new (Mesh,Material[])[3];
                for(int lod=0;lod<3;lod++)
                {
                    var source=AssetDatabase.LoadAssetAtPath<GameObject>($"{SourceRoot}/Models/{id}_LOD{lod}.fbx");
                    var filters=source.GetComponentsInChildren<MeshFilter>(true);
                    if(filters.Length!=1 || filters[0].sharedMesh==null)throw new InvalidOperationException("Road detail requires one mesh per admitted LOD.");
                    var filter=filters[0];var mesh=BakeImportedMesh(filter);
                    // Flatten importer node transforms into the owned readable mesh. The road scene
                    // contains no live prefab link to a replaceable external library generation.
                    string file=generation+"-detail-"+id+"-"+lod+".asset";
                    var saved=ToolSandbox.Persist(mesh,file);ownedAssets.Add(ToolSandbox.Generated+"/"+file);
                    var names=id=="VergeWall_A"?new[]{"VergeStone"}:new[]{"DelineatorIvory","DelineatorBlack","DelineatorAmber"};
                    if(saved.subMeshCount!=names.Length)throw new InvalidOperationException("Road detail material submeshes differ from the admitted asset contract.");
                    var slots=names.Select(Material).ToArray();
                    lods[lod]=(saved,slots);
                }
                library.Add(id,lods);
            }
            var group=new GameObject("Road verge dressing");group.transform.SetParent(parent,false);
            foreach(var p in plan)
            {
                var go=new GameObject(p.assetId+" "+p.sourceId+" "+p.station.ToString("F1",System.Globalization.CultureInfo.InvariantCulture));go.transform.SetParent(group.transform,false);
                go.transform.SetPositionAndRotation(p.position,p.rotation);
                var lods=new LOD[3];
                for(int lod=0;lod<3;lod++)
                {
                    var child=new GameObject("LOD"+lod);child.transform.SetParent(go.transform,false);var data=library[p.assetId][lod];
                    child.AddComponent<MeshFilter>().sharedMesh=data.mesh;var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterials=data.materials;
                    lods[lod]=new LOD(lod==0?.13f:lod==1?.045f:.012f,new Renderer[]{renderer});
                }
                var groupLOD=go.AddComponent<LODGroup>();groupLOD.SetLODs(lods);groupLOD.RecalculateBounds();
            }
        }
        // Persistent model assets have no scene parent. Include their root transform because Unity
        // may store FBX axis/unit conversion there instead of baking it into the mesh vertices.
        static Bounds ImportedBounds(MeshFilter filter) =>
            GeometryUtility.CalculateBounds(filter.sharedMesh.vertices,filter.transform.localToWorldMatrix);

        static Mesh BakeImportedMesh(MeshFilter filter)
        {
            var mesh=Object.Instantiate(filter.sharedMesh);
            var matrix=filter.transform.localToWorldMatrix;
            mesh.vertices=mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
            var normalMatrix=matrix.inverse.transpose;
            mesh.normals=mesh.normals.Select(n=>normalMatrix.MultiplyVector(n).normalized).ToArray();
            mesh.RecalculateTangents();mesh.RecalculateBounds();
            return mesh;
        }

        static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b)
        {var d=b-a;float t=d.sqrMagnitude>0?Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude):0;return Vector2.Distance(p,a+d*t);}
        static bool Finite(float v)=>float.IsFinite(v);
    }
}
