using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bwork.Authoring.Editor.BridgeAssets;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Bwork.Authoring.Editor.Rocks
{
    /// <summary>Immutable content-addressed shared prefabs; rebuilding source never edits meshes used by existing batches.</summary>
    public static class RockLibrary
    {
        public static string DirectoryFor(RockSource source) => ToolSandbox.Generated + "/RockLibrary/" + source.hash;
        public static string PrefabPath(RockSource source, string id) => DirectoryFor(source) + "/" + id + ".prefab";
        public static string MaterialPath(RockSource source, string profile, string materialId = "rock") =>
            DirectoryFor(source) + "/" + (materialId == "rock" ? "" : materialId + "-") + profile + ".mat";
        public static Material MaterialForVariant(RockSource source, string variantId, string profile)
        {
            var variant = source.manifest.variants.SingleOrDefault(v => v.id == variantId);
            RockContract.Require(variant != null && RockContract.Profiles.Contains(profile), "Unknown rock variant or material profile.");
            return AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(source, profile, variant.materialId));
        }
        public static bool Ready(RockSource source) => AssetDatabase.LoadAssetAtPath<TextAsset>(DirectoryFor(source) + "/ready.json") != null &&
            source.manifest.variants.All(v => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(source, v.id)) != null) &&
            source.manifest.materials.All(m => RockContract.Profiles.All(p => AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(source, p, m.id)) != null));
        public static object Build(RockSource source)
        {
            ProjectContext.RequireIdle();
            string directory = DirectoryFor(source); ProjectContext.RejectLinks(directory);
            if (Ready(source)) return new { state = "assets-ready", unchanged = true, sourceHash = source.hash, directory };
            RockContract.Require(!Directory.Exists(directory), "An incomplete rock library exists. Preserve or remove that failed generation before retrying: " + directory);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            RockContract.Require(shader != null && shader.isSupported, "Rock assets require a supported URP Lit shader.");
            Directory.CreateDirectory(directory);
            try
            {
                foreach (string relative in source.files)
                {
                    string destination = directory + "/source/" + relative;
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(RockContract.FileAt(source.root, relative), destination, false);
                    AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
                }
                foreach (var materialSource in source.manifest.materials)
                {
                    BridgeAssetImporter.ConfigureTexture(directory + "/source/" + materialSource.baseColorPath, 0);
                    BridgeAssetImporter.ConfigureTexture(directory + "/source/" + materialSource.normalPath, 1);
                    BridgeAssetImporter.ConfigureTexture(directory + "/source/" + materialSource.metallicSmoothnessPath, 2);
                    for (int i = 0; i < RockContract.Profiles.Length; i++)
                    {
                        var colors = new[] { new Color(.8f, .83f, .85f), new Color(.9f, .73f, .54f), new Color(.36f, .4f, .44f), new Color(.57f, .63f, .45f), new Color(.48f, .51f, .52f) };
                        var material = new Material(shader) { name = "Rock " + RockContract.Profiles[i], enableInstancing = true };
                        material.SetColor("_BaseColor", colors[i]);
                        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(directory + "/source/" + materialSource.baseColorPath));
                        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(directory + "/source/" + materialSource.normalPath));
                        material.SetFloat("_BumpScale", i == 4 ? .35f : .7f); material.EnableKeyword("_NORMALMAP");
                        material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(directory + "/source/" + materialSource.metallicSmoothnessPath));
                        material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", i == 4 ? 1f : i == 2 ? .65f : .4f); material.EnableKeyword("_METALLICSPECGLOSSMAP");
                        if (i == 4) { material.SetTexture("_MetallicGlossMap", null); material.DisableKeyword("_METALLICSPECGLOSSMAP"); material.SetFloat("_Smoothness", .58f); }
                        AssetDatabase.CreateAsset(material, MaterialPath(source, RockContract.Profiles[i], materialSource.id));
                    }
                }
                foreach (var variant in source.manifest.variants) BuildPrefab(source, variant, MaterialForVariant(source, variant.id, "granite"));
                File.WriteAllText(directory + "/manifest.json", source.json);
                AssetDatabase.ImportAsset(directory + "/manifest.json", ImportAssetOptions.ForceSynchronousImport);
                File.WriteAllText(directory + "/ready.json", "{\"sourceHash\":\"" + source.hash + "\"}");
                AssetDatabase.ImportAsset(directory + "/ready.json", ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.SaveAssets();
                return new { state = "assets-ready", unchanged = false, sourceHash = source.hash, directory, variants = source.manifest.variants.Length, materials = RockContract.Profiles.Length * source.manifest.materials.Length };
            }
            catch (Exception error)
            {
                try
                {
                    RockContract.Require(AssetDatabase.DeleteAsset(directory) && !Directory.Exists(directory) && !File.Exists(directory + ".meta"),
                        "Failed to remove the incomplete rock library; preserve this generation for recovery: " + directory);
                }
                catch (Exception cleanupError)
                { throw new AggregateException("Rock library construction failed with a cleanup error.", error, cleanupError); }
                throw;
            }
        }
        static void ConfigureModel(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            RockContract.Require(importer != null, "Missing FBX importer: " + path);
            importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
            importer.addCollider = false; importer.isReadable = true;
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
        }
        static GameObject Model(string path, Transform parent, Material material, out Bounds bounds, out int triangles, bool surface = true)
        {
            ConfigureModel(path);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            RockContract.Require(asset != null, "Missing imported rock model.");
            var model = Object.Instantiate(asset, parent, false);
            var filters = model.GetComponentsInChildren<MeshFilter>(true);
            RockContract.Require(filters.Length > 0 && filters.Length <= 16, "Rock model needs 1..16 static meshes.");
            triangles = 0; bounds = default; bool first = true;
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;
                RockContract.Require(mesh != null && mesh.isReadable && mesh.vertexCount <= 100000, "Invalid rock mesh.");
                var matrix = parent.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (var vertex in mesh.vertices)
                {
                    var p = matrix.MultiplyPoint3x4(vertex);
                    RockContract.Require(float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z), "Nonfinite rock vertex.");
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
                }
                for (int s = 0; s < mesh.subMeshCount; s++)
                { RockContract.Require(mesh.GetTopology(s) == MeshTopology.Triangles, "Rock faces must be triangles."); triangles += (int)mesh.GetIndexCount(s) / 3; }
                if (surface) RockContract.Require(mesh.uv.Length == mesh.vertexCount && mesh.normals.Length == mesh.vertexCount, "Rock surface needs authored UVs and normals.");
                var renderer = filter.GetComponent<MeshRenderer>(); RockContract.Require(renderer != null, "Missing rock renderer.");
                renderer.sharedMaterials = Enumerable.Repeat(material, mesh.subMeshCount).ToArray();
            }
            return model;
        }
        static void BuildPrefab(RockSource source, RockVariant variant, Material material)
        {
            string prefix = DirectoryFor(source) + "/source/";
            var root = new GameObject(variant.displayName ?? variant.id); root.SetActive(false);
            try
            {
                var lods = new LOD[3]; Bounds envelope = default;
                for (int i = 0; i < 3; i++)
                {
                    var model = Model(prefix + variant.lods[i].path, root.transform, material, out Bounds bounds, out int triangles); model.name = "LOD" + i;
                    RockContract.Require(triangles == variant.lods[i].triangles, "Imported rock triangle count differs from source manifest: " + variant.id);
                    if (i == 0)
                    {
                        RockContract.Require(Vector3.Distance(bounds.size, variant.Size) < .08f && Mathf.Abs(bounds.min.y) < .04f && Mathf.Abs(bounds.center.x) < .04f && Mathf.Abs(bounds.center.z) < .04f, "Rock bounds/center differ from manifest; require bottom y=0, centered x/z, meter units: " + variant.id + " actual " + bounds);
                        envelope = bounds; envelope.Expand(.2f);
                    }
                    else RockContract.Require(envelope.Contains(bounds.min) && envelope.Contains(bounds.max), "Lower rock LOD escapes source envelope.");
                    lods[i] = new LOD(variant.lods[i].screenRelativeHeight, model.GetComponentsInChildren<Renderer>(true));
                }
                var collision = Model(prefix + variant.colliderPath, root.transform, material, out Bounds collisionBounds, out int collisionTriangles, false); collision.name = "Collision";
                RockContract.Require(collisionTriangles <= 1000 && envelope.Contains(collisionBounds.min) && envelope.Contains(collisionBounds.max), "Rock collider must fit the source envelope and contain <=1000 triangles.");
                foreach (var filter in collision.GetComponentsInChildren<MeshFilter>(true))
                { var collider = filter.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = filter.sharedMesh; collider.convex = false; }
                foreach (var renderer in collision.GetComponentsInChildren<Renderer>(true)) Object.DestroyImmediate(renderer);
                var group = root.AddComponent<LODGroup>(); group.fadeMode = LODFadeMode.None; group.SetLODs(lods); group.RecalculateBounds();
                root.SetActive(true);
                RockContract.Require(PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(source, variant.id)) != null, "Failed to save rock prefab.");
            }
            finally { Object.DestroyImmediate(root); }
        }
        public static object Catalog(RockSource source) => new { ready = Ready(source), sourceHash = source.hash, sourceBytes = source.bytes,
            directory = DirectoryFor(source), materialProfiles = RockContract.Profiles, variants = source.manifest.variants };
    }
}
