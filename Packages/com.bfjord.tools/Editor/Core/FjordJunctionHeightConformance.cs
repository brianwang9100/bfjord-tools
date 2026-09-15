using System;
using System.Collections.Generic;
using System.Linq;

namespace Bwork.FjordCoast.Junctions
{
    [Serializable]
    public sealed class JunctionHeightCorrection
    {
        public int geometricVertex;
        public float sourceX, sourceY, sourceZ, height;
    }

    public sealed class JunctionHeightPreparation
    {
        public readonly string JunctionId;
        public readonly JunctionMesh Mesh;
        public readonly V3[][] Paths;
        public readonly int[] MovableGroups;
        public JunctionHeightPreparation(string junctionId, JunctionMesh mesh, V3[][] paths)
        { JunctionId = junctionId; Mesh = mesh; Paths = paths; MovableGroups = FjordJunctionHeightConformance.MovableGroups(mesh, paths); }
    }

    /// <summary>Version-one local refinement and explicit bounded height corrections.
    /// Native centerlines, complete mouth attributes and original boundary XYZ remain fixed.</summary>
    public static class FjordJunctionHeightConformance
    {
        public const float MaximumHeightChangeMeters = 2f;
        public const int MaximumRefinedVertices = 8192;
        public const int MaximumRefinedTriangles = 16384;
        public const float TerrainGridSpacingMeters = 5750f / 1024f;
        const double PathTolerance = .0015;

        public static JunctionMesh Refine(JunctionMesh mesh, V3[][] paths)
        {
            var result = FjordJunctionPathConformance.RefineTerrainGrid(mesh, paths, TerrainGridSpacingMeters);
            Need(result.Vertices.Length <= MaximumRefinedVertices && result.Triangles.Sum(t => t.Length / 3) <= MaximumRefinedTriangles, "Local refinement exceeds 8192 vertices / 16384 triangles.");
            return result;
        }

        public static int[] MovableGroups(JunctionMesh mesh, V3[][] paths)
        {
            Need(mesh != null && paths != null && paths.Length == 3, "Complete refined junction and native paths required.");
            var fixedGroups = new HashSet<int>(mesh.OuterBoundary.Select(i => mesh.GeometricVertex[i]));
            foreach (var mouth in mesh.Mouths) foreach (int i in mouth.Indices) fixedGroups.Add(mesh.GeometricVertex[i]);
            var positions = new Dictionary<int, V3>();
            for (int i = 0; i < mesh.Vertices.Length; i++) positions[mesh.GeometricVertex[i]] = mesh.Vertices[i].Position;
            foreach (var pair in positions)
            {
                if (fixedGroups.Contains(pair.Key)) continue;
                foreach (var path in paths)
                {
                    Need(path != null && path.Length >= 2, "Missing native path.");
                    for (int i = 1; i < path.Length; i++) if (OnPath(pair.Value, path[i - 1], path[i])) { fixedGroups.Add(pair.Key); break; }
                    if (fixedGroups.Contains(pair.Key)) break;
                }
            }
            return mesh.Triangles.Take(2).SelectMany(t => t).Select(i => mesh.GeometricVertex[i]).Distinct().Where(g => !fixedGroups.Contains(g)).OrderBy(g => g).ToArray();
        }

        public static JunctionMesh Apply(JunctionMesh mesh, V3[][] paths, JunctionHeightCorrection[] corrections)
        {
            if (corrections == null || corrections.Length == 0) return mesh;
            Need(corrections.Length <= MaximumRefinedVertices, "Height correction count exceeds local budget.");
            var movable = new HashSet<int>(MovableGroups(mesh, paths)); var changed = new Dictionary<int, float>();
            var sources = new Dictionary<int, V3>();
            for (int i = 0; i < mesh.Vertices.Length; i++) sources[mesh.GeometricVertex[i]] = mesh.Vertices[i].Position;
            foreach (var correction in corrections)
            {
                Need(correction != null && movable.Contains(correction.geometricVertex) && !changed.ContainsKey(correction.geometricVertex), "Correction names a fixed, missing or repeated geometric group.");
                var source = sources[correction.geometricVertex];
                Need(source.X == correction.sourceX && source.Y == correction.sourceY && source.Z == correction.sourceZ, "Height correction source XYZ differs from deterministic refinement.");
                Need(!float.IsNaN(correction.height) && !float.IsInfinity(correction.height) && Math.Abs(correction.height - source.Y) <= MaximumHeightChangeMeters, "Off-path height correction exceeds the 2 m bound.");
                changed.Add(correction.geometricVertex, correction.height);
            }
            var vertices = (Vertex[])mesh.Vertices.Clone();
            for (int i = 0; i < vertices.Length; i++) if (changed.TryGetValue(mesh.GeometricVertex[i], out float height))
            { var v = vertices[i]; vertices[i] = new Vertex(new V3(v.Position.X, height, v.Position.Z), v.Normal, v.UV, v.Mask); }
            var sums = new Dictionary<int, double[]>();
            foreach (var t in mesh.Triangles) for (int i = 0; i < t.Length; i += 3)
            {
                var a = vertices[t[i]].Position; var b = vertices[t[i + 1]].Position; var c = vertices[t[i + 2]].Position;
                double ux = (double)b.X - a.X, uy = (double)b.Y - a.Y, uz = (double)b.Z - a.Z, vx = (double)c.X - a.X, vy = (double)c.Y - a.Y, vz = (double)c.Z - a.Z;
                for (int k = 0; k < 3; k++)
                {
                    int g = mesh.GeometricVertex[t[i + k]]; if (!sums.TryGetValue(g, out var n)) { n = new double[3]; sums.Add(g, n); }
                    n[0] += uy * vz - uz * vy; n[1] += uz * vx - ux * vz; n[2] += ux * vy - uy * vx;
                }
            }
            var mouthIndices = new HashSet<int>(mesh.Mouths.SelectMany(m => m.Indices));
            for (int i = 0; i < vertices.Length; i++) if (!mouthIndices.Contains(i))
            {
                var n = sums[mesh.GeometricVertex[i]]; double length = Math.Sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2]); Need(length > 0, "Unused correction vertex.");
                var v = vertices[i]; vertices[i] = new Vertex(v.Position, new V3((float)(n[0] / length), (float)(n[1] / length), (float)(n[2] / length)), v.UV, v.Mask);
            }
            return new JunctionMesh { Vertices = vertices, Triangles = mesh.Triangles.Select(t => (int[])t.Clone()).ToArray(), GeometricVertex = (int[])mesh.GeometricVertex.Clone(),
                OuterBoundary = (int[])mesh.OuterBoundary.Clone(), Mouths = mesh.Mouths.Select(m => new MouthJoin { Id = m.Id, Indices = (int[])m.Indices.Clone() }).ToArray(), AttributeSeamDuplicates = mesh.AttributeSeamDuplicates };
        }

        static bool OnPath(V3 p, V3 a, V3 b)
        {
            double x = (double)b.X - a.X, z = (double)b.Z - a.Z, px = (double)p.X - a.X, pz = (double)p.Z - a.Z, length = Math.Sqrt(x * x + z * z);
            if (length == 0) return Math.Sqrt(px * px + pz * pz) <= PathTolerance;
            double along = (px * x + pz * z) / length;
            return along >= -PathTolerance && along <= length + PathTolerance && Math.Abs(px * z - pz * x) / length <= PathTolerance;
        }
        static void Need(bool ok, string message) { if (!ok) throw new ArgumentException("Junction height correction: " + message); }
    }
}
