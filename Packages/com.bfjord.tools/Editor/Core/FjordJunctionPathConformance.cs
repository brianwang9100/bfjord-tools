using System;
using System.Collections.Generic;
using System.Linq;

namespace Bwork.FjordCoast.Junctions
{
    /// <summary>Inserts native hub-to-mouth centerpaths into an existing junction disk. The
    /// physical footprint, material regions, original mouth attributes and boundary XYZ stay
    /// fixed. Pavement follows the exact paths with bounded transverse grades.</summary>
    public static class FjordJunctionPathConformance
    {
        public const int MaximumVertices = 16384;
        public const int MaximumTriangles = 32768;
        public const double MaximumPavementSlope = .30;
        // World coordinates near 11.5 km have approximately 1 mm float spacing. Predicate
        // tolerance is positional, never an area epsilon that grows with triangle size.
        const double PositionTolerance = .0015;
        const double HeightTolerance = .003;

        /// <summary>Paths contain exact native samples ordered hub to exact mouth center,
        /// including both endpoints. Returns a fresh mesh; input arrays are never mutated.
        /// Native Y is used with zero clearance. Rejects incompatible crossing heights,
        /// paths outside pavement/edge slots 0/1, and exceeded finite geometry bounds.</summary>
        public static JunctionMesh Apply(JunctionMesh mesh, V3[][] paths)
        {
            return new Cutter(mesh, paths).Run();
        }

        internal static JunctionMesh RefineTerrainGrid(JunctionMesh mesh, V3[][] paths, float spacing)
        {
            return new Cutter(mesh, paths).RunGrid(spacing);
        }

        sealed class Face
        {
            public readonly int Slot, A, B, C;
            public Face(int slot, int a, int b, int c) { Slot = slot; A = a; B = b; C = c; }
            public int At(int i) => i == 0 ? A : i == 1 ? B : C;
        }

        sealed class Cutter
        {
            readonly JunctionMesh source;
            readonly V3[][] paths;
            readonly List<Vertex> vertices;
            readonly List<int> groups;
            readonly List<V3> positions = new List<V3>();
            readonly List<Face> faces = new List<Face>();
            readonly HashSet<int> boundary = new HashSet<int>();
            readonly HashSet<int> mouthVertices = new HashSet<int>();
            readonly Dictionary<int, float> constrainedHeights = new Dictionary<int, float>();
            long work;

            public Cutter(JunctionMesh mesh, V3[][] nativePaths)
            {
                Need(mesh != null && mesh.Vertices != null && mesh.GeometricVertex != null &&
                    mesh.Vertices.Length == mesh.GeometricVertex.Length && mesh.Vertices.Length <= MaximumVertices,
                    "Bounded mesh with geometric groups required.");
                Need(mesh.Triangles != null && mesh.Triangles.Length == 4 && mesh.Mouths != null &&
                    mesh.Mouths.Length >= 3 && mesh.Mouths.Length <= 4 && mesh.OuterBoundary != null, "Complete four-slot junction required.");
                Need(nativePaths != null && nativePaths.Length == mesh.Mouths.Length && nativePaths.All(p => p != null && p.Length >= 2) &&
                    nativePaths.Sum(p => p.Length) <= 1024, "One hub-to-mouth path per mouth, at most 1024 total samples required.");
                source = mesh; paths = nativePaths; vertices = new List<Vertex>(mesh.Vertices);
                groups = new List<int>(mesh.Vertices.Length);
                var remap = new Dictionary<int, int>();
                for (int i = 0; i < vertices.Count; i++)
                {
                    var p = vertices[i].Position; Need(Finite(p), "Nonfinite input position.");
                    if (!remap.TryGetValue(mesh.GeometricVertex[i], out int g))
                    { g = positions.Count; remap.Add(mesh.GeometricVertex[i], g); positions.Add(p); }
                    else Need(positions[g].Equals(p), "Geometric seam has conflicting XYZ.");
                    groups.Add(g);
                }
                foreach (int i in mesh.OuterBoundary) { Index(i); boundary.Add(groups[i]); }
                foreach (var mouth in mesh.Mouths)
                {
                    Need(mouth != null && mouth.Indices != null && mouth.Indices.Length == 9, "Exact nine-row mouth required.");
                    foreach (int i in mouth.Indices) { Index(i); mouthVertices.Add(i); boundary.Add(groups[i]); }
                }
                for (int slot = 0; slot < 4; slot++)
                {
                    var t = mesh.Triangles[slot]; Need(t != null && t.Length % 3 == 0, "Invalid triangles.");
                    for (int i = 0; i < t.Length; i += 3) AddFace(slot, t[i], t[i + 1], t[i + 2]);
                }
                var usedMouths = new HashSet<int>();
                foreach (var path in paths)
                {
                    Need(path.All(Finite) && SameXZ(path[0], paths[0][0]) && Math.Abs(path[0].Y - paths[0][0].Y) <= HeightTolerance,
                        "Paths must share one finite native hub.");
                    int m = Array.FindIndex(mesh.Mouths, mouth => vertices[mouth.Indices[4]].Position.Equals(path[path.Length - 1]));
                    Need(m >= 0 && usedMouths.Add(m), "Each path must end at a distinct exact native mouth center.");
                    for (int i = 1; i < path.Length; i++) Need(Distance(path[i - 1], path[i]) > PositionTolerance * 2, "Repeated/too-short native path segment.");
                }
            }

