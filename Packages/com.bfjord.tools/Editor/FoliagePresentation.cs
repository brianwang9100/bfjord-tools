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
        const string Revision = "foliage-presentation-5";
        const string DetailedRevision = "foliage-presentation-4";
        const string WoodlandRevision = "foliage-presentation-3";
        const string PreviousRevision = "foliage-presentation-2";
        const string LegacyRevision = "foliage-presentation-1";
        const string OriginalRoot = "Assets/BFjord/OriginalFoliage";
        static readonly string[] BotanicalNames = { "RoseThicket_A", "MeadowDaisy_A", "WoodSorrel_A", "CoastalGrass_A" };
        static readonly string[] WoodlandNames = { "MatureOak_A", "SilverBirch_A", "FallenHollowLog_A", "TallMeadowGrass_A" };
        static readonly string[] DetailedTreeNames = { "MatureOak_B", "SilverBirch_B" };
        static readonly string[] OriginalNames = BotanicalNames.Concat(WoodlandNames).Concat(DetailedTreeNames).Concat(NatureAssets07.Names).ToArray();
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
            .Concat(Enumerable.Range(1, 6).Select(n => "rock_moss_set_01_rock" + n.ToString("00"))).Concat(OriginalNames).ToArray();

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

        [InitializeOnLoadMethod]
        static void ResetWindPreview() => Shader.SetGlobalVector("_BFjordWindPreview", Vector4.zero);

        /// <summary>Freezes the shader time for reproducible wind screenshots; -1 restores engine time.</summary>
        public static object WindFrame(float seconds)
        {
            if (!float.IsFinite(seconds) || (seconds < 0 && seconds != -1) || seconds > 60)
                throw new ArgumentOutOfRangeException(nameof(seconds), "Use -1 for live wind or 0–60 seconds for a fixed capture.");
            Shader.SetGlobalVector("_BFjordWindPreview", seconds == -1 ? Vector4.zero : new Vector4(1, seconds, 0, 0));
            SceneView.RepaintAll();
            return new { live = seconds == -1, seconds, maximumGrassTipDisplacementMeters = .14225f };
        }

        public static object GrassView()
        {
            var plant = ToolSandbox.Root.GetComponentsInChildren<Transform>()
                .FirstOrDefault(value => value.name == "TallMeadowGrass_A");
            if (plant == null) throw new InvalidOperationException("Apply woodland-grass.json before framing the grass wind view.");
            var camera = ToolSandbox.Root.GetComponentInChildren<Camera>();
            if (camera == null) throw new InvalidOperationException("The sandbox camera is missing.");
            camera.transform.position = plant.position + new Vector3(1.8f, 1.0f, -2.3f);
            camera.transform.LookAt(plant.position + Vector3.up * .62f);
            camera.orthographic = false; camera.fieldOfView = 48;
            return new { camera = camera.name, plant = plant.name, view = "grass-wind" };
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
            string fingerprint = Hash128.Compute(Revision + ":root-mikktspace-1:" + string.Join("|", inputs.Select(p => p + ":" + AssetDatabase.GetAssetDependencyHash(p)))).ToString();
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
            string[] expected = catalog == null ? null : catalog.revision switch {
                Revision => Names,
                DetailedRevision => Names.Except(NatureAssets07.Names).ToArray(),
                WoodlandRevision => Names.Except(NatureAssets07.Names).Except(DetailedTreeNames).ToArray(),
                PreviousRevision => Names.Except(NatureAssets07.Names).Except(DetailedTreeNames).Except(WoodlandNames).ToArray(),
                LegacyRevision => Names.Except(OriginalNames).ToArray(),
                _ => null
            };
            if (expected == null || catalog.entries == null || catalog.entries.Length != expected.Length ||
                catalog.entries.Select(e => e?.id).Distinct(StringComparer.Ordinal).Count() != expected.Length ||
                string.IsNullOrEmpty(catalog.directory) || !catalog.directory.StartsWith(Root + "/", StringComparison.Ordinal) ||
                catalog.entries.Any(e => e == null || !expected.Contains(e.id) || string.IsNullOrEmpty(e.prefabPath) ||
                    !e.prefabPath.StartsWith(catalog.directory + "/", StringComparison.Ordinal) || e.prefabPath.Contains("..")))
                throw new InvalidDataException("Foliage catalog receipt is invalid; preserve the library for recovery.");
            return catalog;
        }

        static bool IsCanopy(string id) => id == "MatureFir_A" || id == "MatureOak_A" || id == "SilverBirch_A" || DetailedTreeNames.Contains(id) || NatureAssets07.IsCanopy(id);
        static string AtlasStem(string id) => DetailedTreeNames.Contains(id) ? "Woodland06" : WoodlandNames.Contains(id) ? "Woodland" : "Foliage";

        static string Source(string id) => (NatureAssets07.Contains(id) ? NatureAssets07.Root(id) : OriginalNames.Contains(id) ? OriginalRoot : id == "MatureFir_A" ? ProjectContext.Current.matureFirRoot : ProjectContext.Current.natureRoot) + "/Models/" + id + ".fbx";

        static Entry BuildPrefab(string id, string directory, Shader shader)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source(id));
            var root = new GameObject(id);
            bool rigid = id.StartsWith("rock_", StringComparison.Ordinal) || id == "FallenHollowLog_A" || NatureAssets07.IsRigid(id);
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
            float amplitude = rigid ? 0 : id == "TallMeadowGrass_A" ? .14f : IsCanopy(id) ? .22f : id == "WoodSorrel_A" ? .009f : id == "CoastalGrass_A" ? .045f : id == "MatureFir_A" ? .22f : id.StartsWith("pine_", StringComparison.Ordinal) ? .055f : .035f;
            // Maximum horizontal shader displacement is amplitude*sqrt(1+.18^2). Scale may be as low as .5.
            float padding = amplitude * 2.04f;
            var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
            var materialBindings = OriginalNames.Contains(id) ? null : MaterialBindings(id);
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
                    Material[] sourceMaterials;
                    if (OriginalNames.Contains(id)) sourceMaterials = new Material[] { null };
                    else if (!materialBindings.TryGetValue(RendererKey(filter.name), out sourceMaterials))
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
                bool canopy = IsCanopy(id);
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
            var uv = mesh.uv;
            if (vertices.Length == 0 || normals.Length != vertices.Length || uv.Length != vertices.Length || uv.Any(v => !float.IsFinite(v.x) || !float.IsFinite(v.y)))
                throw new InvalidDataException("Root-space foliage baking requires positions, normals, and finite UVs: " + label);
            var normalMatrix = matrix.inverse.transpose;
            float handedness = matrix.determinant < 0 ? -1 : 1;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                var normal = normalMatrix.MultiplyVector(normals[i]);
                if (!Finite(normal) || normal.sqrMagnitude < 1e-12f) throw new InvalidDataException("Invalid transformed foliage normal: " + label);
                normal.Normalize(); normals[i] = normal;
            }
            mesh.vertices = vertices;
            mesh.normals = normals;
            if (handedness < 0)
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                        throw new InvalidDataException("Mirrored foliage baking requires triangle topology: " + label);
                    var indices = mesh.GetTriangles(submesh, true);
                    for (int i = 0; i < indices.Length; i += 3) (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
                    mesh.SetTriangles(indices, submesh, false);
                }
            // Rebuild MikkTSpace from final positions/normals/UVs. Importer-generated
            // cap tangents can be parallel to their normals; preserving that basis
            // through a coordinate bake gives invalid normal-map lighting.
            mesh.RecalculateTangents();
            var bakedTangents = mesh.tangents;
            if (bakedTangents.Length != vertices.Length) throw new InvalidDataException("Baked tangent data is missing: " + label);
            for (int i = 0; i < bakedTangents.Length; i++)
            {
                var t = bakedTangents[i];
                var xyz = new Vector3(t.x, t.y, t.z);
                if (!Finite(xyz) || !float.IsFinite(t.w) || Mathf.Abs(Mathf.Abs(t.w)-1) > .001f ||
                    xyz.sqrMagnitude < 1e-12f || Mathf.Abs(Vector3.Dot(normals[i], xyz.normalized)) > .001f)
                    throw new InvalidDataException("Invalid regenerated foliage tangent: " + label);
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
            bool original = OriginalNames.Contains(id);
            if (source == null && !original) throw new InvalidDataException("The reviewed prefab has an unbound material: " + id);
            string name = original ? "OriginalFoliage" : source.name.Replace(" (Instance)", "");
            if (cache.TryGetValue(name, out var value)) return value;
            value = original ? new Material(shader) : new Material(source);
            value.name = id + "-" + name; value.enableInstancing = true;
            if (original)
            {
                var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(id, "Atlas"));
                if (atlas == null) throw new FileNotFoundException("Restore the original foliage atlas before building foliage.");
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(id, "Normal"));
                if (normal == null) throw new FileNotFoundException("Restore the original foliage normal map before building foliage.");
                value.SetTexture("_BaseMap", atlas);
                value.SetTexture("_BumpMap", normal);
                value.SetFloat("_BumpScale", .55f);
                value.SetColor("_BaseColor", Color.white);
                value.SetFloat("_Smoothness", .21f);
                if (WoodlandNames.Contains(id) || DetailedTreeNames.Contains(id) || NatureAssets07.Contains(id))
                {
                    var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(id, "Mask"));
                    if (mask == null) throw new FileNotFoundException("Restore the original woodland PBR mask before building foliage.");
                    value.SetTexture("_MetallicGlossMap", mask);
                    value.SetFloat("_Smoothness", 1);
                    value.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                value.SetFloat("_Metallic", 0);
                // The atlas is opaque; retaining the cutout path also enables the shared thin-leaf transmission.
                value.SetFloat("_AlphaClip", 1);
            }
            if (!rigid || original)
            {
                value.shader = shader;
                value.SetFloat("_WindRootY", bounds.min.y);
                value.SetFloat("_WindHeight", Mathf.Max(.1f, bounds.size.y));
                value.SetFloat("_WindAmplitude", amplitude);
                value.SetFloat("_WindSpeed", id == "TallMeadowGrass_A" ? 1.5f : 1);
                value.SetFloat("_Transmission", rigid ? 0 : .12f);
                value.SetFloat("_Cull", 0);
                if (value.GetTexture("_BumpMap") != null) value.EnableKeyword("_NORMALMAP");
                if (value.GetTexture("_MetallicGlossMap") != null) value.EnableKeyword("_METALLICSPECGLOSSMAP");
                value.renderQueue = 2450;
            }
            AssetDatabase.CreateAsset(value, directory + "/" + value.name + ".mat");
            cache.Add(name, value);
            return value;
        }

        static string TexturePath(string id, string channel) => NatureAssets07.Contains(id) ? NatureAssets07.Texture(id, channel) :
            OriginalRoot + "/Textures/" + AtlasStem(id) + channel + ".png";

        static IEnumerable<string> MaterialDependencyPaths(string id)
        {
            if (OriginalNames.Contains(id))
            {
                yield return TexturePath(id, "Atlas");
                yield return TexturePath(id, "Normal");
                if (WoodlandNames.Contains(id) || DetailedTreeNames.Contains(id) || NatureAssets07.Contains(id)) yield return TexturePath(id, "Mask");
                yield break;
            }
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
