using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Bwork.Authoring.Editor
{
    /// <summary>Builds ordinary shared LOD prefabs from the retained CC0 model library.</summary>
    public static class FoliagePresentation
    {
        const string Revision = "foliage-presentation-1";
        static string Root => ToolSandbox.Generated + "/FoliageLibrary";
        static string ReceiptPath => Root + "/catalog.json";
        [Serializable] public sealed class Catalog { public string revision, fingerprint, directory; public Entry[] entries; }
        [Serializable] public sealed class Entry
        {
            public string id, prefabPath;
            public int[] triangles;
            public float height, windAmplitude, boundsPadding;
        }
        public static string[] Names => new[] { "MatureFir_A", "pine_sapling_small_b" }
            .Concat(new[] { "a", "b", "c", "d" }.Select(s => "fern_02_" + s))
            .Concat(new[] { "a", "b", "c", "d" }.Select(s => "shrub_03_" + s))
            .Concat(Enumerable.Range(1, 6).Select(n => "rock_moss_set_01_rock" + n.ToString("00"))).ToArray();

        public static object Status()
        {
            var catalog = Read();
            return new { prepared = catalog != null, catalog = catalog, sourceModels = Names.Select(n => new { id = n, available = AssetDatabase.LoadAssetAtPath<GameObject>(Source(n)) != null }).ToArray() };
        }

        public static object View()
        {
            var terrain = ToolSandbox.RequireTerrain();
            var camera = ToolSandbox.Root.GetComponentInChildren<Camera>();
            if (camera == null) throw new InvalidOperationException("The sandbox camera is missing.");
            Vector3 Ground(float x, float z, float height) => new Vector3(x,
                terrain.transform.position.y + terrain.SampleHeight(new Vector3(x, 0, z)) + height, z);
            camera.transform.position = Ground(222, 195, 1.45f);
            camera.transform.LookAt(Ground(201, 185, 1.6f));
            camera.orthographic = false; camera.fieldOfView = 62;
            var sun = ToolSandbox.Root.GetComponentsInChildren<Light>()
                .FirstOrDefault(light => light.type == LightType.Directional);
            if (sun != null) sun.transform.rotation = Quaternion.Euler(48, -115, 0);
            var position = camera.transform.position;
            return new { camera = camera.name, position = new { x = position.x, y = position.y, z = position.z }, view = "forest-edge" };
        }

        public static Catalog Build()
        {
            ToolSandbox.RequireTerrain();
            var shader = Shader.Find("BFjord/Foliage Wind") ?? throw new InvalidOperationException("BFjord foliage wind shader is missing.");
            var missing = Names.Where(n => AssetDatabase.LoadAssetAtPath<GameObject>(Source(n)) == null).ToArray();
            if (missing.Length != 0) throw new FileNotFoundException("Restore the CC0 asset catalog before building foliage: " + string.Join(", ", missing));
            var inputs = Names.Select(Source).Concat(new[] { AssetDatabase.GetAssetPath(shader) }).Concat(
                Names.SelectMany(MaterialDependencyPaths))
                .Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            // Dependency hashes include the importer and the prepared materials' texture dependencies.
            string fingerprint = Hash128.Compute(Revision + string.Join("|", inputs.Select(p => p + ":" + AssetDatabase.GetAssetDependencyHash(p)))).ToString();
            var prior = Read();
            if (prior != null && prior.fingerprint == fingerprint && prior.entries.All(e => AssetDatabase.LoadAssetAtPath<GameObject>(e.prefabPath) != null)) return prior;
            string directory = Root + "/" + fingerprint + "-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(directory);
            try
            {
                var entries = Names.Select(n => BuildPrefab(n, directory, shader)).ToArray();
                foreach (var guid in AssetDatabase.FindAssets("", new[] { directory })) AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)));
                var catalog = new Catalog { revision = Revision, fingerprint = fingerprint, directory = directory, entries = entries };
                File.WriteAllText(ReceiptPath, JsonUtility.ToJson(catalog, true));
                AssetDatabase.ImportAsset(ReceiptPath, ImportAssetOptions.ForceSynchronousImport);
                // Old library generations can still be referenced by existing batches; leave them intact.
                return catalog;
            }
            catch { AssetDatabase.DeleteAsset(directory); throw; }
        }

        public static string Resolve(string key)
        {
            if (!key.StartsWith("bfjord:", StringComparison.Ordinal)) return ProjectContext.RemapLegacyAssetPath(key);
            string id = key.Substring(7);
            var entry = Read()?.entries?.SingleOrDefault(e => e.id == id);
            return entry?.prefabPath ?? throw new InvalidOperationException("Unknown or unprepared foliage variant " + id + "; run bwork_foliage action=build-assets first.");
        }

        static Catalog Read()
        {
            if (!File.Exists(ReceiptPath)) return null;
            var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(ReceiptPath));
            if (catalog == null || catalog.revision != Revision || catalog.entries == null || catalog.entries.Length != Names.Length ||
                string.IsNullOrEmpty(catalog.directory) || !catalog.directory.StartsWith(Root + "/", StringComparison.Ordinal) ||
                catalog.entries.Any(e => e == null || !Names.Contains(e.id) || string.IsNullOrEmpty(e.prefabPath) ||
                    !e.prefabPath.StartsWith(catalog.directory + "/", StringComparison.Ordinal) || e.prefabPath.Contains("..")))
                throw new InvalidDataException("Foliage catalog receipt is invalid; preserve the library for recovery.");
            return catalog;
        }

        static string Source(string id) => (id == "MatureFir_A" ? ProjectContext.Current.matureFirRoot : ProjectContext.Current.natureRoot) + "/Models/" + id + ".fbx";

        static Entry BuildPrefab(string id, string directory, Shader shader)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source(id));
            var root = new GameObject(id);
            bool rigid = id.StartsWith("rock_", StringComparison.Ordinal);
            var filters = source.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0) throw new InvalidDataException("Model has no mesh filters: " + id);
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null) throw new InvalidDataException("Model mesh is missing: " + id + "/" + filter.name);
                var matrix = source.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                ValidateMatrix(matrix, id + "/" + filter.name);
                var partBounds = TransformBounds(filter.sharedMesh.bounds, matrix);
                if (!hasBounds) { bounds = partBounds; hasBounds = true; }
                else bounds.Encapsulate(partBounds);
            }
            float amplitude = rigid ? 0 : id == "MatureFir_A" ? .22f : id.StartsWith("pine_", StringComparison.Ordinal) ? .055f : .035f;
            // Maximum horizontal shader displacement is amplitude*sqrt(1+.18^2). Scale may be as low as .5.
            float padding = amplitude * 2.04f;
            var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
            var materialBindings = MaterialBindings(id);
            var renderers = new List<Renderer>[] { new List<Renderer>(), new List<Renderer>(), new List<Renderer>() };
            var triangles = new int[3];
            try
            {
                foreach (var filter in filters)
                {
                    var match = Regex.Match(filter.name, "_LOD([012])(?:_|$)");
                    if (!match.Success) continue;
                    int lod = int.Parse(match.Groups[1].Value);
                    var child = new GameObject(filter.name); child.transform.SetParent(root.transform, false);
                    Mesh mesh = filter.sharedMesh;
                    var matrix = source.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    bool bake = !ApproximatelyIdentity(matrix);
                    if (!rigid || bake)
                    {
                        mesh = Object.Instantiate(mesh); mesh.name = id + "-" + filter.name;
                        if (bake) BakeToRoot(mesh, matrix, id + "/" + filter.name);
                        var expanded = mesh.bounds; expanded.Expand(padding * 2); mesh.bounds = expanded;
                        AssetDatabase.CreateAsset(mesh, directory + "/" + mesh.name + ".asset");
                    }
                    child.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = child.AddComponent<MeshRenderer>();
                    if (filter.GetComponent<MeshRenderer>() == null) throw new InvalidDataException("Mesh renderer is missing: " + filter.name);
                    if (!materialBindings.TryGetValue(RendererKey(filter.name), out var sourceMaterials))
                        throw new InvalidDataException("The reviewed prefab has no material binding for " + id + "/" + filter.name);
                    if (sourceMaterials.Length != mesh.subMeshCount)
                        throw new InvalidDataException("Prepared material slots do not match the model submeshes: " + id + "/" + filter.name);
                    renderer.sharedMaterials = sourceMaterials.Select(sourceMaterial =>
                        MaterialFor(sourceMaterial, id, rigid, bounds, amplitude, directory, shader, materials)).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                    renderers[lod].Add(renderer);
                    for (int s = 0; s < mesh.subMeshCount; s++) triangles[lod] += (int)(mesh.GetIndexCount(s) / 3);
                }
                if (renderers.Any(r => r.Count == 0)) throw new InvalidDataException("Three nonempty LODs are required for " + id);
                var group = root.AddComponent<LODGroup>();
                bool canopy = id == "MatureFir_A";
                group.SetLODs(new[] { new LOD(canopy ? .28f : .16f, renderers[0].ToArray()),
                    new LOD(canopy ? .12f : .055f, renderers[1].ToArray()), new LOD(canopy ? .018f : .012f, renderers[2].ToArray()) });
                group.fadeMode = LODFadeMode.None;
                group.RecalculateBounds();
                string path = directory + "/" + id + ".prefab";
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new IOException("Could not publish foliage prefab " + id);
                return new Entry { id = id, prefabPath = path, height = bounds.size.y, triangles = triangles, windAmplitude = amplitude, boundsPadding = padding };
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void BakeToRoot(Mesh mesh, Matrix4x4 matrix, string label)
        {
            ValidateMatrix(matrix, label);
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var tangents = mesh.tangents;
            if (vertices.Length == 0 || normals.Length != vertices.Length || tangents.Length != vertices.Length)
                throw new InvalidDataException("Root-space foliage baking requires positions, normals, and tangents: " + label);
            var normalMatrix = matrix.inverse.transpose;
            float handedness = matrix.determinant < 0 ? -1 : 1;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                var normal = normalMatrix.MultiplyVector(normals[i]);
                if (!Finite(normal) || normal.sqrMagnitude < 1e-12f) throw new InvalidDataException("Invalid transformed foliage normal: " + label);
                normal.Normalize(); normals[i] = normal;
                var tangent = matrix.MultiplyVector(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z));
                tangent -= normal * Vector3.Dot(normal, tangent);
                if (!Finite(tangent) || tangent.sqrMagnitude < 1e-12f || !float.IsFinite(tangents[i].w))
                    throw new InvalidDataException("Invalid transformed foliage tangent: " + label);
                tangent.Normalize(); tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w * handedness);
            }
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.tangents = tangents;
            if (handedness < 0)
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                        throw new InvalidDataException("Mirrored foliage baking requires triangle topology: " + label);
                    var indices = mesh.GetTriangles(submesh, true);
                    for (int i = 0; i < indices.Length; i += 3) (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
                    mesh.SetTriangles(indices, submesh, false);
                }
            mesh.RecalculateBounds();
        }

        static Bounds TransformBounds(Bounds source, Matrix4x4 matrix)
        {
            var x = matrix.MultiplyVector(new Vector3(source.extents.x, 0, 0));
            var y = matrix.MultiplyVector(new Vector3(0, source.extents.y, 0));
            var z = matrix.MultiplyVector(new Vector3(0, 0, source.extents.z));
            var extents = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            var result = new Bounds(matrix.MultiplyPoint3x4(source.center), extents * 2);
            if (!Finite(result.center) || !Finite(result.extents)) throw new InvalidDataException("Invalid transformed foliage bounds.");
            return result;
        }

        static void ValidateMatrix(Matrix4x4 matrix, string label)
        {
            for (int row = 0; row < 4; row++) for (int column = 0; column < 4; column++)
                if (!float.IsFinite(matrix[row, column])) throw new InvalidDataException("Nonfinite foliage transform: " + label);
            if (!float.IsFinite(matrix.determinant) || Mathf.Abs(matrix.determinant) < 1e-8f)
                throw new InvalidDataException("Singular foliage transform: " + label);
        }

        static bool ApproximatelyIdentity(Matrix4x4 matrix)
        {
            var identity = Matrix4x4.identity;
            for (int row = 0; row < 4; row++) for (int column = 0; column < 4; column++)
                if (Mathf.Abs(matrix[row, column] - identity[row, column]) > 1e-6f) return false;
            return true;
        }

        static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        static Material MaterialFor(Material source, string id, bool rigid, Bounds bounds, float amplitude, string directory, Shader shader, Dictionary<string, Material> cache)
        {
            if (source == null) throw new InvalidDataException("The reviewed prefab has an unbound material: " + id);
            string name = source.name.Replace(" (Instance)", "");
            if (cache.TryGetValue(name, out var value)) return value;
            value = new Material(source) { name = id + "-" + name, enableInstancing = true };
            if (!rigid)
            {
                value.shader = shader;
                value.SetFloat("_WindRootY", bounds.min.y);
                value.SetFloat("_WindHeight", Mathf.Max(.1f, bounds.size.y));
                value.SetFloat("_WindAmplitude", amplitude);
                value.SetFloat("_WindSpeed", 1);
                value.SetFloat("_Transmission", .12f);
                value.SetFloat("_Cull", 0);
                if (value.GetTexture("_BumpMap") != null) value.EnableKeyword("_NORMALMAP");
                if (value.GetTexture("_MetallicGlossMap") != null) value.EnableKeyword("_METALLICSPECGLOSSMAP");
                value.renderQueue = 2450;
            }
            AssetDatabase.CreateAsset(value, directory + "/" + value.name + ".mat");
            cache.Add(name, value);
            return value;
        }

        static IEnumerable<string> MaterialDependencyPaths(string id)
        {
            string prefabPath = MaterialTemplate(id);
            yield return prefabPath;
            foreach (var material in MaterialBindings(id).Values.SelectMany(value => value).Distinct())
            {
                string path = AssetDatabase.GetAssetPath(material);
                if (string.IsNullOrEmpty(path)) throw new InvalidDataException("The reviewed foliage prefab has a non-asset material: " + id);
                yield return path;
            }
        }

        static Dictionary<string, Material[]> MaterialBindings(string id)
        {
            string path = MaterialTemplate(id);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new FileNotFoundException("Missing reviewed foliage material prefab", path);
            var bindings = new Dictionary<string, Material[]>(StringComparer.Ordinal);
            foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                string key = RendererKey(renderer.name);
                if (renderer.sharedMaterials.Length == 0 || renderer.sharedMaterials.Any(material => material == null) ||
                    !bindings.TryAdd(key, renderer.sharedMaterials))
                    throw new InvalidDataException("Invalid or duplicate material binding in " + path + ": " + renderer.name);
            }
            if (bindings.Count == 0) throw new InvalidDataException("Reviewed foliage prefab has no material bindings: " + path);
            return bindings;
        }

        static string MaterialTemplate(string id)
        {
            string root = id == "MatureFir_A" ? ProjectContext.Current.matureFirRoot : ProjectContext.Current.natureRoot;
            string template = id == "MatureFir_A" || id == "pine_sapling_small_b" ? id :
                id.StartsWith("fern_02_", StringComparison.Ordinal) ? "fern_02_b" :
                id.StartsWith("shrub_03_", StringComparison.Ordinal) ? "shrub_03_a" :
                id.StartsWith("rock_moss_set_01_rock", StringComparison.Ordinal) ? "rock_moss_set_01_rock01" :
                throw new InvalidDataException("Unknown foliage material catalog entry: " + id);
            return root + "/Prefabs/" + template + ".prefab";
        }

        static string RendererKey(string name)
        {
            int start = name.IndexOf("_LOD", StringComparison.Ordinal);
            if (start < 0) throw new InvalidDataException("Foliage renderer does not identify its LOD material slot: " + name);
            return name.Substring(start);
        }
    }
}