            public JunctionMesh Run()
            {
                // Endpoints first: every subsequent crossing is on a shared mesh edge.
                foreach (var path in paths) foreach (var p in path) SetHeight(InsertPoint(p), p.Y);
                foreach (var path in paths) for (int i = 1; i < path.Length; i++) CutSegment(path[i - 1], path[i]);
                // Later paths can split earlier constraints. Recheck their complete chains
                // and shared heights once all intersections have been inserted.
                foreach (var path in paths) for (int i = 1; i < path.Length; i++) ValidateSegment(path[i - 1], path[i]);
                FitPavementHeights();
                ImprovePavementTriangles();
                BoundPavementSlope();
                return Finish();
            }

            void BoundPavementSlope()
            {
                var pavement = faces.Where(f => f.Slot <= 1).ToArray();
                bool settled = false;
                for (int pass = 0; pass < 512; pass++)
                {
                    double maximum = 0;
                    foreach (var face in pavement)
                    {
                        int ga = groups[face.A], gb = groups[face.B], gc = groups[face.C];
                        var a = positions[ga]; var b = positions[gb]; var c = positions[gc];
                        double area = Area(a, b, c);
                        double ax = (b.Z - c.Z) / area, bx = (c.Z - a.Z) / area, cx = (a.Z - b.Z) / area;
                        double az = (c.X - b.X) / area, bz = (a.X - c.X) / area, cz = (b.X - a.X) / area;
                        double x = ax * a.Y + bx * b.Y + cx * c.Y, z = az * a.Y + bz * b.Y + cz * c.Y;
                        double slope = Math.Sqrt(x * x + z * z); maximum = Math.Max(maximum, slope);
                        if (slope <= MaximumPavementSlope + 1e-5) continue;
                        double Derivative(int g, double dx, double dz) => boundary.Contains(g) || constrainedHeights.ContainsKey(g) ? 0 : (x * dx + z * dz) / slope;
                        double da = Derivative(ga, ax, az), db = Derivative(gb, bx, bz), dc = Derivative(gc, cx, cz);
                        double norm = da * da + db * db + dc * dc;
                        Need(norm > 1e-16, "Fixed path/mouth heights exceed the 30% pavement surface slope limit.");
                        // Small margin prevents float storage rounding from reopening a constraint.
                        double step = (slope - (MaximumPavementSlope - .0001)) / norm;
                        void Move(int g, V3 p, double derivative) { if (derivative != 0) positions[g] = new V3(p.X, (float)(p.Y - step * derivative), p.Z); }
                        Move(ga, a, da); Move(gb, b, db); Move(gc, c, dc);
                    }
                    if (maximum <= MaximumPavementSlope + 1e-5) { settled = true; break; }
                }
                Need(settled, "Cannot fit the constrained junction within the 30% pavement surface slope limit.");
                for (int i = 0; i < vertices.Count; i++)
                { var v = vertices[i]; vertices[i] = new Vertex(positions[groups[i]], v.Normal, v.UV, v.Mask); }
            }

