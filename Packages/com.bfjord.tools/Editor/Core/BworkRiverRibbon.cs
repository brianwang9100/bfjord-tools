using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bwork.Authoring.WaterSandbox
{
    [Serializable]
    public sealed class RiverKnot
    {
        public Vector3 position, handleIn, handleOut; // Handles are world positions.
        public float width = 12;
        public float foam;
    }

    [Serializable]
    public sealed class RiverRecipe
    {
        public int schemaVersion = 1;
        public string id = "sandbox-river";
        public float sampleSpacing = 1, maximumWaveHeight = .12f;
        public int crossSegments = 12;
        public float widthScale = 1, bedDepth = 2, bankFalloff = 18;
        public float flowSpeed = .8f, waveLength = 14, waveSpeed = .8f;
        public float oceanWaveHeight = .55f, oceanWaveLength = 28, oceanWaveSpeed = .65f;
        public RiverKnot[] knots;
    }

    /// <summary>Editor-time, finite mesh builder. Caller owns persistence and scene objects.</summary>
    public static class BworkRiverRibbon
    {
        public static Mesh Build(RiverRecipe recipe)
        {
            if (recipe == null || recipe.schemaVersion != 1 || string.IsNullOrWhiteSpace(recipe.id) ||
                recipe.knots == null || recipe.knots.Length < 2 || recipe.knots.Length > 128 ||
                !float.IsFinite(recipe.sampleSpacing) || recipe.sampleSpacing < .25f || recipe.sampleSpacing > 10 ||
                !float.IsFinite(recipe.maximumWaveHeight) || recipe.maximumWaveHeight < 0 || recipe.maximumWaveHeight > 1 ||
                recipe.crossSegments < 4 || recipe.crossSegments > 64)
                throw new ArgumentException("Bounded river recipe required.");
            foreach (var k in recipe.knots)
                if (k == null || !Finite(k.position) || !Finite(k.handleIn) || !Finite(k.handleOut) ||
                    !float.IsFinite(k.width) || k.width < 1 || k.width > 100 || !float.IsFinite(k.foam) || k.foam < 0 || k.foam > 1)
                    throw new ArgumentException("Finite river knots, widths and foam required.");
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var flow = new List<Vector2>();
            var colors = new List<Color>(); var triangles = new List<int>();
            int stride = recipe.crossSegments + 1;
            for (int segment = 0; segment < recipe.knots.Length - 1; segment++)
            {
                var a = recipe.knots[segment]; var b = recipe.knots[segment + 1];
                // Monotone Bezier control elevations guarantee a downstream water surface.
                if (a.position.y < a.handleOut.y || a.handleOut.y < b.handleIn.y || b.handleIn.y < b.position.y)
                    throw new ArgumentException("River Bezier elevations must descend monotonically.");
                float bound = Vector3.Distance(a.position,a.handleOut) + Vector3.Distance(a.handleOut,b.handleIn) + Vector3.Distance(b.handleIn,b.position);
                if (bound < .25f || bound > 2000) throw new ArgumentException("River segment length bound.");
                float derivativeBound = 3*Mathf.Max(Vector3.Distance(a.position,a.handleOut),Mathf.Max(Vector3.Distance(a.handleOut,b.handleIn),Vector3.Distance(b.handleIn,b.position)));
                int steps = Mathf.CeilToInt(derivativeBound / recipe.sampleSpacing);
                for (int j = segment == 0 ? 0 : 1; j <= steps; j++)
                {
                    if (vertices.Count + stride > 200000) throw new ArgumentException("River vertex budget.");
                    float t = (float)j / steps, u = 1 - t;
                    Vector3 p = u*u*u*a.position + 3*u*u*t*a.handleOut + 3*u*t*t*b.handleIn + t*t*t*b.position;
                    Vector3 d = 3*u*u*(a.handleOut-a.position) + 6*u*t*(b.handleIn-a.handleOut) + 3*t*t*(b.position-b.handleIn);
                    var direction = new Vector2(d.x,d.z);
                    if (direction.sqrMagnitude < 1e-6f) throw new ArgumentException("River cannot have a zero horizontal tangent.");
                    direction.Normalize(); var right = new Vector3(direction.y,0,-direction.x);
                    float width = Mathf.Lerp(a.width,b.width,t), foam = Mathf.Lerp(a.foam,b.foam,t);
                    for (int c = 0; c <= recipe.crossSegments; c++)
                    {
                        float across = (float)c / recipe.crossSegments;
                        Vector3 v = p + right*((across-.5f)*width);
                        // The squared envelope fixes both bank height and its first derivative.
                        float envelope = 4*across*(1-across); envelope *= envelope;
                        vertices.Add(v); uv.Add(new Vector2(v.x,v.z)); flow.Add(direction);
                        colors.Add(new Color(envelope,foam,0,1));
                        if (vertices.Count > stride && c > 0)
                        {
                            int n = vertices.Count-1;
                            triangles.Add(n-stride-1); triangles.Add(n-1); triangles.Add(n-stride);
                            triangles.Add(n-stride); triangles.Add(n-1); triangles.Add(n);
                        }
                    }
                }
            }
            // Reject foldover locally, including excessive width at tight bends.
            for (int i = 0; i < triangles.Count; i += 3)
                if (Vector3.Cross(vertices[triangles[i+1]]-vertices[triangles[i]],vertices[triangles[i+2]]-vertices[triangles[i]]).y <= 1e-6f)
                    throw new ArgumentException("River folds or degenerates; reduce width or ease its bend.");
            var mesh = new Mesh { name = recipe.id, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetUVs(1,flow); mesh.SetColors(colors); mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var bounds = mesh.bounds; bounds.Expand(new Vector3(0,2*recipe.maximumWaveHeight,0)); mesh.bounds = bounds;
            return mesh;
        }

        public static Mesh BuildOcean(Vector3 center, Vector2 size, float spacing = 2, float maximumWaveHeight = .55f)
        {
            if (!Finite(center) || !float.IsFinite(size.x) || !float.IsFinite(size.y) || size.x < 1 || size.y < 1 ||
                size.x > 1000 || size.y > 1000 || !float.IsFinite(spacing) || spacing < .5f || spacing > 20 ||
                !float.IsFinite(maximumWaveHeight) || maximumWaveHeight < 0 || maximumWaveHeight > 1)
                throw new ArgumentException("Bounded ocean patch required.");
            int nx = Mathf.CeilToInt(size.x/spacing), nz = Mathf.CeilToInt(size.y/spacing);
            if ((long)(nx+1)*(nz+1) > 200000) throw new ArgumentException("Ocean vertex budget.");
            var v = new Vector3[(nx+1)*(nz+1)]; var uv = new Vector2[v.Length]; var flow = new Vector2[v.Length]; var colors = new Color[v.Length];
            var triangles = new int[nx*nz*6]; int at = 0;
            for (int z=0;z<=nz;z++) for (int x=0;x<=nx;x++)
            {
                int i=z*(nx+1)+x; v[i]=center+new Vector3(((float)x/nx-.5f)*size.x,0,((float)z/nz-.5f)*size.y);
                uv[i]=new Vector2(v[i].x,v[i].z);flow[i]=new Vector2(.8f,.6f);colors[i]=new Color(1,0,0,1);
                if(x<nx&&z<nz){triangles[at++]=i;triangles[at++]=i+nx+1;triangles[at++]=i+1;triangles[at++]=i+1;triangles[at++]=i+nx+1;triangles[at++]=i+nx+2;}
            }
            var mesh=new Mesh{name="sandbox-ocean",indexFormat=v.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16,vertices=v,uv=uv,uv2=flow,colors=colors,triangles=triangles};
            mesh.RecalculateNormals();mesh.RecalculateBounds();var b=mesh.bounds;b.Expand(new Vector3(0,2*maximumWaveHeight,0));mesh.bounds=b;return mesh;
        }
        static bool Finite(Vector3 p) => float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
    }
}
