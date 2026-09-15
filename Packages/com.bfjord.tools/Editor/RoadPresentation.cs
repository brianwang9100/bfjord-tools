using System;
using System.Collections.Generic;
using System.Linq;
using Bwork.WorldAuthoring;
using UnityEditor;
using UnityEngine;

namespace Bwork.Authoring.Editor
{
    /// <summary>Shared scanned surfaces and geometric normals; does not alter path fitting or collision vertices.</summary>
    public static class RoadPresentation
    {
        public static Dictionary<string, Material[]> Materials()
        {
            Material Get(string name) => AssetDatabase.LoadAssetAtPath<Material>(ProjectContext.Material(name)) ??
                throw new InvalidOperationException("Missing reviewed CC0 road material: " + name);
            var asphalt = Get("Asphalt"); var gravel = Get("Gravel"); var dirt = Get("Dirt");
            return new Dictionary<string, Material[]>(StringComparer.Ordinal)
            {
                ["asphalt"] = new[] { asphalt, Get("AsphaltEdge"), Get("GravelVerge") },
                ["gravel"] = new[] { gravel, gravel, Get("GravelVerge") },
                ["dirt"] = new[] { dirt, dirt, Get("DirtVerge") }
            };
        }

        // Area-weighted normals from the finished crown/cut/fill geometry. Accumulation across
        // coincident junction mouths prevents two independently generated meshes making a seam.
        public static Vector3[][] FittedNormals(SandboxMesh[] meshes)
        {
            var sums = new Dictionary<(int,int,int), Vector3>();
            (int,int,int) Key(Vector3 p) => (Mathf.RoundToInt(p.x*10000), Mathf.RoundToInt(p.y*10000), Mathf.RoundToInt(p.z*10000));
            Vector3 Position(SandboxMesh mesh, int i) { var p = mesh.Vertices[i].Position; return new Vector3(p.X,p.Y,p.Z); }
            foreach (var mesh in meshes) foreach (var indices in mesh.Triangles)
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var a = Position(mesh, indices[i]); var b = Position(mesh, indices[i+1]); var c = Position(mesh, indices[i+2]);
                    Vector3 normal = Vector3.Cross(b-a,c-a);
                    foreach (var p in new[] { a,b,c })
                    { var key = Key(p); sums.TryGetValue(key, out var sum); sums[key] = sum+normal; }
                }
            return meshes.Select(mesh => mesh.Vertices.Select(v =>
            {
                var key = Key(new Vector3(v.Position.X,v.Position.Y,v.Position.Z));
                return sums.TryGetValue(key,out var sum) && sum.sqrMagnitude > 1e-12f ? sum.normalized : Vector3.up;
            }).ToArray()).ToArray();
        }
    }
}