            void FitPavementHeights()
            {
                // The previous corner rim interpolated only hub/mouth heights, disagreeing
                // with intermediate path samples by metres. Blend nearest path heights
                // across the interior before enforcing the plane-slope constraints.
                var pavement = new HashSet<int>(faces.Where(f => f.Slot <= 1).SelectMany(f => new[] { groups[f.A], groups[f.B], groups[f.C] }));
                foreach (int g in pavement)
                {
                    if (boundary.Contains(g) || constrainedHeights.ContainsKey(g)) continue;
                    var p = positions[g]; double sum = 0, weights = 0, nearest = double.PositiveInfinity;
                    foreach (var path in paths)
                    {
                        double distanceSquared = double.PositiveInfinity, height = 0;
                        for (int i = 1; i < path.Length; i++)
                        {
                            var a = path[i - 1]; var b = path[i]; double t = Math.Max(0, Math.Min(1, Projection(a, b, p)));
                            double x = a.X + ((double)b.X - a.X) * t, z = a.Z + ((double)b.Z - a.Z) * t;
                            double d = (p.X - x) * (p.X - x) + (p.Z - z) * (p.Z - z);
                            if (d < distanceSquared) { distanceSquared = d; height = a.Y + ((double)b.Y - a.Y) * t; }
                        }
                        double weight = 1 / Math.Max(distanceSquared, 1e-8);
                        sum += height * weight; weights += weight; nearest = Math.Min(nearest, distanceSquared);
                    }
                    float y = (float)(sum / weights - .08 * Math.Min(Math.Sqrt(nearest) / 2.8, 1));
                    positions[g] = new V3(p.X, y, p.Z);
                    for (int i = 0; i < vertices.Count; i++) if (groups[i] == g)
                    { var v = vertices[i]; vertices[i] = new Vertex(positions[g], v.Normal, v.UV, v.Mask); }
                }
            }

