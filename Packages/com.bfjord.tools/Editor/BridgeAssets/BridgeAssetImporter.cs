using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Bwork.Authoring.Editor.BridgeAssets
{
    internal sealed class BridgeImported
    {
        public GameObject Root;
        public Bounds Bounds;
        public int[] Triangles;
        public int Colliders, Renderers;
    }

    internal static class BridgeAssetImporter
    {
        public static BridgeImported Import(BridgePreparedSource source, string generationDirectory, string rootName)
        {
            var manifest = source.Manifest;
            // Copy complete source files before importing; do not load newly created assets while import is paused.
            foreach (string relative in source.RelativeFiles)
            {
                string destination = generationDirectory + "/source/" + relative;
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(BridgeAssetContract.ResolveRelative(source.Directory, relative), destination, false);
            }
            File.WriteAllText(generationDirectory + "/manifest.json", source.ManifestJson);
            foreach (string relative in source.RelativeFiles)
                AssetDatabase.ImportAsset(generationDirectory + "/source/" + relative, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(generationDirectory + "/manifest.json", ImportAssetOptions.ForceSynchronousImport);

            var textureKinds = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var material in manifest.materials)
                foreach (var entry in new[] { (material.baseColorPath, 0), (material.normalPath, 1), (material.maskPath, 2) })
                    if (!string.IsNullOrEmpty(entry.Item1))
                    {
                        if (textureKinds.TryGetValue(entry.Item1, out int kind) && kind != entry.Item2)
                            throw new InvalidDataException("One texture cannot serve conflicting sRGB/normal/mask roles: " + entry.Item1);
                        textureKinds[entry.Item1] = entry.Item2;
                    }
            foreach (var entry in textureKinds) ConfigureTexture(generationDirectory + "/source/" + entry.Key, entry.Value);
            foreach (string path in manifest.lods.Select(l => l.fbxRelativePath).Append(manifest.colliderRelativePath))
                ConfigureModel(generationDirectory + "/source/" + path);

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            BridgeAssetContract.Require(shader != null && shader.isSupported, "The bridge requires a supported URP Lit shader.");
            var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (var definition in manifest.materials)
            {
                var material = Material(definition, shader, generationDirectory + "/source/");
                string path = generationDirectory + "/" + definition.key + ".mat";
                AssetDatabase.CreateAsset(material, path);
                AssetDatabase.SaveAssetIfDirty(material);
                materials.Add(definition.key, material);
            }
            var slots = manifest.materialSlots.ToDictionary(s => s.slotName, s => materials[s.materialKey], StringComparer.Ordinal);
            var root = new GameObject(rootName);
            root.SetActive(false);
            try
            {
                root.transform.SetPositionAndRotation(source.Placement.position.Vector, Quaternion.Euler(0, source.Placement.yawDegrees, 0));
                var lods = new LOD[3]; var counts = new int[3]; Bounds bounds = default;
                for (int i = 0; i < 3; i++)
                {
                    var imported = InstantiateModel(generationDirectory + "/source/" + manifest.lods[i].fbxRelativePath, root.transform, "LOD" + i);
                    var renderers = imported.GetComponentsInChildren<MeshRenderer>(true);
                    BridgeAssetContract.Require(renderers.Length > 0 && renderers.Length <= 128 && imported.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0, "Bridge LOD requires 1..128 static renderers.");
                    foreach (var renderer in renderers)
                    {
                        var sourceMaterials = renderer.sharedMaterials;
                        renderer.sharedMaterials = sourceMaterials.Select(m =>
                        {
                            BridgeAssetContract.Require(m != null && slots.ContainsKey(m.name), "Unrecognized FBX material slot: " + (m == null ? "null" : m.name));
                            return slots[m.name];
                        }).ToArray();
                        renderer.shadowCastingMode = ShadowCastingMode.On;
                        renderer.receiveShadows = true;
                    }
                    var measured = Measure(imported, root.transform, out counts[i], out int vertices);
                    BridgeAssetContract.Require(counts[i] == manifest.lods[i].triangles && vertices <= 2000000, "Imported LOD" + i + " triangle/vertex counts differ from manifest (actual triangles " + counts[i] + ").");
                    if (i == 0)
                    {
                        bounds = measured;
                        BridgeAssetContract.Require(Vector3.Distance(bounds.min, manifest.boundsMin.Vector) < .15f && Vector3.Distance(bounds.max, manifest.boundsMax.Vector) < .15f,
                            "Imported LOD0 bounds differ from manifest. Actual min " + bounds.min.ToString("F3") + ", max " + bounds.max.ToString("F3") + ". Check FBX axes/units.");
                    }
                    else
                    {
                        var envelope = bounds; envelope.Expand(.5f);
                        BridgeAssetContract.Require(envelope.Contains(measured.min) && envelope.Contains(measured.max), "A lower LOD escapes LOD0 bounds.");
                    }
                    lods[i] = new LOD(manifest.lods[i].screenRelativeHeight, renderers);
                }
                var collisionSource = InstantiateModel(generationDirectory + "/source/" + manifest.colliderRelativePath, root.transform, "Deck Collision");
                Bounds collisionBounds = Measure(collisionSource, root.transform, out int collisionTriangles, out _, false);
                BridgeAssetContract.Require(collisionTriangles > 0 && collisionTriangles <= 2048 && Mathf.Abs(collisionBounds.max.y) <= .05f && collisionBounds.min.y >= -2 && Mathf.Abs(collisionBounds.center.x) <= .05f && Mathf.Abs(collisionBounds.center.z) <= .05f && Mathf.Abs(collisionBounds.size.z - manifest.totalLength) <= .15f && Mathf.Abs(collisionBounds.size.x - manifest.clearCarriageway) <= .15f, "Collision proxy must span the full length and clearCarriageway width, centered at x/z=0 with top at y=0.");
                int colliderCount = 0;
                foreach (var filter in collisionSource.GetComponentsInChildren<MeshFilter>(true))
                {
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    collider.convex = false;
                    colliderCount++;
                }
                foreach (var renderer in collisionSource.GetComponentsInChildren<Renderer>(true)) Object.DestroyImmediate(renderer);
                var group = root.AddComponent<LODGroup>();
                group.fadeMode = LODFadeMode.None;
                group.SetLODs(lods); group.RecalculateBounds();
                return new BridgeImported { Root = root, Bounds = bounds, Triangles = counts, Colliders = colliderCount, Renderers = lods.Sum(l => l.renderers.Length) };
            }
            catch { Object.DestroyImmediate(root); throw; }
        }

        static GameObject InstantiateModel(string path, Transform parent, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            BridgeAssetContract.Require(prefab != null, "Failed to import FBX: " + path);
            BridgeAssetContract.Require(Vector3.Distance(prefab.transform.localScale, Vector3.one) < .001f, "FBX root must import at unit scale: " + path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            // Imported meshes are immutable; scene-only render/material overrides are owned by this generation.
            go.name = name;
            BridgeAssetContract.Require(go.transform.localPosition.sqrMagnitude < .0001f && Quaternion.Angle(go.transform.localRotation, Quaternion.identity) < .01f, "FBX root must import at zero position and rotation: " + path);
            BridgeAssetContract.Require(go.GetComponentsInChildren<Collider>(true).Length == 0 && go.GetComponentsInChildren<Camera>(true).Length == 0 && go.GetComponentsInChildren<Light>(true).Length == 0, "FBX unexpectedly contains collider/camera/light components.");
            return go;
        }

        static void ConfigureModel(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            BridgeAssetContract.Require(importer != null, "No model importer: " + path);
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
            importer.addCollider = false; importer.isReadable = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.SaveAndReimport();
        }

        internal static void ConfigureTexture(string path, int kind)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            BridgeAssetContract.Require(importer != null, "No texture importer: " + path);
            importer.textureType = kind == 1 ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = kind == 0;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        internal static Material Material(BridgeMaterialSource source, Shader shader, string prefix)
        {
            var material = new Material(shader) { name = "Bridge " + source.key };
            Color tint = source.baseColor == null ? Color.white : new Color(source.baseColor[0], source.baseColor[1], source.baseColor[2], source.baseColor[3]);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Metallic", source.metallic); material.SetFloat("_Smoothness", source.smoothness);
            Texture2D Load(string path) => string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + path) ?? throw new InvalidDataException("Texture import failed: " + path);
            var baseMap = Load(source.baseColorPath); var normal = Load(source.normalPath); var mask = Load(source.maskPath);
            if (baseMap != null) material.SetTexture("_BaseMap", baseMap);
            if (normal != null) { material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", .65f); material.EnableKeyword("_NORMALMAP"); }
            if (mask != null)
            {
                material.SetTexture("_MetallicGlossMap", mask); material.SetTexture("_OcclusionMap", mask);
                material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", 1); material.SetFloat("_OcclusionStrength", 1);
                material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.EnableKeyword("_OCCLUSIONMAP");
            }
            material.SetTextureScale("_BaseMap", Vector2.one / source.tilingMeters);
            material.SetFloat("_Surface", 0); material.SetFloat("_AlphaClip", 0); material.SetFloat("_Cull", (float)CullMode.Back);
            return material;
        }

        static Bounds Measure(GameObject model, Transform root, out int triangles, out int vertices, bool requireSurface = true)
        {
            Bounds bounds = default; bool first = true; triangles = 0; vertices = 0;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                BridgeAssetContract.Require(mesh != null && mesh.isReadable, "Bridge mesh must be readable for import validation.");
                if (requireSurface)
                {
                    var uv = mesh.uv;
                    BridgeAssetContract.Require(uv.Length == mesh.vertexCount && uv.All(p => float.IsFinite(p.x) && float.IsFinite(p.y)), "Bridge surfaces require finite UV0 for every vertex.");
                    var normals = mesh.normals;
                    BridgeAssetContract.Require(normals.Length == mesh.vertexCount && normals.All(n => BridgeAssetContract.Finite(n) && n.sqrMagnitude > .5f), "Bridge surfaces require valid imported normals.");
                }
                var matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (Vector3 vertex in mesh.vertices)
                {
                    Vector3 p = matrix.MultiplyPoint3x4(vertex);
                    BridgeAssetContract.Require(BridgeAssetContract.Finite(p), "Nonfinite imported bridge vertex.");
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
                }
                vertices += mesh.vertexCount;
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    BridgeAssetContract.Require(mesh.GetTopology(i) == MeshTopology.Triangles, "Bridge mesh requires triangles.");
                    triangles += (int)mesh.GetIndexCount(i) / 3;
                }
                var renderer = filter.GetComponent<MeshRenderer>();
                BridgeAssetContract.Require(renderer != null && renderer.sharedMaterials.Length == mesh.subMeshCount, "Bridge mesh/material slot count mismatch.");
            }
            BridgeAssetContract.Require(!first, "FBX contains no mesh vertices.");
            return bounds;
        }
    }
}
