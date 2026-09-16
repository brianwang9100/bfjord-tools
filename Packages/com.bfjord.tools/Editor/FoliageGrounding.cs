using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Unity.Collections;

namespace Bwork.Authoring.Editor
{
    /// <summary>Upright canopy support, evaluated across the root collar rather than the crown.</summary>
    public static class FoliageGrounding
    {
        public readonly struct Profile
        {
            public readonly float radius,bottom,height;
            public Profile(float radius,float bottom,float height)
            {this.radius=radius;this.bottom=bottom;this.height=height;}
        }

        public static bool IsCanopy(string key)
        {
            string id=Path.GetFileNameWithoutExtension(key).Split(':').Last();
            return id.StartsWith("MatureFir_",StringComparison.Ordinal)||id.StartsWith("MatureOak_",StringComparison.Ordinal)||
                NatureAssets07.IsCanopy(id)||id.StartsWith("SilverBirch_",StringComparison.Ordinal)||id.StartsWith("pine_",StringComparison.Ordinal);
        }

        public static Profile FromPrefab(GameObject prefab)
        {
            var parts=new List<(Vector3[] vertices,Matrix4x4 matrix)>();
            float low=float.PositiveInfinity,high=float.NegativeInfinity;
            foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.sharedMesh==null)throw new InvalidDataException("Canopy root geometry is missing: "+prefab.name);
                var matrix=prefab.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                // Editor snapshots read import-optimized meshes without changing their import settings.
                using var snapshot=MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh);
                using var positions=new NativeArray<Vector3>(snapshot[0].vertexCount,Allocator.Temp);
                snapshot[0].GetVertices(positions);
                var vertices=positions.ToArray();parts.Add((vertices,matrix));
                foreach(var vertex in vertices){var p=matrix.MultiplyPoint3x4(vertex);if(!Finite(p))throw new InvalidDataException("Canopy geometry is nonfinite.");low=Mathf.Min(low,p.y);high=Mathf.Max(high,p.y);}
            }
            if(!float.IsFinite(low)||high-low<.1f)throw new InvalidDataException("Canopy root geometry is absent or degenerate.");
            float collar=low+Mathf.Clamp((high-low)*.02f,.03f,.3f),radius=0,bottom=low;
            foreach(var part in parts)foreach(var vertex in part.vertices)
            {
                var p=part.matrix.MultiplyPoint3x4(vertex);if(p.y>collar)continue;
                radius=Mathf.Max(radius,new Vector2(p.x,p.z).magnitude);bottom=Mathf.Max(bottom,p.y);
            }
            if(radius<=0||radius>12)throw new InvalidDataException("Canopy root footprint is outside the supported bounds.");
            return new Profile(radius,bottom,high-low);
        }

        public static Vector3? Fit(Profile root,Vector3 position,float scale,Func<Rect,float?> minimum)
        {
            if(!float.IsFinite(root.radius)||root.radius<=0||!float.IsFinite(root.bottom)||!float.IsFinite(root.height)||root.height<=0||!Finite(position)||!float.IsFinite(scale)||scale<=0)
                throw new ArgumentException("Root support inputs must be finite and positive.");
            float radius=root.radius*scale;
            float? floor=minimum(new Rect(position.x-radius,position.z-radius,radius*2,radius*2));
            if(!floor.HasValue||!float.IsFinite(floor.Value))return null;
            float y=Mathf.Min(position.y,floor.Value-root.bottom*scale-.04f);
            float limit=Mathf.Clamp(root.height*.12f,.25f,2f)*scale;
            return position.y-y>limit?(Vector3?)null:new Vector3(position.x,y,position.z);
        }

        static bool Finite(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);

        /// <summary>Clip intersected cell triangles to the root footprint before taking their minimum.</summary>
        public sealed class TerrainSupport
        {
            readonly float[,] heights;
            readonly bool[,] holes;
            readonly Vector3 origin,size;
            readonly int nx,nz;
            public TerrainSupport(float[,] heights,bool[,] holes,Vector3 origin,Vector3 size)
            {
                if(heights==null||holes==null||heights.GetLength(0)<2||heights.GetLength(1)<2||!Finite(origin)||!Finite(size)||size.x<=0||size.y<=0||size.z<=0||holes.GetLength(0)!=heights.GetLength(0)-1||holes.GetLength(1)!=heights.GetLength(1)-1)
                    throw new ArgumentException("A finite matching terrain height/hole grid is required.");
                this.heights=heights;this.holes=holes;this.origin=origin;this.size=size;nx=heights.GetLength(1);nz=heights.GetLength(0);
            }
            public static TerrainSupport Read(Terrain terrain)
            {
                if(terrain.transform.rotation!=Quaternion.identity||terrain.transform.lossyScale!=Vector3.one)throw new InvalidOperationException("Root support requires an unscaled, unrotated Terrain.");
                var data=terrain.terrainData;int n=data.heightmapResolution;
                return new TerrainSupport(data.GetHeights(0,0,n,n),data.GetHoles(0,0,data.holesResolution,data.holesResolution),terrain.transform.position,data.size);
            }
            public float? Minimum(Rect area)
            {
                float u0=(area.xMin-origin.x)/size.x,v0=(area.yMin-origin.z)/size.z,u1=(area.xMax-origin.x)/size.x,v1=(area.yMax-origin.z)/size.z;
                if(!float.IsFinite(u0)||!float.IsFinite(v0)||!float.IsFinite(u1)||!float.IsFinite(v1)||u0<0||v0<0||u1>1||v1>1||u1<=u0||v1<=v0)return null;
                int x0=Mathf.FloorToInt(u0*(nx-1)),z0=Mathf.FloorToInt(v0*(nz-1)),x1=Mathf.CeilToInt(u1*(nx-1)),z1=Mathf.CeilToInt(v1*(nz-1));
                for(int z=z0;z<z1;z++)for(int x=x0;x<x1;x++)if(!holes[z,x])return null;
                float min=float.PositiveInfinity;
                var polygon=new List<Vector3>(8);var scratch=new List<Vector3>(8);
                for(int z=z0;z<z1;z++)for(int x=x0;x<x1;x++)
                {
                    var a=new Vector3(0,heights[z,x],0);var b=new Vector3(1,heights[z,x+1],0);
                    var c=new Vector3(1,heights[z+1,x+1],1);var d=new Vector3(0,heights[z+1,x],1);
                    if(!Finite(a)||!Finite(b)||!Finite(c)||!Finite(d))return null;
                    var clip=Rect.MinMaxRect(Mathf.Clamp01(u0*(nx-1)-x),Mathf.Clamp01(v0*(nz-1)-z),
                        Mathf.Clamp01(u1*(nx-1)-x),Mathf.Clamp01(v1*(nz-1)-z));
                    // Bound both possible cell diagonals. Heights outside the actual root
                    // rectangle may define a triangle plane, but cannot supply its minimum.
                    min=Mathf.Min(min,TriangleMinimum(a,b,c,clip,polygon,scratch));
                    min=Mathf.Min(min,TriangleMinimum(a,c,d,clip,polygon,scratch));
                    min=Mathf.Min(min,TriangleMinimum(a,b,d,clip,polygon,scratch));
                    min=Mathf.Min(min,TriangleMinimum(b,c,d,clip,polygon,scratch));
                }
                return float.IsFinite(min)?origin.y+min*size.y:(float?)null;
            }
            static float TriangleMinimum(Vector3 a,Vector3 b,Vector3 c,Rect clip,List<Vector3> polygon,List<Vector3> scratch)
            {
                polygon.Clear();polygon.Add(a);polygon.Add(b);polygon.Add(c);
                void Cut(int axis,float bound,bool above)
                {
                    scratch.Clear();
                    if(polygon.Count>0)
                    {
                        var previous=polygon[polygon.Count-1];bool before=above?previous[axis]>=bound:previous[axis]<=bound;
                        foreach(var current in polygon)
                        {
                            bool after=above?current[axis]>=bound:current[axis]<=bound;
                            if(before!=after)
                            {
                                float t=(bound-previous[axis])/(current[axis]-previous[axis]);
                                var crossing=Vector3.LerpUnclamped(previous,current,t);crossing[axis]=bound;scratch.Add(crossing);
                            }
                            if(after)scratch.Add(current);previous=current;before=after;
                        }
                    }
                    var swap=polygon;polygon=scratch;scratch=swap;
                }
                Cut(0,clip.xMin,true);Cut(0,clip.xMax,false);Cut(2,clip.yMin,true);Cut(2,clip.yMax,false);
                float minimum=float.PositiveInfinity;
                foreach(var vertex in polygon)minimum=Mathf.Min(minimum,vertex.y);
                return minimum;
            }
        }
    }
}