            void ImprovePavementTriangles()
            {
                // Inserting a curved centerpath into the old hub fan leaves long, narrow
                // triangles between consecutive samples. Their exact path heights can
                // create a near-vertical transverse plane. Retriangulate the free edges;
                // centerpaths, material seams, boundary attributes and XYZ stay fixed.
                var fixedEdges = new HashSet<(int, int)>();
                foreach (var edge in Edges())
                {
                    var p = positions[edge.Item1]; var q = positions[edge.Item2];
                    foreach (var path in paths) for (int i = 1; i < path.Length; i++)
                    {
                        var a = path[i - 1]; var b = path[i]; double tolerance = PositionTolerance / Distance(a, b);
                        if (LineDistance(a, b, p) <= PositionTolerance && LineDistance(a, b, q) <= PositionTolerance &&
                            Projection(a, b, p) >= -tolerance && Projection(a, b, p) <= 1 + tolerance &&
                            Projection(a, b, q) >= -tolerance && Projection(a, b, q) <= 1 + tolerance) fixedEdges.Add(edge);
                    }
                }
                int flips = 0;
                while (true)
                {
                    var incident = new Dictionary<(int, int), List<Face>>();
                    foreach (var face in faces) for (int k = 0; k < 3; k++)
                    {
                        var edge = Key(groups[face.At(k)], groups[face.At((k + 1) % 3)]);
                        if (!incident.TryGetValue(edge, out var list)) { list = new List<Face>(); incident.Add(edge, list); }
                        list.Add(face);
                    }
                    bool changed = false;
                    foreach (var pair in incident.OrderBy(p => p.Key.Item1).ThenBy(p => p.Key.Item2))
                    {
                        Tick(); if (fixedEdges.Contains(pair.Key) || pair.Value.Count != 2) continue;
                        var f = pair.Value[0]; var g = pair.Value[1];
                        if (f.Slot > 1 || f.Slot != g.Slot) continue;
                        int k = 0; while (Key(groups[f.At(k)], groups[f.At((k + 1) % 3)]) != pair.Key) k++;
                        int a = f.At(k), b = f.At((k + 1) % 3), c = f.At((k + 2) % 3);
                        int l = 0; while (Key(groups[g.At(l)], groups[g.At((l + 1) % 3)]) != pair.Key) l++;
                        int d = g.At((l + 2) % 3);
                        // Attribute seams must not be bridged by a new diagonal.
                        if (g.At(l) != b || g.At((l + 1) % 3) != a || incident.ContainsKey(Key(groups[c], groups[d]))) continue;
                        var pa = vertices[a].Position; var pb = vertices[b].Position;
                        var pc = vertices[c].Position; var pd = vertices[d].Position;
                        if (Area(pc, pd, pb) >= -1e-8 || Area(pd, pc, pa) >= -1e-8) continue;
                        double oldQuality = Math.Min(Quality(pa, pb, pc), Quality(pb, pa, pd));
                        double newQuality = Math.Min(Quality(pc, pd, pb), Quality(pd, pc, pa));
                        if (newQuality <= oldQuality + 1e-10) continue;
                        faces.Remove(f); faces.Remove(g);
                        AddFace(f.Slot, c, d, b); AddFace(f.Slot, d, c, a);
                        Need(++flips <= MaximumTriangles * 8, "Pavement triangulation work budget.");
                        changed = true; break;
                    }
                    if (!changed) break;
                }
                foreach (var path in paths) for (int i = 1; i < path.Length; i++) ValidateSegment(path[i - 1], path[i]);
            }

            static double Quality(V3 a, V3 b, V3 c)
            {
                double ab = Distance(a, b), bc = Distance(b, c), ca = Distance(c, a);
                return Math.Abs(Area(a, b, c)) / (ab * ab + bc * bc + ca * ca);
            }

            public JunctionMesh RunGrid(float spacing)
            {
                Need(spacing > 0 && spacing < 16, "Bounded Terrain grid spacing required.");
                // Subdivide only the finite pavement and its incident material edges. The
                // exact mouth/outer boundary segments stay intact; adjacent verge faces are
                // split on their shared edges so there are no internal T junctions.
                for (int axis = 0; axis < 3; axis++)
                {
                    double Coordinate(V3 p) => axis == 0 ? p.X : axis == 1 ? p.Z : (double)p.X - p.Z;
                    var pavement = faces.Where(f => f.Slot <= 1).SelectMany(f => new[] { f.A, f.B, f.C }).Distinct().ToArray();
                    double minimum = pavement.Min(i => Coordinate(vertices[i].Position)), maximum = pavement.Max(i => Coordinate(vertices[i].Position));
                    int first = (int)Math.Floor(minimum / spacing) + 1, last = (int)Math.Ceiling(maximum / spacing) - 1;
                    Need(last - first < 256, "Terrain refinement exceeds local grid bounds.");
                    for (int line = first; line <= last; line++)
                    {
                        double level = (double)line * spacing;
                        var edges = new HashSet<(int, int)>();
                        foreach (var face in faces.Where(f => f.Slot <= 1)) for (int k = 0; k < 3; k++) edges.Add(Key(groups[face.At(k)], groups[face.At((k + 1) % 3)]));
                        foreach (var edge in edges.OrderBy(e => e.Item1).ThenBy(e => e.Item2))
                        {
                            Tick(); if (BoundaryEdge(edge)) continue;
                            var a = positions[edge.Item1]; var b = positions[edge.Item2];
                            double da = Coordinate(a) - level, db = Coordinate(b) - level;
                            if (da * db >= 0) continue;
                            double t = da / (da - db);
                            var p = new V3((float)(a.X + ((double)b.X - a.X) * t), (float)(a.Y + ((double)b.Y - a.Y) * t), (float)(a.Z + ((double)b.Z - a.Z) * t));
                            if (Distance(p, a) <= PositionTolerance || Distance(p, b) <= PositionTolerance || !CanSplitEdge(edge, p)) continue;
                            SplitEdge(edge, p, t);
                        }
                    }
                }
                return Finish();
            }

