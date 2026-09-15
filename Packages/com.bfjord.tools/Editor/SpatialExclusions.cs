using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Bwork.Authoring.Editor
{
    /// <summary>Immutable XZ footprint index for caller-owned generated geometry and recipe primitives.</summary>
    public sealed class SpatialExclusions
    {
        const float CellSize = 16;
        static readonly string[] OwnedGroups =
        {
            "Roads", "Water Sample", "Connected Water Sample", "Structure Sample", "Structures", "Bwork Structures [owned:bwork_structures:v1]",
        };

        [Serializable]
        public sealed class Primitive
        {
            public string id, kind; // circle, capsule, or box
            public Vector2 a, b, size;
            public float radius;
        }

        readonly struct Triangle
        {
            public readonly Vector2 a, b, c;
            public Triangle(Vector3 a, Vector3 b, Vector3 c)
            { this.a = new Vector2(a.x, a.z); this.b = new Vector2(b.x, b.z); this.c = new Vector2(c.x, c.z); }
        }

        readonly List<Triangle> triangles = new List<Triangle>();
        readonly Dictionary<long, List<int>> cells = new Dictionary<long, List<int>>();
        readonly Primitive[] primitives;
        public int TriangleCount => triangles.Count;
        public int PrimitiveCount => primitives.Length;
        public int GroupCount { get; }

        SpatialExclusions(Transform root, Primitive[] primitives, bool includeWater)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            this.primitives = Validate(primitives ?? Array.Empty<Primitive>());
            var children = root.Cast<Transform>().ToArray();
            var groups = new List<Transform>();
            foreach (string name in OwnedGroups)
            {
                if (!includeWater && (name == "Water Sample" || name == "Connected Water Sample")) continue;
                var matches = children.Where(child => child.name == name).ToArray();
                if (matches.Length > 1) throw new InvalidOperationException("Duplicate tool-owned exclusion root: " + name);
                if (matches.Length == 1) groups.Add(matches[0]);
            }
            GroupCount = groups.Count;
            foreach (var filter in groups.SelectMany(group => group.GetComponentsInChildren<MeshFilter>(true)))
                Add(filter);
        }

        public static SpatialExclusions Create(Transform root, Primitive[] primitives = null, bool includeWater = true) =>
            new SpatialExclusions(root, primitives, includeWater);

        public bool Intersects(Vector3 point, float footprintRadius)
        {
            if (!Finite(point) || !float.IsFinite(footprintRadius) || footprintRadius < 0 || footprintRadius > 100)
                throw new ArgumentOutOfRangeException(nameof(footprintRadius));
            var p = new Vector2(point.x, point.z);
            float radiusSquared = footprintRadius * footprintRadius;
            int range = Mathf.CeilToInt(footprintRadius / CellSize);
            int cx = Cell(p.x), cz = Cell(p.y);
            var visited = new HashSet<int>();
            for (int z = cz - range; z <= cz + range; z++)
            for (int x = cx - range; x <= cx + range; x++)
                if (cells.TryGetValue(Key(x, z), out var bucket))
                    foreach (int index in bucket)
                        if (visited.Add(index) && DistanceSquared(p, triangles[index]) <= radiusSquared) return true;
            foreach (var primitive in primitives)
                if (PrimitiveDistanceSquared(p, primitive) <= footprintRadius * footprintRadius) return true;
            return false;
        }

        void Add(MeshFilter filter)
        {
            var mesh = filter.sharedMesh;
            string path = mesh == null ? null : AssetDatabase.GetAssetPath(mesh);
            bool ownedAsset = !string.IsNullOrEmpty(path) &&
                (path.StartsWith(ToolSandbox.Generated + "/", StringComparison.Ordinal) ||
                    path.StartsWith(ProjectContext.Current.natureRoot + "/", StringComparison.Ordinal) ||
                    path.StartsWith(ProjectContext.Current.matureFirRoot + "/", StringComparison.Ordinal));
            if (mesh == null || !mesh.isReadable || !ownedAsset)
                throw new InvalidOperationException("Owned exclusion geometry requires a readable generated or shared catalog mesh: " + filter.name);
            var vertices = mesh.vertices;
            var indices = mesh.triangles;
            if (indices.Length % 3 != 0) throw new InvalidOperationException("Generated mesh has an invalid triangle index buffer: " + path);
            var matrix = filter.transform.localToWorldMatrix;
            for (int i = 0; i < indices.Length; i += 3)
            {
                int ia = indices[i], ib = indices[i + 1], ic = indices[i + 2];
                if ((uint)ia >= vertices.Length || (uint)ib >= vertices.Length || (uint)ic >= vertices.Length)
                    throw new InvalidOperationException("Generated mesh triangle index is out of range: " + path);
                Add(new Triangle(matrix.MultiplyPoint3x4(vertices[ia]), matrix.MultiplyPoint3x4(vertices[ib]),
                    matrix.MultiplyPoint3x4(vertices[ic])));
            }
        }

        void Add(Triangle triangle)
        {
            int index = triangles.Count;
            triangles.Add(triangle);
            int x0 = Cell(Mathf.Min(triangle.a.x, Mathf.Min(triangle.b.x, triangle.c.x)));
            int x1 = Cell(Mathf.Max(triangle.a.x, Mathf.Max(triangle.b.x, triangle.c.x)));
            int z0 = Cell(Mathf.Min(triangle.a.y, Mathf.Min(triangle.b.y, triangle.c.y)));
            int z1 = Cell(Mathf.Max(triangle.a.y, Mathf.Max(triangle.b.y, triangle.c.y)));
            for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
            {
                long key = Key(x, z);
                if (!cells.TryGetValue(key, out var bucket)) cells.Add(key, bucket = new List<int>());
                bucket.Add(index);
            }
        }

        static float DistanceSquared(Vector2 p, Triangle triangle)
        {
            float ab = Cross(triangle.b - triangle.a, p - triangle.a);
            float bc = Cross(triangle.c - triangle.b, p - triangle.b);
            float ca = Cross(triangle.a - triangle.c, p - triangle.c);
            float area = Cross(triangle.b - triangle.a, triangle.c - triangle.a);
            if (Mathf.Abs(area) > 1e-8f &&
                ((ab >= 0 && bc >= 0 && ca >= 0) || (ab <= 0 && bc <= 0 && ca <= 0))) return 0;
            return Mathf.Min(SegmentDistanceSquared(p, triangle.a, triangle.b),
                Mathf.Min(SegmentDistanceSquared(p, triangle.b, triangle.c), SegmentDistanceSquared(p, triangle.c, triangle.a)));
        }

        static float PrimitiveDistanceSquared(Vector2 p, Primitive primitive)
        {
            if (primitive.kind == "circle")
            { float distance = Mathf.Max(0, Vector2.Distance(p, primitive.a) - primitive.radius); return distance * distance; }
            if (primitive.kind == "capsule")
            {
                float distance = Mathf.Sqrt(SegmentDistanceSquared(p, primitive.a, primitive.b));
                distance = Mathf.Max(0, distance - primitive.radius); return distance * distance;
            }
            var delta = new Vector2(Mathf.Max(0, Mathf.Abs(p.x - primitive.a.x) - primitive.size.x * .5f),
                Mathf.Max(0, Mathf.Abs(p.y - primitive.a.y) - primitive.size.y * .5f));
            return delta.sqrMagnitude;
        }

        static Primitive[] Validate(Primitive[] values)
        {
            if (values.Length > 256) throw new ArgumentException("At most 256 stored exclusion primitives are supported.");
            foreach (var value in values)
                if (value == null || string.IsNullOrWhiteSpace(value.id) ||
                    (value.kind != "circle" && value.kind != "capsule" && value.kind != "box") ||
                    !Finite(value.a) || !Finite(value.b) || !Finite(value.size) || !float.IsFinite(value.radius) ||
                    ((value.kind == "circle" || value.kind == "capsule") && (value.radius < 0 || value.radius > 100)) ||
                    (value.kind == "box" && (value.size.x <= 0 || value.size.y <= 0)))
                    throw new ArgumentException("Stored exclusion primitives must have a unique ID and finite circle, capsule, or box geometry.");
            if (values.Select(value => value.id).Distinct(StringComparer.Ordinal).Count() != values.Length)
                throw new ArgumentException("Stored exclusion primitive IDs must be unique.");
            return values.ToArray();
        }

        static float SegmentDistanceSquared(Vector2 p, Vector2 a, Vector2 b)
        { var d = b - a; float t = d.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude) : 0; return (p - (a + d * t)).sqrMagnitude; }
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        static int Cell(float value) => Mathf.FloorToInt(value / CellSize);
        static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
        static bool Finite(Vector2 value) => float.IsFinite(value.x) && float.IsFinite(value.y);
        static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