            JunctionMesh Finish()
            {
                ValidateDisk();
                RecomputeNormals();
                return new JunctionMesh
                {
                    Vertices = vertices.ToArray(), GeometricVertex = groups.ToArray(),
                    Triangles = Enumerable.Range(0, 4).Select(slot => faces.Where(f => f.Slot == slot).SelectMany(f => new[] { f.A, f.B, f.C }).ToArray()).ToArray(),
                    Mouths = source.Mouths.Select(m => new MouthJoin { Id = m.Id, Indices = (int[])m.Indices.Clone() }).ToArray(),
                    OuterBoundary = (int[])source.OuterBoundary.Clone(), AttributeSeamDuplicates = vertices.Count - positions.Count
                };
            }

            int InsertPoint(V3 p)
            {
                int exact = positions.FindIndex(q => SameXZ(p, q));
                if (exact >= 0) { Need(PavementGroup(exact), "Path endpoint outside pavement."); return exact; }
                // Reuse a representable vertex within 1.5 mm instead of creating a sliver.
                int close = positions.FindIndex(q => Distance(p, q) <= PositionTolerance);
                if (close >= 0) { Need(PavementGroup(close), "Path endpoint outside pavement."); return close; }
                foreach (var edge in Edges())
                {
                    var a = positions[edge.Item1]; var b = positions[edge.Item2];
                    double t = Projection(a, b, p);
                    if (t > 0 && t < 1 && LineDistance(a, b, p) <= PositionTolerance && CanSplitEdge(edge, p))
                    {
                        Need(PavementEdge(edge), "Path point lies outside pavement/edge materials.");
                        return SplitEdge(edge, p, t);
                    }
                }
                foreach (var face in faces.ToArray())
                {
                    Tick(); if (face.Slot > 1) continue;
                    var a = vertices[face.A].Position; var b = vertices[face.B].Position; var c = vertices[face.C].Position;
                    double area = Area(a, b, c), wa = Area(p, b, c) / area, wb = Area(a, p, c) / area, wc = 1 - wa - wb;
                    if (wa < 0 || wb < 0 || wc < 0) continue;
                    int g = NewGroup(p), index = AddVertex(Blend(vertices[face.A], vertices[face.B], vertices[face.C], wa, wb, wc, p), g);
                    faces.Remove(face); AddFace(face.Slot, face.A, face.B, index); AddFace(face.Slot, face.B, face.C, index); AddFace(face.Slot, face.C, face.A, index);
                    return g;
                }
                throw new ArgumentException("Native junction path endpoint is outside physical pavement/edge coverage.");
            }

            void CutSegment(V3 a, V3 b)
            {
                // Splitting an intersected edge creates only spokes from that intersection;
                // those spokes cannot cross this segment elsewhere. The snapshot is sufficient.
                foreach (var edge in Edges())
                {
                    Tick(); var c = positions[edge.Item1]; var d = positions[edge.Item2];
                    double rx = (double)b.X - a.X, rz = (double)b.Z - a.Z, sx = (double)d.X - c.X, sz = (double)d.Z - c.Z;
                    double determinant = rx * sz - rz * sx;
                    if (Math.Abs(determinant) < 1e-12) continue;
                    double cx = (double)c.X - a.X, cz = (double)c.Z - a.Z;
                    double t = (cx * sz - cz * sx) / determinant, u = (cx * rz - cz * rx) / determinant;
                    if (t <= 0 || t >= 1 || u <= 0 || u >= 1) continue;
                    var p = new V3((float)(a.X + rx * t), (float)(a.Y + ((double)b.Y - a.Y) * t), (float)(a.Z + rz * t));
                    // Near endpoint contacts already have a representable shared vertex.
                    if (Distance(p, c) <= PositionTolerance) { SetHeight(edge.Item1, p.Y); continue; }
                    if (Distance(p, d) <= PositionTolerance) { SetHeight(edge.Item2, p.Y); continue; }
                    if (Distance(p, a) <= PositionTolerance || Distance(p, b) <= PositionTolerance) continue;
                    Need(PavementEdge(edge), "Native path crosses outside pavement/edge materials.");
                    int g = SplitEdge(edge, p, u); SetHeight(g, p.Y);
                }
                ValidateSegment(a, b);
            }

            void ValidateSegment(V3 a, V3 b)
            {
                // The surviving on-line edges must cover the entire finite segment. This
                // also detects a path that exits/reenters the physical patch between samples.
                var coverage = new List<(double, double)>(); double length = Distance(a, b), tolerance = PositionTolerance / length;
                foreach (var edge in Edges())
                {
                    var p = positions[edge.Item1]; var q = positions[edge.Item2];
                    if (LineDistance(a, b, p) > PositionTolerance || LineDistance(a, b, q) > PositionTolerance || !PavementEdge(edge)) continue;
                    double t = Projection(a, b, p), u = Projection(a, b, q), lo = Math.Max(0, Math.Min(t, u)), hi = Math.Min(1, Math.Max(t, u));
                    if (hi <= lo) continue;
                    if (t >= -tolerance && t <= 1 + tolerance) SetHeight(edge.Item1, (float)(a.Y + ((double)b.Y - a.Y) * Math.Max(0, Math.Min(1, t))));
                    if (u >= -tolerance && u <= 1 + tolerance) SetHeight(edge.Item2, (float)(a.Y + ((double)b.Y - a.Y) * Math.Max(0, Math.Min(1, u))));
                    coverage.Add((lo, hi));
                }
                double reached = 0;
                foreach (var interval in coverage.OrderBy(x => x.Item1))
                { Need(interval.Item1 <= reached + tolerance, "Centerpath constraint has an uncovered segment."); reached = Math.Max(reached, interval.Item2); }
                Need(reached >= 1 - tolerance, "Centerpath does not reach its exact mouth through pavement.");
            }

            int SplitEdge((int, int) edge, V3 position, double t)
            {
                Need(!BoundaryEdge(edge), "Native path would split an original outer boundary edge.");
                var touching = faces.Where(f => HasEdge(f, edge)).ToArray(); Need(touching.Length == 2, "Shared edge needs exactly two incident faces.");
                int g = NewGroup(position); var attributes = new Dictionary<(int, int), int>();
                foreach (var face in touching)
                {
                    int k = 0; while (Key(groups[face.At(k)], groups[face.At((k + 1) % 3)]) != edge) k++;
                    int ia = face.At(k), ib = face.At((k + 1) % 3), ic = face.At((k + 2) % 3); var attributeKey = Key(ia, ib);
                    if (!attributes.TryGetValue(attributeKey, out int added))
                    {
                        double fraction = groups[ia] == edge.Item1 ? t : 1 - t;
                        added = AddVertex(Blend(vertices[ia], vertices[ib], vertices[ib], 1 - fraction, fraction, 0, position), g); attributes.Add(attributeKey, added);
                    }
                    faces.Remove(face); AddFace(face.Slot, ia, added, ic); AddFace(face.Slot, added, ib, ic);
                }
                return g;
            }

            bool CanSplitEdge((int, int) edge, V3 p)
            {
                // Several nearly collinear spokes can be less than one float spacing
                // apart. Proximity alone must never move an edge across its opposite
                // vertex; keep the exact native sample inside its actual incident faces.
                foreach (var face in faces.Where(f => HasEdge(f, edge)))
                {
                    int k = 0; while (Key(groups[face.At(k)], groups[face.At((k + 1) % 3)]) != edge) k++;
                    var a = vertices[face.At(k)].Position; var b = vertices[face.At((k + 1) % 3)].Position; var c = vertices[face.At((k + 2) % 3)].Position;
                    if (Area(a, p, c) >= 0 || Area(p, b, c) >= 0) return false;
                }
                return true;
            }

            void SetHeight(int g, float y)
            {
                Need(PavementGroup(g), "Native height constraint outside pavement/edge materials.");
                if (constrainedHeights.TryGetValue(g, out float previous))
                { Need(Math.Abs(previous - y) <= HeightTolerance, "Intersecting native paths have incompatible heights."); return; }
                constrainedHeights.Add(g, y); var p = positions[g];
                if (boundary.Contains(g)) { Need(Math.Abs(p.Y - y) <= HeightTolerance, "Native path conflicts with fixed boundary height."); return; }
                positions[g] = new V3(p.X, y, p.Z);
                for (int i = 0; i < vertices.Count; i++) if (groups[i] == g)
                { var v = vertices[i]; vertices[i] = new Vertex(positions[g], v.Normal, v.UV, v.Mask); }
            }

            void RecomputeNormals()
            {
                var x = new double[positions.Count]; var y = new double[x.Length]; var z = new double[x.Length];
                foreach (var f in faces)
                {
                    var a = vertices[f.A].Position; var b = vertices[f.B].Position; var c = vertices[f.C].Position;
                    double ux = (double)b.X - a.X, uy = (double)b.Y - a.Y, uz = (double)b.Z - a.Z;
                    double vx = (double)c.X - a.X, vy = (double)c.Y - a.Y, vz = (double)c.Z - a.Z;
                    for (int k = 0; k < 3; k++) { int g = groups[f.At(k)]; x[g] += uy * vz - uz * vy; y[g] += uz * vx - ux * vz; z[g] += ux * vy - uy * vx; }
                }
                for (int i = 0; i < vertices.Count; i++) if (!mouthVertices.Contains(i))
                {
                    int g = groups[i]; double length = Math.Sqrt(x[g] * x[g] + y[g] * y[g] + z[g] * z[g]); Need(length > 0, "Unused/zero normal vertex.");
                    var v = vertices[i]; vertices[i] = new Vertex(v.Position, new V3((float)(x[g] / length), (float)(y[g] / length), (float)(z[g] / length)), v.UV, v.Mask);
                }
            }

            void ValidateDisk()
            {
                var counts = new Dictionary<(int, int), int>(); var used = new HashSet<int>();
                foreach (var f in faces) for (int k = 0; k < 3; k++)
                {
                    int a = groups[f.At(k)], b = groups[f.At((k + 1) % 3)]; used.Add(a); var key = Key(a, b);
                    counts.TryGetValue(key, out int n); counts[key] = n + 1;
                }
                var expected = new HashSet<(int, int)>();
                for (int i = 0; i < source.OuterBoundary.Length; i++) expected.Add(Key(groups[source.OuterBoundary[i]], groups[source.OuterBoundary[(i + 1) % source.OuterBoundary.Length]]));
                Need(counts.All(e => e.Value <= 2) && expected.SetEquals(counts.Where(e => e.Value == 1).Select(e => e.Key)) && used.Count - counts.Count + faces.Count == 1,
                    "Constrained mesh has a hole, T junction or nonmanifold edge.");
                // Every subdivision has positive upward area and preserves the original simple
                // disk boundary. The disk therefore has no overlapping interior sheets.
                foreach (var f in faces) Need(Area(vertices[f.A].Position, vertices[f.B].Position, vertices[f.C].Position) < 0, "Folded constrained face.");
            }

            (int, int)[] Edges()
            {
                var result = new HashSet<(int, int)>();
                foreach (var f in faces) for (int k = 0; k < 3; k++) result.Add(Key(groups[f.At(k)], groups[f.At((k + 1) % 3)]));
                return result.OrderBy(e => e.Item1).ThenBy(e => e.Item2).ToArray();
            }
            bool PavementGroup(int g) => faces.Any(f => f.Slot <= 1 && (groups[f.A] == g || groups[f.B] == g || groups[f.C] == g));
            bool PavementEdge((int, int) edge) => faces.Any(f => f.Slot <= 1 && HasEdge(f, edge));
            bool HasEdge(Face f, (int, int) edge) => Enumerable.Range(0, 3).Any(k => Key(groups[f.At(k)], groups[f.At((k + 1) % 3)]) == edge);
            bool BoundaryEdge((int, int) edge)
            {
                for (int i = 0; i < source.OuterBoundary.Length; i++) if (Key(groups[source.OuterBoundary[i]], groups[source.OuterBoundary[(i + 1) % source.OuterBoundary.Length]]) == edge) return true;
                return false;
            }
            int NewGroup(V3 p) { Need(Finite(p) && positions.Count < MaximumVertices, "Geometric vertex budget/nonfinite position."); positions.Add(p); return positions.Count - 1; }
            int AddVertex(Vertex v, int g) { Need(vertices.Count < MaximumVertices, "Constrained junction exceeds 16384 attribute vertices."); vertices.Add(v); groups.Add(g); return vertices.Count - 1; }
            void AddFace(int slot, int a, int b, int c)
            {
                Index(a); Index(b); Index(c); Need(faces.Count < MaximumTriangles, "Constrained junction exceeds 32768 triangles.");
                double area = Area(vertices[a].Position, vertices[b].Position, vertices[c].Position);
                Need(area < 0, $"Constraint insertion folds/collapses slot {slot} triangle {a}/{b}/{c} (area {area:R})."); faces.Add(new Face(slot, a, b, c));
            }
            void Index(int i) => Need(i >= 0 && i < vertices.Count, "Invalid mesh index.");
            void Tick() { Need(++work <= 64000000, "Constrained junction work budget."); }
        }

        static Vertex Blend(Vertex a, Vertex b, Vertex c, double wa, double wb, double wc, V3 p) => new Vertex(p, new V3(0, 1, 0),
            new V2((float)(a.UV.X * wa + b.UV.X * wb + c.UV.X * wc), (float)(a.UV.Y * wa + b.UV.Y * wb + c.UV.Y * wc)),
            new V2((float)(a.Mask.X * wa + b.Mask.X * wb + c.Mask.X * wc), (float)(a.Mask.Y * wa + b.Mask.Y * wb + c.Mask.Y * wc)));
        static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);
        static bool SameXZ(V3 a, V3 b) => a.X == b.X && a.Z == b.Z;
        static double Area(V3 a, V3 b, V3 c) => ((double)b.X - a.X) * ((double)c.Z - a.Z) - ((double)b.Z - a.Z) * ((double)c.X - a.X);
        static double Distance(V3 a, V3 b) { double x = (double)a.X - b.X, z = (double)a.Z - b.Z; return Math.Sqrt(x * x + z * z); }
        static double Projection(V3 a, V3 b, V3 p) { double x = (double)b.X - a.X, z = (double)b.Z - a.Z; return (((double)p.X - a.X) * x + ((double)p.Z - a.Z) * z) / (x * x + z * z); }
        static double LineDistance(V3 a, V3 b, V3 p) => Math.Abs(Area(a, b, p)) / Distance(a, b);
        static bool Finite(V3 p) => !(float.IsNaN(p.X) || float.IsNaN(p.Y) || float.IsNaN(p.Z) || float.IsInfinity(p.X) || float.IsInfinity(p.Y) || float.IsInfinity(p.Z)) && Math.Abs(p.X) <= 20000 && Math.Abs(p.Z) <= 20000 && Math.Abs(p.Y) <= 5000;
        static void Need(bool ok, string message) { if (!ok) throw new ArgumentException("Junction native-path surface: " + message); }
    }
}
